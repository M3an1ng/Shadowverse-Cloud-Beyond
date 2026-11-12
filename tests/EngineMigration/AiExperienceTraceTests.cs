using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Services;

// Exercises the experience consumer with committed-event fixtures, not a full game simulation.
internal static class AiExperienceTraceTests
{
    private static MethodInfo Method(string name) => typeof(AiStrategyPlanner).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!;
    private static void Check(bool condition,string message) {if(!condition)throw new InvalidOperationException(message);}
    private static AiStrategyPlanner.Lesson Read(AiStrategyPlanner planner) =>
        ((IReadOnlyList<AiStrategyPlanner.HistoryEntry>)Method("ReadHistory").Invoke(planner,null)!).Single().Lesson;
    private static AiStrategyPlanner.ActionOption Action(string kind,int? card=1,int? target=null,string? mode=null,int? cost=null) =>
        new(kind,card,target,null,3,PlayMode:mode,PaidCost:cost);

    internal static void LongGame()
    {
        using var fixture=new Fixture();var p=fixture.Planner;var game=fixture.Game;var own=fixture.Own;var enemy=fixture.Enemy;
        var data=GameDataProvider.Instance.GetCardDefinition(3)!;var oldType=data.Type;
        try {
            data.Type=CardType.Leader;
            var leader=new CardInstance(Guid.NewGuid(),3,enemy.PlayerId);
            for(var i=0;i<120;i++) {
                own.TurnNumber=3+i;
                var card=new CardInstance(Guid.NewGuid(),i==119?2:1,own.PlayerId);
                var target=new CardInstance(Guid.NewGuid(),2,enemy.PlayerId);
                var comparison=new AiStrategyPlanner.ActionComparisonFact(own.TurnNumber,Action("EndTurn",null),Action("PlayCard",card.BaseDefinitionId,mode:"Base",cost:1));
                Method("SetPendingComparison").Invoke(p,[game,comparison]);
                Method("BindPendingComparison").Invoke(p,[game,new PlayCardActionDto {CardUid=card.InstanceId,PlayMode=PlayMode.Base,PaidCost=1}]);
                Method("SetPendingAttack").Invoke(p,[game,card.InstanceId,target.InstanceId,
                    new AiStrategyPlanner.AttackFact(own.TurnNumber,card.BaseDefinitionId,2,i%2==0,true,1,1,1,20,20,1)]);
                var runtime=Guid.NewGuid();
                Method("SetPendingAbility").Invoke(p,[game,runtime,new AiStrategyPlanner.LocalAbilityCandidate(card.BaseDefinitionId,"act",1,"test",1)]);
                p.ObserveCommittedEvents(game,[new PlayCardEvent(own,card,PlayMode.Base,1),new AttackEvent(card,target),
                    new ActInvokedEvent(own,card,1,runtime),new DiscoverEvent(own,card),new DamageEvent(leader,1,card),
                    new PlayCardEvent(enemy,new CardInstance(Guid.NewGuid(),1,enemy.PlayerId),PlayMode.Base,1)]);
            }
            // An attack without pre-action context must be counted as observed but not invented as a detailed fact.
            p.ObserveCommittedEvents(game,[new AttackEvent(new CardInstance(Guid.NewGuid(),1,own.PlayerId),leader),
                new GameEndEvent(own.PlayerId,enemy.PlayerId,"fixture finished")]);
            var lesson=Read(p);var coverage=lesson.Recording!;
            Check(coverage.FromTurn==3&&coverage.ThroughTurn==122,"observation boundaries lost");
            foreach(var (name,limit) in new[]{("plays",40),("handActions",96),("abilityActions",40),("choices",24),("comparisons",60),("attackContexts",64)}) {
                var section=coverage.Sections[name];
                Check(section.Observed==120&&section.Retained==limit&&section.Omitted==120-limit,
                    "incorrect observed/retained/omitted counts for "+name);
            }
            Check(lesson.Played.Length==40&&lesson.Played[0]==1&&lesson.Played[^1]==2
                &&lesson.HandActions![0].Turn==3&&lesson.HandActions[^1].Turn==122
                &&lesson.AbilityActions![^1].CardId==2&&lesson.Choices![^1].CardId==2
                &&lesson.Attacks![0].Turn==3&&lesson.Attacks[^1].Turn==122&&lesson.ActionComparisons![^1].Turn==122,
                "late winning actions replaced all opening evidence or were silently dropped");
            Check(coverage.EventTotals["ownAttackEvents"]==121&&coverage.Sections["attackContexts"].Observed==120
                &&coverage.EventTotals["stormFaceContexts"]==60&&coverage.EventTotals["stormTradeContexts"]==60,
                "missing attack context was fabricated or full context totals were truncated");
            Check(coverage.EventTotals["leaderDamageEvents"]==120&&coverage.EventTotals["leaderDamageAmount"]==120
                &&coverage.EventTotals["unretainedLeaderDamageEvents"]==72&&coverage.EventTotals["unretainedLeaderDamageAmount"]==72,
                "damage group truncation was silently treated as complete damage");
            var evidence=typeof(AiStrategyPlanner).GetMethod("EvidenceForModel",BindingFlags.Static|BindingFlags.NonPublic)!;
            using var payload=JsonDocument.Parse(JsonSerializer.Serialize(evidence.Invoke(null,[lesson]),new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            Check(payload.RootElement.GetProperty("recording").GetProperty("known").GetBoolean()
                &&payload.RootElement.GetProperty("recording").GetProperty("sections").GetProperty("plays").GetProperty("omitted").GetInt32()==80,
                "model input omitted recording limits");
            var storedBytes=JsonSerializer.SerializeToUtf8Bytes(coverage,new JsonSerializerOptions(JsonSerializerDefaults.Web)).Length;
            var modelBytes=System.Text.Encoding.UTF8.GetByteCount(payload.RootElement.GetProperty("recording").GetRawText());
            Check(modelBytes<storedBytes*.9,"coverage prompt repeated storage-only retention metadata");
            Console.WriteLine($"PASS compact coverage: {storedBytes} -> {modelBytes} bytes; omission counts retained");
            var legacy=JsonSerializer.Deserialize<AiStrategyPlanner.Lesson>("""{"opponentClass":"Havencraft","first":true,"won":true,"turns":9,"endReason":"old","plans":[],"played":[]}""",new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            using var old=JsonDocument.Parse(JsonSerializer.Serialize(evidence.Invoke(null,[legacy]),new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            Check(!old.RootElement.GetProperty("recording").GetProperty("known").GetBoolean(),"legacy absence was declared complete");
            var compact=typeof(AiStrategyPlanner).GetMethod("LessonForModel",BindingFlags.Static|BindingFlags.NonPublic)!;
            var text=JsonSerializer.Serialize(compact.Invoke(null,[lesson]),new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Check(!text.Contains(own.PlayerId.ToString())&&!text.Contains(enemy.PlayerId.ToString()),"trace exposed player identities");
        } finally {data.Type=oldType;}
    }

    internal static void CommitMatching()
    {
        using var f=new Fixture();var p=f.Planner;var game=f.Game;var own=f.Own;var enemy=f.Enemy;
        var card=new CardInstance(Guid.NewGuid(),1,own.PlayerId);var duplicate=new CardInstance(Guid.NewGuid(),1,own.PlayerId);
        var target=new CardInstance(Guid.NewGuid(),2,enemy.PlayerId);var otherTarget=new CardInstance(Guid.NewGuid(),2,enemy.PlayerId);
        var cache=typeof(AiStrategyPlanner).GetField("matches",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(p)!;
        object?[] args=[game,null];cache.GetType().GetMethod("TryGetValue")!.Invoke(cache,args);
        var state=args[1]!;
        int Count()=>((List<AiStrategyPlanner.ActionComparisonFact>)state.GetType().GetProperty("ActionComparisons")!.GetValue(state)!).Count;
        void Pending(AiStrategyPlanner.ActionOption selected,object data,AiStrategyPlanner.ActionPosition? position=null) {
            Method("SetPendingComparison").Invoke(p,[game,new AiStrategyPlanner.ActionComparisonFact(3,Action("EndTurn",null),selected,position)]);
            Method("BindPendingComparison").Invoke(p,[game,data]);
        }
        Pending(Action("PlayCard",mode:"Base",cost:1),new PlayCardActionDto{CardUid=card.InstanceId,PlayMode=PlayMode.Base,PaidCost=1});
        p.ObserveCommittedEvents(game,[new PlayCardEvent(enemy,target,PlayMode.Base,1),new PlayCardEvent(own,duplicate,PlayMode.Base,1),
            new PlayCardEvent(own,card,PlayMode.Enhance,1),new PlayCardEvent(own,card,PlayMode.Base,2)]);
        Check(Count()==0,"unrelated owner/copy/mode/cost committed the proposal");
        p.ObserveCommittedEvents(game,[new PlayCardEvent(own,card,PlayMode.Base,1)]);Check(Count()==1,"matching play missing");
        Pending(Action("PerformAttack",target:2),new AttackActionDto{AttackerUid=card.InstanceId,TargetUid=target.InstanceId});
        p.ObserveCommittedEvents(game,[new AttackEvent(card,otherTarget)]);Check(Count()==1,"wrong attack target committed proposal");
        p.ObserveCommittedEvents(game,[new AttackEvent(card,target)]);Check(Count()==2,"matching attack missing");
        Pending(Action("PerformEvolve"),new EvolveActionDto{UnitUid=card.InstanceId.ToString()});
        p.ObserveCommittedEvents(game,[new EvolveEvent(card,TargetInfo.ForCard(card)),new EvolveEvent(duplicate,TargetInfo.ForPlayer(own),isManualEvolution:true)]);
        Check(Count()==2,"effect evolution or another copy was treated as manual action");
        p.ObserveCommittedEvents(game,[new EvolveEvent(card,TargetInfo.ForPlayer(own),isManualEvolution:true)]);Check(Count()==3,"manual evolution missing");
        var position=new AiStrategyPlanner.ActionPosition(20,9,3,7,2,2,true,true,false,1,4,2,2,6,
            new([1],1,2,0,1,false));
        Pending(Action("PerformSuperEvolve"),new SuperEvolveActionDto{UnitUid=card.InstanceId.ToString()},position);
        p.ObserveCommittedEvents(game,[new SuperEvolveEvent(card,TargetInfo.ForCard(card))]);Check(Count()==3,"effect super evolution committed manual proposal");
        p.ObserveCommittedEvents(game,[new SuperEvolveEvent(card,TargetInfo.ForPlayer(own),isManualEvolution:true),new EvolveEvent(card,TargetInfo.ForPlayer(own),true,true)]);
        Check(Count()==4,"super evolution doubled or was lost");
        var runtime=Guid.NewGuid();
        Pending(Action("RequestAct"),new ActActionDto{CardUid=card.InstanceId,AbilityRuntimeId=runtime});
        p.ObserveCommittedEvents(game,[new FusionInvokedEvent(own,card),new ActInvokedEvent(own,duplicate,1,runtime),new ActInvokedEvent(own,card,1,Guid.NewGuid())]);
        Check(Count()==4,"request committed before the matching activation");
        p.ObserveCommittedEvents(game,[new ActInvokedEvent(own,card,1,runtime)]);Check(Count()==5,"activation missing");
        Pending(Action("RequestFusion"),new FusionActionDto{CardUid=card.InstanceId,AbilityRuntimeId=runtime});
        p.ObserveCommittedEvents(game,[new ActInvokedEvent(own,card,1,runtime)]);Check(Count()==5,"activation committed fusion");
        p.ObserveCommittedEvents(game,[new FusionInvokedEvent(own,card,triggeredAbilityRuntimeId:runtime)]);Check(Count()==6,"fusion missing");
        Pending(Action("EndTurn",null),new{});p.ObserveCommittedEvents(game,[new TurnEndEvent(enemy)]);Check(Count()==6,"enemy turn end committed proposal");
        p.ObserveCommittedEvents(game,[new TurnEndEvent(own)]);Check(Count()==7,"turn end missing");
        Pending(Action("PerformPpBoost",null),new{});p.ObserveCommittedEvents(game,[new PpBoostStateUpdatedEvent(enemy,1,true)]);Check(Count()==7,"enemy boost committed proposal");
        p.ObserveCommittedEvents(game,[new PpBoostStateUpdatedEvent(own,1,true)]);Check(Count()==8,"boost toggle missing");
        Pending(Action("PlayCard"),new PlayCardActionDto{CardUid=card.InstanceId});Method("SetPendingComparison").Invoke(p,[game,null]);
        p.ObserveCommittedEvents(game,[new PlayCardEvent(own,card),new GameEndEvent(own.PlayerId,enemy.PlayerId,"fixture end")]);
        var lesson=Read(p);
        var savedPosition=lesson.ActionComparisons!.Single(item=>item.Selected.Kind=="PerformSuperEvolve").Position!;
        Check(lesson.ActionComparisons.Length==8&&(savedPosition with {EndTurnEstimate=null})==(position with {EndTurnEstimate=null})
            &&savedPosition.EndTurnEstimate?.EnemyLeaderBaseDamage==2&&savedPosition.EndTurnEstimate.SourceCardIds.SequenceEqual(new[]{1}),
            "cancelled proposal recorded or pre-action EP/SEP context lost");
        var facts=typeof(AiStrategyPlanner).GetMethod("EvidenceForModel",BindingFlags.Static|BindingFlags.NonPublic)!;
        using(var evidence=JsonDocument.Parse(JsonSerializer.Serialize(facts.Invoke(null,[lesson]),new JsonSerializerOptions(JsonSerializerDefaults.Web)))) {
            var resources=evidence.RootElement.GetProperty("resourceActions");
            Check(resources.EnumerateArray().Any(item=>item.GetProperty("action").GetProperty("kind").GetString()=="PerformSuperEvolve"
                &&item.GetProperty("before").GetProperty("ep").GetInt32()==2
                &&item.GetProperty("before").GetProperty("endTurnEstimate").GetProperty("unmodeledParts").GetInt32()==1)
                &&!resources.EnumerateArray().Any(item=>item.TryGetProperty("local",out _)),
                "reflection lost committed resource use or treated the unchosen local action as observed");
        }
        var method=typeof(AiStrategyPlanner).GetMethod("LessonForModel",BindingFlags.Static|BindingFlags.NonPublic)!;
        using var compact=JsonDocument.Parse(JsonSerializer.Serialize(method.Invoke(null,[lesson]),new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Check(compact.RootElement.GetProperty("decisionComparisons").GetProperty("changed").EnumerateArray()
            .Any(item=>item.GetProperty("position").ValueKind==JsonValueKind.Object&&item.GetProperty("position").GetProperty("ep").GetInt32()==2),
            "resource choice context not available to later learning");
    }

    internal static void RejectedMatch()
    {
        using var fixture=new Fixture(rejectPlans:true);
        var card=new CardInstance(Guid.NewGuid(),1,fixture.Own.PlayerId);
        fixture.Planner.ObserveCommittedEvents(fixture.Game,[new PlayCardEvent(fixture.Own,card),
            new GameEndEvent(fixture.Own.PlayerId,fixture.Enemy.PlayerId,"fallback fixture end")]);
        var lesson=Read(fixture.Planner);
        Check(lesson.Plans.Length==0&&lesson.PlanValidations is {Length:1} checks&&!checks[0].Repaired
            &&lesson.TurnDecisions is {Attempts:1,Accepted:0,Invalid:1}&&lesson.Played.SequenceEqual([1]),
            "all-rejected model match was lost or misclassified as successful model execution");
        var method=typeof(AiStrategyPlanner).GetMethod("LessonForModel",BindingFlags.Static|BindingFlags.NonPublic)!;
        using var payload=JsonDocument.Parse(JsonSerializer.Serialize(method.Invoke(null,[lesson]),new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Check(payload.RootElement.GetProperty("observed").GetProperty("modelParticipation").GetProperty("turns").GetProperty("accepted").GetInt32()==0
            &&payload.RootElement.GetProperty("rejectedPlanConstraints").GetArrayLength()==1
            &&payload.RootElement.GetProperty("observed").GetProperty("played").GetArrayLength()==1,
            "future learning confused failed proposals with committed fallback actions");
    }

    private sealed class Fixture:IDisposable
    {
        private readonly string root=Path.Combine(Path.GetTempPath(),"ai-trace-test-"+Guid.NewGuid().ToString("N"));
        private readonly Provider provider=new();
        internal AiStrategyPlanner Planner {get;}
        internal PlayerState Own {get;}=new(Guid.NewGuid()){IsAI=true,TurnNumber=3,InitialDeckDefinitionIds=[1,2]};
        internal PlayerState Enemy {get;}=new(Guid.NewGuid());
        internal GameState Game {get;}
        internal Fixture(bool rejectPlans=false) {
            provider.RejectPlans=rejectPlans;
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
                ["AiStrategy:Enabled"]="true",["AiStrategy:Endpoint"]="https://example.invalid/chat/completions",
                ["AiStrategy:Model"]="fixture",["AiStrategy:MemoryPath"]=root,["AiStrategy:GuideCacheEnabled"]="false"
            }).Build();
            Planner=new(provider,config,new Host(),NullLogger<AiStrategyPlanner>.Instance);
            Game=new("trace-fixture",Own,Enemy,Own.PlayerId);
            var snapshot=new GameStateDto{Players=[new(){PlayerId=Own.PlayerId,TurnNumber=3},new(){PlayerId=Enemy.PlayerId}]};
            Planner.WarmDeckAsync(Game,Own.PlayerId,"Neutral")!.GetAwaiter().GetResult();
            Planner.PlanAsync(Game,snapshot,Own.PlayerId,default,(_,_)=>Task.FromResult(new PlayabilityResultDto())).GetAwaiter().GetResult();
        }
        public void Dispose() {
            Planner.Dispose();provider.Dispose();var resolved=Path.GetFullPath(root);
            if(!resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)
                ||!Path.GetFileName(resolved).StartsWith("ai-trace-test-",StringComparison.Ordinal))throw new InvalidOperationException("Unexpected test path");
            if(Directory.Exists(resolved))Directory.Delete(resolved,true);
        }
    }
    private sealed class Host:IHostEnvironment {
        public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="Trace fixtures";
        public string ContentRootPath{get;set;}=".";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
    }
    private sealed class Provider:HttpMessageHandler,IHttpClientFactory {
        internal bool RejectPlans;
        public HttpClient CreateClient(string name)=>new(this,false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) {
            var content="""{"gamePlan":"fixture","cards":[{"id":1,"role":"tempo"},{"id":2,"role":"value"}],"focus":"value","plan":"fixture","prioritize":[],"hold":[]}""";
            if(RejectPlans)content=content[..^1]+""", "plays":[{"handIndex":99,"cardId":999,"mode":"Base","cost":1,"intent":"use"}]}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{
                choices=new[]{new{message=new{content}}}
            }))});
        }
    }
}
