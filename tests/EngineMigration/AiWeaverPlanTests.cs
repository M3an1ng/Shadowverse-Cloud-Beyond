using System.Net;
using System.Reflection;
using System.Text.Json;
using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Persistence;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class AiWeaverPlanTests
{
    const int Weaver=1474173,Call=1475138,Leader=2000001530,Junk=2000001531,Body=2000001532;
    static readonly BindingFlags Hidden=BindingFlags.Static|BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly Type Plan=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiWeaverPlan")!;
    static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static object? Invoke(string method,params object?[] args)=>Plan.GetMethod(method,Hidden)!.Invoke(null,args);
    internal static void Run(string db,string output,string? weights = null)
    {
        var old=GameDataProvider.Instance;var oldAccessor=GameServiceProvider.ContextAccessor;
        using var catalog=new ServiceCollection().AddLogging(l=>l.ClearProviders()).AddDbContext<ApplicationDbContext>(o=>o.UseSqlite(new SqliteConnectionStringBuilder{DataSource=db,Mode=SqliteOpenMode.ReadOnly}.ToString()))
            .AddScoped<ICardRepository,EFCardRepository>().AddScoped<AbilityLoader>().BuildServiceProvider();
        try{
            var native=new GameDataProvider(catalog.GetRequiredService<IServiceScopeFactory>(),NullLogger<GameDataProvider>.Instance);native.InitializeAsync().GetAwaiter().GetResult();
            Check((bool)Invoke("RulesValid")!,"native Weaver/Call profile does not match actual rules");
            var rows = new List<object> { Scene(native,true), Scene(native,false), EvolutionOrder(native,false), EvolutionOrder(native,true) };
            if (weights != null)
            {
                rows.Add(ExecuteTurn(native, weights, false, false));
                rows.Add(ExecuteTurn(native, weights, true, false));
                rows.Add(ExecuteTurn(native, weights, true, true));
                rows.Add(ExecuteTurn(native, weights, true, false, emergency: true));
                rows.Add(ExecuteTurn(native, weights, false, false, benchmark: true));
                rows.Add(ExecuteTurn(native, weights, true, false, benchmark: true));
            }
            File.WriteAllText(output,JsonSerializer.Serialize(new{providerRequests=0,cases=rows,scope="Native own/enemy evolution and deck-instance summon transactions, actual configuration read-only. Mulligan and cached-guide correction tests; no paid provider, model fitting or win-rate claim."},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("PASS Weaver: own versus enemy evolution, once-per-turn boost, Call deck-only summon, duplicate-aware production mulligan, native rule drift guard, legacy guide cache correction without request");
        }finally{GameDataProvider.SetInstanceForTesting(old);GameServiceProvider.ContextAccessor=oldAccessor;}
    }
    static object Scene(IGameDataProvider native,bool targetInDeck)
    {
        var defs=native.GetAllCardDefinitions().ToDictionary(p=>p.Key,p=>p.Value);
        defs[Leader]=new(){Id=Leader,Type=CardType.Leader,Health=20};defs[Junk]=new(){Id=Junk,Name="inert draw",Type=CardType.Spell,Cost=20};defs[Body]=new(){Id=Body,Name="inert body",Type=CardType.Follower,Cost=2,Attack=1,Health=1};
        var data=Proxy<IGameDataProvider>.Make((m,a)=>m.Name switch{"GetCardDefinition"=>defs.GetValueOrDefault((int)a![0]!),"GetAllCardDefinitions"=>defs,"GetAbilityConfigs"=>(int)a![0]!>=Leader?new List<AbilityConfig>():native.GetAbilityConfigs((int)a[0]!),_=>m.Invoke(native,a)});
        GameDataProvider.SetInstanceForTesting(data);var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;GameState game=null!;
        var games=Proxy<IGameService>.Make((m,a)=>m.Name=="GetGameState"?game:null);
        using var spRoot=AiEndTurnEngineTests.Services(data,accessor,games:games);using var scope=spRoot.CreateScope();var sp=scope.ServiceProvider;var factory=sp.GetRequiredService<ICardFactory>();
        var own=new PlayerState(Guid.NewGuid()){IsAI=true,TurnNumber=5,CurrentPlayPoints=10,MaxPlayPoints=10,IsEvolutionUnlocked=true,InitialDeckDefinitionIds=[Weaver,Weaver,Weaver,Call,Call,Call,1472979]};
        var enemy=new PlayerState(Guid.NewGuid()){TurnNumber=5};game=new("weaver-"+targetInDeck,own,enemy,own.PlayerId){Status=GameStatus.InProgress,Phase=GamePhase.Main,AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<IBuffManager>()};
        CardInstance Add(int id,CardZone zone,PlayerState? player=null)=>factory.CreateAndRegisterNewCard(game,id,(player??own).PlayerId,zone);
        own.Leader=Add(Leader,CardZone.Field);enemy.Leader=Add(Leader,CardZone.Field,enemy);var weaver=Add(Weaver,CardZone.Field);
        var inHand=Add(Weaver,CardZone.Hand);var second=Add(Weaver,CardZone.Hand);var call=Add(Call,CardZone.Hand);
        if(targetInDeck)Add(Weaver,CardZone.Deck);for(var i=0;i<3;i++)Add(Junk,CardZone.Deck);
        using var context=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId);
        GameStateDto View()=>GameStateProjection.Create(game,context);
        void Tx(IGameEvent ev){var result=sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(context,[ev]).GetAwaiter().GetResult();Check(result.IsSuccess&&!result.IsCancelled,result.ErrorMessage??"transaction cancelled");}
        int Cost()=>call.CurrentCost;var initial=Cost();
        Tx(new EvolveEvent(Add(Body,CardZone.Field,enemy),TargetInfo.ForPlayer(enemy)));Check(Cost()==initial,"enemy evolution boosted own hand");
        Tx(new EvolveEvent(Add(Body,CardZone.Field),TargetInfo.ForPlayer(own)));Check(Cost()==initial-1,"own evolution did not boost Call");
        Tx(new EvolveEvent(Add(Body,CardZone.Field),TargetInfo.ForPlayer(own)));Check(Cost()==initial-1,"single Weaver boosted twice in same turn");
        var modelConfig=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["AiStrategy:Enabled"]="false",["AiModel:Path"]=Path.Combine(Path.GetTempPath(),"missing-weaver-model.json")}).Build();
        var model=new AiLearningModel(new Host(),modelConfig,NullLogger<AiLearningModel>.Instance);var ai=new AiService(NullLogger<AiService>.Instance,spRoot.GetRequiredService<IServiceScopeFactory>(),model,modelConfig);
        var replacements=ai.DecideMulliganAsync(View(),own.PlayerId).GetAwaiter().GetResult();
        Check(replacements.Contains(inHand.InstanceId.ToString())&&replacements.Contains(second.InstanceId.ToString())&&!replacements.Contains(call.InstanceId.ToString()),"production mulligan sought hand Weavers over Call");
        var noCall=View().Players.Single(p=>p.PlayerId==own.PlayerId);noCall.Hand.RemoveAll(c=>c.CardId==Call);
        Check(((List<string>)Invoke("Mulligan",noCall,own.InitialDeckDefinitionIds)!).Count==1,"no-Call opening retained all duplicate Weavers");
        var bodyCost=defs[Body].Cost;defs[Body].Cost=1;
        try{Check(Invoke("Mulligan",noCall,new[]{Weaver,Call,Body})==null,"mixed 1-cost pool used stable-tutor mulligan");}finally{defs[Body].Cost=bodyCost;}
        if(targetInDeck)CacheAndDrift(game,native);
        var fieldBefore=own.Field.Count(c=>c.CurrentDefinitionId==Weaver);var handWeavers=own.Hand.Count(c=>c.CurrentDefinitionId==Weaver);
        Tx(new PlayCardRequestedEvent(own.PlayerId,new PlayCardActionDto{CardUid=call.InstanceId,PlayMode=PlayMode.Base,PaidCost=Cost()}));
        Check(own.Field.Count(c=>c.CurrentDefinitionId==Weaver)==fieldBefore+(targetInDeck?1:0),"Call pulled wrong zone or failed native deck summon");
        Check(own.Hand.Count(c=>c.CurrentDefinitionId==Weaver)==handWeavers,"Call consumed a Weaver in hand");
        return new{targetInDeck,enemyEvolveBoost=0,ownEvolveBoost=1,secondOwnEvolveBoost=0,newFieldWeavers=own.Field.Count(c=>c.CurrentDefinitionId==Weaver)-fieldBefore,passed=true};
    }
    static object EvolutionOrder(IGameDataProvider native,bool prepare)
    {
        var defs = native.GetAllCardDefinitions().ToDictionary(pair => pair.Key, pair => pair.Value);
        defs[Leader] = new() { Id = Leader, Type = CardType.Leader, Health = 20 };
        defs[Body] = new() { Id = Body, Type = CardType.Follower, Attack = 1, Health = 1, Cost = 2 };
        var data = Proxy<IGameDataProvider>.Make((method,args) => method.Name switch {
            "GetCardDefinition" => defs.GetValueOrDefault((int)args![0]!),
            "GetAllCardDefinitions" => defs,
            "GetAbilityConfigs" => (int)args![0]! >= Leader ? new List<AbilityConfig>() : native.GetAbilityConfigs((int)args[0]!),
            _ => method.Invoke(native,args)
        });
        GameDataProvider.SetInstanceForTesting(data);
        var accessor = new GameContextAccessor();
        GameServiceProvider.ContextAccessor = accessor;
        using var services = AiEndTurnEngineTests.Services(data, accessor);
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var own = new PlayerState(Guid.NewGuid()) { IsAI = true, TurnNumber = 5, CurrentPlayPoints = 5, MaxPlayPoints = 5,
            CurrentEvolutionPoints = 2, IsEvolutionUnlocked = true, InitialDeckDefinitionIds = [Weaver, Call, Body] };
        var enemy = new PlayerState(Guid.NewGuid());
        var game = new GameState("weaver-order", own, enemy, own.PlayerId) { Status = GameStatus.InProgress,
            Phase = GamePhase.Main, AbilityManager = sp.GetRequiredService<IAbilityManager>(), BuffManager = sp.GetRequiredService<IBuffManager>() };
        var factory = sp.GetRequiredService<ICardFactory>();
        CardInstance Add(int id,CardZone zone,PlayerState player) => factory.CreateAndRegisterNewCard(game,id,player.PlayerId,zone);
        own.Leader = Add(Leader,CardZone.Field,own); enemy.Leader = Add(Leader,CardZone.Field,enemy);
        var weaver = Add(Weaver,CardZone.Hand,own); var call = Add(Call,CardZone.Hand,own); var body = Add(Body,CardZone.Field,own);
        var tutorTarget = Add(Weaver,CardZone.Deck,own);
        Add(Body,CardZone.Deck,own);
        using var context = new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId);
        PlayerStateDto View() => GameStateProjection.Create(game,context).Players.Single(player => player.PlayerId == own.PlayerId);
        Guid? Candidate(PlayerStateDto player) => (Guid?)Invoke("PrepareBeforeEvolution",context,player);
        var view = View();
        Check(Candidate(view) == weaver.InstanceId,"native Weaver/Call setup was missed");
        var priorCanEvolve = view.CanEvolve;
        view.CanEvolve = false;
        Check(Candidate(view) == null,"setup proposed before a locked evolution");
        view.CanEvolve = priorCanEvolve;
        var pp = view.CurrentPP;
        view.CurrentPP = 4;
        Check(Candidate(view) == null,"setup proposed without the supported Call budget");
        view.CurrentPP = pp;
        var tx = sp.GetRequiredService<IGameActionTransactionService>();
        void Resolve(IGameEvent item)
        {
            var result = tx.ProcessTransactionAsync(context,[item]).GetAwaiter().GetResult();
            Check(result.IsSuccess && !result.IsCancelled,result.ErrorMessage ?? "Weaver order transaction failed");
        }
        void PlayWeaver() => Resolve(new PlayCardRequestedEvent(own.PlayerId,
            new PlayCardActionDto { CardUid = weaver.InstanceId, PlayMode = PlayMode.Base, PaidCost = 1 }));
        if (prepare) PlayWeaver();
        Resolve(new EvolveRequestedEvent(own.PlayerId, new EvolveActionDto { UnitUid = body.InstanceId.ToString() }));
        if (!prepare) PlayWeaver();
        Check(call.CurrentCost == (prepare ? 4 : 5) && own.CurrentPlayPoints == 4,
            "native order did not preserve the ability to play Call");
        Check(Candidate(View()) == null,"the setup tried to add another Weaver despite an existing field copy");
        var cost = call.CurrentCost;
        var remaining = own.CurrentPlayPoints;
        if (prepare)
        {
            Resolve(new PlayCardRequestedEvent(own.PlayerId,
                new PlayCardActionDto { CardUid = call.InstanceId, PlayMode = PlayMode.Base, PaidCost = cost }));
            Check(tutorTarget.CurrentZone == CardZone.Field && own.CurrentPlayPoints == 0,
                "the affordable Call did not actually summon from the deck");
        }
        return new { prepare, callCost = cost, remainingPp = remaining,
            tutorSummoned = tutorTarget.CurrentZone == CardZone.Field, passed = true };
    }

    static object ExecuteTurn(IGameDataProvider native, string weights, bool prepare, bool lethal, bool emergency = false, bool benchmark = false)
    {
        const int EnemyBody = Body + 1;
        var defs = native.GetAllCardDefinitions().ToDictionary(pair => pair.Key, pair => pair.Value);
        defs[Leader] = new() { Id = Leader, Type = CardType.Leader, Health = 20 };
        defs[Junk] = new() { Id = Junk, Name = "draw fixture", Type = CardType.Spell, Cost = 20 };
        defs[Body] = new() { Id = Body, Name = "evolution fixture", Type = CardType.Follower, Attack = 3, Health = 3, Cost = 2 };
        defs[EnemyBody] = new() { Id = EnemyBody, Name = "opposing fixture", Type = CardType.Follower, Attack = 4, Health = 4, Cost = 4 };
        var data = Proxy<IGameDataProvider>.Make((method,args) => method.Name switch {
            "GetCardDefinition" => defs.GetValueOrDefault((int)args![0]!),
            "GetAllCardDefinitions" => defs,
            "GetAbilityConfigs" => (int)args![0]! >= Leader ? new List<AbilityConfig>() : native.GetAbilityConfigs((int)args[0]!),
            _ => method.Invoke(native,args)
        });
        GameDataProvider.SetInstanceForTesting(data);
        var accessor = new GameContextAccessor();
        GameServiceProvider.ContextAccessor = accessor;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["AiStrategy:Enabled"] = "false",
            ["AiStrategy:PrepareWeaverEvolution"] = (benchmark ? !prepare : prepare).ToString(),
            ["AiModel:Path"] = Path.GetFullPath(weights)
        }).Build();
        var model = new AiLearningModel(new Host(),config,NullLogger<AiLearningModel>.Instance);
        Check(model.Current != null, "frozen model unavailable");
        GameState game = null!;
        ServiceProvider services = null!;
        GameService playability = null!;
        var actions = new List<string>();
        int? callPaidCost = null;
        async Task<PlayabilityResultDto> Legal(Guid uid, PlayMode mode)
        {
            var card = game.FindCardInstance(uid)!;
            using var scope = services.CreateScope();
            var sp = scope.ServiceProvider;
            using var context = new GameContext(sp, sp.GetRequiredService<IMapper>(), accessor, game, game.Player1.PlayerId);
            return await (Task<PlayabilityResultDto>)typeof(GameService).GetMethod("CheckPlayabilityCoreLogicAsync", Hidden)!
                .Invoke(playability, [context with { SourceCard = card }, game.Player1, card, defs[card.CurrentDefinitionId], mode])!;
        }
        async Task<bool> Submit(PlayerActionWrapper action)
        {
            var kind = action.ActionType;
            IGameEvent ev = action.ActionData switch
            {
                PlayCardActionDto card => new PlayCardRequestedEvent(action.PlayerId, card),
                EvolveActionDto evolve => new EvolveRequestedEvent(action.PlayerId, evolve),
                AttackActionDto attack => new AttackRequestedEvent(action.PlayerId, attack),
                _ when kind == "EndTurn" => new EndTurnRequestedEvent(action.PlayerId),
                _ => throw new InvalidOperationException("Unexpected Weaver fixture action " + kind)
            };
            if (action.ActionData is PlayCardActionDto play)
                kind += ":" + game.FindCardInstance(play.CardUid)!.CurrentDefinitionId;
            using var scope = services.CreateScope();
            var sp = scope.ServiceProvider;
            using var context = new GameContext(sp, sp.GetRequiredService<IMapper>(), accessor, game, action.PlayerId, triggeringEvent:ev);
            var result = await sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(context,[ev]);
            Check(result.IsSuccess && !result.IsCancelled, result.ErrorMessage ?? "Native action cancelled");
            actions.Add(kind);
            var paidCall = result.ProcessedEvents.OfType<PlayCardEvent>()
                .SingleOrDefault(item => item.PlayedCard?.BaseDefinitionId == Call);
            if (paidCall != null) callPaidCost = paidCall.PaidCost;
            action.MarkCompleted();
            return true;
        }
        var games = Proxy<IGameService>.Make((method,args) => method.Name switch
        {
            "GetGameState" => game,
            "GetCardPlayabilityAsync" => Legal((Guid)args![2]!, PlayMode.Base),
            "GetCardPlayabilityForModeAsync" => Legal((Guid)args![2]!, (PlayMode)args[3]!),
            "EnqueuePlayerActionAsync" => Submit((PlayerActionWrapper)args![1]!),
            _ => null
        });
        var pending = Proxy<IPendingActionService>.Make((method,_) => method.ReturnType == typeof(bool) ? false : null);
        using (services = AiEndTurnEngineTests.Services(data, accessor, pending, games))
        {
            using var scope = services.CreateScope();
            var sp = scope.ServiceProvider;
            playability = new GameService(Proxy<Microsoft.AspNetCore.SignalR.IHubContext<ShadowverseBeyond.Server.Hubs.GameHub>>.Make((m,a)=>null),
                NullLogger<GameService>.Instance, sp.GetRequiredService<IMapper>(), Proxy<IClientMethodMapper>.Make((m,a)=>null),
                data, Proxy<IMatchService>.Make((m,a)=>null), Proxy<IFirstPlayerSelector>.Make((m,a)=>null),
                services.GetRequiredService<IServiceScopeFactory>(), pending, Proxy<IGameStatsRuntimeTracker>.Make((m,a)=>null));
            var own = new PlayerState(Guid.NewGuid()) { IsAI = true, TurnNumber = 5, CurrentPlayPoints = 5, MaxPlayPoints = 5,
                CurrentEvolutionPoints = 2, MaxEvolutionPoints = 2, IsEvolutionUnlocked = true,
                InitialDeckDefinitionIds = [Weaver, Weaver, Call, Body, Junk] };
            var enemy = new PlayerState(Guid.NewGuid()) { TurnNumber = 5 };
            game = new GameState("weaver-actual-turn", own, enemy, own.PlayerId) { IsLocalAiTraining = true,
                Status = GameStatus.InProgress, Phase = GamePhase.Main, AbilityManager = sp.GetRequiredService<IAbilityManager>(),
                BuffManager = sp.GetRequiredService<IBuffManager>() };
            if (benchmark) Invoke("BindBenchmark", game, prepare ? own.PlayerId : enemy.PlayerId);
            var factory = sp.GetRequiredService<ICardFactory>();
            CardInstance Add(PlayerState player, int id, CardZone zone) => factory.CreateAndRegisterNewCard(game, id, player.PlayerId, zone);
            own.Leader = Add(own, Leader, CardZone.Field);
            enemy.Leader = Add(enemy, Leader, CardZone.Field);
            var body = Add(own, Body, CardZone.Field);
            body.ZoneStayTurn = lethal ? 1 : 0;
            if (lethal) enemy.Leader.Damage = 15;
            else Add(enemy, EnemyBody, CardZone.Field);
            if (emergency) own.Leader.Damage = 16;
            Add(own, Weaver, CardZone.Hand);
            var call = Add(own, Call, CardZone.Hand);
            var tutor = Add(own, Weaver, CardZone.Deck);
            for (var i = 0; i < 3; i++) { Add(own, Junk, CardZone.Deck); Add(enemy, Junk, CardZone.Deck); }
            var ai = new AiService(NullLogger<AiService>.Instance,services.GetRequiredService<IServiceScopeFactory>(),model,config);
            var executor = new AiTurnExecutor(games, ai, NullLogger<AiTurnExecutor>.Instance, sp.GetRequiredService<IMapper>(), services, model, config);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            executor.ExecuteTurnAsync(game.GameId, own.PlayerId, timeout.Token).GetAwaiter().GetResult();
            var sequence = string.Join(", ", actions);
            if (lethal)
                Check(actions.First() == "PerformEvolve" && game.WinnerId == own.PlayerId, "Weaver interrupted direct lethal: " + sequence);
            else if (emergency)
                Check(actions.First() == "PerformEvolve" && enemy.Field.Count == 0 && !game.IsGameOver,
                    "Weaver delayed an urgent defensive evolution: " + sequence);
            else if (prepare)
                Check(actions.First() == "PlayCard:" + Weaver && actions.Contains("PerformEvolve")
                    && callPaidCost == 4 && tutor.CurrentZone == CardZone.Field,
                    "executor did not finish the Weaver evolution and Call line: " + sequence);
            else
                Check(actions.First() == "PerformEvolve" && callPaidCost == null && call.CurrentCost == 5,
                    "control did not show the missed discount: " + sequence);
            var weaverPreparations = (int)Invoke("PreparationCount", game)!;
            Check(weaverPreparations == (benchmark && prepare ? 1 : 0),
                "Weaver comparison counted the wrong completed actions: " + sequence);
            return new { prepare, lethal, emergency, benchmark, actions, callPaidCost, weaverPreparations,
                tutorSummoned = tutor.CurrentZone == CardZone.Field, passed = true };
        }
    }

    static void CacheAndDrift(GameState game,IGameDataProvider native)
    {
        var folder=Path.Combine(Path.GetTempPath(),"weaver-guide-test-"+Guid.NewGuid().ToString("N"));
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["AiStrategy:Enabled"]="true",["AiStrategy:Endpoint"]="https://example.invalid/chat/completions",["AiStrategy:Model"]="offline-fake",["AiStrategy:MemoryPath"]=folder}).Build();
        using var provider=new Fake();AiStrategyPlanner New()=>new(provider,config,new Host(),NullLogger<AiStrategyPlanner>.Instance);
        using(var planner=New())Check(planner.WarmDeckAsync(game,game.Player1.PlayerId,"Neutral")!.GetAwaiter().GetResult()!=null,"guide test warm failed");
        Check(provider.Calls==1,"unexpected fake guide count");var file=Directory.GetFiles(Path.Combine(folder,"guides"),"*.json").Single();
        var input=provider.Input;var read=typeof(AiStrategyPlanner).GetMethod("ReadDeck",Hidden)!.Invoke(null,[game,game.Player1.PlayerId]);
        var deckKey=typeof(AiStrategyPlanner).GetMethod("DeckKey",Hidden)!.Invoke(null,[read]);
        var prompt=(string)typeof(AiStrategyPlanner).GetField("GuidePrompt",Hidden)!.GetValue(null)!;
        var rules=new{metadata=input.GetProperty("deckMetadata"),references=input.GetProperty("deckReferences"),definitions=input.GetProperty("definitionRules"),abilities=input.GetProperty("abilityRules")};
        var legacy=typeof(AiStrategyPlanner).GetMethod("GuideIdentity",Hidden)!.Invoke(null,[rules,deckKey,"offline-fake","https://example.invalid/chat/completions",prompt,input.GetProperty("references"),input.GetProperty("verifiedLines"),Array.Empty<string>()]);
        var legacyPath=Path.Combine(folder,"guides",legacy+".json");var bad=new AiStrategyPlanner.DeckGuide("对手进化时织法触发增幅；优先多拿织法上手。其他过牌策略保留。",game.Player1.InitialDeckDefinitionIds.Distinct().Order().Select(id=>new AiStrategyPlanner.CardRole(id,"old wrong role")).ToArray());
        File.WriteAllText(legacyPath,JsonSerializer.Serialize(new{createdAt=DateTimeOffset.UtcNow,guide=bad},Json));var original=File.ReadAllBytes(legacyPath);if(file!=legacyPath)File.Delete(file);
        using(var planner=New()){
            var corrected=planner.WarmDeckAsync(game,game.Player1.PlayerId,"Neutral")!.GetAwaiter().GetResult()!;
            Check(provider.Calls==1&&corrected.RuleCorrections.Length==1&&!corrected.GamePlan.Contains("对手进化时织法触发")&&corrected.GamePlan.Contains("其他过牌策略保留")&&corrected.GamePlan.Contains("不追求多张织法上手"),"legacy guide not corrected locally without re-analysis");
        }
        Check(File.ReadAllBytes(legacyPath).SequenceEqual(original),"historical raw guide overwritten");
        var ability=native.GetAbilityConfigs(Weaver)!.Single(a=>a.Trigger.EventTypes.Contains(EventType.Evolve));var condition=ability.Conditions.Single(c=>c.IsEnabled);var old=condition.Expression;
        try{condition.Expression="context.triggeringEvent.EvolvedCard.ownerPlayerId != context.owner.playerId";Check(!(bool)Invoke("RulesValid")!&&((string[])Invoke("Principles",game.Player1.InitialDeckDefinitionIds)!).Length==0,"changed native rule retained old role");}finally{condition.Expression=old;}
    }
    sealed class Fake:HttpMessageHandler,IHttpClientFactory
    {
        public int Calls;public JsonElement Input;
        public HttpClient CreateClient(string name)=>new(this,false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Calls++;using var doc=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));var fields=new Dictionary<string,JsonElement>();foreach(var message in doc.RootElement.GetProperty("messages").EnumerateArray().Where(m=>m.GetProperty("role").GetString()=="user")){using var part=JsonDocument.Parse(message.GetProperty("content").GetString()!);foreach(var field in part.RootElement.EnumerateObject())fields[field.Name]=field.Value.Clone();}Input=JsonSerializer.SerializeToElement(fields,Json);
            var guide=new AiStrategyPlanner.DeckGuide("对手进化时织法触发增幅；优先多拿织法上手。其他过牌策略保留。",Input.GetProperty("deck").EnumerateArray().Select(c=>new AiStrategyPlanner.CardRole(c.GetProperty("id").GetInt32(),"old role")).ToArray());
            return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=JsonSerializer.Serialize(guide,Json)}}}}))};}
    }
    sealed class Host:IWebHostEnvironment{public string ApplicationName{get;set;}="Weaver test";public string EnvironmentName{get;set;}="Test";public string WebRootPath{get;set;}=Path.GetTempPath();public string ContentRootPath{get;set;}=Path.GetTempPath();public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
}
