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
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
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

// Controlled native comparison of recorded turn 5 facts, not a restored full replay.
internal static class AiTurnFiveAuditTests
{
    private const int Leader=2000001510,Junk=2000001511;
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    internal static void Boundaries()
    {
        var type=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiExpiringBoardValue")!;
        bool Expiring(AbilityConfig a)=>(bool)type.GetMethod("SelfVanish",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[a])!;
        AbilityConfig Rule()=>new(){Type=AbilityType.Triggered,Trigger=new(){EventTypes=[EventType.TurnEnd],ValidZones=[CardZone.Field]},
            Targets=new(){["self"]=new(){Query=new(){SourcePoolExpression="context.sourceCard"}}},Effects=[new(){Type=ShadowverseBeyond.Server.Domain.Effects.EffectType.Vanish}]};
        Check(Expiring(Rule()),"plain current self-Vanish was not recognized");
        var own=Rule();own.Conditions.Add(new(){Expression="((context.triggeringEvent.Source) != (null)) and ((context.triggeringEvent.Source.InstanceId) == (context.owner.PlayerId))"});Check(Expiring(own),"native Ghost owner predicate unavailable");
        var wrong=Rule();wrong.Trigger.RelationType=TriggerRelationType.TargetSelf;Check(!Expiring(wrong),"non-triggering relation falsely discounted a body");
        var dynamic=Rule();dynamic.Conditions.Add(new(){Expression="count(context.owner.hand)==0"});Check(!Expiring(dynamic),"mutable hand condition treated as certain");
        var repeat=Rule();repeat.Effects[0].RepeatCount=2;Check(!Expiring(repeat),"repeat was treated as a simple self expiry");
        var chained=Rule();chained.Effects[0].ChainedAbilities.Add(new());Check(!Expiring(chained),"unknown chain was projected");
        var limited=Rule();limited.Trigger.CountLimit=new();Check(!Expiring(limited),"count-limited expiry assumed available");
        var other=Rule();other.Targets["self"].Query=new(){SourcePoolExpression="context.opponent.field"};Check(!Expiring(other),"opponent removal changed own board value");
        var choice=Rule();choice.Targets["self"].PlayerChoiceOptions=new(){IsRequired=true};Check(!Expiring(choice),"optional player selection assumed automatic");
        var disabled=Rule();disabled.IsEnabled=false;Check(!Expiring(disabled),"disabled ability projected");
    }
    internal static void Run(string source,string output,string? weights)
    {
        var old=GameDataProvider.Instance;var oldAccessor=GameServiceProvider.ContextAccessor;
        using var catalog=new ServiceCollection().AddLogging(l=>l.ClearProviders())
            .AddDbContext<ApplicationDbContext>(o=>o.UseSqlite(new SqliteConnectionStringBuilder{DataSource=source,Mode=SqliteOpenMode.ReadOnly}.ToString()))
            .AddScoped<ICardRepository,EFCardRepository>().AddScoped<AbilityLoader>().BuildServiceProvider();
        var rows=new List<object>();
        try{
            Boundaries();var native=new GameDataProvider(catalog.GetRequiredService<IServiceScopeFactory>(),NullLogger<GameDataProvider>.Instance);native.InitializeAsync().GetAwaiter().GetResult();
            foreach(var route in new[]{"recorded","ghost-trade","noble-reiss-evolve","noble-reiss-save-ep","search-control","search-current"}){
                try{rows.Add(Scene(native,route,weights));}catch(Exception e){rows.Add(new{route,passed=false,error=e.GetBaseException().ToString()});}
            }
            File.WriteAllText(output,JsonSerializer.Serialize(new{providerCalls=0,databaseReadOnly=true,cases=rows,
                scope="Native transactions with recorded hand/PP and public units. Blood resource fixed at 2 (recorded successful Noble refund proves affordability), irrelevant deck order replaced by inert draws. No complete historical replay or win-rate claim."},new JsonSerializerOptions{WriteIndented=true}));
            Check(JsonSerializer.SerializeToElement(rows).EnumerateArray().All(r=>r.GetProperty("passed").GetBoolean()),"Turn five audit failed: "+output);
            Console.WriteLine("PASS native turn-five routes: "+rows.Count+"; provider requests=0");
        }finally{GameDataProvider.SetInstanceForTesting(old);GameServiceProvider.ContextAccessor=oldAccessor;}
    }
    private static object Scene(IGameDataProvider native,string route,string? weights)
    {
        var defs=native.GetAllCardDefinitions().ToDictionary(p=>p.Key,p=>p.Value);
        defs[Leader]=new(){Id=Leader,Name="audit leader",Type=CardType.Leader,Health=20};
        defs[Junk]=new(){Id=Junk,Name="inert private draw fixture",Type=CardType.Spell,Cost=20};
        var data=Proxy<IGameDataProvider>.Make((m,a)=>m.Name switch{"GetCardDefinition"=>defs.GetValueOrDefault((int)a![0]!),"GetAllCardDefinitions"=>defs,
            "GetAbilityConfigs"=>(int)a![0]!>=Leader?new List<AbilityConfig>():native.GetAbilityConfigs((int)a[0]!),_=>m.Invoke(native,a)});
        GameDataProvider.SetInstanceForTesting(data);var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["AiStrategy:Enabled"]="false",
            ["AiStrategy:ExpiringBoardValue"]=(route!="search-control").ToString(),["AiModel:Path"]=weights??Path.Combine(Path.GetTempPath(),Guid.NewGuid()+"-no-model")}).Build();
        var model=new AiLearningModel(new Host(),config,NullLogger<AiLearningModel>.Instance);
        GameState game=null!;ServiceProvider services=null!;AiService ai=null!;CardInstance noble=null!,reiss=null!,tutor=null!,kessa=null!;
        var actions=new List<object>();var choices=new List<object>();
        GameStateDto View(){using var s=services.CreateScope();using var c=new GameContext(s.ServiceProvider,s.ServiceProvider.GetRequiredService<IMapper>(),accessor,game,game.Player1.PlayerId);return GameStateProjection.Create(game,c);}
        var pending=Proxy<IPendingActionService>.Make((m,a)=>{
            if(m.ReturnType==typeof(bool))return false;if(m.Name!="WaitForPlayerChoiceAsync")return null;
            var request=(PlayerChoiceRequest)a![1]!;
            var chosen=request.TargetKey=="decision"?request.SelectableTargetIds[0]
                :new[]{kessa.InstanceId,route.StartsWith("noble-reiss")?tutor.InstanceId:reiss.InstanceId}.FirstOrDefault(request.SelectableTargetIds.Contains);
            Check(chosen!=Guid.Empty,"unexpected target pool: "+request.TargetKey);choices.Add(new{request.TargetKey,cardId=game.FindCardInstance(chosen)?.CurrentDefinitionId});
            return Task.FromResult(new PlayerChoiceResult{ChosenTargetIds=[chosen]});
        });
        async Task<bool> Commit(PlayerActionWrapper action){
            var attack=action.ActionData as AttackActionDto;var play=action.ActionData as PlayCardActionDto;
            var sourceId=attack==null?play==null?null:(int?)game.FindCardInstance(play.CardUid)?.CurrentDefinitionId:game.FindCardInstance(attack.AttackerUid)?.CurrentDefinitionId;
            var targetId=attack==null?null:(int?)game.FindCardInstance(attack.TargetUid)?.CurrentDefinitionId;
            IGameEvent ev=action.ActionData switch{PlayCardActionDto p=>new PlayCardRequestedEvent(action.PlayerId,p),AttackActionDto x=>new AttackRequestedEvent(action.PlayerId,x),
                EvolveActionDto e=>new EvolveRequestedEvent(action.PlayerId,e),SuperEvolveActionDto e=>new SuperEvolveRequestedEvent(action.PlayerId,e),_ when action.ActionType=="EndTurn"=>new EndTurnRequestedEvent(action.PlayerId),_=>throw new Exception("unexpected action")};
            using var s=services.CreateScope();using var c=new GameContext(s.ServiceProvider,s.ServiceProvider.GetRequiredService<IMapper>(),accessor,game,action.PlayerId,triggeringEvent:ev);
            var r=await s.ServiceProvider.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(c,[ev]);Check(r.IsSuccess&&!r.IsCancelled,r.ErrorMessage??"native action cancelled");
            actions.Add(new{kind=action.ActionType,sourceId,targetId,pp=game.Player1.CurrentPlayPoints,hp=game.Player1.Leader!.CurrentHealth,enemyHp=game.Player2.Leader!.CurrentHealth,hand=game.Player1.Hand.Count});action.MarkCompleted();return true;
        }
        Task<PlayabilityResultDto> Playable(Guid uid){var card=game.FindCardInstance(uid)!;return Task.FromResult(new PlayabilityResultDto{IsPlayable=card.CurrentCost<=game.Player1.CurrentPlayPoints&&(card.CardType==CardType.Spell||game.Player1.Field.Count<5),SelectedPlayMode=PlayMode.Base,PlayModes=[new(){Mode=PlayMode.Base,EffectiveCost=card.CurrentCost,IsAvailable=true}]});}
        var games=Proxy<IGameService>.Make((m,a)=>m.Name switch{"GetGameState"=>game,"EnqueuePlayerActionAsync"=>Commit((PlayerActionWrapper)a![1]!),"GetCardPlayabilityAsync" or "GetCardPlayabilityForModeAsync"=>Playable((Guid)a![2]!),_=>null});
        using(services=AiEndTurnEngineTests.Services(data,accessor,pending,games,s=>s.AddSingleton<IConfiguration>(config))){
            using var scope=services.CreateScope();var sp=scope.ServiceProvider;var factory=sp.GetRequiredService<ICardFactory>();
            var own=new PlayerState(Guid.NewGuid()){IsAI=true,TurnNumber=5,CurrentPlayPoints=5,MaxPlayPoints=5,CurrentEvolutionPoints=2,MaxEvolutionPoints=2,
                CurrentSuperEvolutionPoints=0,MaxSuperEvolutionPoints=2,IsEvolutionUnlocked=true,IsSuperEvolutionUnlocked=false,InitialDeckDefinitionIds=[1474120,1474113,1473444],BloodPool=2};
            var enemy=new PlayerState(Guid.NewGuid()){TurnNumber=4};
            game=new("turn-five-"+route,own,enemy,own.PlayerId){Status=GameStatus.InProgress,Phase=GamePhase.Main,AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<IBuffManager>()};
            CardInstance Add(int id,CardZone zone=CardZone.Hand)=>factory.CreateAndRegisterNewCard(game,id,own.PlayerId,zone);
            own.Leader=Add(Leader,CardZone.Field);enemy.Leader=factory.CreateAndRegisterNewCard(game,Leader,enemy.PlayerId,CardZone.Field);own.Leader.Damage=3;enemy.Leader.Damage=1;
            var skeleton=Add(1250402,CardZone.Field);skeleton.ZoneStayTurn=1;var other=Add(1250402,CardZone.Field);other.ZoneStayTurn=1;
            noble=Add(1473444);reiss=Add(1474120);tutor=Add(1474113);Add(1474276,CardZone.Emblem);
            kessa=factory.CreateAndRegisterNewCard(game,1474141,enemy.PlayerId,CardZone.Field);kessa.ZoneStayTurn=1;
            var artifact=factory.CreateAndRegisterNewCard(game,1473168,enemy.PlayerId,CardZone.Field);artifact.ZoneStayTurn=1;
            factory.CreateAndRegisterNewCard(game,1474143,enemy.PlayerId,CardZone.Field);
            using(var c=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId)){
                var r=sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(c,[new StatChangedEvent(kessa,2,1,0,DurationType.Permanent)]).GetAwaiter().GetResult();Check(r.IsSuccess,"prepare 4/3 Kessa");
            }
            for(var i=0;i<6;i++){Add(Junk,CardZone.Deck);factory.CreateAndRegisterNewCard(game,Junk,enemy.PlayerId,CardZone.Deck);}
            ai=new(NullLogger<AiService>.Instance,services.GetRequiredService<IServiceScopeFactory>(),model,config);
            void Play(CardInstance card)=>Commit(new("PlayCard",own.PlayerId,new PlayCardActionDto{CardUid=card.InstanceId,PlayMode=PlayMode.Base,PaidCost=card.CurrentCost})).GetAwaiter().GetResult();
            void Attack(CardInstance card,CardInstance target)=>Commit(new("PerformAttack",own.PlayerId,new AttackActionDto{AttackerUid=card.InstanceId,TargetUid=target.InstanceId})).GetAwaiter().GetResult();
            void Evolve()=>Commit(new("PerformEvolve",own.PlayerId,new EvolveActionDto{UnitUid=skeleton.InstanceId.ToString()})).GetAwaiter().GetResult();
            int? localFirst=null;object? refundFacts=null;
            using(var c=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId)){
                var snapshot=GameStateProjection.Create(game,c);localFirst=game.FindCardInstance(ai.DecidePlayCardAsync(View(),own.PlayerId).GetAwaiter().GetResult()!.CardUid)?.CurrentDefinitionId;
                var type=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiPpRefundFacts")!;
                refundFacts=type.GetMethod("ForGame",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[game,snapshot,own.PlayerId]);
                var fact=JsonSerializer.SerializeToElement(refundFacts,new JsonSerializerOptions(JsonSerializerDefaults.Web));
                Check(fact.GetProperty("available").GetBoolean()&&fact.GetProperty("options").GetArrayLength()==1,"conditional refund facts missing");
                var entry=fact.GetProperty("options")[0];Check(entry.GetProperty("restoreUpTo").GetInt32()==2&&entry.GetProperty("ppIfResolved").GetInt32()==3&&entry.GetProperty("paidBlood").GetInt32()==2,"incorrect Noble refund aid");
                var originalBlood=own.BloodPool;own.BloodPool=0;
                var shortBlood=JsonSerializer.SerializeToElement(type.GetMethod("ForGame",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[game,GameStateProjection.Create(game,c),own.PlayerId]),new JsonSerializerOptions(JsonSerializerDefaults.Web));
                Check(!shortBlood.GetProperty("options")[0].GetProperty("bloodAvailable").GetBoolean(),"unfunded blood refund advertised as available");own.BloodPool=originalBlood;
                var originalDamage=own.Leader.Damage;own.Leader.Damage=19;
                var dying=JsonSerializer.SerializeToElement(type.GetMethod("ForGame",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[game,GameStateProjection.Create(game,c),own.PlayerId]),new JsonSerializerOptions(JsonSerializerDefaults.Web));
                Check(!dying.GetProperty("options")[0].GetProperty("survivesKnownSelfDamage").GetBoolean(),"lethal self damage ignored by refund aid");own.Leader.Damage=originalDamage;
            }
            if(route.StartsWith("noble-reiss")){
                Play(noble);Check(own.CurrentPlayPoints==3&&own.Hand.Count==2,"Noble did not refund 2 PP");Play(reiss);Check(own.Hand.Count==0&&own.CurrentPlayPoints==1,"Reiss did not empty hand leaving 1PP");
                if(route.EndsWith("evolve"))Evolve();Attack(other,enemy.Leader);Attack(skeleton,enemy.Leader);
            }else{
                Play(tutor);Play(noble);Evolve();Attack(other,enemy.Leader);
                var ghost=own.Field.Single(c=>c.CurrentDefinitionId==1050403);
                if(route.StartsWith("search")){
                    using(var c=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId)){
                        var snapshot=GameStateProjection.Create(game,c);var type=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiExpiringBoardValue")!;
                        var ids=(HashSet<Guid>)type.GetMethod("Read",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[c,snapshot.Players[0]])!;
                        Check(ids.Contains(ghost.InstanceId)==(route=="search-current"),"expiry recognition did not match control");
                    }
                    var executor=new AiTurnExecutor(games,ai,NullLogger<AiTurnExecutor>.Instance,sp.GetRequiredService<IMapper>(),services,model,config);
                    using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));executor.ExecuteTurnAsync(game.GameId,own.PlayerId,timeout.Token).GetAwaiter().GetResult();
                }else if(route=="ghost-trade"){Attack(ghost,artifact);Attack(skeleton,enemy.Leader);}
                else{Attack(ghost,enemy.Leader);Attack(skeleton,artifact);}
            }
            if(!route.StartsWith("search"))Commit(new("EndTurn",own.PlayerId,new{})).GetAwaiter().GetResult();
            var expected=route is "recorded" or "search-control"?17:route is "ghost-trade" or "search-current"?15:route=="noble-reiss-evolve"?13:15;
            Check(enemy.Leader.CurrentHealth==expected,"enemy HP "+enemy.Leader.CurrentHealth+" expected "+expected);
            Check(enemy.Field.All(c=>!c.IsFollower),"enemy follower survived");
            Check(own.CurrentEvolutionPoints==(route=="noble-reiss-save-ep"?2:1),"wrong EP use");
            Check(own.Hand.Count==2,"end-turn refill missing");
            var final=View();var finalOwn=final.Players.First(p=>p.PlayerId==own.PlayerId);
            return new{route,localFirst,refundFacts,actions,choices,hp=own.Leader.CurrentHealth,enemyHp=enemy.Leader.CurrentHealth,pp=own.CurrentPlayPoints,ep=own.CurrentEvolutionPoints,
                field=finalOwn.Field.Select(c=>new{cardId=c.CardId,attack=c.CurrentAttack,health=c.CurrentHealth}).ToArray(),enemyField=enemy.Field.Select(c=>c.CurrentDefinitionId),hand=own.Hand.Count,passed=true};
        }
    }
    private sealed class Host:IWebHostEnvironment{public string ApplicationName{get;set;}="audit";public string EnvironmentName{get;set;}="Test";public string WebRootPath{get;set;}=Path.GetTempPath();public string ContentRootPath{get;set;}=Path.GetTempPath();public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
}
