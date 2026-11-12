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

internal static class AiPlannedTargetTests
{
    const int Leader=2000001540,Small=2000001541,Junk=2000001542,Reactor=2000001543;
    static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    static readonly Type Targets=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiPlannedTargets")!;
    static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static object? Invoke(string name,params object?[] args)=>Targets.GetMethod(name,Hidden)!.Invoke(null,args);
    static object? Planner(AiStrategyPlanner p,string name,params object?[] args)=>typeof(AiStrategyPlanner).GetMethod(name,Hidden)!.Invoke(p,args);
    internal static void Boundaries()
    {
        var own=new PlayerStateDto{PlayerId=Guid.NewGuid()};var enemy=new PlayerStateDto{PlayerId=Guid.NewGuid()};
        var uid=Guid.NewGuid();own.Hand.Add(new(){InstanceId=uid,OwnerPlayerId=own.PlayerId,CardId=12,Zone=CardZone.Hand});
        var advice=new AiStrategyPlanner.TargetAdvice("target1","own","hand",0,12);
        Check(Invoke("Resolve",advice,own,enemy)!=null,"own hand could not bind");
        foreach(var bad in new[]{advice with{Side="enemy"},advice with{Index=1},advice with{Index=-1},advice with{CardId=13},advice with{Key="x.bad"},advice with{Zone="deck"},advice with{Side="unknown"}})
            Check(Invoke("Resolve",bad,own,enemy)==null,"invalid/hidden target bound");
        var shifted=own with{Hand=[new(){InstanceId=Guid.NewGuid(),OwnerPlayerId=own.PlayerId,CardId=20},own.Hand[0]]};
        Check(((AiStrategyPlanner.TargetAdvice)Invoke("Rebase",advice,own,enemy,shifted,enemy)!).Index==1,"target did not follow instance after hand shift");
        shifted.Hand.RemoveAt(1);Check(Invoke("Rebase",advice,own,enemy,shifted,enemy)==null,"missing instance rebound to other card");
        own.Hand[0].OwnerPlayerId=enemy.PlayerId;Check(Invoke("Resolve",advice,own,enemy)==null,"forged owner accepted");
    }
    internal static void Run(string db,string output,string weights,bool materialOnly=false)
    {
        Boundaries();var previous=GameDataProvider.Instance;var oldAccessor=GameServiceProvider.ContextAccessor;
        using var catalog=new ServiceCollection().AddLogging(l=>l.ClearProviders()).AddDbContext<ApplicationDbContext>(o=>o.UseSqlite(new SqliteConnectionStringBuilder{DataSource=db,Mode=SqliteOpenMode.ReadOnly}.ToString()))
            .AddScoped<ICardRepository,EFCardRepository>().AddScoped<AbilityLoader>().BuildServiceProvider();
        try{
            var native=new GameDataProvider(catalog.GetRequiredService<IServiceScopeFactory>(),NullLogger<GameDataProvider>.Instance);native.InitializeAsync().GetAwaiter().GetResult();
            var rows=new List<object>();foreach(var kind in new[]{"none","small","invalid","hidden","face","lethal","tutor-discard","reiss-discard","tutor-line","reiss-line","reiss-material-line","reiss-material-legacy-line","reiss-material-duplicate-line","reiss-empty-line","reiss-empty-legacy-line","reiss-empty-order-line","reiss-empty-reactive-line"}.Where(k=>!materialOnly||k.Contains("material")||k.Contains("empty")))rows.Add(Scene(native,kind,weights));
            File.WriteAllText(output,JsonSerializer.Serialize(new{realProviderRequests=0,cases=rows,scope="Scripted HTTP answers exercise production planning, current native single-target requests, model target validation, source root binding and submitted facts. Native card effects/legality unchanged. This is wiring validation, not evidence of real LLM quality."},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("PASS planned target native controls: "+rows.Count+"; real provider requests=0");
        }finally{GameDataProvider.SetInstanceForTesting(previous);GameServiceProvider.ContextAccessor=oldAccessor;}
    }
    static object Scene(IGameDataProvider native,string kind,string weights)
    {
        var empty=kind.Contains("empty");var material=kind.Contains("material");var line=kind.EndsWith("line");var discard=kind.EndsWith("discard")||line;var sourceId=kind.StartsWith("tutor-")?1474113:kind.StartsWith("reiss-")?1474120:1472979;
        var defs=native.GetAllCardDefinitions().ToDictionary(p=>p.Key,p=>p.Value);
        defs[Leader]=new(){Id=Leader,Type=CardType.Leader,Health=20};defs[Small]=new(){Id=Small,Type=CardType.Follower,Attack=1,Health=1};defs[Junk]=new(){Id=Junk,Name="inert draw",Type=CardType.Spell,Cost=20};
        defs[Reactor]=new(){Id=Reactor,Type=CardType.Amulet,Cost=0};
        var reactive=new AbilityConfig{Id=900003,CardId=Reactor,Name="offline public draw reaction",Type=ShadowverseBeyond.Server.Domain.Abilities.Common.AbilityType.Triggered,
            Trigger=new(){IsEnabled=true,EventTypes=[EventType.PlayCard],RelationType=TriggerRelationType.Any,Timing=ShadowverseBeyond.Server.Domain.Abilities.Common.TriggerTiming.On,ValidZones=[CardZone.Field]},
            Effects=[new(){Type=EffectType.Draw,Parameters=new(){["DrawAmount"]=new ShadowverseBeyond.Server.Domain.Abilities.Common.StaticValueConfig{Value=1},["PlayerScope"]=new ShadowverseBeyond.Server.Domain.Abilities.Common.StaticValueConfig{Value="self"}}}]};
        var data=Proxy<IGameDataProvider>.Make((m,a)=>m.Name switch{"GetCardDefinition"=>defs.GetValueOrDefault((int)a![0]!),"GetAllCardDefinitions"=>defs,"GetAbilityConfigs"=>(int)a![0]! == Reactor?new List<AbilityConfig>{reactive}:(int)a[0]!>=Leader?new List<AbilityConfig>():native.GetAbilityConfigs((int)a[0]!),_=>m.Invoke(native,a)});
        GameDataProvider.SetInstanceForTesting(data);var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
        var folder=Path.Combine(Path.GetTempPath(),"planned-target-"+Guid.NewGuid().ToString("N"));
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["AiStrategy:Enabled"]="true",["AiStrategy:Endpoint"]="https://example.invalid/chat/completions",["AiStrategy:Model"]="offline-fake",["AiStrategy:GuideCacheEnabled"]="false",["AiStrategy:ProtectPlannedMaterials"]=kind.Contains("legacy")?"false":"true",["AiStrategy:KnownHandMaterialValidation"]=kind.Contains("empty-legacy")?"false":"true",["AiStrategy:MemoryPath"]=folder,["AiModel:Path"]=Path.GetFullPath(weights)}).Build();
        ServiceProvider services=null!;var model=new AiLearningModel(new Host(),config,NullLogger<AiLearningModel>.Instance);using var http=new Fake(kind);
        var ruleScopes=line?Proxy<IServiceScopeFactory>.Make((m,a)=>services.CreateScope()):null;
        using var planner=new AiStrategyPlanner(http,config,new Host(),NullLogger<AiStrategyPlanner>.Instance,model,ruleScopes:ruleScopes);
        GameState game=null!;AiService ai=null!;var selected=new List<int>();var observations=new List<object>();var materialBoundariesChecked=false;var followUpPlayable=false;var followUpResolved=false;
        GameStateDto View(){using var s=services.CreateScope();using var c=new GameContext(s.ServiceProvider,s.ServiceProvider.GetRequiredService<IMapper>(),accessor,game,game.Player1.PlayerId);return GameStateProjection.Create(game,c);}
        var pending=Proxy<IPendingActionService>.Make((m,a)=>{if(m.ReturnType==typeof(bool))return false;if(m.Name!="WaitForPlayerChoiceAsync")return null;var request=(PlayerChoiceRequest)a![1]!;
            // Other mode/search choices are outside the single direct target advice and are deterministic in this fixture.
            if(request.TargetKey=="decision"||request.MinSelectionCount!=1||request.MaxSelectionCount!=1)return Task.FromResult(new PlayerChoiceResult{ChosenTargetIds=request.SelectableTargetIds.Take(request.MinSelectionCount).ToList()});
            if(kind=="reiss-material-line"&&!materialBoundariesChecked){MaterialBoundaries(planner,game,View(),request);materialBoundariesChecked=true;}
            var choice=ai.DecideTargetChoiceAsync(request,View()).GetAwaiter().GetResult();selected.AddRange(choice.ChosenTargetIds.Select(id=>game.FindCardInstance(id)?.CurrentDefinitionId??0));return Task.FromResult(choice);});
        var games=Proxy<IGameService>.Make((m,a)=>m.Name=="GetGameState"?game:null);
        using(services=AiEndTurnEngineTests.Services(data,accessor,pending,games,s=>{s.AddSingleton<IConfiguration>(config);s.AddSingleton(planner);})){using var scope=services.CreateScope();var sp=scope.ServiceProvider;var f=sp.GetRequiredService<ICardFactory>();
            var own=new PlayerState(Guid.NewGuid()){IsAI=true,TurnNumber=3,CurrentPlayPoints=3,MaxPlayPoints=3,InitialDeckDefinitionIds=discard?[sourceId,1473444,1474113,1474120]:[1472979]};var enemy=new PlayerState(Guid.NewGuid()){TurnNumber=3};
            game=new("planned-target-"+kind,own,enemy,own.PlayerId){Status=GameStatus.InProgress,Phase=GamePhase.Main,AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<IBuffManager>()};
            own.Leader=f.CreateAndRegisterNewCard(game,Leader,own.PlayerId,CardZone.Field);enemy.Leader=f.CreateAndRegisterNewCard(game,Leader,enemy.PlayerId,CardZone.Field);if(kind=="lethal")enemy.Leader.Damage=19;
            var pegasus=f.CreateAndRegisterNewCard(game,1471971,enemy.PlayerId,CardZone.Field);var small=f.CreateAndRegisterNewCard(game,Small,enemy.PlayerId,CardZone.Field);
            if(kind.Contains("reactive"))f.CreateAndRegisterNewCard(game,Reactor,own.PlayerId,CardZone.Field);
            var source=f.CreateAndRegisterNewCard(game,sourceId,own.PlayerId,CardZone.Hand);
            if(discard){f.CreateAndRegisterNewCard(game,kind.Contains("duplicate")?1474113:1473444,own.PlayerId,CardZone.Hand);f.CreateAndRegisterNewCard(game,sourceId==1474120?1474113:1474120,own.PlayerId,CardZone.Hand);}
            // Tutor also needs a discard material: retain a real fourth card instead of asserting an impossible last-card follow-up.
            if(material||kind=="reiss-line")f.CreateAndRegisterNewCard(game,1050403,own.PlayerId,CardZone.Hand);
            for(var i=0;i<5;i++)f.CreateAndRegisterNewCard(game,Junk,own.PlayerId,CardZone.Deck);
            ai=new(NullLogger<AiService>.Instance,services.GetRequiredService<IServiceScopeFactory>(),model,config);
            bool beforeCandidate(Guid uid)=>own.Hand.FindIndex(c=>c.InstanceId==uid)==2;
            Task<PlayabilityResultDto> Legal(Guid uid,CancellationToken token)=>Task.FromResult(new PlayabilityResultDto{IsPlayable=uid==source.InstanceId||line&&beforeCandidate(uid),SelectedPlayMode=PlayMode.Base,PlayModes=[new(){Mode=PlayMode.Base,EffectiveCost=game.FindCardInstance(uid)!.CurrentCost,IsAvailable=true}]});
            using var planningContext=line?null:new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId);var before=View();
            planner.WarmDeckAsync(game,own.PlayerId,"Neutral")!.GetAwaiter().GetResult();var plan=planner.PlanAsync(game,before,own.PlayerId,CancellationToken.None,Legal).GetAwaiter().GetResult();
            Check(plan?.Play!=null&&(kind=="none"||material||plan.Play.Target!=null),"model target not parsed into accepted first play: "+kind);
            Check(http.Inputs.Last().GetProperty("legalPlays")[0].GetProperty("targetKeys").GetArrayLength()>0,"native direct choice keys missing from prompt");
            if(line)Check(GameServiceProvider.CurrentGameContext==null,"native planning context retained after model request");
            if(kind.Contains("empty-order")){source=game.FindCardInstance(before.Players.Single(p=>p.PlayerId==own.PlayerId).Hand[plan!.Play!.HandIndex].InstanceId)!;sourceId=source.CurrentDefinitionId;}
            using var context=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId);
            var action=new PlayCardActionDto{CardUid=source.InstanceId,PlayMode=PlayMode.Base,PaidCost=source.CurrentCost};var root=new PlayerActionWrapper("PlayCard",own.PlayerId,action);
            Planner(planner,"BeginActionOutcome",game,root,before,null);
            if(kind.Contains("ended"))root.MarkCompleted();
            var result=sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(context,[new PlayCardRequestedEvent(own.PlayerId,action)]).GetAwaiter().GetResult();Check(result.IsSuccess&&!result.IsCancelled,result.ErrorMessage??"native play cancelled");
            planner.ObserveCommittedEvents(game,result.ProcessedEvents);
            root.MarkCompleted();Planner(planner,"EndActionOutcome",game,root);
            if(discard){var expected=material&&kind!="reiss-material-line"?1474113:line?1473444:sourceId==1474120?1474113:1474120;Check(material||selected.Contains(expected)&&own.Hand.All(c=>c.CurrentDefinitionId!=expected),"explicit hand discard not committed: "+kind);}
            else if(kind=="small")Check(small.CurrentZone==CardZone.Graveyard&&pegasus.CurrentZone==CardZone.Field&&enemy.Leader.CurrentHealth==20,"explicit 1/1 advice not used");
            else if(kind is "face" or "lethal")Check(enemy.Leader.CurrentHealth==(kind=="lethal"?0:19),"face advice or local lethal priority failed");
            else Check(pegasus.CurrentZone==CardZone.Graveyard&&enemy.Leader.CurrentHealth==20,"missing/invalid advice did not use local 2/1 scoring");
            var field=typeof(AiStrategyPlanner).GetField("matches",Hidden)!.GetValue(planner)!;object?[] arguments=[game,null];Check((bool)field.GetType().GetMethod("TryGetValue")!.Invoke(field,arguments)!,"missing match history");var state=arguments[1]!;
            var facts=JsonSerializer.SerializeToElement(state.GetType().GetProperty("TargetChoices")!.GetValue(state),Json);Check(facts.GetArrayLength()>0,"submitted choice not recorded");var first=facts[0];Check(first.GetProperty("sourceCardId").GetInt32()==sourceId,"own played source missing from submitted choice history");
            var expectedStatus=material?"none":kind is "none"?"none":kind is "invalid" or "hidden"?"rejected":kind=="lethal"?"blocked":"selected";Check(first.GetProperty("adviceStatus").GetString()==expectedStatus,"incorrect advice status: "+first);
            Check(state.GetType().GetField("PendingTarget",Hidden)!.GetValue(state)==null,"target lease retained after root completion");
            var text=facts.GetRawText();Check(!text.Contains(own.PlayerId.ToString())&&!text.Contains(enemy.PlayerId.ToString())&&!text.Contains(source.InstanceId.ToString()),"private identities persisted");
            var proposed=(List<AiStrategyPlanner.PlayAdviceFact>)state.GetType().GetProperty("ProposedPlays")!.GetValue(state)!;
            if(line)Check(proposed.Last().HandMaterials!=null,"native material validation scope missing from persisted proposal facts");
            var completed=(List<AiCompletedActionFact>)state.GetType().GetProperty("CompletedActions")!.GetValue(state)!;
            Check(completed.Count==1&&completed[0].ChoiceTargets?.Requests.Length>0,"native completed spell target outcomes missing");
            var target=completed[0].ChoiceTargets!.Requests[0].Targets.Single();
            if(kind is "face" or "lethal")Check(target.Before.Health==(kind=="lethal"?1:20)&&target.After?.Health==(kind=="lethal"?0:19)&&target.Observation=="visible","native leader target health not observed");
            else Check(target.After==null&&target.Observation=="not-visible","removed/discarded card falsely retained or cause invented");
            if(kind=="lethal"){var file=Directory.GetFiles(folder,"*.json").Single();var lesson=JsonSerializer.Deserialize<AiStrategyPlanner.Lesson[]>(File.ReadAllText(file),Json)!.Single();Check(lesson.CompletedActions!.Single().ChoiceTargets!.Requests[0].Targets[0].After?.Health==0,"lethal selected endpoint not saved");}
            if(line&&!material&&!empty){
                var validations=JsonSerializer.SerializeToElement(state.GetType().GetProperty("PlanValidations")!.GetValue(state),Json);
                Check(validations.GetArrayLength()==1&&validations[0].GetProperty("repaired").GetBoolean(),"contradictory selected discard line not repaired");
                Check(plan!.Plays is {Length:2}&&plan.Plays[1].HandIndex==2&&own.Hand.Any(c=>c.InstanceId==before.Players.Single(p=>p.PlayerId==own.PlayerId).Hand[2].InstanceId),"repair dropped the intended follow-up or discarded its instance");
                Check(http.Inputs[1].GetProperty("nativePlanningContext").GetString()=="scoped-current-native-rules"&&http.Inputs[1].GetProperty("selectedHandLossRules").GetArrayLength()>0&&http.Inputs[1].GetProperty("legalPlays")[0].GetProperty("targetKeys")[0].GetProperty("selectedHandEffect").GetString()=="discard","native selected discard semantics missing from model input");
            }
            Check(http.Inputs.Count==(empty?(kind.Contains("legacy")||kind.Contains("reactive")?2:3):line&&!material?3:2),"unexpected target/repair model request count");
            if(kind=="small"){
                own.TurnNumber+=2;
                planner.PlanAsync(game,View(),own.PlayerId,CancellationToken.None,Legal).GetAwaiter().GetResult();
                var input=http.Inputs.Last();var recorded=input.GetProperty("recentCompletedActions").GetProperty("samples")[0].GetProperty("choiceTargets").GetProperty("requests")[0].GetProperty("targets")[0];
                Check(recorded.GetProperty("before").GetProperty("cardId").GetInt32()==Small&&recorded.GetProperty("observation").GetString()=="not-visible","next real planning payload lost selected target outcome");
            }
            if(material){
                Check(plan!.LineConstraintsChecked&&plan.Plays is {Length:2},"material fixture line not checked");
                var kept=own.Hand.Any(c=>c.InstanceId==before.Players.Single(p=>p.PlayerId==own.PlayerId).Hand[2].InstanceId);
                Check(kept==!kind.Contains("legacy"),"future instance reservation did not change native discard choice: "+kind);
                if(!kind.Contains("legacy")){
                    var materialLease=state.GetType().GetField("PendingMaterials",Hidden)!.GetValue(state);
                    Check(materialLease==null,"material lease retained after completed root");
                    var follow=own.Hand.Single(c=>c.InstanceId==before.Players.Single(p=>p.PlayerId==own.PlayerId).Hand[2].InstanceId);
                    var preflight=new GameService(Proxy<Microsoft.AspNetCore.SignalR.IHubContext<ShadowverseBeyond.Server.Hubs.GameHub>>.Make((m,a)=>null),NullLogger<GameService>.Instance,sp.GetRequiredService<IMapper>(),Proxy<IClientMethodMapper>.Make((m,a)=>null),data,Proxy<IMatchService>.Make((m,a)=>null),Proxy<IFirstPlayerSelector>.Make((m,a)=>null),services.GetRequiredService<IServiceScopeFactory>(),pending,Proxy<IGameStatsRuntimeTracker>.Make((m,a)=>null));
                    var preflightMethod=typeof(GameService).GetMethod("CheckPlayabilityCoreLogicAsync",Hidden)!;
                    var legalFollow=((Task<PlayabilityResultDto>)preflightMethod.Invoke(preflight,[context with{SourceCard=follow},own,follow,defs[follow.CurrentDefinitionId],PlayMode.Base])!).GetAwaiter().GetResult();
                    followUpPlayable=legalFollow.IsPlayable;Check(followUpPlayable,"reserved follow-up failed actual native playability: "+JsonSerializer.Serialize(legalFollow,Json));
                    var followAction=new PlayCardActionDto{CardUid=follow.InstanceId,PlayMode=PlayMode.Base,PaidCost=follow.CurrentCost};
                    var followResult=sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(context,[new PlayCardRequestedEvent(own.PlayerId,followAction)]).GetAwaiter().GetResult();
                    followUpResolved=followResult.IsSuccess&&!followResult.IsCancelled&&follow.CurrentZone!=CardZone.Hand;
                    Check(followUpResolved,"preserved follow-up did not resolve native transaction");

                }
            }
            if(empty){
                var finalSteps=plan!.Plays!.Length;var corrected=http.Inputs.Any(x=>x.TryGetProperty("correction",out _));
                Check(finalSteps==(kind.Contains("legacy")||kind.Contains("reactive")||kind.Contains("empty-order")?2:1)&&corrected==!(kind.Contains("legacy")||kind.Contains("reactive")),"empty-material line did not distinguish bounded correction from legacy");
                var rulesInput=http.Inputs[1].GetProperty("knownHandMaterialRules");
                if(!kind.Contains("legacy"))Check(rulesInput.EnumerateArray().Any(x=>x.GetProperty("cardId").GetInt32()==1474120&&x.GetProperty("closedVisibleHandAfterPlay").GetBoolean()==!kind.Contains("reactive"))
                    &&rulesInput.EnumerateArray().Any(x=>x.GetProperty("cardId").GetInt32()==1474113&&x.GetProperty("blocksPlayWithoutMaterial").GetBoolean()),"native closed prefix/required material facts missing");
                var tutor=own.Hand.Single(c=>c.CurrentDefinitionId==(kind.Contains("empty-order")?1474120:1474113));
                var preflight=new GameService(Proxy<Microsoft.AspNetCore.SignalR.IHubContext<ShadowverseBeyond.Server.Hubs.GameHub>>.Make((m,a)=>null),NullLogger<GameService>.Instance,sp.GetRequiredService<IMapper>(),Proxy<IClientMethodMapper>.Make((m,a)=>null),data,Proxy<IMatchService>.Make((m,a)=>null),Proxy<IFirstPlayerSelector>.Make((m,a)=>null),services.GetRequiredService<IServiceScopeFactory>(),pending,Proxy<IGameStatsRuntimeTracker>.Make((m,a)=>null));
                var legal=((Task<PlayabilityResultDto>)typeof(GameService).GetMethod("CheckPlayabilityCoreLogicAsync",Hidden)!.Invoke(preflight,[context with{SourceCard=tutor},own,tutor,defs[tutor.CurrentDefinitionId],PlayMode.Base])!).GetAwaiter().GetResult();
                if(kind.Contains("empty-order")){
                    Check(legal.IsPlayable,"reordered final Reiss did not pass actual native empty-hand optional-target check");
                    var lastAction=new PlayCardActionDto{CardUid=tutor.InstanceId,PlayMode=PlayMode.Base,PaidCost=tutor.CurrentCost};
                    var last=sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(context,[new PlayCardRequestedEvent(own.PlayerId,lastAction)]).GetAwaiter().GetResult();
                    followUpPlayable=legal.IsPlayable;followUpResolved=last.IsSuccess&&!last.IsCancelled;
                    Check(followUpResolved&&own.Hand.Count==0&&own.Emblem.Any(c=>c.CurrentDefinitionId==1474276)&&own.CurrentPlayPoints==0,"reordered Tutor to Reiss did not empty hand and establish emblem through actual native actions");
                }else if(kind.Contains("reactive")){
                    Check(legal.IsPlayable,"visible reaction hand supply was treated as zero");
                    var extra=own.Hand.First(c=>c.CurrentDefinitionId==Junk);Check(extra.CurrentZone==CardZone.Hand,"native visible reaction failed to draw actual material");
                    var nextAction=new PlayCardActionDto{CardUid=tutor.InstanceId,PlayMode=PlayMode.Base,PaidCost=tutor.CurrentCost};
                    var next=sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(context,[new PlayCardRequestedEvent(own.PlayerId,nextAction)]).GetAwaiter().GetResult();
                    followUpPlayable=true;followUpResolved=next.IsSuccess&&!next.IsCancelled;
                    Check(followUpResolved,"actual supplied hand material did not permit native follow-up");
                }else Check(!legal.IsPlayable&&legal.FailureReason!.Contains("目标"),"native source no-material control did not block Tutor");
                if(corrected)Check(http.Inputs.Last().GetProperty("correction").GetProperty("reason").GetString()!.Contains("素材"),"material failure not sent to the sole correction request");
            }
            return new{kind,selected,acceptedLineSteps=plan?.Plays?.Length,followUpPlayable,followUpResolved,remainingEnemyHp=enemy.Leader.CurrentHealth,adviceStatus=expectedStatus,fakeRequests=http.Inputs.Count,passed=true};
        }
    }
    static void MaterialBoundaries(AiStrategyPlanner planner,GameState game,GameStateDto snapshot,PlayerChoiceRequest request)
    {
        var matches=typeof(AiStrategyPlanner).GetField("matches",Hidden)!.GetValue(planner)!;
        object?[] lookup=[game,null];Check((bool)matches.GetType().GetMethod("TryGetValue")!.Invoke(matches,lookup)!,"material match missing");
        var lease=lookup[1]!.GetType().GetField("PendingMaterials",Hidden)!.GetValue(lookup[1])!;
        Check(lease!=null,"material reservation was not bound at native root start");
        var source=(Ability)game.AbilityManager.GetAbilityByRuntimeId(request.SourceAbilityRuntimeId)!;
        var type=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiPlannedMaterials")!;
        HashSet<Guid> Read(GameStateDto? state=null,PlayerChoiceRequest? choice=null,object? bound=null)=>(HashSet<Guid>)type.GetMethod("Read",Hidden)!.Invoke(null,[game,state??snapshot,choice??request,source,bound??lease])!;
        var reserved=Read();Check(reserved.Count==1,"native future instance missing from reservation");var id=reserved.Single();
        var own=snapshot.Players.Single(p=>p.PlayerId==request.RequestingPlayerId);
        GameStateDto Hand(Func<CardInstanceDto,CardInstanceDto> change)=>snapshot with{Players=snapshot.Players.Select(p=>p.PlayerId==own.PlayerId?p with{Hand=p.Hand.Select(change).ToList()}:p).ToList()};
        Check(Read(Hand(c=>c.InstanceId==id?c with{InstanceId=Guid.NewGuid()}:c)).Count==0,"same-name replacement inherited reservation");
        Check(Read(Hand(c=>c.InstanceId==id?c with{CurrentCost=c.CurrentCost+1}:c)).Count==0,"changed future cost retained reservation");
        Check(Read(Hand(c=>c.InstanceId==id?c with{Zone=CardZone.Field}:c)).Count==0,"wrong-zone registry retained reservation");
        Check(Read(Hand(c=>c.InstanceId==id?c with{OwnerPlayerId=Guid.NewGuid()}:c)).Count==0,"foreign hand registry retained reservation");
        var clone=new PlayerChoiceRequest{GameId=request.GameId,PendingChoiceId=request.PendingChoiceId,TargetKey=request.TargetKey,
            SourceAbilityRuntimeId=request.SourceAbilityRuntimeId,RequestingPlayerId=request.RequestingPlayerId,MinSelectionCount=1,MaxSelectionCount=1,SelectableTargetIds=[id]};
        Check(Read(choice:clone).SetEquals(new[]{id}),"all-reserved native pool blocked instead of remaining selectable");
        clone.TargetKey="other";Check(Read(choice:clone).Count==0,"other target key inherited reservation");clone.TargetKey=request.TargetKey;
        clone.SourceAbilityRuntimeId=Guid.NewGuid();Check(Read(choice:clone).Count==0,"other runtime ability inherited reservation");clone.SourceAbilityRuntimeId=request.SourceAbilityRuntimeId;
        clone.MaxSelectionCount=2;Check(Read(choice:clone).Count==0,"multi-target request inferred as one material");
        var copy=lease.GetType().GetMethod("<Clone>$")!.Invoke(lease,null)!;
        var root=new PlayerActionWrapper("PlayCard",own.PlayerId,new PlayCardActionDto());root.MarkCompleted();
        copy.GetType().GetProperty("Root")!.SetValue(copy,root);Check(Read(bound:copy).Count==0,"completed root kept reservation");
        var changed=snapshot with{Players=snapshot.Players.Select(p=>p.PlayerId==own.PlayerId?p with{TurnNumber=p.TurnNumber+1}:p).ToList()};
        Check(Read(changed).Count==0,"another turn inherited reservation");
        var ownHand=game.Player1.Hand;var future=own.Hand.First(c=>reserved.Contains(c.InstanceId));
        var candidateType=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiPlayCandidates+Candidate")!;
        var candidates=Array.CreateInstance(candidateType,1);candidates.SetValue(Activator.CreateInstance(candidateType,future,future,own.Hand.IndexOf(future),PlayMode.Base,future.CurrentCost,false,null,future.CardId),0);
        var reader=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiHandMaterialRules")!.GetMethod("Read",Hidden)!;
        Check(((Array)reader.Invoke(null,[game,candidates])!).Length>0,"normal registry did not yield native hand material facts");
        ownHand.Add(game.Player2.Leader!);
        try{Check(((Array)reader.Invoke(null,[game,candidates])!).Length==0,"foreign/wrong-zone hand registry inspected as own-hand material facts");}
        finally{ownHand.Remove(game.Player2.Leader!);}
        Console.WriteLine("PASS root-bound discard reservation: exact instance, cost, owner, zone, request, turn and completion boundaries");
    }
    sealed class Fake(string kind):HttpMessageHandler,IHttpClientFactory
    {
        public List<JsonElement> Inputs=[];public HttpClient CreateClient(string name)=>new(this,false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){if(kind.EndsWith("line"))Check(GameServiceProvider.CurrentGameContext==null,"native context retained during provider request");using var doc=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));var fields=new Dictionary<string,JsonElement>();foreach(var message in doc.RootElement.GetProperty("messages").EnumerateArray().Where(m=>m.GetProperty("role").GetString()=="user")){using var part=JsonDocument.Parse(message.GetProperty("content").GetString()!);foreach(var p in part.RootElement.EnumerateObject())fields[p.Name]=p.Value.Clone();}var input=JsonSerializer.SerializeToElement(fields,Json);Inputs.Add(input);object answer;
            if(input.TryGetProperty("legalPlays",out var empty)&&empty.GetArrayLength()==0)answer=new AiStrategyPlanner.TurnPlan("value","offline next state",[],[]);
            else if(input.TryGetProperty("legalPlays",out var linePlays)&&kind.EndsWith("line")){
                var candidate=linePlays[0];var rule=candidate.GetProperty("targetKeys")[0];var repaired=input.TryGetProperty("correction",out _);var targetIndex=kind.Contains("empty")?1:repaired?1:2;
                var id=input.GetProperty("ownHand")[targetIndex].GetProperty("card").GetProperty("id").GetInt32();
                var advice=new AiStrategyPlanner.PlayAdvice(0,candidate.GetProperty("cardId").GetInt32(),"Base",candidate.GetProperty("cost").GetInt32(),"use",kind.Contains("material")?null:new(rule.GetProperty("key").GetString()!,"own","hand",targetIndex,id,rule.GetProperty("abilityDefinitionId").GetInt64()));
                var next=linePlays.EnumerateArray().Single(p=>p.GetProperty("handIndex").GetInt32()==2);var second=new AiStrategyPlanner.PlayAdvice(2,next.GetProperty("cardId").GetInt32(),"Base",next.GetProperty("cost").GetInt32(),"use");
                if(kind.Contains("empty-order")&&repaired){
                    var ownRule=next.GetProperty("targetKeys")[0];
                    var first=new AiStrategyPlanner.PlayAdvice(2,next.GetProperty("cardId").GetInt32(),"Base",next.GetProperty("cost").GetInt32(),"use",new(ownRule.GetProperty("key").GetString()!,"own","hand",1,id,ownRule.GetProperty("abilityDefinitionId").GetInt64()));
                    answer=new AiStrategyPlanner.TurnPlan("setup","offline order repair: Tutor discards Noble, mode 1, then optional empty-hand Reiss",[],[],Play:first,Plays:[first,advice with{Target=null}]);
                }else answer=new AiStrategyPlanner.TurnPlan("setup","offline repaired discard line",[],[],Play:advice,Plays:kind.Contains("empty")&&repaired?[advice]:[advice,second]);
            }
            else if(input.TryGetProperty("legalPlays",out var plays)){var candidate=plays[0];var rule=candidate.GetProperty("targetKeys")[0];AiStrategyPlanner.TargetAdvice? target=kind=="none"?null:new(rule.GetProperty("key").GetString()!,kind.EndsWith("discard")?"own":"enemy",kind.EndsWith("discard")||kind=="hidden"?"hand":kind=="face"?"leader":"field",kind.EndsWith("discard")?2:kind=="face"?0:1,kind.EndsWith("discard")?(kind=="reiss-discard"?1474113:1474120):kind=="invalid"?999999:kind=="face"?Leader:Small,rule.GetProperty("abilityDefinitionId").GetInt64());
                answer=new AiStrategyPlanner.TurnPlan("setup","offline explicit target",[],[],Play:new(candidate.GetProperty("handIndex").GetInt32(),candidate.GetProperty("cardId").GetInt32(),"Base",candidate.GetProperty("cost").GetInt32(),"use",target));}
            else answer=new AiStrategyPlanner.DeckGuide("offline guide",input.GetProperty("deck").EnumerateArray().Select(c=>new AiStrategyPlanner.CardRole(c.GetProperty("id").GetInt32(),"fixture role")).ToArray());
            return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=JsonSerializer.Serialize(answer,Json)}}}}))};}
    }
    sealed class Host:IWebHostEnvironment{public string ApplicationName{get;set;}="planned target test";public string EnvironmentName{get;set;}="Test";public string WebRootPath{get;set;}=Path.GetTempPath();public string ContentRootPath{get;set;}=Path.GetTempPath();public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
}
