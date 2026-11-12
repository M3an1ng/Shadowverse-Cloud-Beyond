using System.Net;
using System.Reflection;
using System.Text.Json;
using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class AiDecisionFlowTests
{
    private const int Leader=2000001500,Source=2000001501,Token=2000001502,Drawn=2000001503,HiddenCard=2000001504;
    private static readonly BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    private static readonly Type Flow=typeof(AiStrategyPlanner).Assembly.GetType("ShadowverseBeyond.Server.Services.AiDecisionFlowFacts")!;
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    private static void Check(bool condition,string reason){if(!condition)throw new InvalidOperationException(reason);}
    private static AbilityConfig TokenRule()=>new(){Name="identical label",Effects=[new(){Type=EffectType.GetToken,Parameters=new(){
        ["CardDefinitionId"]=new StaticValueConfig{Value=new[]{Token}},["Amount"]=new StaticValueConfig{Value=1},
        ["PlayerScope"]=new StaticValueConfig{Value="self"},["CardSourceMode"]=new StaticValueConfig{Value="definition"}}}]};
    private static AbilityConfig DrawRule()=>new(){Name="identical label",Effects=[new(){Type=EffectType.Draw,Parameters=new(){
        ["DrawAmount"]=new StaticValueConfig{Value=1},["PlayerScope"]=new StaticValueConfig{Value="self"},["UseSearchMode"]=new StaticValueConfig{Value=false}}}]};
    private static AbilityConfig Parent()=>new(){Id=Source,CardId=Source,Name="ordered source",Type=AbilityType.Triggered,
        Trigger=new(){EventTypes=[EventType.PlayCard],RelationType=TriggerRelationType.TargetSelf,ValidZones=[CardZone.Hand]},
        Targets=new(){["self"]=new(){Query=new(){SourcePoolExpression="context.owner.leader"}}},Effects=[
            new(){Type=EffectType.Damage,Parameters=new(){["DamageAmount"]=new StaticValueConfig{Value=3},["DistributionMode"]=new StaticValueConfig{Value="perTarget"}}},
            new(){Type=EffectType.Choose,RepeatCount=1,TargetSelector=[],Parameters=new(){["SelectionCount"]=new StaticValueConfig{Value=1},["AbilityConfigs"]=new StaticValueConfig{Value=new[]{TokenRule(),DrawRule()}}}}]};
    private static IGameDataProvider Data(AbilityConfig root)
    {
        var revision=Guid.NewGuid();var definitions=new Dictionary<int,CardDefinition>{
            [Leader]=new(){Id=Leader,Type=CardType.Leader,Health=20},[Source]=new(){Id=Source,Name="source",Type=CardType.Spell,Cost=1},
            [Token]=new(){Id=Token,Name="one generated token",Type=CardType.Follower,Cost=0,Attack=1,Health=1},
            [Drawn]=new(){Id=Drawn,Name="different drawn card",Type=CardType.Spell,Cost=9},
            [HiddenCard]=new(){Id=HiddenCard,Name="PRIVATE_ENEMY_HAND",Type=CardType.Spell,Cost=7}};
        return Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch{"get_DataRevision"=>revision,"GetCardDefinition"=>definitions.GetValueOrDefault((int)args![0]!),
            "GetAllCardDefinitions"=>definitions,"GetAbilityConfigs"=>(int)args![0]! == Source?new List<AbilityConfig>{root}:new List<AbilityConfig>(),
            "GetAllAbilityConfigs"=>definitions.Keys.ToDictionary(id=>id,id=>id==Source?new List<AbilityConfig>{root}:new List<AbilityConfig>()),
            "GetAllBuffDefinitions"=>new Dictionary<string,BuffDefinition>(),_=>null});
    }
    private static GameState Game(IServiceProvider sp,GameContextAccessor accessor)
    {
        var own=new PlayerState(Guid.NewGuid()){IsAI=true,TurnNumber=6,CurrentPlayPoints=4,MaxPlayPoints=4,InitialDeckDefinitionIds=[Source,Drawn]};var other=new PlayerState(Guid.NewGuid()){TurnNumber=6};
        var game=new GameState("decision-flow",own,other,own.PlayerId){Phase=GamePhase.Main,Status=GameStatus.InProgress,IsLocalAiTraining=true,
            AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<IBuffManager>()};
        var factory=sp.GetRequiredService<ICardFactory>();own.Leader=factory.CreateAndRegisterNewCard(game,Leader,own.PlayerId,CardZone.Field);own.Leader.Damage=12;
        other.Leader=factory.CreateAndRegisterNewCard(game,Leader,other.PlayerId,CardZone.Field);factory.CreateAndRegisterNewCard(game,Source,own.PlayerId,CardZone.Hand);
        factory.CreateAndRegisterNewCard(game,Drawn,own.PlayerId,CardZone.Deck);factory.CreateAndRegisterNewCard(game,HiddenCard,other.PlayerId,CardZone.Hand);
        typeof(GameState).GetProperty("StrategyBenchmarkPlayerId",Hidden)!.SetValue(game,own.PlayerId);return game;
    }
    private static PlayerChoiceRequest Request(GameState game,CardInstance source,Ability parent,Guid invocation,int execution)
    {
        var ids=new[]{Guid.NewGuid(),Guid.NewGuid()};var request=new PlayerChoiceRequest{TargetKey="decision",RequestingPlayerId=game.Player1.PlayerId,
            MinSelectionCount=1,MaxSelectionCount=1,SelectableTargetIds=ids.ToList()};
        typeof(PlayerChoiceRequest).GetProperty("DecisionOptions",Hidden)!.SetValue(request,new Dictionary<Guid,AbilityConfig>{{ids[0],TokenRule()},{ids[1],DrawRule()}});
        typeof(PlayerChoiceRequest).GetProperty("OptionSourceCard",Hidden)!.SetValue(request,source);
        var effect=new Effect(game.Player1.PlayerId){TriggeredEventId=invocation,EffectIndex=1,ExecutionIndex=execution};
        Flow.GetMethod("Bind",Hidden)!.Invoke(null,[request,effect,parent,true]);return request;
    }
    private static object Read(GameState game,PlayerChoiceRequest request,int budget=4096)=>Flow.GetMethod("Read",Hidden)!.Invoke(null,[game,request,budget])!;
    private static JsonElement Packed(GameState game,PlayerChoiceRequest request,int budget=4096)=>JsonSerializer.SerializeToElement(Read(game,request,budget),Json);
    private static void Remember(GameState game,PlayerChoiceRequest request,Guid id)=>Flow.GetMethod("Remember",Hidden)!.Invoke(null,[game,request,new List<Guid>{id}]);
    internal static void Boundaries()
    {
        var previous=GameDataProvider.Instance;var oldAccessor=GameServiceProvider.ContextAccessor;var root=Parent();var data=Data(root);GameDataProvider.SetInstanceForTesting(data);
        var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
        try{using var services=AiEndTurnEngineTests.Services(data,accessor);using var scope=services.CreateScope();var sp=scope.ServiceProvider;var game=Game(sp,accessor);
            using var context=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,game.Player1.PlayerId);var source=game.Player1.Hand.Single();
            var parent=context.AbilityManager.GetEffectiveAbilitiesForCard(source.InstanceId,context).OfType<Ability>().Single();var invocation=Guid.NewGuid();
            Check(!Packed(game,new()).GetProperty("available").GetBoolean(),"unbound request was treated as an empty native history");
            var first=Request(game,source,parent,invocation,0);var before=Packed(game,first);Check(before.GetProperty("phase").GetString()=="preflight"&&before.GetProperty("parentRule").GetProperty("effects")[0].GetProperty("type").GetString()=="Damage","native parent prefix missing");
            Remember(game,first,first.SelectableTargetIds[0]);var next=Request(game,source,parent,invocation,1);var flow=Packed(game,next);
            Check(flow.GetProperty("priorProposals").GetArrayLength()==1&&flow.GetProperty("priorProposals")[0].GetProperty("rules")[0].GetProperty("effects")[0].GetProperty("type").GetString()=="GetToken","actual proposed branch missing");
            Check(Packed(game,Request(game,source,parent,invocation,1)).GetRawText()==flow.GetRawText(),"random request/option GUIDs polluted the cache input");
            var tiny=Packed(game,next,8);Check(tiny.GetProperty("contentBytes").GetInt32()==0&&tiny.GetProperty("parentRule").ValueKind==JsonValueKind.Null&&tiny.GetProperty("omitted").GetArrayLength()>0&&tiny.GetProperty("priorProposals")[0].GetProperty("currentOptionIndexes")[0][0].GetInt32()==0,"oversized rule was clipped or claimed present");
            var retry=Request(game,source,parent,Guid.NewGuid(),1);Check(!Packed(game,retry).GetProperty("stamp").GetProperty("priorChoicesKnown").GetBoolean(),"missing earlier execution was called complete");
            var freshInvocation=Guid.NewGuid();var fresh=Request(game,source,parent,freshInvocation,0);Check(Packed(game,fresh).GetProperty("priorProposals").GetArrayLength()==0,"cross-invocation proposal leaked");
            Remember(game,fresh,Guid.NewGuid());Check(Packed(game,Request(game,source,parent,freshInvocation,1)).GetProperty("priorProposals").GetArrayLength()==0,"invalid selected ID was retained");
            var limitEvent=Guid.NewGuid();for(var i=0;i<66;i++){var request=Request(game,source,parent,limitEvent,i);Remember(game,request,request.SelectableTargetIds[0]);}
            var limited=Packed(game,Request(game,source,parent,limitEvent,66),128);Check(limited.GetProperty("contentBytes").GetInt32()<=128&&limited.GetProperty("priorProposals").GetArrayLength()==4
                &&limited.GetProperty("stamp").GetProperty("priorChoicesObserved").GetInt32()==64&&!limited.GetProperty("stamp").GetProperty("priorChoicesKnown").GetBoolean(),"long repeated ability exceeded bounds or hid omissions");
            root.Properties["unsafe"]=new StaticValueConfig{Value=new Trap()};Check(Packed(game,first).GetProperty("parentRule").ValueKind==JsonValueKind.Null&&Trap.Reads==0,"captured private object was evaluated");root.Properties.Clear();
            var fingerprint=before.GetProperty("parentRulesFingerprint").GetString();root.Effects[0].Parameters["DamageAmount"]=new StaticValueConfig{Value=4};Check(Packed(game,first).GetProperty("parentRulesFingerprint").GetString()!=fingerprint,"runtime parent mutation reused stale semantics");root.Effects[0].Parameters["DamageAmount"]=new StaticValueConfig{Value=3};
            Check(game.Player1.CurrentPlayPoints==4&&game.Player1.Leader.CurrentHealth==8&&game.Player1.Hand.Count==1,"reading flow executed native effects");
            var text=flow.GetRawText();Check(!text.Contains(game.Player1.PlayerId.ToString())&&!text.Contains(invocation.ToString())&&!text.Contains("PRIVATE_ENEMY_HAND")&&!text.Contains(HiddenCard.ToString()),"private identities or hidden cards leaked");
            var factory=sp.GetRequiredService<ICardFactory>();var hiddenSource=factory.CreateAndRegisterNewCard(game,Source,game.Player2.PlayerId,CardZone.Hand);
            var hiddenParent=context.AbilityManager.GetAbilitiesForCard(hiddenSource.InstanceId).OfType<Ability>().Single();
            Check(!Packed(game,Request(game,hiddenSource,hiddenParent,Guid.NewGuid(),0)).GetProperty("available").GetBoolean(),"enemy hidden source exposed its parent rules");
            typeof(GameState).GetMethod("IncrementStateRevision",Hidden)!.Invoke(game,null);
            Check(!Packed(game,first).GetProperty("available").GetBoolean(),"old request was reused after a native state commit");
            Remember(game,first,first.SelectableTargetIds[0]);var afterCommit=Request(game,source,parent,Guid.NewGuid(),0);
            Check(Packed(game,afterCommit).GetProperty("priorProposals").GetArrayLength()==0,"late old proposal polluted the new invocation");
            StageStats(data,before,flow);
            Console.WriteLine("PASS native decision flow: complete safe rules, source order, same-request replacement, retries, private getters, 64/4 limits, byte bounds and stage statistics; provider calls=0");
        }finally{GameDataProvider.SetInstanceForTesting(previous);GameServiceProvider.ContextAccessor=oldAccessor;}
    }
    private static void StageStats(IGameDataProvider data,JsonElement first,JsonElement second)
    {
        var type=typeof(AiStrategyPlanner).Assembly.GetType("ShadowverseBeyond.Server.Services.AiDefinitionRules")!;var stamp=(AiDefinitionStamp)type.GetMethod("Read",Hidden)!.Invoke(null,[data])!;
        var a=JsonSerializer.Deserialize<AiChoiceFlowStamp>(first.GetProperty("stamp"),Json)!;var b=JsonSerializer.Deserialize<AiChoiceFlowStamp>(second.GetProperty("stamp"),Json)!;
        var pos=new AiStrategyPlanner.ChoicePosition(8,20,4,1,0,0,0,0,0){NativeOptionRules=new string('a',64),DecisionFlow=a};
        var lesson=new AiStrategyPlanner.Lesson("Neutral",true,true,6,"fixture",[],[],DefinitionRules:stamp,Choices:[new(6,Source,"mode",[1],Position:pos),new(6,Source,"mode",[1],Position:pos with{DecisionFlow=b}),
            new(6,Source,"mode",[1],Position:pos with{DecisionFlow=b with{PriorChoices=[b.PriorChoices[0] with{RuleFingerprints=[new string('b',64)]}]}})]);
        using var http=new Provider();var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["AiStrategy:Enabled"]="false",["AiStrategy:MemoryPath"]=Path.GetTempPath()}).Build();
        using var planner=new AiStrategyPlanner(http,config,new Host(),NullLogger<AiStrategyPlanner>.Instance);
        var summary=JsonSerializer.SerializeToElement(typeof(AiStrategyPlanner).GetMethod("SummarizeExperience",Hidden)!.Invoke(planner,[new[]{lesson},"Neutral",new string('f',64)]),Json);
        Check(summary.GetProperty("modes").GetArrayLength()==3&&summary.GetProperty("modes").EnumerateArray().Select(mode=>mode.GetProperty("decisionStage").GetString()).Distinct().Count()==3,"identical mode indices in different executions were merged");
    }
    internal static void Native(string output)
    {
        Boundaries();var rows=new List<object>();foreach(var enabled in new[]{false,true})foreach(var reverse in new[]{false,true})rows.Add(Execute(enabled,reverse));
        File.WriteAllText(output,JsonSerializer.Serialize(new{realProviderRequests=0,nativeSourceTransactions=4,cases=rows,
            scope="Scripted provider tests what actual request information permits, not real LLM quality. Actual source transactions, unresolved prefix, choices and effects; synthetic cards, isolated temporary histories. No weight fitting or whole-match strength claim."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("PASS native repeated-choice information control: 4 transactions; real provider requests=0");
    }
    private static object Execute(bool enabled,bool reverse)
    {
        var previous=GameDataProvider.Instance;var oldAccessor=GameServiceProvider.ContextAccessor;var root=Parent();var data=Data(root);GameDataProvider.SetInstanceForTesting(data);
        var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;var directory=Path.Combine(Path.GetTempPath(),"ai-decision-flow-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try{
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["AiStrategy:Enabled"]="true",["AiStrategy:ApiKey"]="offline-fake",["AiStrategy:Model"]="offline-fake",["AiStrategy:Endpoint"]="https://example.invalid/chat/completions",
                ["AiStrategy:MemoryPath"]=directory,["AiStrategy:DecisionFlowFacts"]=enabled.ToString(),["AiModel:Path"]=Path.Combine(directory,"missing-model.json")}).Build();
            using var http=new Provider();var model=new AiLearningModel(new Host(),config,NullLogger<AiLearningModel>.Instance);using var planner=new AiStrategyPlanner(http,config,new Host(),NullLogger<AiStrategyPlanner>.Instance,model);
            GameState game=null!;ServiceProvider services=null!;AiService ai=null!;var selected=new List<string>();var rawPositions=new List<object>();
            var games=Proxy<IGameService>.Make((method,args)=>method.Name=="GetGameState"?game:null);
            var pending=Proxy<IPendingActionService>.Make((method,args)=>{
                if(method.ReturnType==typeof(bool))return false;if(method.Name!="WaitForPlayerChoiceAsync")return null;var request=(PlayerChoiceRequest)args![1]!;
                if(reverse){request.SelectableTargetIds.Reverse();request.SelectableCardInstances.Reverse();}
                using var scope=services.CreateScope();using var context=new GameContext(scope.ServiceProvider,scope.ServiceProvider.GetRequiredService<IMapper>(),accessor,game,game.Player1.PlayerId);
                var snapshot=GameStateProjection.Create(game,context);rawPositions.Add(new{health=game.Player1.Leader.CurrentHealth,pp=game.Player1.CurrentPlayPoints,hand=game.Player1.Hand.Count});
                Check(game.Player1.Leader.CurrentHealth==8&&game.Player1.CurrentPlayPoints==4&&game.Player1.Hand.Count==1,"preflight unexpectedly executed the earlier source prefix");
                var choice=ai.DecideTargetChoiceAsync(request,snapshot).GetAwaiter().GetResult();var calls=http.Inputs.Count;
                var repeat=ai.DecideTargetChoiceAsync(request,snapshot).GetAwaiter().GetResult();Check(repeat.ChosenTargetIds.SequenceEqual(choice.ChosenTargetIds)&&http.Inputs.Count==calls,"same invocation slot was re-requested or counted twice");
                var bindings=(IReadOnlyDictionary<Guid,AbilityConfig>)typeof(PlayerChoiceRequest).GetProperty("DecisionOptions",Hidden)!.GetValue(request)!;
                selected.Add(bindings[choice.ChosenTargetIds.Single()].Effects[0].Type.ToString());
                Check(!Directory.EnumerateFiles(directory,"*.json").Any(path=>Path.GetFileNameWithoutExtension(path).Length==64),"preflight proposal was persisted as completed experience");return Task.FromResult(choice);
            });
            using(services=AiEndTurnEngineTests.Services(data,accessor,pending,games,items=>items.AddSingleton(planner))){
                using var scope=services.CreateScope();var sp=scope.ServiceProvider;game=Game(sp,accessor);var source=game.Player1.Hand.Single();
                ai=new(NullLogger<AiService>.Instance,services.GetRequiredService<IServiceScopeFactory>(),model,config);
                planner.WarmDeckAsync(game,game.Player1.PlayerId,"Neutral")!.GetAwaiter().GetResult();
                using var context=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,game.Player1.PlayerId);
                planner.PlanAsync(game,GameStateProjection.Create(game,context),game.Player1.PlayerId,default,(_,_)=>Task.FromResult(new PlayabilityResultDto())).GetAwaiter().GetResult();
                var action=new PlayCardRequestedEvent(game.Player1.PlayerId,new PlayCardActionDto{CardUid=source.InstanceId,PlayMode=PlayMode.Base,PaidCost=1});
                var result=sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(context,[action]).GetAwaiter().GetResult();Check(result.IsSuccess&&!result.IsCancelled,result.ErrorMessage??"native source transaction failed");
                planner.ObserveCommittedEvents(game,result.ProcessedEvents);planner.ObserveCommittedEvents(game,[new GameEndEvent(game.Player1.PlayerId,game.Player2.PlayerId,"isolated source fixture")]);
                var history=Directory.EnumerateFiles(directory,"*.json").Where(path=>Path.GetFileNameWithoutExtension(path).Length==64).SelectMany(path=>JsonSerializer.Deserialize<AiStrategyPlanner.Lesson[]>(File.ReadAllText(path),Json)??[]).Single();
                Check(selected.SequenceEqual(enabled?new[]{"GetToken","Draw"}:new[]{"GetToken","GetToken"}),"information-only control did not expose the repeated-choice difference");
                Check(game.Player1.Hand.Count==2&&game.Player1.Hand.Count(card=>card.CurrentDefinitionId==Token)==(enabled?1:2)&&game.Player1.Leader.CurrentHealth==5&&game.Player1.CurrentPlayPoints==3,"native chosen effects did not match actual hand/resources");
                Check(history.Choices?.Length==2&&(enabled?history.Choices.All(choice=>choice.Position?.DecisionFlow!=null):history.Choices.All(choice=>choice.Position?.DecisionFlow==null)),"committed choice stage missing, duplicated or fabricated");
                var modeInputs=http.Inputs.Where(input=>input.TryGetProperty("options",out _)).ToArray();Check(modeInputs.Length==(enabled?2:1),"mode cap/cache count differed from the bounded information control");
                Check(http.Prefixes.Distinct().Count()==1&&!JsonSerializer.Serialize(http.Inputs).Contains("PRIVATE_ENEMY_HAND")&&!JsonSerializer.Serialize(http.Inputs).Contains(game.Player1.PlayerId.ToString())
                    &&!JsonSerializer.Serialize(http.Inputs).Contains(source.InstanceId.ToString())&&!JsonSerializer.Serialize(http.Inputs).Contains(HiddenCard.ToString()),"stable prompt prefix or hidden-card exclusion regressed");
                foreach(var frame in modeInputs)Check(frame.GetProperty("decisionRules").GetProperty("contentBytes").GetInt32()+frame.GetProperty("currentAbilityRules").GetProperty("contentBytes").GetInt32()+frame.GetProperty("currentRuntimeRules").GetProperty("contentBytes").GetInt32()+(enabled?frame.GetProperty("decisionFlow").GetProperty("contentBytes").GetInt32():0)<=16384,"flow exceeded shared whole-rule budget");
                return new{enabled,reverse,selected,rawPositions,nativeSuccess=result.IsSuccess,modeRequests=modeInputs.Length,hand=game.Player1.Hand.Select(card=>card.CurrentDefinitionId).ToArray(),committedChoices=history.Choices,modeInputs,passed=true};
            }
        }finally{GameDataProvider.SetInstanceForTesting(previous);GameServiceProvider.ContextAccessor=oldAccessor;Check(Path.GetFullPath(directory).StartsWith(Path.Combine(Path.GetTempPath(),"ai-decision-flow-"),StringComparison.OrdinalIgnoreCase),"unexpected cleanup target");if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
    private sealed class Host:Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {public string WebRootPath{get;set;}=Path.GetTempPath();public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="offline";public string ContentRootPath{get;set;}=Path.GetTempPath();public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
    private sealed class Trap {internal static int Reads;public string Value{get{Reads++;throw new InvalidOperationException("No private getter");}}}
    private sealed class Provider:HttpMessageHandler,IHttpClientFactory
    {
        internal List<JsonElement> Inputs=[];internal List<string> Prefixes=[];public HttpClient CreateClient(string name)=>new(this,false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            using var envelope=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));var messages=envelope.RootElement.GetProperty("messages").EnumerateArray().ToArray();Prefixes.Add(string.Join("\n",messages.Take(2).Select(message=>message.GetRawText())));
            var fields=new Dictionary<string,JsonElement>();foreach(var message in messages.Where(message=>message.GetProperty("role").GetString()=="user")){using var part=JsonDocument.Parse(message.GetProperty("content").GetString()!);foreach(var property in part.RootElement.EnumerateObject())fields[property.Name]=property.Value.Clone();}
            var input=JsonSerializer.SerializeToElement(fields,Json);Inputs.Add(input);object answer;
            if(input.TryGetProperty("decisionRules",out var rules)&&rules.ValueKind==JsonValueKind.Object){
                var hasEarlier=input.TryGetProperty("decisionFlow",out var flow)&&flow.ValueKind==JsonValueKind.Object&&flow.GetProperty("available").GetBoolean()&&flow.GetProperty("priorProposals").GetArrayLength()>0;
                var desired=hasEarlier?"Draw":"GetToken";var option=input.GetProperty("options").EnumerateArray().Single(option=>rules.GetProperty("rules")[option.GetProperty("rule").GetInt32()].GetProperty("effects")[0].GetProperty("type").GetString()==desired);
                answer=new{indexes=new[]{option.GetProperty("index").GetInt32()}};
            }else if(input.TryGetProperty("legalPlays",out _))answer=new{focus="pressure",plan="offline fixture",prioritize=Array.Empty<int>(),hold=Array.Empty<int>()};
            else answer=new{gamePlan="offline fixture",cards=new[]{new{id=Source,role="fixture"}}};
            return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=JsonSerializer.Serialize(answer)}}}}))};
        }
    }
}
