using System.Reflection;
using System.Text.Json;
using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class AiActionOutcomeTests
{
    private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    internal static void RootCompletionAndRaces()
    {
        using var f=new Fixture();
        var root=f.Begin();
        f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId));
        f.Commit(new HealEvent(f.Own.Leader,2,f.Source));
        Check(f.Facts.Count==0,"admission or the first transaction was treated as full root completion");
        root.MarkCompleted();f.End(root);f.End(root);
        Check(f.Facts.Count==1&&f.Facts[0].Before.OwnHealth==15&&f.Facts[0].After.OwnHealth==17
            &&!f.Facts[0].Before.BoostArmed&&f.Facts[0].After.BoostArmed&&f.Facts[0].After.BoostRemaining==2,
            "root result missed a triggered response, resource toggle or deduplication");
        var next=f.Begin();f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId));next.MarkCompleted();
        // Simulate the native game loop starting the next root before the first await continuation resumes.
        f.Commit(new HealEvent(f.Own.Leader,3,f.Source));
        Check(f.Facts.Count==2&&f.Facts[1].After.OwnHealth==17&&f.Own.Leader.CurrentHealth==20,
            "a subsequent root's response contaminated the previous completed action");
        f.End(next);Check(f.Facts.Count==2,"late executor continuation duplicated the result");
        var noMatch=f.Begin();noMatch.MarkCompleted();f.End(noMatch);Check(f.Facts.Count==2,"completion without matching commit fabricated a result");
        var rejected=f.Begin();f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId));rejected.MarkRejected("fixture rejection");f.End(rejected);
        var cancelled=f.Begin();f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId));cancelled.MarkCancelled("fixture cancellation");f.End(cancelled);
        var failed=f.Begin();f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId));failed.MarkFailed("fixture failure");f.End(failed);
        Check(f.Facts.Count==2,"partial rejected/cancelled/failed root was promoted as completed");
        var pending=f.Begin();var overlapping=f.Begin();f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId));
        overlapping.MarkCompleted();f.End(overlapping);Check(f.Facts.Count==2,"overlapping request stole an existing outcome window");
        pending.MarkCompleted();f.End(pending);Check(f.Facts.Count==3,"original root lease was lost");
        var played=f.AddOwnHandCard();var play=new PlayerActionWrapper("PlayCard",f.Own.PlayerId,new PlayCardActionDto{CardUid=played.InstanceId,PlayMode=PlayMode.Base,PaidCost=0});
        f.Begin(play);f.Commit(new PlayCardEvent(f.Own,played,PlayMode.Base,0));play.MarkCompleted();f.End(play);
        Check(f.Facts[^1].Action is {Kind:"PlayCard",CardId:2,PlayMode:"Base",PaidCost:0,HandIndex:0},
            "a local fallback lost the actual mode, paid cost or hand slot");
        Check(f.Http.Requests==0,"boundary tests requested a real provider");
    }
    internal static void TerminalPersistenceAndPrivacy()
    {
        using var f=new Fixture();
        // Hidden opponent identity must not enter before/after target snapshots, even for malformed requests.
        var bad=new PlayerActionWrapper("PerformAttack",f.Own.PlayerId,new AttackActionDto{AttackerUid=f.Source.InstanceId,TargetUid=f.Hidden.InstanceId});
        f.Begin(bad);f.Planner.ObserveCommittedEvents(f.Game,[new AttackEvent(f.Source,f.Hidden)]);bad.MarkCompleted();f.End(bad);
        var packed=JsonSerializer.Serialize(f.Facts);
        Check(f.Facts[0].TargetBefore==null&&f.Facts[0].TargetAfter==null&&!packed.Contains("2000000999")
            &&!packed.Contains(f.Own.PlayerId.ToString())&&!packed.Contains(f.Hidden.InstanceId.ToString()),"hidden target or runtime identities leaked into action evidence");
        for(var index=0;index<40;index++){
            var root=f.Begin();f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId));root.MarkCompleted();f.End(root);
        }
        Check(f.Facts.Count==32&&f.Facts[0].Action.Kind=="PerformAttack","long traces lost opening evidence or exceeded their limit");
        var last=f.Begin();f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId),new GameEndEvent(f.Own.PlayerId,f.Enemy.PlayerId,"fixture terminal boundary"));
        Check(!File.Exists(f.HistoryFile),"terminal experience was saved before the winning root completed");
        last.MarkCompleted();f.End(last);f.End(last);
        var lessons=JsonSerializer.Deserialize<AiStrategyPlanner.Lesson[]>(File.ReadAllText(f.HistoryFile),new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Check(lessons.Length==1&&lessons[0].CompletedActions?.Length==32&&lessons[0].CompletedActions![^1].Result=="won"
            &&lessons[0].Recording!.Sections["completedActions"].Omitted==10,"terminal save lost final root result or its missing-data scope");
        f.Planner.ObserveCommittedEvents(f.Game,[new GameEndEvent(f.Own.PlayerId,f.Enemy.PlayerId,"duplicate")]);
        Check(JsonSerializer.Deserialize<AiStrategyPlanner.Lesson[]>(File.ReadAllText(f.HistoryFile),new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Length==1,"duplicate end event appended another match");
        var method=typeof(AiStrategyPlanner).GetMethod("EvidenceForModel",BindingFlags.Static|BindingFlags.NonPublic)!;
        var model=JsonSerializer.SerializeToElement(method.Invoke(null,[lessons[0]]),new JsonSerializerOptions(JsonSerializerDefaults.Web)).GetProperty("completedActions");
        Check(model.GetProperty("included").GetInt32()==4&&model.GetProperty("omitted").GetInt32()==28
            &&model.GetProperty("samples")[3].GetProperty("result").GetString()=="won","model input lost bounded outcomes or their terminal result");
        var legacy=JsonSerializer.SerializeToElement(method.Invoke(null,[lessons[0] with{CompletedActions=null}]),new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Check(legacy.GetProperty("completedActions").ValueKind==JsonValueKind.Null,"legacy lack of observations became proof of no action effects");
    }
    internal static void CompletedDecisionAttribution()
    {
        using var f = new Fixture();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var compare = typeof(AiStrategyPlanner).Assembly.GetType("ShadowverseBeyond.Server.Services.AiCommittedAction")!
            .GetMethod("SameSelection", BindingFlags.Static | BindingFlags.NonPublic)!;
        var source = f.Source.InstanceId;
        var abilityId = Guid.NewGuid();
        bool? Same(string type, object a, object b) => (bool?)compare.Invoke(null, [type, a, type, b]);
        Check(Same("RequestAct", new ActActionDto { CardUid = source, AbilityRuntimeId = abilityId },
            new ActActionDto { CardUid = source, AbilityRuntimeId = Guid.NewGuid() }) == false,
            "different registered abilities on the same source were collapsed");
        Check(Same("PlayCard", new PlayCardActionDto { CardUid = source, PlayMode = PlayMode.Base, PaidCost = 0 },
            new PlayCardActionDto { CardUid = source, PlayMode = PlayMode.Base, PaidCost = 0 }) == true,
            "equivalent plays were counted as a change");
        var decision = new AiStrategyPlanner.ActionComparisonFact(f.Own.TurnNumber,
            new("PlayCard", 3, null, null, 1, HandIndex: 0),
            new("PerformPpBoost", null, null, null, 2))
            { SameSelection = false, SelectionPolicy = "plan-guided-ranking" };
        var rejected = f.Begin(comparison: decision);
        f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId));
        rejected.MarkRejected("not completed");
        f.End(rejected);
        Check(f.Facts.Count == 0, "a rejected action produced a learning result");
        for (var index = 0; index < 8; index++)
        {
            var root = f.Begin(comparison: index == 3 ? decision : null);
            if (index == 3)
                f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId), new HealEvent(f.Own.Leader, 2, f.Source));
            else if (index == 7)
                f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId), new GameEndEvent(f.Enemy.PlayerId, f.Own.PlayerId, "fixture loss"));
            else f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId));
            root.MarkCompleted();
            f.End(root);
        }
        var lesson = JsonSerializer.Deserialize<AiStrategyPlanner.Lesson[]>(File.ReadAllText(f.HistoryFile), options)!.Single();
        var saved = lesson.CompletedActions!;
        Check(saved.Length == 8 && saved[3].Decision is { Different: true, Local.CardId: 3 }
            && saved[3].After.OwnHealth == 17 && !lesson.Won
            && lesson.Recording!.EventTotals["differentCompletedActions"] == 1
            && saved.Where((_, index) => index != 3).All(action => action.Decision == null),
            "commit attribution or terminal persistence attached a comparison to the wrong action");
        var facts = typeof(AiStrategyPlanner).GetMethod("EvidenceForModel", BindingFlags.Static | BindingFlags.NonPublic)!;
        var payload = JsonSerializer.SerializeToElement(facts.Invoke(null, [lesson]), options);
        var samples = payload.GetProperty("completedActions").GetProperty("samples").EnumerateArray().ToArray();
        var paired = samples.Single(row => row.GetProperty("sourceIndex").GetInt32() == 3);
        Check(samples.Length == 4 && paired.GetProperty("decision").GetProperty("local").GetProperty("cardId").GetInt32() == 3
            && !paired.ToString().Contains(abilityId.ToString()) && f.Http.Requests == 0,
            "bounded learning excerpts omitted the change, leaked an ID or called a provider");
    }

    internal static void CommittedDiscoverComparison()
    {
        Fixture fixture = null!;
        var submitted = new List<PlayerChoiceRequest>();
        var record = typeof(AiStrategyPlanner).GetMethod("RecordOptionComparison", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var pending = Proxy<IPendingActionService>.Make((method, args) =>
        {
            if (method.ReturnType == typeof(bool)) return false;
            if (method.Name != "WaitForPlayerChoiceAsync") return null;
            var request = (PlayerChoiceRequest)args![1]!;
            submitted.Add(request);
            var local = new[] { request.SelectableTargetIds[0] };
            var selected = new[] { request.SelectableTargetIds[^1] };
            // Retry records are replaced; the committed event can be counted only once.
            record.Invoke(fixture.Planner, [fixture.Game, request, local, selected, "model-option"]);
            record.Invoke(fixture.Planner, [fixture.Game, request, local, selected, "model-option"]);
            return Task.FromResult(new PlayerChoiceResult { ChosenTargetIds = selected.ToList() });
        });
        using var f = fixture = new Fixture(pending);
        var root = f.Begin();
        var first = f.AddCard(2, CardZone.Deck);
        var second = f.AddCard(3, CardZone.Deck);
        var events = f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId),
            new DiscoverRequestEvent(f.Own, [first, second], false, Guid.Empty),
            new DiscoverRequestEvent(f.Own, [second, first], false, Guid.Empty),
            new GameEndEvent(f.Own.PlayerId, f.Enemy.PlayerId, "discover observation"));
        Check(submitted.Count == 2 && f.Own.Hand.Select(card => card.CurrentDefinitionId).SequenceEqual(new[] { 3, 2 }),
            "the native discover transaction did not add the two selected cards");
        root.MarkCompleted();
        f.End(root);
        var json = File.ReadAllText(f.HistoryFile);
        var lesson = JsonSerializer.Deserialize<AiStrategyPlanner.Lesson[]>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Single();
        var comparisons = lesson.Choices!.Select(choice => (choice.CardId, choice.Comparison)).ToArray();
        Check(comparisons.Length == 2 && comparisons[0] is (3, { Different: true })
            && comparisons[1] is (2, { Different: true })
            && comparisons[0].Comparison!.LocalCardIds!.SequenceEqual(new[] { 2 })
            && comparisons[1].Comparison!.LocalCardIds!.SequenceEqual(new[] { 3 })
            && lesson.Recording!.EventTotals["differentCommittedOptions"] == 2
            && !json.Contains(first.InstanceId.ToString()) && !json.Contains(second.InstanceId.ToString()),
            "discover evidence was misjoined, double-counted or leaked instance identity");
        var count = typeof(AiStrategyPlanner).GetMethod("CountDecisionDifferences", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Check(((ValueTuple<int, int>)count.Invoke(f.Planner, [f.Game])!).Item2 == 2 && f.Http.Requests == 0,
            "completed option differences did not reach the benchmark counter");
    }

    internal static void OwnHandEndpoints()
    {
        using var f=new Fixture();
        var used=f.AddOwnHandCard();var retained=f.AddOwnHandCard();
        var drawn=f.AddCard(3,CardZone.Deck);
        var root=f.Begin();f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId),
            new CardDiscardedEvent(f.Own,used,EventReasonSource.Ability),new DrawEvent(f.Own,1));
        root.MarkCompleted();f.End(root);
        var hand=f.Facts[^1].OwnHandChanges!;
        Check(hand is {BeforeCount:2,AfterCount:2}&&hand.Changes.Length==2
            &&hand.Changes.Single(c=>c.Change=="departed").Before is {CardId:2,Index:0}
            &&hand.Changes.Single(c=>c.Change=="arrived").After is {CardId:3,Index:1,Cost:4,Attack:3,Health:2}
            &&!hand.Changes.Any(c=>c.Change=="changed"),"same-size replacement or duplicate identities were reduced to counts/slot movement");
        var prior=JsonSerializer.Serialize(hand);
        // Actual native buff evaluation, not the definition's printed cost or a forecast.
        root=f.Begin();var buff=new Buff(Guid.Empty,drawn.InstanceId,6,[
            new StatBuffEffect([new(StatType.Cost,-4,ModifierLayer.Additive)]),new KeywordBuffEffect([CardKeyword.Storm])]);
        f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId),new BuffAppliedEvent(buff,drawn,f.Source));root.MarkCompleted();f.End(root);
        var change=f.Facts[^1].OwnHandChanges!.Changes.Single();
        Check(change.Change=="changed"&&change.Before is {CardId:3,Cost:4}&&change.After is {CardId:3,Cost:0}
            &&change.After.Keywords.Contains("Storm")&&JsonSerializer.Serialize(hand)==prior,
            "actual cost/keyword update missing or later buffs rewrote prior observed evidence");
        root=f.Begin();f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId));root.MarkCompleted();f.End(root);
        Check(f.Facts[^1].OwnHandChanges!.Changes.Length==0,"unchanged endpoint became an invented resource gain");
        root=f.Begin();var fleeting=f.AddCard(2,CardZone.Staging);
        f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId),new AddCardToHandEvent(f.Own,[fleeting]),
            new CardDiscardedEvent(f.Own,fleeting,EventReasonSource.Ability));root.MarkCompleted();f.End(root);
        Check(f.Facts[^1].OwnHandChanges!.Changes.Length==0,"transient hand card was misrepresented as present after completion");
        root=f.Begin();f.AddCard(2000000999,CardZone.Deck,f.Enemy);
        f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId),new DrawEvent(f.Enemy,1));root.MarkCompleted();f.End(root);
        Check(f.Facts[^1].OwnHandChanges!.Changes.Length==0,"enemy draw entered own-hand identities");
        root=f.Begin();f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId),new TransformEvent(retained,3));root.MarkCompleted();f.End(root);
        hand=f.Facts[^1].OwnHandChanges!;
        Check(hand.Changes.Single(c=>c.Change=="departed").Before?.CardId==2
            &&hand.Changes.Single(c=>c.Change=="arrived").After?.CardId==3,
            "native transform replacement did not retain its separate instance endpoints");
        var packed=JsonSerializer.Serialize(f.Facts);
        Check(!packed.Contains("2000000999")&&!packed.Contains("PRIVATE_CANARY")&&!packed.Contains(f.Hidden.InstanceId.ToString())
            &&!packed.Contains(f.Own.PlayerId.ToString())&&!packed.Contains(drawn.InstanceId.ToString()),"hand evidence leaked hidden identity or runtime GUIDs");
        var method=typeof(AiStrategyPlanner).Assembly.GetType("ShadowverseBeyond.Server.Services.AiActionOutcomes")!.GetMethod("ForModel",BindingFlags.NonPublic|BindingFlags.Static)!;
        var model=JsonSerializer.SerializeToElement(method.Invoke(null,[f.Facts.ToArray(),true]),new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Check(model.GetProperty("included").GetInt32()==4&&model.GetProperty("samples")[3].GetProperty("ownHandChanges").GetProperty("changes").GetArrayLength()==2,
            "model input omitted completed own hand transitions");
        var observer=typeof(AiStrategyPlanner).Assembly.GetType("ShadowverseBeyond.Server.Services.AiActionOutcomes")!;
        object? Invoke(string method,params object?[] args)=>observer.GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,args);
        foreach(var malformed in new[]{"oversize","wrong-owner","duplicate","wrong-zone","empty-identity"}) {
            var snapshot=f.Snapshot();var own=snapshot.Players.Single(p=>p.PlayerId==f.Own.PlayerId);
            var card=own.Hand[0];
            own.Hand=malformed switch {
                "oversize"=>Enumerable.Range(0,10).Select(_=>card with{InstanceId=Guid.NewGuid()}).ToList(),
                "wrong-owner"=>[card with{OwnerPlayerId=f.Enemy.PlayerId}],
                "duplicate"=>[card,card],"wrong-zone"=>[card with{Zone=CardZone.Deck}],_=>[card with{InstanceId=Guid.Empty}]};
            var request=new PlayerActionWrapper("PerformPpBoost",f.Own.PlayerId,new{});
            var lease=Invoke("Begin",f.Game,request,snapshot,null)!;
            var committed=f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId));
            Invoke("Observe",lease,f.Game,committed,null);
            request.MarkCompleted();
            Check(((AiCompletedActionFact)Invoke("Finish",lease)!).OwnHandChanges==null,malformed+" partial snapshot fabricated no hand changes");
        }
        var old=f.Facts[0] with{OwnHandChanges=null};
        Check(JsonSerializer.Deserialize<AiCompletedActionFact>(JsonSerializer.Serialize(old))?.OwnHandChanges==null,
            "unobserved legacy hand data became a claim of no changes");
        var last=f.Begin();f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId),new GameEndEvent(f.Own.PlayerId,f.Enemy.PlayerId,"hand outcome terminal"));
        last.MarkCompleted();f.End(last);
        var saved=JsonSerializer.Deserialize<AiStrategyPlanner.Lesson[]>(File.ReadAllText(f.HistoryFile),new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Check(saved.Single().CompletedActions![0].OwnHandChanges?.Changes.Length==2,"normal match persistence discarded own hand evidence");
        Check(f.Http.Requests==0,"own hand observations made a paid provider call");
    }
    internal sealed class Fixture:IDisposable
    {
        private readonly IGameDataProvider Prior=GameDataProvider.Instance;
        private readonly IGameContextAccessor? PriorAccessor=GameServiceProvider.ContextAccessor;
        private readonly string DirectoryPath=Path.Combine(Path.GetTempPath(),"ai-root-outcome-"+Guid.NewGuid().ToString("N"));
        private readonly ServiceProvider Services;private readonly IServiceScope Scope;private readonly GameContext Context;
        internal readonly PlayerState Own=new(Guid.NewGuid()){IsAI=true,TurnNumber=6,CurrentPlayPoints=3,MaxPlayPoints=6,CurrentEvolutionPoints=2,CurrentSuperEvolutionPoints=2,PpBoostUsesAvailable=2};
        internal readonly PlayerState Enemy=new(Guid.NewGuid()){TurnNumber=6};
        internal readonly GameState Game;internal readonly CardInstance Source,Hidden;
        internal readonly NoProvider Http=new();internal readonly AiStrategyPlanner Planner;
        internal readonly List<AiCompletedActionFact> Facts;
        internal string HistoryFile=>Path.Combine(DirectoryPath,new string('e',64)+".json");
        internal Fixture(IPendingActionService? pending = null)
        {
            var defs=new Dictionary<int,CardDefinition>{[1]=new(){Id=1,Name="leader",Type=CardType.Leader,Health=20},
                [2]=new(){Id=2,Name="public source",Type=CardType.Follower,Attack=1,Health=3},
                [3]=new(){Id=3,Name="public hand reward",Type=CardType.Follower,Cost=4,Attack=3,Health=2},
                [2000000999]=new(){Id=2000000999,Name="PRIVATE_CANARY",Type=CardType.Follower,Attack=9,Health=9}};
            var data=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch{"GetCardDefinition"=>defs.GetValueOrDefault((int)args![0]!),"GetAllCardDefinitions"=>defs,"GetAbilityConfigs"=>new List<AbilityConfig>(),_=>null});
            GameDataProvider.SetInstanceForTesting(data);var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
            Services=AiEndTurnEngineTests.Services(data,accessor,pending);Scope=Services.CreateScope();var sp=Scope.ServiceProvider;
            Game=new("root-outcome-native",Own,Enemy,Own.PlayerId){Phase=GamePhase.Main,Status=GameStatus.InProgress,AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<IBuffManager>()};
            var factory=sp.GetRequiredService<ICardFactory>();Own.Leader=factory.CreateAndRegisterNewCard(Game,1,Own.PlayerId,CardZone.Field);Enemy.Leader=factory.CreateAndRegisterNewCard(Game,1,Enemy.PlayerId,CardZone.Field);
            Own.IsFirst=false;Source=factory.CreateAndRegisterNewCard(Game,2,Own.PlayerId,CardZone.Field);Hidden=factory.CreateAndRegisterNewCard(Game,2000000999,Enemy.PlayerId,CardZone.Hand);Own.Leader.Damage=5;
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["AiStrategy:Enabled"]="false",["AiStrategy:MemoryPath"]=DirectoryPath}).Build();
            Planner=new(Http,config,new Host(),NullLogger<AiStrategyPlanner>.Instance);
            var table=typeof(AiStrategyPlanner).GetField("matches",HiddenFlags)!.GetValue(Planner)!;var type=table.GetType().GetGenericArguments()[1];var state=Activator.CreateInstance(type,true)!;
            type.GetField("PlayerId")!.SetValue(state,Own.PlayerId);type.GetField("DeckKey")!.SetValue(state,new string('e',64));type.GetField("Attempts")!.SetValue(state,1);table.GetType().GetMethod("Add")!.Invoke(table,[Game,state]);
            Facts=(List<AiCompletedActionFact>)type.GetProperty("CompletedActions")!.GetValue(state)!;
            Context=new(sp,sp.GetRequiredService<IMapper>(),accessor,Game,Own.PlayerId);
        }
        private const BindingFlags HiddenFlags=BindingFlags.Instance|BindingFlags.NonPublic;
        internal PlayerActionWrapper Begin(PlayerActionWrapper? request=null,AiStrategyPlanner.ActionComparisonFact? comparison=null){request??=new("PerformPpBoost",Own.PlayerId,new{});typeof(AiStrategyPlanner).GetMethod("BeginActionOutcome",HiddenFlags)!.Invoke(Planner,[Game,request,GameStateProjection.Create(Game,Context),comparison]);return request;}
        internal ShadowverseBeyond.Server.Dtos.GameStateDto Snapshot()=>GameStateProjection.Create(Game,Context);
        internal CardInstance AddOwnHandCard()=>Scope.ServiceProvider.GetRequiredService<ICardFactory>().CreateAndRegisterNewCard(Game,2,Own.PlayerId,CardZone.Hand);
        internal CardInstance AddCard(int id,CardZone zone,PlayerState? player=null)=>Scope.ServiceProvider.GetRequiredService<ICardFactory>().CreateAndRegisterNewCard(Game,id,(player??Own).PlayerId,zone);
        internal void End(PlayerActionWrapper request)=>typeof(AiStrategyPlanner).GetMethod("EndActionOutcome",HiddenFlags)!.Invoke(Planner,[Game,request]);
        internal IReadOnlyList<IGameEvent> Commit(params IGameEvent[] events){var result=Scope.ServiceProvider.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(Context,events).GetAwaiter().GetResult();Check(result.IsSuccess&&!result.IsCancelled,"native observation transaction failed");Planner.ObserveCommittedEvents(Game,result.ProcessedEvents);return result.ProcessedEvents;}
        public void Dispose(){Context.Dispose();Planner.Dispose();Http.Dispose();Scope.Dispose();Services.Dispose();GameDataProvider.SetInstanceForTesting(Prior);GameServiceProvider.ContextAccessor=PriorAccessor;
            var path=Path.GetFullPath(DirectoryPath);Check(path.StartsWith(Path.Combine(Path.GetTempPath(),"ai-root-outcome-"),StringComparison.OrdinalIgnoreCase),"unexpected cleanup path");if(Directory.Exists(path))Directory.Delete(path,true);}
    }
    internal sealed class NoProvider:HttpMessageHandler,IHttpClientFactory
    {internal int Requests;public HttpClient CreateClient(string name)=>new(this,false);protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Requests++;throw new InvalidOperationException("No provider expected");}}
    private sealed class Host:IWebHostEnvironment
    {public string ApplicationName{get;set;}="offline";public string EnvironmentName{get;set;}="Development";public string ContentRootPath{get;set;}=".";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();public string WebRootPath{get;set;}=".";public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();}
}
