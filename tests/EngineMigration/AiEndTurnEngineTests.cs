using System.Reflection;
using System.Text.Json;
using AutoMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.GameFlow;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Persistence;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Options;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Models;

// Native configurations and the production transaction pipeline, with memory-only persistence/broadcast adapters.
internal static class AiEndTurnEngineTests
{
    private const int LeaderId=2000000100, UnitId=2000000101;
    private static readonly BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Static;
    private sealed record Scenario(string Name,int Source,int EnemyHp,int Enemies=2,bool Hand=false,bool Silenced=false,bool Shield=false,bool Response=false,int OwnHp=20,bool SourceAlreadyAttacked=false,int TargetAttack=1,int TargetHealth=3,bool TargetShield=false);

    private sealed record RefillOpponent(int CardId, bool SummonsBeforeDraw = false);

    internal static void Run(string source,string output)
    {
        var original=GameDataProvider.Instance;
        var originalAccessor=GameServiceProvider.ContextAccessor;
        var snapshot=Path.Combine(Path.GetTempPath(),"ai-endturn-engine-"+Guid.NewGuid().ToString("N")+".db");
        using(var input=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=source,Mode=SqliteOpenMode.ReadOnly}.ToString()))
        using(var copy=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=snapshot}.ToString())) {
            input.Open();copy.Open();input.BackupDatabase(copy);
        }
        using var catalogServices=new ServiceCollection().AddLogging(log=>log.ClearProviders())
            .AddDbContext<ApplicationDbContext>(options=>options.UseSqlite(new SqliteConnectionStringBuilder{DataSource=snapshot,Mode=SqliteOpenMode.ReadOnly}.ToString()))
            .AddScoped<ICardRepository,EFCardRepository>().AddScoped<AbilityLoader>().BuildServiceProvider();
        var rows=new List<object>();
        try {
            var native=new GameDataProvider(catalogServices.GetRequiredService<IServiceScopeFactory>(),NullLogger<GameDataProvider>.Instance);
            native.InitializeAsync().GetAwaiter().GetResult();
            foreach(var scenario in new[] {
                new Scenario("reiss-independent-leader-lethal",1474120,2),
                new Scenario("hostess-sweep-lethal",1474116,3),
                new Scenario("hostess-safe-sweep-without-fox-trade",1474116,20,OwnHp:2,SourceAlreadyAttacked:true),
                new Scenario("reiss-empty-optional-pool",1474120,2,0),
                new Scenario("reiss-singleton-preserves-fox",1474120,20,1,OwnHp:2,SourceAlreadyAttacked:true,TargetAttack:3,TargetHealth:2),
                new Scenario("reiss-singleton-shielded",1474120,20,1,OwnHp:2,SourceAlreadyAttacked:true,TargetAttack:3,TargetHealth:2,TargetShield:true),
                new Scenario("reiss-hand-condition-false",1474120,2,Hand:true),
                new Scenario("reiss-silenced",1474120,2,Silenced:true),
                new Scenario("reiss-shield",1474120,2,Shield:true),
                new Scenario("reiss-public-heal-response",1474120,20,Response:true)
            }) {
                try { rows.Add(Execute(native,scenario)); }
                catch(Exception error){rows.Add(new{scenario=scenario.Name,passed=false,error=error.ToString()});}
            }
            foreach (var (name, ready, silenced, enemyHealth) in new[] {
                ("alice-ep-conversion-lethal", true, false, 4),
                ("alice-silenced-no-conversion", true, true, 4),
                ("alice-fresh-versus-ready-attackers", false, false, 14)
            })
            {
                try { rows.Add(ExecuteEvolution(native, name, ready, silenced, enemyHealth)); }
                catch (Exception error) { rows.Add(new { scenario=name, passed=false, error=error.ToString() }); }
            }
            foreach (var (count, super, withReiss, nativeLeader) in new[] {
                (1, false, false, false), (2, false, false, false), (3, false, false, false),
                (1, true, false, false), (2, true, false, false), (3, true, false, false),
                (1, true, true, false), (1, true, true, true) })
            {
                var name = $"hostess-discard-{count}-{(super ? "sep" : "ep")}" + (withReiss ? "-versus-reiss" : "")
                    + (nativeLeader ? "-native-leader" : "");
                try { rows.Add(ExecuteHostess(native, name, count, super, withReiss, nativeLeader)); }
                catch (Exception error) { rows.Add(new { scenario=name, passed=false, error=error.ToString() }); }
            }
            foreach (var super in new[] { false,true })
            {
                var name = super ? "hostess-with-reiss-and-refill-four" : "hostess-with-reiss-and-refill-two";
                try { rows.Add(ExecuteHostess(native, name, 1, super, true, true, withCrest:true, crestDeckCount:2)); }
                catch (Exception error) { rows.Add(new { scenario=name, passed=false, error=error.ToString() }); }
            }
            const string shortDeck = "hostess-with-refill-deck-exhaustion";
            try { rows.Add(ExecuteHostess(native, shortDeck, 1, true, true, true, withCrest:true, crestDeckCount:1)); }
            catch (Exception error) { rows.Add(new { scenario=shortDeck, passed=false, error=error.ToString() }); }
            foreach (var health in new[] { 20, 5 })
            {
                var name = health==5 ? "refill-exhaustion-does-not-block-direct-lethal" : "refill-exhaustion-retains-hand";
                try { rows.Add(ExecuteHostess(native,name,1,true,false,true,withCrest:true,
                    crestDeckCount:1,enemyHealth:health,checkExhaustionChoice:true)); }
                catch (Exception error) { rows.Add(new {scenario=name,passed=false,error=error.ToString()}); }
            }
            foreach (var (name,health,fortFirst,refillBeforeFort,forceClear) in new[]
            {
                ("cold-fort-exhaustion-retains-hand",20,false,false,false),
                ("cold-fort-damage-before-draw-lethal",5,false,false,false),
                ("cold-fort-draw-before-damage-retains-hand",5,true,false,false),
                ("cold-fort-draw-preempts-native-damage",5,true,false,true),
                ("cold-fort-unknown-intermediate-tuck",20,false,true,false)
            })
            {
                try { rows.Add(ExecuteHostess(native,name,1,true,true,true,withCrest:true,crestDeckCount:2,
                    enemyHealth:health,laterFieldCard:1474115,checkExhaustionChoice:true,
                    laterDrawFirst:fortFirst,tuckBeforeLaterDraw:refillBeforeFort,forceClear:forceClear)); }
                catch (Exception error) { rows.Add(new {scenario=name,passed=false,error=error.ToString()}); }
            }
            foreach (var (name, drawOnPlay, storm) in new[]
            {
                ("last-card-normal-play", false, false),
                ("last-card-storm-lethal", false, true),
                ("last-card-replacement-draw", true, false)
            })
            {
                try { rows.Add(ExecuteLastCardPair(native,name,drawOnPlay,storm)); }
                catch (Exception error) { rows.Add(new {scenario=name,passed=false,error=error.ToString()}); }
            }
            const string crestLethal = "hostess-refill-and-damage-lethal";
            try { rows.Add(ExecuteHostess(native, crestLethal, 1, true, true, true, withCrest:true, crestDeckCount:2, enemyHealth:5)); }
            catch (Exception error) { rows.Add(new { scenario=crestLethal, passed=false, error=error.ToString() }); }
            const string reactingEnemy = "hostess-refill-before-enemy-damage-reaction";
            try { rows.Add(ExecuteHostess(native, reactingEnemy, 1, true, true, true, withCrest:true,
                crestDeckCount:2, enemyHealth:6, reactiveEnemy:true)); }
            catch (Exception error) { rows.Add(new { scenario=reactingEnemy, passed=false, error=error.ToString() }); }
            foreach (var (fieldCard,name) in new[] {
                (1475086,"refill-before-night-attendant-tuck"), (1474117,"refill-before-void-guest-evolution"),
                (1050403,"refill-before-ghost-vanishes") })
            {
                try { rows.Add(ExecuteHostess(native,name,1,true,true,true,withCrest:true,
                    crestDeckCount:2,laterFieldCard:fieldCard)); }
                catch (Exception error) { rows.Add(new { scenario=name, passed=false, error=error.ToString() }); }
            }
            foreach (var (deckSize,name) in new[] { (3,"refill-with-cold-fort"), (2,"refill-with-cold-fort-deck-exhaustion") })
            {
                try { rows.Add(ExecuteHostess(native,name,1,true,true,true,withCrest:true,
                    crestDeckCount:deckSize,laterFieldCard:1474115)); }
                catch (Exception error) { rows.Add(new { scenario=name, passed=false, error=error.ToString() }); }
            }
            foreach (var reaction in new[] { false, true })
            {
                var name = reaction ? "refill-with-hand-entry-discard" : "refill-with-buffed-night-attendant";
                try { rows.Add(ExecuteHostess(native,name,1,true,true,true,withCrest:true,
                    crestDeckCount:2,buffedDeckCard:true,reactiveDeckCard:reaction)); }
                catch (Exception error) { rows.Add(new { scenario=name, passed=false, error=error.ToString() }); }
            }
            foreach (var (name, opponent) in new (string, RefillOpponent)[]
            {
                ("refill-with-opposing-void-guest", new(1474117)),
                ("refill-with-summon-listener", new(1475209)),
                ("refill-with-early-summon", new(1475209, true))
            })
            {
                try { rows.Add(ExecuteHostess(native,name,1,true,true,true,withCrest:true,
                    crestDeckCount:2,opposingRefill:opponent)); }
                catch (Exception error) { rows.Add(new { scenario=name, passed=false, error=error.ToString() }); }
            }
            foreach (var (name, victim, super, extraDraw, reaction) in new[]
            {
                ("ice-prison-after-crest-four",1474117,true,false,false),
                ("ice-prison-before-cold-fort-draw",1473444,false,true,false),
                ("ice-prison-death-draw-remains-unknown",1473444,true,true,true)
            })
            {
                try { rows.Add(ExecuteIcePrisonRefill(native,name,victim,super,extraDraw,reaction)); }
                catch (Exception error) { rows.Add(new {scenario=name,passed=false,error=error.ToString()}); }
            }
            var results=JsonSerializer.SerializeToElement(rows);
            var passed=results.EnumerateArray().Count(row=>row.GetProperty("passed").GetBoolean());
            File.WriteAllText(output,JsonSerializer.Serialize(new {
                externalProviderCalls=0,readOnlySource=true,isolatedSqliteSnapshot=true,
                passed,total=rows.Count,cases=rows,
                scope="Native card configurations, production evolution/attack/end-turn transactions and local tactical search. Synthetic neutral targets/leaders and optional reactive fixture. No full-match win-rate or real-model test."
            },new JsonSerializerOptions{WriteIndented=true}));
            if(passed!=rows.Count)throw new Exception($"Native end-turn cases failed: {passed}/{rows.Count}; inspect {output}");
            Console.WriteLine($"Native offline end-turn: {passed}/{rows.Count}; provider calls=0");
        }
        finally {
            GameDataProvider.SetInstanceForTesting(original);GameServiceProvider.ContextAccessor=originalAccessor;
            SqliteConnection.ClearAllPools();
            var resolved=Path.GetFullPath(snapshot);
            if(!resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)
                ||!Path.GetFileName(resolved).StartsWith("ai-endturn-engine-",StringComparison.Ordinal))throw new InvalidOperationException("Unexpected cleanup path");
            foreach(var suffix in new[]{"","-wal","-shm"})if(File.Exists(resolved+suffix))File.Delete(resolved+suffix);
        }
    }

    internal static void TerminalBoundary()
    {
        var original=GameDataProvider.Instance;var oldAccessor=GameServiceProvider.ContextAccessor;
        var definitions=new Dictionary<int,CardDefinition>{
            [LeaderId]=new(){Id=LeaderId,Name="Boundary leader",Type=CardType.Leader,Health=20},
            [UnitId]=new(){Id=UnitId,Name="Boundary deck card",Type=CardType.Follower,Attack=1,Health=3}};
        var data=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch {
            "GetCardDefinition"=>definitions.GetValueOrDefault((int)args![0]!),"GetAllCardDefinitions"=>definitions,
            "GetAbilityConfigs"=>new List<AbilityConfig>(),_=>null});
        try {
            GameDataProvider.SetInstanceForTesting(data);
            foreach(var lethal in new[]{false,true}) {
                var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
                using var services=Services(data,accessor);using var scope=services.CreateScope();var sp=scope.ServiceProvider;
                var own=new PlayerState(Guid.NewGuid());var enemy=new PlayerState(Guid.NewGuid());
                var game=new GameState("terminal-boundary-"+lethal,own,enemy,own.PlayerId){Status=GameStatus.InProgress,Phase=GamePhase.Main,
                    AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<IBuffManager>()};
                var factory=sp.GetRequiredService<ICardFactory>();
                own.Leader=factory.CreateAndRegisterNewCard(game,LeaderId,own.PlayerId,CardZone.Field);
                enemy.Leader=factory.CreateAndRegisterNewCard(game,LeaderId,enemy.PlayerId,CardZone.Field);
                enemy.Leader.Damage=lethal?18:0;
                factory.CreateAndRegisterNewCard(game,UnitId,enemy.PlayerId,CardZone.Deck);
                using var context=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId);
                var result=sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(context,
                    [new DamageEvent(enemy.Leader,2,own.Leader,DamageKind.Ability),new HealEvent(enemy.Leader,8,own.Leader),new TurnStartEvent(enemy)]).GetAwaiter().GetResult();
                if(!result.IsSuccess||result.IsCancelled)throw new Exception("Boundary transaction failed: "+result.ErrorMessage);
                if(lethal) {
                    if(game.WinnerId!=own.PlayerId||enemy.Leader.CurrentHealth!=0||enemy.Hand.Count!=0
                        ||result.ProcessedEvents.Count(ev=>ev is GameEndEvent)!=1
                        ||result.ProcessedEvents.Any(ev=>ev is HealEvent or TurnStartEvent))throw new Exception("Game continued after terminal damage");
                } else if(game.IsGameOver||enemy.Leader.CurrentHealth!=20||enemy.Hand.Count!=1
                    ||!result.ProcessedEvents.Any(ev=>ev is HealEvent)||!result.ProcessedEvents.Any(ev=>ev is TurnStartEvent))
                    throw new Exception("Nonterminal queued events were incorrectly discarded");
            }
        } finally {GameDataProvider.SetInstanceForTesting(original);GameServiceProvider.ContextAccessor=oldAccessor;}
    }

    private static object ExecuteEvolution(IGameDataProvider native, string name, bool ready, bool silenced, int enemyHealth)
    {
        const int aliceId = 1473469;
        var leader = new CardDefinition { Id=LeaderId, Name="Offline leader", Type=CardType.Leader, Health=20 };
        var data = Proxy<IGameDataProvider>.Make((method,args) => method.Name switch {
            "GetCardDefinition" when (int)args![0]! == LeaderId => leader,
            "GetAbilityConfigs" when (int)args![0]! == LeaderId => new List<AbilityConfig>(),
            _ => method.Invoke(native,args)
        });
        GameDataProvider.SetInstanceForTesting(data);
        var accessor = new GameContextAccessor();
        GameServiceProvider.ContextAccessor = accessor;
        using var services = Services(data,accessor);
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var own = new PlayerState(Guid.NewGuid()) { TurnNumber=6, IsEvolutionUnlocked=true, CurrentEvolutionPoints=1, MaxEvolutionPoints=1 };
        var enemy = new PlayerState(Guid.NewGuid()) { TurnNumber=6 };
        var game = new GameState(name,own,enemy,own.PlayerId) { Status=GameStatus.InProgress, Phase=GamePhase.Main,
            AbilityManager=sp.GetRequiredService<IAbilityManager>(), BuffManager=sp.GetRequiredService<IBuffManager>() };
        var factory = sp.GetRequiredService<ICardFactory>();
        own.Leader = factory.CreateAndRegisterNewCard(game,LeaderId,own.PlayerId,CardZone.Field);
        enemy.Leader = factory.CreateAndRegisterNewCard(game,LeaderId,enemy.PlayerId,CardZone.Field);
        enemy.Leader.Damage = 20 - enemyHealth;
        var alice = factory.CreateAndRegisterNewCard(game,aliceId,own.PlayerId,CardZone.Field);
        alice.ZoneStayTurn = ready ? 1 : 0;
        if (silenced) typeof(CardInstance).GetProperty("IsSilenced")!.SetValue(alice,true);
        if (!ready)
        {
            factory.CreateAndRegisterNewCard(game,1474120,own.PlayerId,CardZone.Field).ZoneStayTurn = 1;
            factory.CreateAndRegisterNewCard(game,1250402,own.PlayerId,CardZone.Field).ZoneStayTurn = 1;
        }
        using var context = new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId);
        var view = GameStateProjection.Create(game,context);
        var assembly = typeof(AiTurnExecutor).Assembly;
        var projection = assembly.GetType("ShadowverseBeyond.Server.Services.AiCombatProjection")!;
        var frame = projection.GetMethod("Read",Hidden)!.Invoke(null,[context,view,own.PlayerId])!;
        var replacements = (IReadOnlySet<Guid>)frame.GetType().GetProperty("OrdinaryEvolutionReplacements",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(frame)!;
        var search = assembly.GetType("ShadowverseBeyond.Server.Services.AiTacticalSearch")!;
        object? Plan(object? rules) => search.GetMethod("FindWithCombat",Hidden)!.Invoke(null,[view,own.PlayerId,CancellationToken.None,null,
            new Dictionary<Guid,(double,double)>{{alice.InstanceId,(silenced?0:2,0)}},null,null,null,rules]);
        (string? Kind,int? Card,bool Lethal) First(object? plan)
        {
            var move = plan?.GetType().GetProperty("First")!.GetValue(plan);
            return ((string?)move?.GetType().GetProperty("Type")!.GetValue(move),
                (move?.GetType().GetProperty("Source")!.GetValue(move) as CardInstanceDto)?.CardId,
                plan?.GetType().GetProperty("ProjectsLethal")!.GetValue(plan) is true);
        }
        var baseline = First(Plan(null));
        var updated = First(Plan(frame));
        var action = new EvolveActionDto { UnitUid=alice.InstanceId.ToString() };
        var wrapper = new PlayerActionWrapper { PlayerId=own.PlayerId, ActionType="PerformEvolve", ActionData=action };
        var outcomes = assembly.GetType("ShadowverseBeyond.Server.Services.AiActionOutcomes")!;
        var lease = outcomes.GetMethod("Begin",Hidden)!.Invoke(null,[game,wrapper,view,null])!;
        var result = sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(context,
            [new EvolveRequestedEvent(own.PlayerId,action)]).GetAwaiter().GetResult();
        outcomes.GetMethod("Observe",Hidden)!.Invoke(null,[lease,game,result.ProcessedEvents.ToArray(),null]);
        wrapper.MarkCompleted();
        var fact = outcomes.GetMethod("Finish",Hidden)!.Invoke(null,[lease]) as AiCompletedActionFact;
        var lethalSettled = !ready || silenced;
        if (!lethalSettled)
        {
            var attack = new AttackActionDto { AttackerUid=alice.InstanceId, TargetUid=enemy.Leader.InstanceId };
            var attackResult = sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(context,
                [new AttackRequestedEvent(own.PlayerId,attack)]).GetAwaiter().GetResult();
            lethalSettled = attackResult.IsSuccess && enemy.Leader.CurrentHealth == 0 && game.WinnerId == own.PlayerId;
        }
        var passed = lethalSettled && result.IsSuccess && !result.IsCancelled && replacements.Contains(alice.InstanceId) == !silenced
            && alice.IsSuperEvolved == !silenced && fact?.Before.EP == 1 && fact.After.EP == 0
            && fact.Before.SEP == fact.After.SEP && (fact.EvolutionSettlement != null) == !silenced
            && (!ready || silenced || (updated.Kind == "PerformEvolve" && updated.Card == aliceId && updated.Lethal && !baseline.Lethal))
            && (!silenced || !updated.Lethal)
            && (ready || updated.Kind != "PerformEvolve" || updated.Card != aliceId);
        return new { scenario=name, passed, recognized=replacements.Contains(alice.InstanceId),
            baseline=new { baseline.Kind,baseline.Card,baseline.Lethal }, updated=new { updated.Kind,updated.Card,updated.Lethal },
            alice.IsSuperEvolved, alice.CurrentAttack, alice.CurrentHealth, ep=own.CurrentEvolutionPoints, sep=own.CurrentSuperEvolutionPoints,
            result.IsSuccess, result.ErrorMessage, settlement=fact?.EvolutionSettlement, recorded=fact!=null, lethalSettled,
            events=result.ProcessedEvents.Select(item=>item.EventType.ToString()).ToArray() };
    }

    private static object ExecuteIcePrisonRefill(IGameDataProvider native,string name,int victimId,bool super,bool extraDraw,bool reaction)
    {
        var leader=new CardDefinition {Id=LeaderId,Name="Offline leader",Type=CardType.Leader,Health=20};
        var filler=new CardDefinition {Id=UnitId,Name="Offline deck card",Type=CardType.Follower,Attack=1,Health=1};
        var data=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch
        {
            "GetCardDefinition" when (int)args![0]! == LeaderId=>leader,
            "GetCardDefinition" when (int)args![0]! == UnitId=>filler,
            "GetAbilityConfigs" when (int)args![0]! is LeaderId or UnitId=>new List<AbilityConfig>(),
            _=>method.Invoke(native,args)
        });
        GameDataProvider.SetInstanceForTesting(data);
        var accessor=new GameContextAccessor();
        GameServiceProvider.ContextAccessor=accessor;
        CardInstance victim=null!;
        var pending=Proxy<IPendingActionService>.Make((method,args)=>
        {
            if (method.ReturnType==typeof(bool)) return false;
            if (method.Name!=nameof(IPendingActionService.WaitForPlayerChoiceAsync))
                throw new InvalidOperationException($"Unexpected choice call: {method.Name}");
            var request=(PlayerChoiceRequest)args![1]!;
            if (request.SelectableTargetIds is not [var id] || id!=victim.InstanceId)
                throw new InvalidOperationException("Ice Prison must target the prepared follower.");
            return Task.FromResult(new PlayerChoiceResult {ChosenTargetIds=[id]});
        });
        using var services=Services(data,accessor,pending);
        using var scope=services.CreateScope();
        var sp=scope.ServiceProvider;
        var own=new PlayerState(Guid.NewGuid()){TurnNumber=7,IsEvolutionUnlocked=true,IsSuperEvolutionUnlocked=super};
        var enemy=new PlayerState(Guid.NewGuid()){TurnNumber=7,CurrentPlayPoints=10,MaxPlayPoints=10};
        var game=new GameState(name,own,enemy,enemy.PlayerId)
        {
            Status=GameStatus.InProgress,Phase=GamePhase.Main,
            AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<IBuffManager>()
        };
        var factory=sp.GetRequiredService<ICardFactory>();
        own.Leader=factory.CreateAndRegisterNewCard(game,GameConstants.Cards.LeaderCardId,own.PlayerId,CardZone.Field);
        enemy.Leader=factory.CreateAndRegisterNewCard(game,LeaderId,enemy.PlayerId,CardZone.Field);
        victim=factory.CreateAndRegisterNewCard(game,victimId,own.PlayerId,CardZone.Field);
        factory.CreateAndRegisterNewCard(game,1474276,own.PlayerId,CardZone.Emblem);
        for (var i=0;i<5;i++) factory.CreateAndRegisterNewCard(game,UnitId,own.PlayerId,CardZone.Deck);
        factory.CreateAndRegisterNewCard(game,UnitId,enemy.PlayerId,CardZone.Deck);
        var fort=extraDraw?factory.CreateAndRegisterNewCard(game,1474115,own.PlayerId,CardZone.Field):null;
        if (reaction)
        {
            var whenDestroyed=new AbilityConfig
            {
                Id=200000010308,CardId=1474115,Type=AbilityType.Triggered,
                Trigger=new(){EventTypes=[EventType.Destroy],ValidZones=[CardZone.Field],Timing=TriggerTiming.On},
                Effects=[new(){Type=EffectType.Draw,Parameters=new()
                {
                    ["DrawAmount"]=new StaticValueConfig{Value=1},
                    ["PlayerScope"]=new StaticValueConfig{Value="self"},
                    ["UseSearchMode"]=new StaticValueConfig{Value=false}
                }}]
            };
            game.AbilityManager.CreateAbilitiesForCard(fort!,[whenDestroyed],null,game);
        }
        var ice=factory.CreateAndRegisterNewCard(game,1474180,enemy.PlayerId,CardZone.Hand);
        var transactions=sp.GetRequiredService<IGameActionTransactionService>();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using (var castContext=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,enemy.PlayerId,timeout.Token))
        {
            var play=new PlayCardRequestedEvent(enemy.PlayerId,new PlayCardActionDto
                {CardUid=ice.InstanceId,PlayMode=PlayMode.Base,PaidCost=ice.CurrentCost,TargetUids=[victim.InstanceId]});
            var cast=transactions.ProcessTransactionAsync(castContext,[play]).GetAwaiter().GetResult();
            if (!cast.IsSuccess || cast.IsCancelled || !cast.ProcessedEvents.OfType<GrantAbilityEvent>()
                .Any(grant=>grant.TargetCard?.InstanceId==victim.InstanceId))
                throw new InvalidOperationException(cast.ErrorMessage??"Native Ice Prison did not grant its TurnEnd ability.");
        }

        game.CurrentPlayerId=own.PlayerId;
        using var context=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId,timeout.Token);
        var view=GameStateProjection.Create(game,context);
        var forecastType=typeof(AiTurnExecutor).Assembly.GetType("ShadowverseBeyond.Server.Services.AiEndTurnForecast")!;
        var read=(Task)forecastType.GetMethod("ReadAsync",Hidden)!.Invoke(null,[context,view.Players[0]])!;
        read.GetAwaiter().GetResult();
        var forecast=read.GetType().GetProperty("Result")!.GetValue(read)!;
        var facts=JsonSerializer.SerializeToElement(forecastType.GetMethod("ForModel",Hidden)!.Invoke(null,[forecast,view,own.PlayerId]));
        var refill=facts.GetProperty("refill");
        var firstDraw=super?4:2;
        var correctForecast=refill.ValueKind==JsonValueKind.Object && refill.GetProperty("drawCount").GetInt32()==firstDraw
            && refill.GetProperty("laterDrawsKnown").GetBoolean()==!reaction
            && refill.GetProperty("laterFieldDraws").GetArrayLength()==(extraDraw && !reaction?1:0)
            && facts.GetProperty("deckExhaustion").ValueKind==JsonValueKind.Null
            && facts.GetProperty("damage").GetArrayLength()==0;
        var end=transactions.ProcessTransactionAsync(context,[new EndTurnRequestedEvent(own.PlayerId)]).GetAwaiter().GetResult();
        var events=end.ProcessedEvents.ToList();
        var firstBatch=events.FindIndex(item=>item is DrawEvent draw && draw.DrawingPlayer?.PlayerId==own.PlayerId);
        var destroyed=events.FindIndex(item=>item is DestroyEvent destroy && destroy.DestroyTarget?.InstanceId==victim.InstanceId);
        var settled=events.OfType<DrawEvent>().Where(draw=>draw.DrawingPlayer?.PlayerId==own.PlayerId)
            .Select(draw=>draw.DrawnCards.Count).ToArray();
        var expected=reaction?new[]{firstDraw,1,1}:extraDraw?new[]{firstDraw,1}:new[]{firstDraw};
        var passed=correctForecast && end.IsSuccess && !end.IsCancelled && firstBatch>=0 && destroyed>firstBatch
            && victim.CurrentZone==CardZone.Graveyard && settled.SequenceEqual(expected) && own.Hand.Count==expected.Sum();
        return new {scenario=name,passed,correctForecast,refill,firstBatch,destroyed,settled,handAfter=own.Hand.Count};
    }

    private sealed record LastCardRun(string[] Actions, bool Warned, bool Exhausted, bool Won, int OwnHand, bool KeptOriginal,
        AiEvolutionMatchFacts Measurement);

    private static object ExecuteLastCardPair(IGameDataProvider native,string name,bool drawOnPlay,bool storm)
    {
        LastCardRun Run(bool enabled)
        {
            const int scriptedCardId=2000000102;
            var leader=new CardDefinition {Id=LeaderId,Name="Offline leader",Type=CardType.Leader,Health=20};
            var unit=new CardDefinition {Id=UnitId,Name="Offline replacement",Type=CardType.Follower,Attack=1,Health=1,Cost=20};
            var scripted=new CardDefinition {Id=scriptedCardId,Name="Offline last follower",Type=CardType.Follower,
                Attack=5,Health=3,Cost=1,Keywords=storm?[nameof(CardKeyword.Storm)]:[]};
            var ability=new AbilityConfig
            {
                Id=200000010201, CardId=scriptedCardId, Type=AbilityType.Triggered,
                Trigger=new(){EventTypes=[EventType.PlayCard],RelationType=TriggerRelationType.TargetSelf,Timing=TriggerTiming.On},
                Effects=[new(){Type=EffectType.Draw,Parameters=new()
                {
                    ["DrawAmount"]=new StaticValueConfig{Value=1},
                    ["PlayerScope"]=new StaticValueConfig{Value="self"},
                    ["UseSearchMode"]=new StaticValueConfig{Value=false}
                }}]
            };
            var data=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch
            {
                "GetCardDefinition" when (int)args![0]! ==LeaderId => leader,
                "GetCardDefinition" when (int)args![0]! ==UnitId => unit,
                "GetCardDefinition" when (int)args![0]! ==scriptedCardId => scripted,
                "GetAbilityConfigs" when (int)args![0]! is LeaderId or UnitId => new List<AbilityConfig>(),
                "GetAbilityConfigs" when (int)args![0]! ==scriptedCardId => drawOnPlay?new List<AbilityConfig>{ability}:new List<AbilityConfig>(),
                _=>method.Invoke(native,args)
            });
            GameDataProvider.SetInstanceForTesting(data);
            var accessor=new GameContextAccessor();
            GameServiceProvider.ContextAccessor=accessor;
            var own=new PlayerState(Guid.NewGuid()) {IsAI=true,TurnNumber=7,CurrentPlayPoints=1,MaxPlayPoints=7,IsSuperEvolutionUnlocked=true};
            var enemy=new PlayerState(Guid.NewGuid()) {TurnNumber=7};
            var actions=new List<string>();
            var exhausted=false;
            GameState game=null!;
            ServiceProvider services=null!;
            async Task<bool> Submit(PlayerActionWrapper action)
            {
                using var scope=services.CreateScope();
                var sp=scope.ServiceProvider;
                IGameEvent root=action.ActionData switch
                {
                    PlayCardActionDto play=>new PlayCardRequestedEvent(action.PlayerId,play),
                    AttackActionDto attack=>new AttackRequestedEvent(action.PlayerId,attack),
                    _ when action.ActionType=="EndTurn"=>new EndTurnRequestedEvent(action.PlayerId),
                    _=>throw new InvalidOperationException("Unexpected action in last-card fixture")
                };
                using var context=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,action.PlayerId,triggeringEvent:root);
                var result=await sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(context,[root]);
                if (!result.IsSuccess || result.IsCancelled) throw new InvalidOperationException(result.ErrorMessage);
                actions.Add(action.ActionType);
                exhausted|=result.ProcessedEvents.OfType<DeckEmptyOnDrawEvent>().Any(draw=>draw.Source?.Player?.PlayerId==own.PlayerId);
                action.MarkCompleted();
                return true;
            }
            Task<PlayabilityResultDto> Playable(Guid uid)
            {
                using var scope=services.CreateScope();
                var sp=scope.ServiceProvider;
                using var context=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId);
                var result=PlayModeResolver.Resolve(game.FindCardInstance(uid)!,own,context);
                return Task.FromResult(new PlayabilityResultDto
                {
                    IsPlayable=result.HasAvailableMode,SelectedPlayMode=result.SelectedMode,PlayModes=result.Modes.ToList()
                });
            }
            var games=Proxy<IGameService>.Make((method,args)=>method.Name switch
            {
                "GetGameState"=>game,
                "EnqueuePlayerActionAsync"=>Submit((PlayerActionWrapper)args![1]!),
                "GetCardPlayabilityAsync" or "GetCardPlayabilityForModeAsync"=>Playable((Guid)args![2]!),
                _=>null
            });
            using (services=Services(data,accessor,games:games))
            {
                using var scope=services.CreateScope();
                var sp=scope.ServiceProvider;
                game=new(name,own,enemy,own.PlayerId){IsLocalAiTraining=true,Status=GameStatus.InProgress,Phase=GamePhase.Main,
                    AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<IBuffManager>()};
                var factory=sp.GetRequiredService<ICardFactory>();
                own.Leader=factory.CreateAndRegisterNewCard(game,GameConstants.Cards.LeaderCardId,own.PlayerId,CardZone.Field);
                enemy.Leader=factory.CreateAndRegisterNewCard(game,LeaderId,enemy.PlayerId,CardZone.Field);
                if (storm) enemy.Leader.Damage=15;
                factory.CreateAndRegisterNewCard(game,1474276,own.PlayerId,CardZone.Emblem);
                factory.CreateAndRegisterNewCard(game,UnitId,own.PlayerId,CardZone.Deck);
                factory.CreateAndRegisterNewCard(game,UnitId,enemy.PlayerId,CardZone.Deck);
                var source=factory.CreateAndRegisterNewCard(game,storm||drawOnPlay?scriptedCardId:1250402,own.PlayerId,CardZone.Hand);
                var ai=Proxy<IAiService>.Make((method,args)=>method.Name=="DecidePlayCardAsync"
                    ?Task.FromResult<PlayCardActionDto?>(own.Hand.Contains(source)
                        ?new(){CardUid=source.InstanceId,PlayMode=PlayMode.Base,PaidCost=source.CurrentCost}:null)
                    :throw new InvalidOperationException($"Unexpected AI call: {method.Name}"));
                var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
                    {["AiStrategy:BenchmarkAvoidDeckExhaustion"]=enabled.ToString()}).Build();
                var measurementType=typeof(AiTurnExecutor).Assembly.GetType("ShadowverseBeyond.Server.Services.AiEvolutionEvaluation")!;
                measurementType.GetMethod("Bind",Hidden)!.Invoke(null,[game,own.PlayerId,true]);
                var warned=false;
                using (var context=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId))
                {
                    var view=GameStateProjection.Create(game,context);
                    var forecastType=typeof(AiTurnExecutor).Assembly.GetType("ShadowverseBeyond.Server.Services.AiEndTurnForecast")!;
                    var read=(Task)forecastType.GetMethod("ReadForPolicyAsync",Hidden)!.Invoke(null,
                        [context,view.Players.Single(player=>player.PlayerId==own.PlayerId),true])!;
                    read.GetAwaiter().GetResult();
                    var forecast=read.GetType().GetProperty("Result")!.GetValue(read);
                    var facts=JsonSerializer.SerializeToElement(forecastType.GetMethod("ForModel",Hidden)!.Invoke(null,[forecast,view,own.PlayerId]));
                    var risk=facts.GetProperty("deckExhaustion");
                    warned=risk.ValueKind==JsonValueKind.Object && risk.GetProperty("drawCount").GetInt32()==4
                        && risk.GetProperty("availableAfterInsert").GetInt32()==3
                        && risk.GetProperty("lastHandPlay").ValueKind==JsonValueKind.Object
                        && risk.GetProperty("lastHandPlay").GetProperty("cardId").GetInt32()==source.CurrentDefinitionId;
                }
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var executor=new AiTurnExecutor(games,ai,NullLogger<AiTurnExecutor>.Instance,sp.GetRequiredService<IMapper>(),services,configuration:config);
                executor.ExecuteTurnAsync(game.GameId,own.PlayerId,timeout.Token).GetAwaiter().GetResult();
                var measurement=measurementType.GetMethod("For",Hidden)!.Invoke(null,[game]);
                var metrics=(AiEvolutionMatchFacts)measurementType.GetMethod("Snapshot",BindingFlags.Instance|BindingFlags.NonPublic)!
                    .Invoke(measurement,[game])!;
                return new(actions.ToArray(),warned,exhausted,game.WinnerId==own.PlayerId,own.Hand.Count,own.Hand.Contains(source),metrics);
            }
        }

        var baseline=Run(false);
        var current=Run(true);
        var correctMeasurement=baseline.Measurement.DeckExhaustionForecastTurns==(drawOnPlay?0:1)
            && current.Measurement.DeckExhaustionForecastTurns==(drawOnPlay?0:1)
            && baseline.Measurement.DeckExhaustionClearCommits==(drawOnPlay?0:1)
            && current.Measurement.DeckExhaustionClearCommits==(storm?1:0)
            && baseline.Measurement.DeckExhaustionEndTurnCommits==(!storm && !drawOnPlay?1:0)
            && current.Measurement.DeckExhaustionEndTurnCommits==0
            && baseline.Measurement.CandidateDeckExhausted==baseline.Exhausted
            && !current.Measurement.CandidateDeckExhausted;
        var passed=correctMeasurement && baseline.Warned==!drawOnPlay && current.Warned==!drawOnPlay && (storm
            ? baseline.Won && current.Won && !current.Exhausted
                && current.Actions.SequenceEqual(["PlayCard","PerformAttack"])
            : drawOnPlay
                ? !baseline.Exhausted && !current.Exhausted && current.OwnHand==1 && !current.KeptOriginal
                    && current.Actions.SequenceEqual(["PlayCard","EndTurn"])
                : baseline.Exhausted && !current.Exhausted && current.KeptOriginal && current.OwnHand==1
                    && baseline.Actions.SequenceEqual(["PlayCard","EndTurn"]) && current.Actions.SequenceEqual(["EndTurn"]));
        return new {scenario=name,passed,baseline,current};
    }

    private static object ExecuteHostess(IGameDataProvider native, string name, int count, bool super, bool withReiss, bool nativeLeader, bool withCrest=false, int crestDeckCount=6, int enemyHealth=20,bool reactiveEnemy=false,int laterFieldCard=0,bool buffedDeckCard=false,bool reactiveDeckCard=false,RefillOpponent? opposingRefill=null,bool checkExhaustionChoice=false,
        bool laterDrawFirst=false,bool tuckBeforeLaterDraw=false,bool forceClear=false)
    {
        const int hostessId = 1474116;
        var leader = new CardDefinition { Id=LeaderId, Name="Offline leader", Type=CardType.Leader, Health=20 };
        var neutral = new CardDefinition { Id=UnitId, Name="Offline follower", Type=CardType.Follower, Attack=3, Health=3 };
        var data = Proxy<IGameDataProvider>.Make((method,args) => method.Name switch {
            "GetCardDefinition" when (int)args![0]! == LeaderId => leader,
            "GetCardDefinition" when (int)args![0]! == UnitId => neutral,
            "GetAbilityConfigs" when (int)args![0]! is LeaderId or UnitId => new List<AbilityConfig>(),
            _ => method.Invoke(native,args)
        });
        GameDataProvider.SetInstanceForTesting(data);
        var accessor = new GameContextAccessor();
        GameServiceProvider.ContextAccessor = accessor;
        var discardChoices = new List<PlayerChoiceRequest>();
        var pending = Proxy<IPendingActionService>.Make((method, args) =>
        {
            if (method.ReturnType == typeof(bool)) return false;
            if (method.Name != nameof(IPendingActionService.WaitForPlayerChoiceAsync))
                throw new InvalidOperationException($"Unexpected selection call: {method.Name}");
            var choice = (PlayerChoiceRequest)args![1]!;
            discardChoices.Add(choice);
            return Task.FromResult(new PlayerChoiceResult { ChosenTargetIds = choice.SelectableTargetIds.ToList() });
        });
        using var services = Services(data,accessor,pending);
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var own = new PlayerState(Guid.NewGuid()) { TurnNumber=7, IsEvolutionUnlocked=true, IsSuperEvolutionUnlocked=!withCrest||super,
            CurrentEvolutionPoints=1, MaxEvolutionPoints=1, CurrentSuperEvolutionPoints=1, MaxSuperEvolutionPoints=1 };
        var enemy = new PlayerState(Guid.NewGuid()) { TurnNumber=7 };
        var game = new GameState(name,own,enemy,own.PlayerId) { Status=GameStatus.InProgress, Phase=GamePhase.Main,
            AbilityManager=sp.GetRequiredService<IAbilityManager>(), BuffManager=sp.GetRequiredService<IBuffManager>() };
        var factory = sp.GetRequiredService<ICardFactory>();
        own.Leader = factory.CreateAndRegisterNewCard(game,nativeLeader?GameConstants.Cards.LeaderCardId:LeaderId,own.PlayerId,CardZone.Field);
        own.Leader.Damage = 5;
        enemy.Leader = factory.CreateAndRegisterNewCard(game,LeaderId,enemy.PlayerId,CardZone.Field);
        enemy.Leader.Damage = 20-enemyHealth;
        var opposingUnit = factory.CreateAndRegisterNewCard(game,
            opposingRefill?.CardId ?? (reactiveEnemy ? 1474296 : UnitId), enemy.PlayerId, CardZone.Field);
        if (opposingRefill?.SummonsBeforeDraw == true)
        {
            var summon = new AbilityConfig
            {
                Id=200000010302,Name="Offline pre-refill summon",CardId=opposingUnit.CurrentDefinitionId,
                Trigger=new(){EventTypes=[EventType.TurnEnd],ValidZones=[CardZone.Field],Timing=TriggerTiming.Before},
                Effects=[new(){Type=EffectType.Summon,Parameters=new()
                {
                    ["CardDefinitionId"]=new StaticValueConfig{Value=new[]{UnitId}},
                    ["PlayerScope"]=new StaticValueConfig{Value="self"},
                    ["CardSourceMode"]=new StaticValueConfig{Value="definition"},
                    ["SummonCount"]=new StaticValueConfig{Value=1}
                }}]
            };
            game.AbilityManager.CreateAbilitiesForCard(opposingUnit,[summon],null,game);
        }
        factory.CreateAndRegisterNewCard(game,UnitId,enemy.PlayerId,CardZone.Deck);
        var laterSource = laterDrawFirst ? factory.CreateAndRegisterNewCard(game,laterFieldCard,own.PlayerId,CardZone.Field) : null;
        var hostess = factory.CreateAndRegisterNewCard(game,hostessId,own.PlayerId,CardZone.Field);
        if(checkExhaustionChoice && laterFieldCard==0 && enemyHealth==5) hostess.ZoneStayTurn=1;
        if (withReiss) factory.CreateAndRegisterNewCard(game,1474120,own.PlayerId,CardZone.Field);
        if (tuckBeforeLaterDraw) factory.CreateAndRegisterNewCard(game,1475086,own.PlayerId,CardZone.Field);
        if (!laterDrawFirst && laterFieldCard!=0)
            laterSource=factory.CreateAndRegisterNewCard(game,laterFieldCard,own.PlayerId,CardZone.Field);
        CardInstance? deckCard = null;
        if (withCrest)
        {
            factory.CreateAndRegisterNewCard(game,1474276,own.PlayerId,CardZone.Emblem);
            var cards = new[] { buffedDeckCard ? 1475086 : 1473441, 1474118 };
            for (var i = 0; i < crestDeckCount; i++)
            {
                var card = factory.CreateAndRegisterNewCard(game,i<cards.Length?cards[i]:UnitId,own.PlayerId,CardZone.Deck);
                if (buffedDeckCard && i==0) deckCard=card;
            }
        }
        for (var i = 0; i < count; i++) factory.CreateAndRegisterNewCard(game,UnitId,own.PlayerId,CardZone.Hand);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var context = new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId,timeout.Token);
        var transactions = sp.GetRequiredService<IGameActionTransactionService>();
        if (deckCard != null)
        {
            var applied = transactions.ProcessTransactionAsync(context,
                [new StatChangedEvent(deckCard,2,null,null),
                 new KeywordAddedEvent(CardKeyword.Storm,deckCard)]).GetAwaiter().GetResult();
            if (!applied.IsSuccess || !deckCard.HasKeyword(CardKeyword.Storm)
                || deckCard.CurrentAttack!=deckCard.CurrentDefinition.Attack+2)
                throw new InvalidOperationException("Could not apply passive draw fixture buffs.");
            if (reactiveDeckCard)
            {
                var discardOnEntry = new AbilityConfig
                {
                    Id=200000010301,Name="Offline discard on draw",CardId=deckCard.CurrentDefinitionId,
                    Trigger=new(){EventTypes=[EventType.CardMoved],ValidZones=[CardZone.Hand],
                        RelationType=TriggerRelationType.TargetSelf,Timing=TriggerTiming.After},
                    Targets=new(){["self"]=new(){Query=new(){SourcePoolExpression="context.sourceCard"}}},
                    Effects=[new(){Type=EffectType.Discard}]
                };
                game.AbilityManager.CreateAbilitiesForCard(deckCard,[discardOnEntry],null,game);
            }
        }
        var view = GameStateProjection.Create(game,context);
        // In the SEP cases the ordinary point is reserved elsewhere for this probe.
        var searchView = super ? view with { Players = view.Players.Select(player=>player.PlayerId==own.PlayerId
            ? player with { CanEvolve=false } : player).ToList() } : view;
        var assembly = typeof(AiTurnExecutor).Assembly;
        var forecastType = assembly.GetType("ShadowverseBeyond.Server.Services.AiEndTurnForecast")!;
        var read = (Task)forecastType.GetMethod("ReadAsync",Hidden)!.Invoke(null,[context,searchView.Players.Single(player=>player.PlayerId==own.PlayerId)])!;
        read.GetAwaiter().GetResult();
        var forecast = read.GetType().GetProperty("Result")!.GetValue(read);
        var modelFacts = JsonSerializer.SerializeToElement(forecastType.GetMethod("ForModel",Hidden)!.Invoke(null,[forecast,searchView,own.PlayerId]));
        var handClear = modelFacts.GetProperty("handClearEvolution").EnumerateArray().ToArray();
        var correctChoiceFacts = handClear is [var route] && route.GetProperty("discardCount").GetInt32()==count
            && route.GetProperty("requiresDiscardChoice").GetBoolean()==(count==3);
        if (discardChoices.Count!=0) throw new InvalidOperationException("Forecast requested a discard choice.");
        var shortDeck = withCrest && super && crestDeckCount<2;
        var withColdFort = laterFieldCard==1474115;
        var combinedShortDeck = withColdFort && crestDeckCount<3;
        var refillBlock = forecast?.GetType().GetProperty("RefillBlock",BindingFlags.Instance|BindingFlags.NonPublic)!
            .GetValue(forecast) as AiRefillBlock;
        var hasRefill = forecast?.GetType().GetProperty("Refill",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(forecast) != null;
        var search = assembly.GetType("ShadowverseBeyond.Server.Services.AiTacticalSearch")!;
        var plan = search.GetMethod("FindWithEndTurn",Hidden)!.Invoke(null,[searchView,own.PlayerId,timeout.Token,null,null,forecast]);
        var projectsLethal = plan?.GetType().GetProperty("ProjectsLethal")!.GetValue(plan) is true;
        bool? baselineLethal = null;
        if(checkExhaustionChoice)
        {
            var baselineRead=(Task)forecastType.GetMethod("ReadForPolicyAsync",Hidden)!.Invoke(null,
                [context,searchView.Players.Single(player=>player.PlayerId==own.PlayerId),false])!;
            baselineRead.GetAwaiter().GetResult();
            var baseline=search.GetMethod("FindWithEndTurn",Hidden)!.Invoke(null,[searchView,own.PlayerId,timeout.Token,null,null,
                baselineRead.GetType().GetProperty("Result")!.GetValue(baselineRead)]);
            static string? Kind(object? route)
            {
                var firstMove=route?.GetType().GetProperty("First")!.GetValue(route);
                return firstMove?.GetType().GetProperty("Type")!.GetValue(firstMove) as string;
            }
            var baselineKind=Kind(baseline);
            var selectedKind=Kind(plan);
            static int? SourceId(object? route)
            {
                var move=route?.GetType().GetProperty("First")!.GetValue(route);
                return (move?.GetType().GetProperty("Source")!.GetValue(move) as CardInstanceDto)?.CardId;
            }
            var baselineSource=SourceId(baseline);
            var selectedSource=SourceId(plan);
            var facts=JsonSerializer.SerializeToElement(forecastType.GetMethod("ForModel",Hidden)!.Invoke(null,[forecast,searchView,own.PlayerId]));
            var warning=facts.GetProperty("deckExhaustion");
            var earlyTurnEndWin=withColdFort && !laterDrawFirst && enemyHealth==5;
            var correctWarning=tuckBeforeLaterDraw ? warning.ValueKind==JsonValueKind.Null
                : warning.ValueKind==JsonValueKind.Object && warning.GetProperty("drawCount").GetInt32()==4
                    && warning.GetProperty("availableAfterInsert").GetInt32()==(withColdFort?4:3)
                    && (withColdFort ? warning.GetProperty("laterDraw").GetProperty("source").GetProperty("cardId").GetInt32()==1474115
                        && warning.GetProperty("damageBeforeLaterDraw").GetArrayLength()==(laterDrawFirst?0:2)
                        : warning.GetProperty("laterDraw").ValueKind==JsonValueKind.Null);
            var committed=new List<IGameEvent>();
            var executionPlan=plan;
            if (forceClear || tuckBeforeLaterDraw && selectedSource!=hostessId)
            {
                var evolveAction=new SuperEvolveActionDto { UnitUid=hostess.InstanceId.ToString() };
                var evolved=transactions.ProcessTransactionAsync(context,[new SuperEvolveRequestedEvent(own.PlayerId,evolveAction)]).GetAwaiter().GetResult();
                if(!evolved.IsSuccess || evolved.IsCancelled) throw new InvalidOperationException(evolved.ErrorMessage);
                committed.AddRange(evolved.ProcessedEvents);
                executionPlan=null;
            }
            if(executionPlan!=null)
            {
                var moves=(System.Collections.IEnumerable)executionPlan.GetType().GetProperty("Steps",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(executionPlan)!;
                foreach(var move in moves)
                {
                    var action=move.GetType().GetProperty("Data")!.GetValue(move);
                    IGameEvent nativeMove=action switch
                    {
                        SuperEvolveActionDto evo=>new SuperEvolveRequestedEvent(own.PlayerId,evo),
                        AttackActionDto attack=>new AttackRequestedEvent(own.PlayerId,attack),
                        _=>throw new InvalidOperationException("Unexpected exhaustion fixture action")
                    };
                    var settled=transactions.ProcessTransactionAsync(context,[nativeMove]).GetAwaiter().GetResult();
                    if(!settled.IsSuccess || settled.IsCancelled)throw new InvalidOperationException(settled.ErrorMessage);
                    committed.AddRange(settled.ProcessedEvents);
                }
            }
            if(!game.IsGameOver)
            {
                var endResult=transactions.ProcessTransactionAsync(context,[new EndTurnRequestedEvent(own.PlayerId)]).GetAwaiter().GetResult();
                if(!endResult.IsSuccess || endResult.IsCancelled)throw new InvalidOperationException(endResult.ErrorMessage);
                committed.AddRange(endResult.ProcessedEvents);
            }
            var exhausted=committed.OfType<DeckEmptyOnDrawEvent>().Any(item=>item.Source?.Player?.PlayerId==own.PlayerId);
            var drawCounts=committed.OfType<DrawEvent>().Where(draw=>draw.DrawingPlayer?.PlayerId==own.PlayerId)
                .Select(draw=>draw.DrawnCards.Count).ToArray();
            var expectedRoute=forceClear ? exhausted && game.WinnerId==enemy.PlayerId && enemy.Leader.CurrentHealth==enemyHealth
                    && drawCounts.SequenceEqual([4,0])
                : tuckBeforeLaterDraw ? !game.IsGameOver && own.Hand.Count==5 && drawCounts.SequenceEqual([4,1])
                : earlyTurnEndWin ? selectedSource==hostessId && projectsLethal && game.WinnerId==own.PlayerId
                    && drawCounts.SequenceEqual([4])
                : !withColdFort && enemyHealth==5
                    ? selectedKind=="PerformSuperEvolve" && projectsLethal && game.WinnerId==own.PlayerId && own.Hand.Count==0
                    : baselineKind=="PerformSuperEvolve" && selectedSource!=hostessId && !game.IsGameOver && own.Hand.Count>=1;
            return new {scenario=name,passed=correctWarning && expectedRoute && (forceClear || !exhausted),baselineKind,baselineSource,
                selectedKind,selectedSource,correctWarning,warning,projectsLethal,ownHand=own.Hand.Count,exhausted,drawCounts,
                won=game.WinnerId==own.PlayerId};
        }
        if (enemyHealth==5)
        {
            var baselineRead = (Task)forecastType.GetMethod("ReadForPolicyAsync",Hidden)!.Invoke(null,
                [context,searchView.Players.Single(player=>player.PlayerId==own.PlayerId),false])!;
            baselineRead.GetAwaiter().GetResult();
            var baseline = search.GetMethod("FindWithEndTurn",Hidden)!.Invoke(null,[searchView,own.PlayerId,timeout.Token,null,null,
                baselineRead.GetType().GetProperty("Result")!.GetValue(baselineRead)]);
            baselineLethal = baseline?.GetType().GetProperty("ProjectsLethal")!.GetValue(baseline) is true;
        }
        var first = plan?.GetType().GetProperty("First")!.GetValue(plan);
        var selected = first?.GetType().GetProperty("Source")!.GetValue(first) as CardInstanceDto;
        var actionKind = first?.GetType().GetProperty("Type")!.GetValue(first) as string;
        var outcome = plan?.GetType().GetProperty("Outcome",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(plan);
        var projectedEnemyHealth = outcome?.GetType().GetProperty("EnemyHealth")!.GetValue(outcome) as int?;
        var projectedOwnHealth = outcome?.GetType().GetProperty("OwnHealth")!.GetValue(outcome) as int?;
        var projectedEnemyField = outcome?.GetType().GetProperty("EnemyField")!.GetValue(outcome) as int?;
        var projectedDraw = outcome?.GetType().GetProperty("OwnEndTurnDraw")!.GetValue(outcome) as int?;
        var expectedKind = super ? "PerformSuperEvolve" : "PerformEvolve";
        var plannedClear = selected?.CardId == hostessId && actionKind == expectedKind
            && projectedOwnHealth == 18 && projectedEnemyField == 0;
        var steps = plan?.GetType().GetProperty("Steps",BindingFlags.Instance|BindingFlags.NonPublic)!
            .GetValue(plan) as System.Collections.IEnumerable;
        IGameEvent request = super
            ? new SuperEvolveRequestedEvent(own.PlayerId,new SuperEvolveActionDto { UnitUid=hostess.InstanceId.ToString() })
            : new EvolveRequestedEvent(own.PlayerId,new EvolveActionDto { UnitUid=hostess.InstanceId.ToString() });
        var evolve = transactions.ProcessTransactionAsync(context,[request]).GetAwaiter().GetResult();
        var handAfter = own.Hand.Count;
        var discarded = evolve.ProcessedEvents.OfType<CardDiscardedEvent>().Count();
        var ownHealth = own.Leader.CurrentHealth;
        if (reactiveEnemy || laterSource!=null || opposingRefill!=null)
        {
            // The hand-clear projection has a separate guard. Here we want the exact empty-hand position
            // after the native evolution, with the opposing reaction still on the board.
            var afterEvolution = GameStateProjection.Create(game,context);
            var refillRead = (Task)forecastType.GetMethod("ReadAsync",Hidden)!.Invoke(null,
                [context,afterEvolution.Players.Single(player=>player.PlayerId==own.PlayerId)])!;
            refillRead.GetAwaiter().GetResult();
            var settledForecast = refillRead.GetType().GetProperty("Result")!.GetValue(refillRead)!;
            hasRefill = settledForecast.GetType().GetProperty("Refill",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(settledForecast) != null;
            var damage = (System.Collections.ICollection)settledForecast.GetType().GetProperty("Damage")!.GetValue(settledForecast)!;
            hasRefill &= damage.Count==0;
            refillBlock = settledForecast.GetType().GetProperty("RefillBlock",BindingFlags.Instance|BindingFlags.NonPublic)!
                .GetValue(settledForecast) as AiRefillBlock;
        }
        var stepsSettled = true;
        foreach (var step in (shortDeck || reactiveEnemy || laterSource!=null || deckCard!=null || opposingRefill!=null ? [] : steps?.Cast<object>() ?? []).Skip(1))
        {
            var action = step.GetType().GetProperty("Data")!.GetValue(step);
            if (action is not AttackActionDto attack)
            {
                stepsSettled = false;
                break;
            }
            var attackResult = transactions.ProcessTransactionAsync(context,
                [new AttackRequestedEvent(own.PlayerId,attack)]).GetAwaiter().GetResult();
            stepsSettled &= attackResult.IsSuccess && !attackResult.IsCancelled;
        }
        var outcomes = assembly.GetType("ShadowverseBeyond.Server.Services.AiActionOutcomes")!;
        var endRequest = new PlayerActionWrapper { PlayerId=own.PlayerId, ActionType="EndTurn", ActionData=new {} };
        var endLease = outcomes.GetMethod("Begin",Hidden)!.Invoke(null,[game,endRequest,GameStateProjection.Create(game,context),null])!;
        var end = transactions.ProcessTransactionAsync(context,[new EndTurnRequestedEvent(own.PlayerId)]).GetAwaiter().GetResult();
        outcomes.GetMethod("Observe",Hidden)!.Invoke(null,[endLease,game,end.ProcessedEvents.ToArray(),null]);
        endRequest.MarkCompleted();
        var endFact = outcomes.GetMethod("Finish",Hidden)!.Invoke(null,[endLease]) as AiCompletedActionFact;
        var endEvents = end.ProcessedEvents.ToList();
        var ownDraw = endEvents.FindIndex(item=>item is DrawEvent draw && draw.DrawingPlayer?.PlayerId==own.PlayerId);
        var firstSummon = endEvents.FindIndex(item=>item is SummonEvent);
        var firstDamage = endEvents.FindIndex(item=>item is DamageEvent);
        var matchedRoute = plannedClear && stepsSettled && enemy.Leader.CurrentHealth == projectedEnemyHealth
            && enemy.Leader.CurrentHealth <= (withReiss ? 15 : 17)
            && (!withCrest || (projectedDraw==(super?4:2) && own.Hand.Count==projectedDraw
                && ownDraw>=0 && firstDamage>ownDraw));
        var laterAction = laterFieldCard switch
        {
            1475086 => endEvents.FindIndex(item=>item is CardMovedEvent
                && item.Target.Any(target=>target.Card?.InstanceId==laterSource!.InstanceId)),
            1050403 => endEvents.FindIndex(item=>item is CardVanishedEvent vanished
                && vanished.VanishedCard?.InstanceId==laterSource!.InstanceId),
            _ => endEvents.FindIndex(item=>item is EvolveEvent evolution
                && evolution.EvolvedCard?.InstanceId==laterSource?.InstanceId)
        };
        var draws = endEvents.OfType<DrawEvent>().Where(item=>item.DrawingPlayer?.PlayerId==own.PlayerId)
            .Select(item=>new {item.Count,drawn=item.DrawnCards.Count,item.SourceAbilityDefinitionId}).ToArray();
        var drawEvidence = endFact?.EndTurnDraws;
        var correctDrawEvidence = !withColdFort || (drawEvidence is {Truncated:false}
            && drawEvidence.OwnDeckBefore==crestDeckCount && drawEvidence.DeckExhausted==combinedShortDeck
            && drawEvidence.Batches.Select(batch=>batch.Requested).SequenceEqual([4,1])
            && drawEvidence.Batches.Select(batch=>batch.Resolved).SequenceEqual(combinedShortDeck?[4,0]:[4,1])
            && drawEvidence.Batches[0].SourceCardId==1474276 && drawEvidence.Batches[1].SourceCardId==1474115
            && drawEvidence.ExhaustedBatchIndex==(combinedShortDeck?1:null)
            && drawEvidence.Batches[1].SourceEvidence==(combinedShortDeck?"public-source-ability":null));
        var correctEnd = withColdFort ? (combinedShortDeck
            ? !hasRefill && refillBlock?.Reason=="combined-refill-deck-exhaustion" && game.WinnerId==enemy.PlayerId
                && draws.Select(draw=>draw.Count).SequenceEqual([4,1]) && draws.Select(draw=>draw.drawn).SequenceEqual([4,0])
            : hasRefill && projectedDraw==5 && own.Hand.Count==5 && own.Leader.CurrentHealth==19
                && draws.Select(draw=>draw.Count).SequenceEqual([4,1]) && draws.Select(draw=>draw.drawn).SequenceEqual([4,1]))
            : laterSource!=null ? hasRefill && own.Hand.Count==4 && ownDraw>=0 && laterAction>ownDraw
            && (laterFieldCard switch
            {
                1475086 => laterSource.CurrentZone==CardZone.Deck,
                1050403 => laterSource.CurrentZone==CardZone.Removed && projectedDraw==4,
                _ => laterSource.IsEvolved
            })
            : deckCard != null ? draws is [{Count:4,drawn:4}] && (reactiveDeckCard
                ? !hasRefill && refillBlock?.Reason=="possible-draw-pile-reaction" && own.Hand.Count==3
                    && deckCard.CurrentZone==CardZone.Graveyard
                    && endEvents.OfType<CardDiscardedEvent>().Any(item=>item.DiscardedCard?.InstanceId==deckCard.InstanceId)
                : hasRefill && projectedDraw==4 && own.Hand.Count==4 && deckCard.CurrentZone==CardZone.Hand
                    && deckCard.HasKeyword(CardKeyword.Storm) && deckCard.CurrentAttack==deckCard.CurrentDefinition.Attack+2)
            : opposingRefill != null ? ownDraw>=0 && own.Hand.Count==4 && !opposingUnit.IsEvolved
                && (opposingRefill.SummonsBeforeDraw
                    ? !hasRefill && refillBlock?.Reason=="public-reaction" && firstSummon>=0 && firstSummon<ownDraw
                    : hasRefill && firstSummon<0)
            : reactiveEnemy ? hasRefill && !projectsLethal
            && own.Hand.Count==4 && ownDraw>=0 && firstDamage>ownDraw
            && enemy.Leader.CurrentHealth>0 && endEvents.Any(item=>item is HealEvent)
            : shortDeck ? !hasRefill && projectedDraw==null && game.WinnerId==enemy.PlayerId
            && enemy.Leader.CurrentHealth==20 && endEvents.Any(item=>item is DeckEmptyOnDrawEvent)
            : enemyHealth==5 ? projectsLethal && baselineLethal==false && selected?.CardId==hostessId
                && actionKind==expectedKind && stepsSettled && game.WinnerId==own.PlayerId
                && enemy.Leader.CurrentHealth==0 && own.Hand.Count==4 && ownDraw>=0 && firstDamage>ownDraw
            : matchedRoute && enemy.Field.Count == 0;
        var correctDiscardChoices = count==3
            ? discardChoices is [{MinSelectionCount:3,MaxSelectionCount:3} choice] && choice.SelectableTargetIds.Count==3
            : discardChoices.Count==0;
        var checkChoiceFacts = laterSource==null && !reactiveEnemy && opposingRefill==null;
        var passed = correctEnd && correctDrawEvidence && correctDiscardChoices && (!checkChoiceFacts || correctChoiceFacts) && evolve.IsSuccess && !evolve.IsCancelled && end.IsSuccess && !end.IsCancelled
            && discarded == count && handAfter == 0 && ownHealth == 18
            && (!nativeLeader || evolve.ProcessedEvents.OfType<CounterChangedEvent>()
                .Any(item=>item.SourcePlayer?.PlayerId==own.PlayerId && item.CounterName=="进化的次数" && item.NewValue==item.OldValue+1))
            && own.CurrentEvolutionPoints == (super ? 1 : 0) && own.CurrentSuperEvolutionPoints == (super ? 0 : 1);
        return new { scenario=name, passed, plannedClear, hasRefill, projectsLethal, baselineLethal, actionKind, selected=selected?.CardId, projectedOwnHealth, projectedEnemyHealth, projectedEnemyField, projectedDraw,
            handBefore=count, handAfter, handAfterEndTurn=own.Hand.Count, discarded, discardPrompts=discardChoices.Count, ownHealth, enemyHealth=enemy.Leader.CurrentHealth,
            enemyField=enemy.Field.Count, ep=own.CurrentEvolutionPoints, sep=own.CurrentSuperEvolutionPoints,
            evolve.ErrorMessage, events=evolve.ProcessedEvents.Select(item=>item.EventType.ToString()).ToArray(),
            laterAction, ownDraw, firstSummon, draws, refillBlock, drawEvidence,
            endEvents=withCrest?end.ProcessedEvents.Select(item=>item.EventType.ToString()).ToArray():null };
    }

    private static object Execute(IGameDataProvider native,Scenario row)
    {
        var cards=native.GetAllCardDefinitions().ToDictionary(pair=>pair.Key,pair=>pair.Value);
        cards.Add(LeaderId,new(){Id=LeaderId,Name="Offline leader",Type=CardType.Leader,Health=20});
        cards.Add(LeaderId+2,new(){Id=LeaderId+2,Name="Offline shield leader",Type=CardType.Leader,Health=20,Keywords=["Shield"]});
        cards.Add(UnitId,new(){Id=UnitId,Name="Offline neutral target",Type=CardType.Follower,Attack=row.TargetAttack,Health=row.TargetHealth,Keywords=row.TargetShield?["Shield","Ward"]:row.TargetAttack==3?["Ward"]:[]});
        var data=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch {
            "GetCardDefinition"=>cards.GetValueOrDefault((int)args![0]!),"GetAllCardDefinitions"=>cards,
            "GetAbilityConfigs"=>(int)args![0]!>=LeaderId?new List<AbilityConfig>():native.GetAbilityConfigs((int)args[0]!),
            _=>method.Invoke(native,args)
        });
        GameDataProvider.SetInstanceForTesting(data);
        var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
        using var services=Services(data,accessor);
        using var scope=services.CreateScope();var sp=scope.ServiceProvider;
        var own=new PlayerState(Guid.Parse("11111111-1111-1111-1111-111111111111")){TurnNumber=6};
        var enemy=new PlayerState(Guid.Parse("22222222-2222-2222-2222-222222222222")){TurnNumber=6};
        var game=new GameState("offline-"+row.Name,own,enemy,own.PlayerId){Status=GameStatus.InProgress,Phase=GamePhase.Main,
            AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<IBuffManager>()};
        var factory=sp.GetRequiredService<ICardFactory>();
        own.Leader=factory.CreateAndRegisterNewCard(game,LeaderId,own.PlayerId,CardZone.Field);
        enemy.Leader=factory.CreateAndRegisterNewCard(game,row.Shield?LeaderId+2:LeaderId,enemy.PlayerId,CardZone.Field);
        enemy.Leader.Damage=20-row.EnemyHp;own.Leader.Damage=20-row.OwnHp;
        var source=factory.CreateAndRegisterNewCard(game,row.Source,own.PlayerId,CardZone.Field);
        var fox=factory.CreateAndRegisterNewCard(game,1474114,own.PlayerId,CardZone.Field);
        source.ZoneStayTurn=1;fox.ZoneStayTurn=1;
        if(row.SourceAlreadyAttacked)source.SetActionUsageCount(CardActionUsageType.Attack,1);
        own.IsEvolutionUnlocked=true;own.IsSuperEvolutionUnlocked=true;own.MaxEvolutionPoints=1;own.MaxSuperEvolutionPoints=1;own.CurrentEvolutionPoints=1;own.CurrentSuperEvolutionPoints=1;own.CurrentPlayPoints=6;own.MaxPlayPoints=6;
        for(var i=0;i<row.Enemies;i++)factory.CreateAndRegisterNewCard(game,UnitId,enemy.PlayerId,CardZone.Field);
        for(var i=0;i<3;i++)factory.CreateAndRegisterNewCard(game,UnitId,enemy.PlayerId,CardZone.Deck);
        if(row.Hand)factory.CreateAndRegisterNewCard(game,UnitId,own.PlayerId,CardZone.Hand);
        if(row.Silenced)typeof(CardInstance).GetProperty("IsSilenced")!.SetValue(source,true);
        if(row.Response) {
            // A public generic damage->heal response; source/fox configs remain unmodified.
            var config=new AbilityConfig{Id=200000010201,Name="Offline public response",CardId=UnitId,
                Trigger=new(){EventTypes=[EventType.Damage],ValidZones=[CardZone.Emblem]},
                Targets=new(){["self-leader"]=new(){Query=new(){SourcePoolExpression="context.owner.leader"}}},
                Effects=[new(){Type=EffectType.Heal,Parameters=new(){["HealAmount"]=new StaticValueConfig{Value=1}}}]};
            var emblem=factory.CreateAndRegisterNewCard(game,UnitId,enemy.PlayerId,CardZone.Emblem);
            game.AbilityManager.CreateAbilitiesForCard(emblem,[config],null,game);
        }
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var context=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId,timeout.Token);
        var view=GameStateProjection.Create(game,context);
        var type=typeof(AiTurnExecutor).Assembly.GetType("ShadowverseBeyond.Server.Services.AiEndTurnForecast")!;
        var task=(Task)type.GetMethod("ReadAsync",Hidden)!.Invoke(null,[context,view.Players.Single(player=>player.PlayerId==own.PlayerId)])!;
        task.GetAwaiter().GetResult();var forecast=task.GetType().GetProperty("Result")!.GetValue(task)!;
        var modelFacts=JsonSerializer.SerializeToElement(type.GetMethod("ForModel",Hidden)!.Invoke(null,[forecast,view,own.PlayerId]),new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
        var search=typeof(AiTurnExecutor).Assembly.GetType("ShadowverseBeyond.Server.Services.AiTacticalSearch")!;
        object? Plan(object? ending)=>search.GetMethod("FindWithEndTurn",Hidden)!.Invoke(null,[view,own.PlayerId,timeout.Token,null,null,ending]);
        var old=Plan(null);var current=Plan(forecast);
        string? Kind(object? plan)=>plan?.GetType().GetProperty("First")!.GetValue(plan) is {} move?(string)move.GetType().GetProperty("Type")!.GetValue(move)!:null;
        bool Lethal(object? plan)=>plan!=null&&(bool)plan.GetType().GetProperty("ProjectsLethal")!.GetValue(plan)!;
        object MoveInfo(object? plan) {
            var move=plan?.GetType().GetProperty("First")!.GetValue(plan);
            var sourceView=move?.GetType().GetProperty("Source")!.GetValue(move) as CardInstanceDto;
            var action=move?.GetType().GetProperty("Data")!.GetValue(move) as ShadowverseBeyond.Server.Dtos.SignalR.Actions.AttackActionDto;
            return new {kind=Kind(plan),cardId=sourceView?.CardId,targetIsLeader=action==null?(bool?)null:action.TargetUid==enemy.Leader.InstanceId,
                targetCardId=action==null?(int?)null:view.Players.SelectMany(player=>player.Field.Concat(player.LeaderCard==null?[]:new[]{player.LeaderCard})).FirstOrDefault(card=>card.InstanceId==action.TargetUid)?.CardId};
        }
        var baselineMove=JsonSerializer.SerializeToElement(MoveInfo(old));var currentMove=JsonSerializer.SerializeToElement(MoveInfo(current));
        var noSweepTrade=row.Name!="hostess-safe-sweep-without-fox-trade"||Kind(current)=="EndTurn"
            ||(Kind(current)=="PerformAttack"&&currentMove.GetProperty("targetIsLeader").GetBoolean());
        var projected = (GameStateDto)search.GetMethod("ProjectEndTurn", Hidden)!.Invoke(null,[view,own.PlayerId,forecast])!;
        var projectedEnemy = projected.Players.Single(player=>player.PlayerId==enemy.PlayerId);
        var beforeHp=enemy.Leader.CurrentHealth;var beforeEp=own.CurrentEvolutionPoints;var beforeSep=own.CurrentSuperEvolutionPoints;
        var result=sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(context,[new EndTurnRequestedEvent(own.PlayerId)]).GetAwaiter().GetResult();
        var leaderDamage=result.ProcessedEvents.OfType<DamageEvent>().Where(ev=>ev.Target?.Any(card=>card.InstanceId==enemy.Leader.InstanceId)==true).Count();
        var estimate=modelFacts.GetProperty("damage").GetArrayLength();
        var expectedDamage=row.Hand||row.Silenced||row.Shield?0:row.Source==1474116?3:2;
        var hpCorrect=row.Response?enemy.Leader.CurrentHealth>=beforeHp-2:enemy.Leader.CurrentHealth==beforeHp-expectedDamage;
        var expectedEnd=row.EnemyHp<=3&&!row.Hand&&!row.Silenced&&!row.Shield&&!row.Response;
        var events=result.ProcessedEvents.ToList();var gameEnd=events.FindIndex(ev=>ev is GameEndEvent);
        var stoppedAfterWin=!expectedEnd||(gameEnd>=0&&events.Count(ev=>ev is GameEndEvent)==1
            &&!events.Skip(gameEnd+1).Any(ev=>ev is TurnStartEvent or DamageEvent or HealEvent)
            &&enemy.Hand.Count==0&&game.WinnerId==own.PlayerId);
        var projectedCountersMatch = row.Name is not ("hostess-safe-sweep-without-fox-trade" or "reiss-singleton-preserves-fox" or "reiss-singleton-shielded")
            || (projectedEnemy.BloodPool==enemy.BloodPool && projectedEnemy.GraveyardCount==enemy.GraveyardCount
                && projectedEnemy.Field.Count==enemy.Field.Count);
        var singletonCorrect=row.Name switch {
            "reiss-singleton-preserves-fox" => Kind(current)=="EndTurn"&&enemy.Field.Count==0
                && own.Field.Contains(fox)&&fox.HasKeyword(CardKeyword.Ward)&&fox.HasKeyword(CardKeyword.Shield),
            "reiss-singleton-shielded" => enemy.Field.Count==1&&Kind(current)!="EndTurn",
            _ => true
        };
        var passed=singletonCorrect&&projectedCountersMatch&&result.IsSuccess&&!result.IsCancelled&&hpCorrect&&own.CurrentEvolutionPoints==beforeEp&&own.CurrentSuperEvolutionPoints==beforeSep
            &&(!expectedEnd||(Kind(current)=="EndTurn"&&Lethal(current)))
            &&(!(row.Hand||row.Silenced||row.Response)||estimate==0)
            &&(!row.Shield||Kind(current)!="EndTurn")&&stoppedAfterWin&&noSweepTrade;
        return new {scenario=row.Name,sourceId=row.Source,nativeAbilities=native.GetAbilityConfigs(row.Source).Select(config=>config.Id),
            baselineFirst=Kind(old),currentFirst=Kind(current),baselineMove,currentMove,noSweepTrade,currentProjectsLethal=Lethal(current),modelFacts,
            transactionSuccess=result.IsSuccess,result.ErrorMessage,stoppedAfterWin,eventTypes=result.ProcessedEvents.Select(ev=>ev.EventType.ToString()).ToArray(),
            leaderHealthBefore=beforeHp,leaderHealthAfter=enemy.Leader.CurrentHealth,leaderDamageEvents=leaderDamage,
            projectedCountersMatch,projectedBloodPool=projectedEnemy.BloodPool,nativeBloodPool=enemy.BloodPool,
            projectedGraveyard=projectedEnemy.GraveyardCount,nativeGraveyard=enemy.GraveyardCount,
            enemyFieldAfter=enemy.Field.Count,epSpent=beforeEp-own.CurrentEvolutionPoints,sepSpent=beforeSep-own.CurrentSuperEvolutionPoints,passed};
    }

    internal static ServiceProvider Services(IGameDataProvider data,GameContextAccessor accessor,IPendingActionService? pending=null,IGameService? games=null,Action<IServiceCollection>? configure=null)
    {
        var services=new ServiceCollection().AddLogging(log=>log.ClearProviders()).AddMemoryCache();
        services.AddSingleton(data).AddSingleton<IGameContextAccessor>(accessor);
        services.AddSingleton<IMapper>(new MapperConfiguration(config=>config.AddMaps(typeof(GameState).Assembly)).CreateMapper());
        services.AddSingleton<IFunctionRegistry,FunctionRegistry>().AddSingleton<IDslExpressionCache,DslExpressionCache>().AddSingleton<IDslCompiler,DslCompiler>();
        services.AddScoped<IConditionEvaluator,DslConditionEvaluator>().AddScoped<IConditionFactory,ConditionFactory>();
        services.AddScoped<IQueryDslParser,QueryDslParser>().AddScoped<IQueryExecutor,QueryExecutor>();
        services.AddScoped<TargetSelectionService>().AddScoped<ITargetFactory,TargetFactory>();
        services.AddSingleton<ILogger>(NullLogger.Instance);
        services.AddSingleton(pending??Proxy<IPendingActionService>.Make((m,a)=>m.ReturnType==typeof(bool)?false:throw new Exception("Unexpected interactive selection: "+m.Name)));
        if(games!=null)services.AddSingleton(games);
        services.AddScoped<IAbilityFactory,AbilityFactory>().AddScoped<IAbilityManager,AbilityManager>();
        services.AddScoped<IBuffFactory,BuffFactory>().AddScoped<IBuffManager,BuffManager>().AddScoped<IBuffEvaluator,BuffEvaluator>()
            .AddSingleton<BuffStackingEngine>().AddScoped<IBuffService,BuffService>().AddScoped<ICardFactory,CardFactory>();
        services.AddSingleton<IEventHistoryService,InMemoryEventHistoryService>().AddScoped<IEventProcessor,EventProcessor>();
        services.AddScoped<IValueSourceResolver,ValueSourceResolver>().AddScoped<ITargetProcessingService,TargetProcessingService>();
        services.AddSingleton<IEffectHandlerRegistry,EffectHandlerRegistry>().AddScoped<IEffectExecutor,EffectExecutor>();
        foreach(var type in typeof(DamageEffectHandler).Assembly.GetTypes().Where(type=>type.IsClass&&!type.IsAbstract
            &&type.GetCustomAttributes().Any(attribute=>attribute.GetType().Name=="EffectHandlerAttribute")))services.AddScoped(type);
        services.AddScoped<ITurnBoundaryCleanupService,TurnBoundaryCleanupService>();
        services.AddSingleton(Proxy<IClientMethodMapper>.Make(NoOp));
        services.AddSingleton(Proxy<ICommittedEventJournal>.Make((m,a)=>throw new Exception("Replay persistence must stay disabled")));
        services.AddSingleton(Proxy<ISnapshotStore>.Make((m,a)=>throw new Exception("Snapshot persistence must stay disabled")));
        services.AddSingleton(Proxy<IRecordedEventMapper>.Make(NoOp));services.AddSingleton(Proxy<IGameEventRawEventMapper>.Make((m,a)=>"{}"));
        services.AddSingleton(Proxy<IMetaProgressionService>.Make(NoOp));services.AddSingleton(Proxy<IWinrateStatsService>.Make(NoOp));
        services.AddSingleton(Options.Create(new ReplayOptions{EnableJournalWrites=false,EnableSnapshots=false,PersistClientDtos=false}));
        services.AddScoped<IGameActionTransactionService,GameActionTransactionService>();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }
    private static object? NoOp(MethodInfo method,object?[]? args)=>method.ReturnType==typeof(Task)?Task.CompletedTask:null;
}
