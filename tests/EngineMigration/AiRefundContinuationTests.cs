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
using Microsoft.Extensions.Hosting;
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
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class AiRefundContinuationTests
{
    const int Leader=2000001580,Junk=2000001581,Noble=1473444,Reiss=1474120,Tutor=1474113,Kessa=1474141,Weaver=1474173,Emperor=1474181;
    static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void BudgetBoundaries()
    {
        var assembly=typeof(AiService).Assembly;var own=new PlayerStateDto{PlayerId=Guid.NewGuid(),CurrentPP=5,MaxPP=5,BloodPool=2};
        own.LeaderCard=new(){InstanceId=Guid.NewGuid(),OwnerPlayerId=own.PlayerId,CardId=Leader,CurrentHealth=20,MaxHealth=20};
        own.Hand=[new(){InstanceId=Guid.NewGuid(),OwnerPlayerId=own.PlayerId,CardId=Noble,CurrentCost=4,Zone=CardZone.Hand},new(){InstanceId=Guid.NewGuid(),OwnerPlayerId=own.PlayerId,CardId=Reiss,CurrentCost=2,Zone=CardZone.Hand}];
        var type=assembly.GetType("ShadowverseBeyond.Server.Services.AiPlayCandidates+Candidate")!;var candidates=Array.CreateInstance(type,2);
        for(var i=0;i<2;i++)candidates.SetValue(Activator.CreateInstance(type,own.Hand[i],own.Hand[i],i,PlayMode.Base,own.Hand[i].CurrentCost,false,null,own.Hand[i].CardId),i);
        var refundType=assembly.GetType("ShadowverseBeyond.Server.Services.AiPpRefundFacts+Refund")!;
        var refunds=JsonSerializer.Deserialize(JsonSerializer.Serialize(new[]{new{handIndex=0,cardId=Noble,baseCost=4,paidBlood=2,ownDamage=1,restoreUpTo=2,ppAfterPayment=1,ppIfResolved=3,bloodAvailable=true,survivesKnownSelfDamage=true,destroyTargetKey="target1",abilityDefinitionId=7}},Json),refundType.MakeArrayType(),Json)!;
        var first=new AiStrategyPlanner.PlayAdvice(0,Noble,"Base",4,"use",new("target1","enemy","field",0,Kessa,7));var second=new AiStrategyPlanner.PlayAdvice(1,Reiss,"Base",2,"use");
        var plan=new AiStrategyPlanner.TurnPlan("setup","conditional arithmetic boundary",[],[],Play:first,Plays:[first,second]);
        JsonElement Read(AiStrategyPlanner.TurnPlan value,object? facts)=>JsonSerializer.SerializeToElement(assembly.GetType("ShadowverseBeyond.Server.Services.AiPlaySequence")!.GetMethod("Validate",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[value,candidates,Array.Empty<AiStrategyPlanner.HandLossRule>(),own,null,facts]),Json);
        bool Valid(AiStrategyPlanner.TurnPlan value,object? facts)=>Read(value,facts).GetProperty("valid").GetBoolean();
        Check(!Valid(plan,null),"old payment-only rule did not reject six-cost route with five PP");
        var good=Read(plan,refunds);Check(good.GetProperty("valid").GetBoolean()&&good.GetProperty("remainingPP").GetInt32()==1&&good.GetProperty("conditionalBudgetSteps")[0].GetInt32()==0,"conditional refund budget or explicit marker incorrect");
        own.BloodPool=1;Check(!Valid(plan,refunds),"unaffordable sacrifice invented credit");own.BloodPool=2;
        own.LeaderCard.CurrentHealth=1;Check(!Valid(plan,refunds),"known lethal self damage granted continuation credit");own.LeaderCard.CurrentHealth=20;
        own.CurrentPP=3;Check(!Valid(plan,refunds),"unpaid first step borrowed its own refund");own.CurrentPP=5;
        foreach(var bad in new[]{first with{Target=null},first with{Target=first.Target! with{Side="own"}},first with{Target=first.Target! with{AbilityDefinitionId=8}},first with{Target=first.Target! with{Key="other"}}})
            Check(!Valid(plan with{Play=bad,Plays=[bad,second]},refunds),"missing/unmatched target or rule granted credit");
        own.IsPpBoostArmed=true;var boosted=Read(plan,refunds);Check(boosted.GetProperty("valid").GetBoolean()&&boosted.GetProperty("conditionalBudgetSteps").GetArrayLength()==0,"armed PP boost was incorrectly combined with a refund forecast");own.IsPpBoostArmed=false;
        Check(own.CurrentPP==5&&own.BloodPool==2&&own.LeaderCard.CurrentHealth==20,"budget simulation mutated actual resources");
        Console.WriteLine("PASS conditional refund budget: known branch only, first payment, blood, lethal self damage, explicit target, boost isolation and no state mutation");
    }
    internal static void Run(string db,string output,string weights)
    {
        BudgetBoundaries();var previous=GameDataProvider.Instance;var prior=GameServiceProvider.ContextAccessor;
        using var source=new ServiceCollection().AddLogging(l=>l.ClearProviders()).AddDbContext<ApplicationDbContext>(o=>o.UseSqlite(new SqliteConnectionStringBuilder{DataSource=db,Mode=SqliteOpenMode.ReadOnly}.ToString()))
            .AddScoped<ICardRepository,EFCardRepository>().AddScoped<AbilityLoader>().BuildServiceProvider();
        var modelConfig=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["AiModel:Path"]=Path.GetFullPath(weights)}).Build();
        var model=new AiLearningModel(new Host(),modelConfig,NullLogger<AiLearningModel>.Instance);Check(model.Current!=null,"frozen weights missing");
        try{
            var native=new GameDataProvider(source.GetRequiredService<IServiceScopeFactory>(),NullLogger<GameDataProvider>.Instance);native.InitializeAsync().GetAwaiter().GetResult();
            var rows=new List<object>();foreach(var kind in new[]{"nominal","legacy","missing-observation","blood-response","blocked-next","replacement","discard-next","local-threat"})
                try{rows.Add(Scene(native,kind,model));}catch(Exception e){rows.Add(new{kind,passed=false,error=e.GetBaseException().ToString()});}
            File.WriteAllText(output,JsonSerializer.Serialize(new{autonomousPaidRequests=0,conditionalBudgetBoundariesPassed=true,cases=rows,scope="Production DI planner and AiTurnExecutor, actual Noble/Reiss/Tutor/Kessa configuration and native commits. Scripted planner outputs, fixed trained weights and inert unknown deck order. Proof of route wiring/reconciliation, not real DeepSeek strength or complete historical replay."},new JsonSerializerOptions{WriteIndented=true}));
            Check(JsonSerializer.SerializeToElement(rows).EnumerateArray().All(r=>r.GetProperty("passed").GetBoolean()),"native refund continuation failed: "+output);
            Console.WriteLine("PASS native conditional refund: 8 executor controls, actual first-step return before follow-up, response/observation/identity/legality guards; real provider requests=0");
        }finally{GameDataProvider.SetInstanceForTesting(previous);GameServiceProvider.ContextAccessor=prior;}
    }
    static object Scene(IGameDataProvider native,string kind,AiLearningModel model)
    {
        var defs=native.GetAllCardDefinitions().ToDictionary(p=>p.Key,p=>p.Value);defs[Leader]=new(){Id=Leader,Type=CardType.Leader,Health=20};defs[Junk]=new(){Id=Junk,Type=CardType.Spell,Cost=20};
        var data=Proxy<IGameDataProvider>.Make((m,a)=>m.Name switch{"GetCardDefinition"=>defs.GetValueOrDefault((int)a![0]!),"GetAllCardDefinitions"=>defs,"GetAbilityConfigs"=>(int)a![0]!>=Leader?new List<AbilityConfig>():native.GetAbilityConfigs((int)a[0]!),_=>m.Invoke(native,a)});
        GameDataProvider.SetInstanceForTesting(data);var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
        var folder=Path.Combine(Path.GetTempPath(),"refund-route-"+Guid.NewGuid().ToString("N"));var host=new Host{ContentRootPath=folder};
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["AiStrategy:Enabled"]="true",["AiStrategy:Endpoint"]="https://example.invalid/chat/completions",["AiStrategy:Model"]="offline-fake",["AiStrategy:MemoryPath"]=folder,["AiStrategy:GuideCacheEnabled"]="false",["AiStrategy:ConditionalRefundLines"]=(kind!="legacy").ToString()}).Build();
        using var http=new Fake(kind=="local-threat");ServiceProvider services=null!;GameState game=null!;AiStrategyPlanner planner=null!;AiService ai=null!;CardInstance noble=null!,reiss=null!;ICardFactory factory=null!;GameService preflight=null!;
        var actions=new List<int>();var choices=new List<int>();var firstEvents=new List<string>();var firstDone=false;var remainingAfterFirst=Array.Empty<int>();var firstPp=-1;var firstBlood=-1;
        GameStateDto View(){using var scope=services.CreateScope();var sp=scope.ServiceProvider;using var c=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,game.Player1.PlayerId);return GameStateProjection.Create(game,c);}
        async Task<PlayabilityResultDto> Playable(Guid uid){
            var card=game.FindCardInstance(uid)!;using var scope=services.CreateScope();var sp=scope.ServiceProvider;
            using var context=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,game.Player1.PlayerId);
            var method=typeof(GameService).GetMethod("CheckPlayabilityCoreLogicAsync",Hidden)!;
            var result=await (Task<PlayabilityResultDto>)method.Invoke(preflight,[context with{SourceCard=card},game.Player1,card,defs[card.CurrentDefinitionId],PlayMode.Base])!;
            if(kind=="blocked-next"&&firstDone&&uid==reiss.InstanceId){result.IsPlayable=false;result.PlayModes.ForEach(p=>p.IsAvailable=false);}
            return result;
        }
        async Task<bool> Commit(PlayerActionWrapper action){
            var first=action.ActionData is PlayCardActionDto p&&p.CardUid==noble.InstanceId;var cardId=action.ActionData is PlayCardActionDto q?game.FindCardInstance(q.CardUid)?.CurrentDefinitionId:0;actions.Add(cardId??0);
            if(first&&kind=="blood-response")game.Player1.BloodPool=1;
            var ev=action.ActionData switch{PlayCardActionDto play=>(IGameEvent)new PlayCardRequestedEvent(action.PlayerId,play),AttackActionDto attack=>new AttackRequestedEvent(action.PlayerId,attack),EvolveActionDto evolve=>new EvolveRequestedEvent(action.PlayerId,evolve),SuperEvolveActionDto super=>new SuperEvolveRequestedEvent(action.PlayerId,super),_=>new EndTurnRequestedEvent(action.PlayerId)};
            using var scope=services.CreateScope();var sp=scope.ServiceProvider;using var cx=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,action.PlayerId);
            var result=await sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(cx,[ev]);Check(result.IsSuccess&&!result.IsCancelled,result.ErrorMessage??"native action cancelled");
            if(!first||kind!="missing-observation")planner.ObserveCommittedEvents(game,result.ProcessedEvents);
            if(first){
                firstEvents.AddRange(result.ProcessedEvents.Select(e=>e.GetType().Name));firstPp=game.Player1.CurrentPlayPoints;firstBlood=game.Player1.BloodPool;remainingAfterFirst=game.Player2.Field.Select(card=>card.CurrentDefinitionId).ToArray();
                if(kind=="replacement")factory.CreateAndRegisterNewCard(game,Kessa,game.Player2.PlayerId,CardZone.Field);
                if(kind=="discard-next"){
                    var r=await sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(cx,[new CardDiscardedEvent(game.Player1,reiss,EventReasonSource.Ability)]);Check(r.IsSuccess,"native follow-up discard failed");planner.ObserveCommittedEvents(game,r.ProcessedEvents);
                }
                firstDone=true;
            }
            action.MarkCompleted();return true;
        }
        var games=Proxy<IGameService>.Make((m,a)=>m.Name switch{"GetGameState"=>game,"GetCardPlayabilityAsync" or "GetCardPlayabilityForModeAsync"=>Playable((Guid)a![2]!),"EnqueuePlayerActionAsync"=>Commit((PlayerActionWrapper)a![1]!),_=>null});
        var pending=Proxy<IPendingActionService>.Make((m,a)=>{if(m.ReturnType==typeof(bool))return false;if(m.Name!="WaitForPlayerChoiceAsync")return null;var req=(PlayerChoiceRequest)a![1]!;var choice=ai.DecideTargetChoiceAsync(req,View()).GetAwaiter().GetResult();choices.AddRange(choice.ChosenTargetIds.Select(uid=>game.FindCardInstance(uid)?.CurrentDefinitionId??0));return Task.FromResult(choice);});
        using(services=AiEndTurnEngineTests.Services(data,accessor,pending,games,s=>{s.AddSingleton<IConfiguration>(config);s.AddSingleton<IHostEnvironment>(host);s.AddSingleton<IHttpClientFactory>(http);s.AddSingleton(model);s.AddSingleton<AiStrategyPlanner>();})){
            using var scope=services.CreateScope();var sp=scope.ServiceProvider;factory=sp.GetRequiredService<ICardFactory>();planner=services.GetRequiredService<AiStrategyPlanner>();
            preflight=new GameService(Proxy<Microsoft.AspNetCore.SignalR.IHubContext<ShadowverseBeyond.Server.Hubs.GameHub>>.Make((m,a)=>null),NullLogger<GameService>.Instance,sp.GetRequiredService<IMapper>(),Proxy<IClientMethodMapper>.Make((m,a)=>null),data,Proxy<IMatchService>.Make((m,a)=>null),Proxy<IFirstPlayerSelector>.Make((m,a)=>null),services.GetRequiredService<IServiceScopeFactory>(),pending,Proxy<IGameStatsRuntimeTracker>.Make((m,a)=>null));
            var own=new PlayerState(Guid.NewGuid()){IsAI=true,TurnNumber=5,CurrentPlayPoints=5,MaxPlayPoints=5,BloodPool=2,InitialDeckDefinitionIds=[Noble,Reiss,Tutor]};var enemy=new PlayerState(Guid.NewGuid()){TurnNumber=4};
            game=new("refund-route-"+kind,own,enemy,own.PlayerId){IsLocalAiTraining=true,Status=GameStatus.InProgress,Phase=GamePhase.Main,AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<IBuffManager>()};
            typeof(GameState).GetProperty("StrategyBenchmarkPlayerId",Hidden)!.SetValue(game,own.PlayerId);
            own.Leader=factory.CreateAndRegisterNewCard(game,Leader,own.PlayerId,CardZone.Field);enemy.Leader=factory.CreateAndRegisterNewCard(game,Leader,enemy.PlayerId,CardZone.Field);
            noble=factory.CreateAndRegisterNewCard(game,Noble,own.PlayerId,CardZone.Hand);reiss=factory.CreateAndRegisterNewCard(game,Reiss,own.PlayerId,CardZone.Hand);factory.CreateAndRegisterNewCard(game,Tutor,own.PlayerId,CardZone.Hand);if(kind=="local-threat"){
                factory.CreateAndRegisterNewCard(game,Weaver,enemy.PlayerId,CardZone.Field);
                factory.CreateAndRegisterNewCard(game,Emperor,enemy.PlayerId,CardZone.Field);
                own.Leader.Damage=8;
            }else factory.CreateAndRegisterNewCard(game,Kessa,enemy.PlayerId,CardZone.Field);
            for(var i=0;i<4;i++){factory.CreateAndRegisterNewCard(game,Junk,own.PlayerId,CardZone.Deck);factory.CreateAndRegisterNewCard(game,Junk,enemy.PlayerId,CardZone.Deck);}
            ai=new(NullLogger<AiService>.Instance,services.GetRequiredService<IServiceScopeFactory>(),model,config);Check(GameServiceProvider.CurrentGameContext==null,"ambient native context leaked into executor");planner.WarmDeckAsync(game,own.PlayerId,"Neutral")!.GetAwaiter().GetResult();
            var executor=new AiTurnExecutor(games,ai,NullLogger<AiTurnExecutor>.Instance,sp.GetRequiredService<IMapper>(),services,model,config);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(45));executor.ExecuteTurnAsync(game.GameId,own.PlayerId,timeout.Token).GetAwaiter().GetResult();
            object?[] args=[game,null];var dictionary=typeof(AiStrategyPlanner).GetField("matches",Hidden)!.GetValue(planner)!;Check((bool)dictionary.GetType().GetMethod("TryGetValue")!.Invoke(dictionary,args)!,"planner state absent");var state=args[1]!;
            var follows=JsonSerializer.SerializeToElement(state.GetType().GetProperty("PlanFollows")!.GetValue(state),Json);var validations=JsonSerializer.SerializeToElement(state.GetType().GetProperty("PlanValidations")!.GetValue(state),Json);
            Check(actions.First()==Noble,"model first step was not native Noble");
            if(kind=="nominal"){
                Check(follows.GetArrayLength()>0&&follows[0].GetProperty("status").GetString()=="revalidated","actual refund did not resume route: "+follows+"; events="+string.Join(',',firstEvents));
                Check(actions.Take(2).SequenceEqual(new[]{Noble,Reiss})&&choices.Contains(Tutor)&&own.Emblem.Any(c=>c.BaseDefinitionId==1474276),"revalidated Reiss/discard/emblem route did not execute");
                var completed=((IEnumerable<AiCompletedActionFact>)state.GetType().GetProperty("CompletedActions")!.GetValue(state)!).ToArray();
                var paid=completed.Single(f=>f.Action.CardId==Noble);var engine=completed.Single(f=>f.Action.CardId==Reiss);
                Check(paid.Before.BloodPool==2&&paid.After.BloodPool==1&&engine.Before.OwnEmblemIds is {Length:0}&&engine.After.OwnEmblemIds!.Contains(1474276),"native Noble/Reiss resource context absent from completed history");
                var proposed=JsonSerializer.SerializeToElement(state.GetType().GetProperty("ProposedPlays")!.GetValue(state),Json);
                Check(proposed[0].GetProperty("conditionalBudgetSteps").GetArrayLength()==1&&follows[follows.GetArrayLength()-1].GetProperty("status").GetString()=="finished","conditional intent/finished line not recorded accurately");
                Check(firstPp==3&&firstBlood==1&&http.Inputs.Count==2&&validations.GetArrayLength()==0,"refund route invented resource or needed additional model call");
            }else if(kind=="local-threat"){
                Check(choices.FirstOrDefault()==Emperor&&remainingAfterFirst.SequenceEqual(new[]{Weaver}),
                    "local destroy target did not remove the largest surviving attack threat: "+string.Join(',',choices));
                Check(firstPp==3&&firstBlood==1,"target ordering changed native sacrifice/refund");
            }else if(kind=="legacy")Check(validations.GetArrayLength()==1&&http.Inputs.Count==3,"legacy PP-only validator failed to reproduce rejection/repair");
            else Check(follows.GetArrayLength()>0&&follows[0].GetProperty("status").GetString()=="interrupted","response/missing evidence/blocked instance still kept stale route: "+kind+" "+follows);
            Check(http.ContextLeaks==0,"native context retained during provider request");
            Check(http.SharedPrefixes.Distinct().Count()==1,"guide/turn/repair changed actual fixed native rule prefix");
            return new{kind,actions,choices,remainingAfterFirst,firstPp,firstBlood,follows,identicalSharedMessagePrefix=true,sharedMessageContentUtf8Bytes=http.SharedContentBytes[0],validationCount=validations.GetArrayLength(),fakeRequests=http.Inputs.Count,firstEvents,passed=true};
        }
    }
    sealed class Fake(bool chooseLocally):HttpMessageHandler,IHttpClientFactory
    {
        internal readonly List<JsonElement> Inputs=[];internal readonly List<string> SharedPrefixes=[];internal readonly List<int> SharedContentBytes=[];internal int ContextLeaks;public HttpClient CreateClient(string name)=>new(this,false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){if(GameServiceProvider.CurrentGameContext!=null)ContextLeaks++;using var doc=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));var shared=doc.RootElement.GetProperty("messages").EnumerateArray().Take(2).ToArray();SharedPrefixes.Add(JsonSerializer.Serialize(shared));SharedContentBytes.Add(shared.Sum(m=>System.Text.Encoding.UTF8.GetByteCount(m.GetProperty("content").GetString()!)));var fields=new Dictionary<string,JsonElement>();foreach(var m in doc.RootElement.GetProperty("messages").EnumerateArray().Where(m=>m.GetProperty("role").GetString()=="user")){using var p=JsonDocument.Parse(m.GetProperty("content").GetString()!);foreach(var f in p.RootElement.EnumerateObject())fields[f.Name]=f.Value.Clone();}var input=JsonSerializer.SerializeToElement(fields,Json);Inputs.Add(input);object answer;
            if(input.TryGetProperty("legalPlays",out var plays)){
                var n=plays.EnumerateArray().Single(x=>x.GetProperty("cardId").GetInt32()==Noble);var r=plays.EnumerateArray().Single(x=>x.GetProperty("cardId").GetInt32()==Reiss);var nk=n.GetProperty("targetKeys")[0];var rk=r.GetProperty("targetKeys")[0];
                var first=new AiStrategyPlanner.PlayAdvice(0,Noble,"Base",4,"use",new(nk.GetProperty("key").GetString()!,"enemy","field",0,Kessa,nk.GetProperty("abilityDefinitionId").GetInt64()));
                var second=new AiStrategyPlanner.PlayAdvice(1,Reiss,"Base",2,"use",new(rk.GetProperty("key").GetString()!,"own","hand",2,Tutor,rk.GetProperty("abilityDefinitionId").GetInt64()));
                if(chooseLocally) {
                    first=first with {Target=null};
                    answer=new AiStrategyPlanner.TurnPlan("setup","native Noble, local target",[],[],Play:first,Plays:[first]);
                }else answer=new AiStrategyPlanner.TurnPlan("setup","native Noble refund then Reiss discards Tutor",[],[],Play:first,Plays:input.TryGetProperty("correction",out _)?[first]:[first,second]);
            }else answer=new AiStrategyPlanner.DeckGuide("offline refund route",input.GetProperty("deck").EnumerateArray().Select(c=>new AiStrategyPlanner.CardRole(c.GetProperty("id").GetInt32(),"fixture")).ToArray());
            return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=JsonSerializer.Serialize(answer,Json)}}}}))};
        }
    }
    sealed class Host:IWebHostEnvironment{public string ApplicationName{get;set;}="refund continuation test";public string EnvironmentName{get;set;}="Test";public string WebRootPath{get;set;}=Path.GetTempPath();public string ContentRootPath{get;set;}=Path.GetTempPath();public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
}
