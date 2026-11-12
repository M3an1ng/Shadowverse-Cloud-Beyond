using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class AiLearningEvidenceTests
{
    private const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Static;
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){Encoder=JavaScriptEncoder.Create(UnicodeRanges.All)};
    private static void Check(bool condition,string reason){if(!condition)throw new InvalidOperationException(reason);}
    private static object Invoke(string name,params object?[] args)=>typeof(AiStrategyPlanner).GetMethod(name,Hidden)!.Invoke(null,args)!;
    internal static void ProseIsReviewMaterial()
    {
        var plans=new[]{"T3 模型未验证解释：并没有回复。『测试』 \"引号\" \\ 路径", "T5 UNREVIEWED_PROSE_ONLY"};
        var lesson=new AiStrategyPlanner.Lesson("Neutral",true,true,6,"fixture",plans,[2],
            HandActions:[new(3,2,"play")],LeaderHealthChanges:[new(3,"enemy",1,3,2)]);
        var archived=JsonSerializer.Serialize(lesson,Json);
        var live=JsonSerializer.SerializeToElement(Invoke("LessonForModel",lesson),Json);
        Check(!live.GetRawText().Contains("UNREVIEWED_PROSE_ONLY")&&!live.TryGetProperty("proposedPlans",out _),"unreviewed explanation was re-used as playing knowledge");
        Check(live.GetProperty("proposalTextCoverage").GetProperty("recorded").GetInt32()==2
            &&live.GetProperty("proposalTextCoverage").GetProperty("included").GetInt32()==0,"archived explanation omission was silent");
        var observed=live.GetProperty("observed");
        Check(observed.GetProperty("handActions")[0].GetProperty("cardId").GetInt32()==2,"actual hand evidence was removed with prose");
        var health=observed.GetProperty("leaderHealthChanges");
        Check(health.GetProperty("columns").EnumerateArray().Select(x=>x.GetString()).SequenceEqual(new[]{"turn","targetSide","eventBefore","eventAfter","change"})
            &&health.GetProperty("rows")[0][1].GetString()=="enemy"&&health.GetProperty("rows")[0][4].GetInt32()==2,"compact public health facts changed meaning");
        var helper=typeof(AiStrategyPlanner).Assembly.GetType("ShadowverseBeyond.Server.Services.AiPlanTextFacts")!;
        JsonElement Review(string[]? input,int budget=8192)=>JsonSerializer.SerializeToElement(helper.GetMethod("ForReview",Hidden)!.Invoke(null,[input,budget]),Json);
        var review=Review(plans);
        Check(review.GetProperty("texts")[0].GetProperty("value").GetString()==plans[0]
            &&review.GetProperty("texts")[1].GetProperty("value").GetString()==plans[1],"review changed quotes, Chinese text or provenance");
        var many=Enumerable.Range(0,20).Select(index=>"T"+index+" "+new string('测',300)).ToArray();
        var bounded=Review(many);
        Check(bounded.GetProperty("included").GetInt32()==8&&bounded.GetProperty("omitted").GetInt32()==12
            &&bounded.GetProperty("texts").EnumerateArray().Select(row=>row.GetProperty("index").GetInt32()).SequenceEqual(new[]{0,1,2,3,16,17,18,19}),"review did not preserve opening/recent source indices");
        var tiny=Review(plans,0);Check(tiny.GetProperty("included").GetInt32()==0&&tiny.GetProperty("omitted").GetInt32()==2,"zero review budget lost its missing-data count");
        var unknown=Review(null);Check(unknown.GetProperty("recorded").ValueKind==JsonValueKind.Null,"legacy absent prose was treated as proven absence");
        var large=Review([new string('长',20000),"recent short text"]);
        Check(large.GetProperty("included").GetInt32()==1&&large.GetProperty("omitted").GetInt32()==1
            &&large.GetProperty("texts")[0].GetProperty("index").GetInt32()==1,"oversized text was silently truncated or blocked all smaller evidence");
        Check(JsonSerializer.Serialize(lesson,Json)==archived&&lesson.Plans.SequenceEqual(plans),"input filtering mutated the archived lesson");
        var legacy=JsonSerializer.SerializeToElement(Invoke("EvidenceForModel",lesson with{LeaderHealthChanges=null}),Json);
        Check(legacy.GetProperty("leaderHealthChanges").ValueKind==JsonValueKind.Null,"legacy unknown leader-health coverage became an empty known ledger");
    }
    internal static void NativeLeaderHealthIsCommittedAndBounded()
    {
        var prior=GameDataProvider.Instance;var priorAccessor=GameServiceProvider.ContextAccessor;
        var directory=Path.Combine(Path.GetTempPath(),"ai-learning-evidence-"+Guid.NewGuid().ToString("N"));
        try {
            var definitions=new Dictionary<int,CardDefinition>{[1]=new(){Id=1,Name="public leader",Type=CardType.Leader,Health=20},
                [2]=new(){Id=2,Name="public source",Type=CardType.Follower,Attack=1,Health=3}};
            var data=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch {
                "GetCardDefinition"=>definitions.GetValueOrDefault((int)args![0]!),"GetAllCardDefinitions"=>definitions,
                "GetAbilityConfigs"=>new List<AbilityConfig>(),_=>null});
            GameDataProvider.SetInstanceForTesting(data);var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
            using var services=AiEndTurnEngineTests.Services(data,accessor);using var scope=services.CreateScope();var sp=scope.ServiceProvider;
            var own=new PlayerState(Guid.NewGuid()){IsAI=true,TurnNumber=6};var enemy=new PlayerState(Guid.NewGuid()){TurnNumber=6};
            var game=new GameState("learning-evidence-native",own,enemy,own.PlayerId){Phase=GamePhase.Main,Status=GameStatus.InProgress,
                AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<IBuffManager>()};
            var factory=sp.GetRequiredService<ICardFactory>();own.Leader=factory.CreateAndRegisterNewCard(game,1,own.PlayerId,CardZone.Field);
            enemy.Leader=factory.CreateAndRegisterNewCard(game,1,enemy.PlayerId,CardZone.Field);
            var source=factory.CreateAndRegisterNewCard(game,2,own.PlayerId,CardZone.Field);
            var hidden=factory.CreateAndRegisterNewCard(game,1,enemy.PlayerId,CardZone.Hand);
            own.Leader.Damage=5;enemy.Leader.Damage=10;
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["AiStrategy:Enabled"]="false",["AiStrategy:MemoryPath"]=directory}).Build();
            using var http=new NeverProvider();using var planner=new AiStrategyPlanner(http,config,new Host(),NullLogger<AiStrategyPlanner>.Instance);
            var table=typeof(AiStrategyPlanner).GetField("matches",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(planner)!;
            var stateType=table.GetType().GetGenericArguments()[1];var state=Activator.CreateInstance(stateType,true)!;
            stateType.GetField("PlayerId")!.SetValue(state,own.PlayerId);stateType.GetField("DeckKey")!.SetValue(state,new string('d',64));
            stateType.GetField("Attempts")!.SetValue(state,1);
            table.GetType().GetMethod("Add")!.Invoke(table,[game,state]);
            var facts=(List<AiStrategyPlanner.LeaderHealthFact>)stateType.GetProperty("LeaderHealthChanges")!.GetValue(state)!;
            var identities=(HashSet<Guid>)stateType.GetField("SeenLeaderHealthEvents",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(state)!;
            using var context=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId);
            var tx=sp.GetRequiredService<IGameActionTransactionService>();
            void Commit(IGameEvent ev,bool repeat=false) {
                var result=tx.ProcessTransactionAsync(context,[ev]).GetAwaiter().GetResult();Check(result.IsSuccess&&!result.IsCancelled,"native public health transaction failed");
                planner.ObserveCommittedEvents(game,result.ProcessedEvents);if(repeat)planner.ObserveCommittedEvents(game,result.ProcessedEvents);
            }
            Check(facts.Count==0,"planning preparation created observed health evidence");
            Commit(new HealEvent(enemy.Leader,3,source),true);Commit(new HealEvent(own.Leader,2,source),true);
            Check(facts.Count==2&&facts[0] is {TargetSide:"enemy",EventBefore:10,EventAfter:13,Change:3}
                &&facts[1] is {TargetSide:"own",Change:2},"native heals lost target ownership, amount or delivery deduplication");
            Commit(new HealthChangedEvent(hidden,20,19,-1));Check(facts.Count==2,"hidden-zone leader-like card leaked into public health observations");
            for(var index=0;index<60;index++) {
                var delta=index%2==0?-1:1;var before=own.Leader.CurrentHealth;Commit(new HealthChangedEvent(own.Leader,before,before+delta,delta));
            }
            Check(facts.Count==48&&facts[0].TargetSide=="enemy"&&facts[^1].Change==1,"long-game ledger was unbounded or lost the opening/recent excerpts");
            var packed=JsonSerializer.Serialize(facts,Json);
            Check(!packed.Contains(own.PlayerId.ToString())&&!packed.Contains(hidden.InstanceId.ToString())&&http.Requests==0,"public health facts leaked identifiers or requested a provider");
            while(identities.Count<4096)identities.Add(Guid.NewGuid());
            var oldCount=facts.Count;var hp=own.Leader.CurrentHealth;Commit(new HealthChangedEvent(own.Leader,hp,hp-1,-1));
            Check(facts.Count==oldCount&&identities.Count==4096,"identity budget silently grew");
            var trace=stateType.GetProperty("Trace")!.GetValue(state)!;
            var coverage=(AiTraceCoverage)trace.GetType().GetMethod("Snapshot",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(trace,null)!;
            Check(coverage.Sections["leaderHealth"].Observed==62&&coverage.Sections["leaderHealth"].Omitted==14
                &&coverage.EventTotals["leaderHealthSignalsOutsideIdentityBudget"]==1,"health evidence omitted unique signals or budget gaps without coverage metadata");
            Commit(new GameEndEvent(own.PlayerId,enemy.PlayerId,"synthetic persistence boundary"));
            var stored=JsonSerializer.Deserialize<AiStrategyPlanner.Lesson[]>(File.ReadAllText(Path.Combine(directory,new string('d',64)+".json")),Json)!;
            Check(stored.Single().LeaderHealthChanges?.Length==48&&stored[0].Recording!.Sections["leaderHealth"].Omitted==14,
                "finished-game persistence dropped public health evidence or its missing-data scope");
        } finally {
            GameDataProvider.SetInstanceForTesting(prior);GameServiceProvider.ContextAccessor=priorAccessor;
            var resolved=Path.GetFullPath(directory);if(!resolved.StartsWith(Path.Combine(Path.GetTempPath(),"ai-learning-evidence-"),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unexpected test cleanup path");
            if(Directory.Exists(resolved))Directory.Delete(resolved,true);
        }
    }
    private sealed class NeverProvider:HttpMessageHandler,IHttpClientFactory {
        internal int Requests;public HttpClient CreateClient(string name)=>new(this,false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Requests++;throw new InvalidOperationException("No provider expected");}
    }
    private sealed class Host:IWebHostEnvironment {
        public string ApplicationName{get;set;}="offline";public string EnvironmentName{get;set;}="Development";public string ContentRootPath{get;set;}=".";
        public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();public string WebRootPath{get;set;}=".";public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();
    }
}
