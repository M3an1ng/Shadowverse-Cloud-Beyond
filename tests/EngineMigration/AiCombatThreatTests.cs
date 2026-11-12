using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Persistence;
using ShadowverseBeyond.Server.Domain.Constants;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Extensions;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class AiCombatThreatTests
{
    private static readonly Type Threat=typeof(AiLearningModel).Assembly.GetType("ShadowverseBeyond.Server.Services.AiCombatThreat")!;
    private static readonly MethodInfo Evaluate=Threat.GetMethod("Evaluate",BindingFlags.Static|BindingFlags.NonPublic)!;
    private static void Check(bool pass,string message){if(!pass)throw new Exception(message);}
    private static (int Damage,bool Complete,int States) Read(List<CardInstanceDto> a,List<CardInstanceDto> d,CardInstanceDto? leader=null){
        var result=Evaluate.Invoke(null,[a,d,leader])!;
        T P<T>(string name)=>(T)result.GetType().GetProperty(name)!.GetValue(result)!;
        return(P<int>("Damage"),P<bool>("Complete"),P<int>("States"));
    }
    private static IDisposable Legacy(bool value)=>(IDisposable)Threat.GetMethod("UseLegacy",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[value])!;
    private static CardInstanceDto Card(int attack,int health,params CardKeyword[] keywords)=>new(){InstanceId=Guid.NewGuid(),Type=CardType.Follower,
        Zone=CardZone.Field,CurrentAttack=attack,CurrentHealth=health,MaxHealth=health,MaxAttacksPerTurn=1,Keywords=keywords.ToList()};
    internal static void NativeSequences()
    {
        var original=GameDataProvider.Instance;var oldAccessor=GameServiceProvider.ContextAccessor;var rows=new List<object>();
        try {
            Run("bane-clears-large-ward",[(1,1,new[]{"Bane"},1),(10,10,Array.Empty<string>(),1)],[(0,20,Array.Empty<string>())],[(0,0),(1,-1)],10);
            Run("two-wards-consume-two-attacks",[(5,5,Array.Empty<string>(),1),(5,5,Array.Empty<string>(),1)],[(0,1,Array.Empty<string>()),(0,1,Array.Empty<string>())],[(0,0),(1,1)],0);
            Run("multi-attack-preserved-for-face",[(3,5,Array.Empty<string>(),2),(5,5,Array.Empty<string>(),1)],[(0,4,Array.Empty<string>())],[(1,0),(0,-1),(0,-1)],6);
            Run("effect-immune-ward-resists-bane",[(1,1,new[]{"Bane"},1),(10,10,Array.Empty<string>(),1)],[(0,20,new[]{"EffectImmune"})],[(0,0),(1,0)],0);
            Run("shielded-ward-needs-an-extra-attack",[(5,5,Array.Empty<string>(),1),(1,1,Array.Empty<string>(),1)],[(0,5,new[]{"Shield"})],[(1,0),(0,0)],0);
            Run("leader-shield-blocks-smallest-face-hit",[(1,1,Array.Empty<string>(),1),(5,5,Array.Empty<string>(),1)],[],[(0,-1),(1,-1)],5,true);
            Run("zero-attack-bane-is-a-legal-clear",[(0,1,new[]{"Bane"},1),(10,10,Array.Empty<string>(),1)],[(0,20,Array.Empty<string>())],[(0,0),(1,-1)],10);
            Run("zero-attack-can-remove-leader-shield",[(0,1,Array.Empty<string>(),1),(5,5,Array.Empty<string>(),1)],[],[(0,-1),(1,-1)],5,true);
            Run("super-evolution-kill-ping",[(2,2,Array.Empty<string>(),1),(5,5,Array.Empty<string>(),1)],[(0,1,Array.Empty<string>())],[(0,0),(1,-1)],6,false,true);
            if(Environment.GetEnvironmentVariable("AI_COMBAT_THREAT_OUTPUT") is {Length:>0} output)
                File.WriteAllText(output,JsonSerializer.Serialize(new{providerCalls=0,scope="Visible basic combat, controlled native attack sequences; no arbitrary triggers or full-match strength claim",cases=rows},new JsonSerializerOptions{WriteIndented=true}));
        } finally {GameDataProvider.SetInstanceForTesting(original);GameServiceProvider.ContextAccessor=oldAccessor;}

        void Run(string name,(int Attack,int Health,string[] Keywords,int Attacks)[] attackers,
            (int Attack,int Health,string[] Keywords)[] wards,(int Source,int Target)[] route,int expected,bool shield=false,bool super=false)
        {
            var definitions=new Dictionary<int,CardDefinition>{[1]=new(){Id=1,Name="Threat leader",Type=CardType.Leader,Health=100}};
            for(var i=0;i<attackers.Length;i++)definitions[10+i]=new(){Id=10+i,Name="Threat attacker "+i,Type=CardType.Follower,
                Attack=attackers[i].Attack,Health=attackers[i].Health,Keywords=attackers[i].Keywords.ToList()};
            for(var i=0;i<wards.Length;i++)definitions[20+i]=new(){Id=20+i,Name="Threat Ward "+i,Type=CardType.Follower,
                Attack=wards[i].Attack,Health=wards[i].Health,Keywords=wards[i].Keywords.Append("Ward").ToList()};
            if(shield)definitions[2]=new(){Id=2,Name="Shielded threat leader",Type=CardType.Leader,Health=100,Keywords=["Shield"]};
            var data=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch{
                "GetCardDefinition"=>definitions.GetValueOrDefault((int)args![0]!),"GetAllCardDefinitions"=>definitions,
                "GetAbilityConfigs"=>new List<AbilityConfig>(),_=>null});
            GameDataProvider.SetInstanceForTesting(data);var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
            using var services=AiEndTurnEngineTests.Services(data,accessor);using var scope=services.CreateScope();var sp=scope.ServiceProvider;
            var own=new PlayerState(Guid.NewGuid()){TurnNumber=6,IsEvolutionUnlocked=true,IsSuperEvolutionUnlocked=true,
                MaxEvolutionPoints=1,CurrentEvolutionPoints=1,MaxSuperEvolutionPoints=1,CurrentSuperEvolutionPoints=1};
            var enemy=new PlayerState(Guid.NewGuid()){TurnNumber=6};
            var game=new GameState("threat-"+name,own,enemy,own.PlayerId){Status=GameStatus.InProgress,Phase=GamePhase.Main,
                AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<IBuffManager>()};
            var factory=sp.GetRequiredService<ICardFactory>();
            own.Leader=factory.CreateAndRegisterNewCard(game,1,own.PlayerId,CardZone.Field);
            enemy.Leader=factory.CreateAndRegisterNewCard(game,shield?2:1,enemy.PlayerId,CardZone.Field);
            var sources=attackers.Select((_,i)=>factory.CreateAndRegisterNewCard(game,10+i,own.PlayerId,CardZone.Field)).ToArray();
            var targets=wards.Select((_,i)=>factory.CreateAndRegisterNewCard(game,20+i,enemy.PlayerId,CardZone.Field)).ToArray();
            foreach(var source in sources)source.ZoneStayTurn=1;
            using var context=new GameContext(sp,sp.GetRequiredService<AutoMapper.IMapper>(),accessor,game,own.PlayerId);
            var tx=sp.GetRequiredService<IGameActionTransactionService>();
            if(super){var evo=tx.ProcessTransactionAsync(context,[new SuperEvolveRequestedEvent(own.PlayerId,new SuperEvolveActionDto{UnitUid=sources[0].InstanceId.ToString()})]).GetAwaiter().GetResult();
                Check(evo.IsSuccess&&!evo.IsCancelled,name+" super evolution failed");}
            for(var i=0;i<sources.Length;i++)sources[i].MaxAttacksPerTurn=attackers[i].Attacks;
            var a=sources.Select(c=>CardInstanceProjection.Create(c,context)).ToList();var d=targets.Select(c=>CardInstanceProjection.Create(c,context)).ToList();
            var leader=CardInstanceProjection.Create(enemy.Leader,context);var input=JsonSerializer.Serialize(new{a,d,leader});
            var estimate=Read(a,d,leader);int before;
            using(Legacy(true))before=Read(a,d,leader).Damage;
            Check(estimate.Complete&&estimate.Damage==expected,name+$" forecast expected {expected}, got {estimate}; "+JsonSerializer.Serialize(a.Select(c=>new{c.CurrentAttack,c.CurrentHealth,c.MaxAttacksPerTurn,c.IsSuperEvolved,c.Keywords})));
            Check(JsonSerializer.Serialize(new{a,d,leader})==input,name+" threat read mutated card views");
            Check(a.All(card=>card.CanPerformAttackDto(own.PlayerId)),name+" DTO hid a native-legal attacker");
            var hp=enemy.Leader.CurrentHealth;
            foreach(var (source,target) in route){
                var destination=target<0?enemy.Leader:targets[target];
                var result=tx.ProcessTransactionAsync(context,[new AttackRequestedEvent(own.PlayerId,new AttackActionDto{AttackerUid=sources[source].InstanceId,TargetUid=destination.InstanceId})]).GetAwaiter().GetResult();
                Check(result.IsSuccess&&!result.IsCancelled,name+" native attack route failed");
            }
            var actual=hp-enemy.Leader.CurrentHealth;
            Check(actual==expected,name+$" native damage expected {expected}, got {actual}");
            rows.Add(new{name,expected,forecast=estimate.Damage,nativeDamage=actual,oldAggregate=before,states=estimate.States});
        }
    }

    internal static void ResourcesAndBounds()
    {
        var me=new PlayerStateDto{PlayerId=Guid.NewGuid(),CurrentPP=2,PpBoostUsesAvailable=1,LeaderCard=Card(0,5),
            Field=[Card(0,1,CardKeyword.Ward),Card(0,1,CardKeyword.Ward)],Hand=[Card(1,1,CardKeyword.Rush) with{CurrentCost=3}]};
        var enemy=new PlayerStateDto{PlayerId=Guid.NewGuid(),LeaderCard=Card(0,20),Field=[Card(5,5),Card(5,5)]};
        var boost=typeof(AiTurnExecutor).GetMethod("ShouldArmBoost",BindingFlags.Static|BindingFlags.NonPublic)!;
        bool Boost()=>(bool)boost.Invoke(null,[me,enemy])!;
        Check(Read(enemy.Field,me.Field).Damage==0&&!Boost(),"AI spent PP+1 on a false lethal threat behind two Wards");
        using(Legacy(true))Check(Boost(),"control did not reproduce the old false defensive PP+1 spend");
        me=me with{Field=[Card(0,20,CardKeyword.Ward)]};enemy=enemy with{Field=[Card(1,1,CardKeyword.Bane),Card(10,10)]};
        Check(Read(enemy.Field,me.Field).Damage==10&&Boost(),"AI failed to prepare an available answer to Bane-assisted lethal");
        using(Legacy(true))Check(!Boost(),"control did not reproduce the missed Bane-assisted lethal");
        var a=new List<CardInstanceDto>{Card(2,2) with{MaxAttacksPerTurn=1000000}};var d=new List<CardInstanceDto>{Card(0,1,CardKeyword.Ward)};
        var bounded=Read(a,d);Check(!bounded.Complete&&bounded.Damage>=1999998,"bounded fallback hid a large attack allowance");
        var leader=Card(0,20);a=[Card(2,2),Card(5,5)];d=[];
        Check(Read(a,d,leader).Damage==7,"open face damage incorrect");leader.Keywords.Add(CardKeyword.Shield);
        Check(Read(a,d,leader).Damage==5,"cached threat ignored a changed leader shield");
        var watch=Stopwatch.StartNew();for(var i=0;i<1000;i++)Read(enemy.Field,me.Field);watch.Stop();
        Console.WriteLine("Threat hot-path 1000 reads ms="+watch.ElapsedMilliseconds);
        var random=new Random(1479);var coldTimes=new List<double>();var completed=0;var maxStates=0;
        for(var i=0;i<100;i++) {
            var sources=Enumerable.Range(0,5).Select(index=>Card(random.Next(0,9),random.Next(1,12),
                random.Next(4)==0?CardKeyword.Bane:CardKeyword.Rush) with{MaxAttacksPerTurn=random.Next(1,3)}).ToList();
            var blockers=Enumerable.Range(0,5).Select(index=>Card(random.Next(0,7),random.Next(1,16),CardKeyword.Ward,
                random.Next(4)==0?CardKeyword.Shield:CardKeyword.Rush)).ToList();
            watch.Restart();var report=Read(sources,blockers);watch.Stop();coldTimes.Add(watch.Elapsed.TotalMilliseconds);
            if(report.Complete)completed++;maxStates=Math.Max(maxStates,report.States);
        }
        Check(maxStates<=769,"combat threat exceeded its explored-state limit");
        Console.WriteLine(JsonSerializer.Serialize(new{coldBoards=100,completed,maxStates,meanMs=coldTimes.Average(),maxMs=coldTimes.Max()}));
    }
    internal static void SourceCards(string source,string output)
    {
        var original=GameDataProvider.Instance;var oldAccessor=GameServiceProvider.ContextAccessor;
        var snapshot=Path.Combine(Path.GetTempPath(),"ai-threat-source-"+Guid.NewGuid().ToString("N")+".db");
        using(var input=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=source,Mode=SqliteOpenMode.ReadOnly}.ToString()))
        using(var copy=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=snapshot}.ToString())){input.Open();copy.Open();input.BackupDatabase(copy);}
        using var catalog=new ServiceCollection().AddLogging(log=>log.ClearProviders())
            .AddDbContext<ApplicationDbContext>(options=>options.UseSqlite(new SqliteConnectionStringBuilder{DataSource=snapshot,Mode=SqliteOpenMode.ReadOnly}.ToString()))
            .AddScoped<ICardRepository,EFCardRepository>().AddScoped<AbilityLoader>().BuildServiceProvider();
        try {
            var native=new GameDataProvider(catalog.GetRequiredService<IServiceScopeFactory>(),NullLogger<GameDataProvider>.Instance);
            native.InitializeAsync().GetAwaiter().GetResult();var cards=native.GetAllCardDefinitions().Values.Where(c=>c.Type==CardType.Follower).OrderBy(c=>c.Id).ToArray();
            bool Basic(CardDefinition card)=>native.GetAbilityConfigs(card.Id).Count==0;
            var poison=cards.First(c=>(c.Attack??0)<=2&&c.Keywords.Contains("Bane")&&c.Keywords.All(k=>k is "Bane" or "Rush" or "Storm")&&Basic(c));
            var wall=cards.First(c=>(c.Health??0)>=7&&c.Keywords.Contains("Ward")&&c.Keywords.All(k=>k is "Ward" or "Rush")&&Basic(c));
            var hitter=cards.First(c=>(c.Attack??0)>=8&&c.Keywords.All(k=>k is "Rush" or "Storm")&&Basic(c));
            const int leaderId=2000000199;
            var leader=new CardDefinition{Id=leaderId,Name="Source threat fixture leader",Type=CardType.Leader,Health=100};
            var data=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch{
                "GetCardDefinition" when (int)args![0]! == leaderId=>leader,
                "GetAbilityConfigs" when (int)args![0]! == leaderId=>new List<AbilityConfig>(),
                _=>method.Invoke(native,args)});
            GameDataProvider.SetInstanceForTesting(data);var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
            using var services=AiEndTurnEngineTests.Services(data,accessor);using var scope=services.CreateScope();var sp=scope.ServiceProvider;
            var own=new PlayerState(Guid.NewGuid()){TurnNumber=6};var enemy=new PlayerState(Guid.NewGuid()){TurnNumber=6};
            var game=new GameState("real-card-threat",own,enemy,own.PlayerId){Status=GameStatus.InProgress,Phase=GamePhase.Main,
                AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<IBuffManager>()};
            var factory=sp.GetRequiredService<ICardFactory>();own.Leader=factory.CreateAndRegisterNewCard(game,leaderId,own.PlayerId,CardZone.Field);
            enemy.Leader=factory.CreateAndRegisterNewCard(game,leaderId,enemy.PlayerId,CardZone.Field);
            var killer=factory.CreateAndRegisterNewCard(game,poison.Id,own.PlayerId,CardZone.Field);killer.ZoneStayTurn=1;
            var attacker=factory.CreateAndRegisterNewCard(game,hitter.Id,own.PlayerId,CardZone.Field);attacker.ZoneStayTurn=1;
            var target=factory.CreateAndRegisterNewCard(game,wall.Id,enemy.PlayerId,CardZone.Field);
            using var context=new GameContext(sp,sp.GetRequiredService<AutoMapper.IMapper>(),accessor,game,own.PlayerId);
            var a=new[]{killer,attacker}.Select(c=>CardInstanceProjection.Create(c,context)).ToList();
            var d=new List<CardInstanceDto>{CardInstanceProjection.Create(target,context)};
            var estimate=Read(a,d,CardInstanceProjection.Create(enemy.Leader,context));int previous;using(Legacy(true))previous=Read(a,d).Damage;
            var hp=enemy.Leader.CurrentHealth;var tx=sp.GetRequiredService<IGameActionTransactionService>();
            foreach(var (from,to) in new[]{(killer,target),(attacker,enemy.Leader)}){
                var result=tx.ProcessTransactionAsync(context,[new AttackRequestedEvent(own.PlayerId,new AttackActionDto{AttackerUid=from.InstanceId,TargetUid=to.InstanceId})]).GetAwaiter().GetResult();
                Check(result.IsSuccess&&!result.IsCancelled,"real card attack route failed");
            }
            var actual=hp-enemy.Leader.CurrentHealth;
            Check(estimate.Complete&&estimate.Damage==actual&&actual==hitter.Attack,"real source card forecast disagreed with native transactions");
            File.WriteAllText(output,JsonSerializer.Serialize(new{providerCalls=0,sourceReadOnly=true,
                cards=new[]{poison,wall,hitter}.Select(c=>new{c.Id,c.Name,c.Attack,c.Health,c.Keywords,registeredAbilities=native.GetAbilityConfigs(c.Id).Count}),
                predicted=estimate.Damage,nativeDamage=actual,previousAggregate=previous,
                scope="Unmodified real basic card definitions and native transaction route; synthetic leaders, no arbitrary triggered abilities"},new JsonSerializerOptions{WriteIndented=true}));
        } finally {GameDataProvider.SetInstanceForTesting(original);GameServiceProvider.ContextAccessor=oldAccessor;SqliteConnection.ClearAllPools();File.Delete(snapshot);}
    }

    internal static void ZeroAttackPlans()
    {
        var own=Guid.NewGuid();var enemy=Guid.NewGuid();
        CardInstanceDto Unit(int attack,int hp,params CardKeyword[] keywords)=>Card(attack,hp,keywords) with{OwnerPlayerId=own,CanAttack=true,ZoneStayTurn=1};
        var zero=Unit(0,2,CardKeyword.Bane);var big=Unit(10,10);var ward=Card(0,20,CardKeyword.Ward) with{OwnerPlayerId=enemy};
        var state=new GameStateDto{CurrentPlayerId=own,Phase=GamePhase.Main,Players=[new(){PlayerId=own,LeaderCard=Card(0,20) with{Type=CardType.Leader,OwnerPlayerId=own},Field=[zero,big]},
            new(){PlayerId=enemy,LeaderCard=Card(0,10) with{Type=CardType.Leader,OwnerPlayerId=enemy},Field=[ward]}]};
        var search=typeof(AiLearningModel).Assembly.GetType("ShadowverseBeyond.Server.Services.AiTacticalSearch")!.GetMethod("Find")!;
        object? Plan()=>search.Invoke(null,[state,own,CancellationToken.None,null,null]);
        var plan=Plan()??throw new Exception("zero Bane route not found");
        var first=plan.GetType().GetProperty("First")!.GetValue(plan)!;var data=(AttackActionDto)first.GetType().GetProperty("Data")!.GetValue(first)!;
        Check((bool)plan.GetType().GetProperty("ProjectsLethal")!.GetValue(plan)!&&data.AttackerUid==zero.InstanceId&&data.TargetUid==ward.InstanceId,
            "search failed to clear Ward with zero attack before lethal");
        var plain=Unit(0,2);state=state with{Players=[state.Players[0] with{Field=[plain]},state.Players[1] with{Field=[]}]};
        plan=Plan();Check(plan==null||(!(bool)plan.GetType().GetProperty("ProjectsLethal")!.GetValue(plan)!
            &&(double)plan.GetType().GetProperty("Gain")!.GetValue(plan)!<=0),"search promoted a useless zero-attack face hit");
    }

    internal static void ExhaustiveSmallBoards()
    {
        var tactical=typeof(AiLearningModel).Assembly.GetType("ShadowverseBeyond.Server.Services.AiTacticalSearch")!;
        var apply=tactical.GetMethod("EstimateKnownAction",BindingFlags.NonPublic|BindingFlags.Static)!;
        var targets=typeof(AiService).GetMethod("LegalAttackTargets",BindingFlags.NonPublic|BindingFlags.Static)!;
        var random=new Random(24519);var checkedBoards=0;
        for(var sample=0;sample<200;sample++) {
            var actor=Guid.NewGuid();var other=Guid.NewGuid();
            var attackers=Enumerable.Range(0,2).Select(index=>{
                var keywords=new List<CardKeyword>();
                foreach(var keyword in new[]{CardKeyword.Bane,CardKeyword.Shield,CardKeyword.IgnoreWard,CardKeyword.CannotAttackLeader,CardKeyword.CannotAttackFollower,CardKeyword.EffectImmune,CardKeyword.DestroyResist})
                    if(random.Next(5)==0)keywords.Add(keyword);
                return Card(random.Next(0,7),random.Next(1,9),keywords.ToArray()) with{OwnerPlayerId=actor,CanAttack=true,ZoneStayTurn=1,
                    MaxAttacksPerTurn=random.Next(1,3),IsSuperEvolved=random.Next(4)==0};
            }).ToList();
            var defenders=Enumerable.Range(0,2).Select(index=>{
                var keywords=new List<CardKeyword>();
                if(random.Next(4)!=0)keywords.Add(CardKeyword.Ward);
                foreach(var keyword in new[]{CardKeyword.Shield,CardKeyword.Bane,CardKeyword.EffectImmune,CardKeyword.DestroyResist})
                    if(random.Next(4)==0)keywords.Add(keyword);
                return Card(random.Next(0,6),random.Next(1,9),keywords.ToArray()) with{OwnerPlayerId=other};
            }).ToList();
            var leader=Card(0,1000) with{Type=CardType.Leader,OwnerPlayerId=other};if(random.Next(4)==0)leader.Keywords.Add(CardKeyword.Shield);
            var state=new GameStateDto{CurrentPlayerId=actor,Players=[new(){PlayerId=actor,LeaderCard=Card(0,1000) with{Type=CardType.Leader},Field=attackers},
                new(){PlayerId=other,LeaderCard=leader,Field=defenders}]};
            var memo=new Dictionary<string,int>();
            int Enumerate(GameStateDto position) {
                var own=position.Players[0];var enemy=position.Players[1];var key=enemy.LeaderCard!.CurrentHealth+":"+enemy.LeaderCard.Keywords.Contains(CardKeyword.Shield)
                    +"|"+string.Join(';',own.Field.Select(c=>$"{c.InstanceId}:{c.CurrentHealth}:{c.AttacksThisTurn}:{c.Keywords.Contains(CardKeyword.Shield)}"))
                    +"|"+string.Join(';',enemy.Field.Select(c=>$"{c.InstanceId}:{c.CurrentHealth}:{c.Keywords.Contains(CardKeyword.Shield)}"));
                if(memo.TryGetValue(key,out var seen))return seen;
                var best=1000-enemy.LeaderCard.CurrentHealth;
                foreach(var card in own.Field.Where(c=>c.CanPerformAttackDto(actor)))
                    foreach(var target in ((IEnumerable<CardInstanceDto>)targets.Invoke(null,[card,enemy])!).ToArray()) {
                        var after=(GameStateDto)apply.Invoke(null,[position,actor,"attack",card,target])!;
                        best=Math.Max(best,Enumerate(after));
                    }
                return memo[key]=best;
            }
            var expected=Enumerate(state);var estimated=Read(attackers,defenders,leader);
            Check(estimated.Complete&&estimated.Damage==expected,$"small-board enumeration disagreed at {sample}: expected {expected}, got {estimated}; "+JsonSerializer.Serialize(new{attackers,defenders,leader}));
            checkedBoards++;
        }
        Console.WriteLine("Complete small-board attack enumerations="+checkedBoards);
    }

    internal static void ScopeIsolation()
    {
        var a=new List<CardInstanceDto>{Card(5,5),Card(5,5)};var d=new List<CardInstanceDto>{Card(0,1,CardKeyword.Ward),Card(0,1,CardKeyword.Ward)};
        var tasks=new[]{true,false}.Select(legacy=>Task.Run(async()=>{using var lease=Legacy(legacy);await Task.Yield();
            for(var i=0;i<20;i++)Check(Read(a,d).Damage==(legacy?5:0),"concurrent comparisons leaked their combat estimator");}));
        Task.WhenAll(tasks).GetAwaiter().GetResult();Check(Read(a,d).Damage==0,"legacy threat scope escaped after evaluation");
    }
}
