using System.Reflection;
using System.Text.Json;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;

internal static class AiTargetOutcomeTests
{
    static readonly BindingFlags Hidden=BindingFlags.Static|BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    static void Check(bool b,string why){if(!b)throw new Exception(why);}
    static void Record(AiActionOutcomeTests.Fixture f,params Guid[] ids){var snapshot=f.Snapshot();var request=new PlayerChoiceRequest{PendingChoiceId=Guid.NewGuid(),RequestingPlayerId=f.Own.PlayerId,TargetKey="target1",MinSelectionCount=1,MaxSelectionCount=ids.Length,SelectableTargetIds=ids.ToList()};
        typeof(AiStrategyPlanner).GetMethod("RecordTargetSelection",Hidden)!.Invoke(f.Planner,[f.Game,snapshot,request,null,ids,ids,null]);}
    static AiObservedChoiceOutcomes Done(AiActionOutcomeTests.Fixture f,ShadowverseBeyond.Server.Models.PlayerActionWrapper root){root.MarkCompleted();f.End(root);return f.Facts.Last().ChoiceTargets!;}
    internal static void Run()
    {
        VisibleAndImmutable();ShieldEndpoints();HiddenAndCancelled();BoundsAndMissing();TerminalAndRetrieval();FocusedPack();
        Console.WriteLine("PASS native target endpoints: selection-time snapshot, damage/visibility, failed roots, frozen completion, terminal persistence, private zones, limits and relevant retrieval; provider calls=0");
    }
    static void VisibleAndImmutable()
    {
        using var f=new AiActionOutcomeTests.Fixture();var target=f.AddCard(2,CardZone.Field,f.Enemy);var root=f.Begin();Record(f,target.InstanceId);
        f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId),new DamageEvent(target,1,f.Source,DamageKind.Ability));
        Check(f.Facts.Count==0,"submitted target became completed evidence before root completion");
        var observed=Done(f,root);var chosen=observed.Requests.Single().Targets.Single();
        Check(chosen.Before.Health==3&&chosen.After?.Health==2&&chosen.Before.Side=="enemy"&&chosen.Observation=="visible","native selected target health endpoints missing");
        f.Commit(new DamageEvent(target,1,f.Source,DamageKind.Ability));f.End(root);
        Check(chosen.After.Health==2&&f.Facts.Count==1,"subsequent action mutated finished target endpoint");
        var text=JsonSerializer.Serialize(observed,Json);Check(!text.Contains(target.InstanceId.ToString())&&!text.Contains(f.Own.PlayerId.ToString()),"runtime identifiers persisted");
        // A new same-name instance must not be used as the old target's endpoint.
        var next=f.Begin();Record(f,target.InstanceId);f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId),new CardMovedEvent(target,CardZone.Field,CardZone.Graveyard));f.AddCard(2,CardZone.Field,f.Enemy);
        var removed=Done(f,next).Requests.Single().Targets.Single();Check(removed.After==null&&removed.Observation=="not-visible","target rebound by card definition/name");
        Check(f.Http.Requests==0,"observation requested a provider");
    }
    static void HiddenAndCancelled()
    {
        using var f=new AiActionOutcomeTests.Fixture();var target=f.AddCard(2,CardZone.Field,f.Enemy);var root=f.Begin();Record(f,target.InstanceId,f.Hidden.InstanceId);
        f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId),new CardMovedEvent(target,CardZone.Field,CardZone.Hand),new TransformEvent(target,2000000999,f.Source));
        var evidence=Done(f,root);Check(evidence.Requests.Single().TargetsSubmitted==2&&evidence.Requests.Single().Targets.Length==1,"hidden submitted target exposed or omission hidden");
        Check(evidence.Requests.Single().Targets[0] is {After:null,Observation:"not-visible"},"enemy hidden endpoint exposed");
        var text=JsonSerializer.Serialize(f.Facts,Json);Check(!text.Contains("2000000999")&&!text.Contains("PRIVATE_CANARY")&&!text.Contains(f.Hidden.InstanceId.ToString()),"hidden changed definition leaked");
        var cancelled=f.Begin();Record(f,f.Source.InstanceId);cancelled.MarkCancelled("fixture");f.End(cancelled);Check(f.Facts.Count==1,"cancelled root became completed target evidence");
    }
    static void ShieldEndpoints()
    {
        using var f=new AiActionOutcomeTests.Fixture();GameDataProvider.Instance.GetCardDefinition(3)!.Keywords=["Shield"];
        var target=f.AddCard(3,CardZone.Field,f.Enemy);var root=f.Begin();Record(f,target.InstanceId);var damage=new DamageEvent(target,1,f.Source,DamageKind.Ability);
        f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId),damage);var selected=Done(f,root).Requests.Single().Targets.Single();
        Check(damage.ActualDamage==0&&selected.Before.Health==2&&selected.After?.Health==2&&selected.Before.Keywords!.Contains("Shield")&&!selected.After.Keywords!.Contains("Shield"),"zero damage shield consumption not visible in target endpoints");
    }
    static void BoundsAndMissing()
    {
        using var f=new AiActionOutcomeTests.Fixture();var root=f.Begin();
        for(var i=0;i<12;i++)Record(f,Enumerable.Repeat(f.Source.InstanceId,8).ToArray());
        f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId));var evidence=Done(f,root);
        Check(evidence.SubmissionsObserved==12&&evidence.SubmissionsRetained==4&&evidence.SubmissionsOmitted==8&&evidence.Requests.Select(r=>r.Submission).SequenceEqual(new[]{1,2,11,12})&&evidence.Requests.All(r=>r.Targets.Length==4&&r.TargetsSubmitted==8),"target observation window not bounded or omissions wrong");
        var next=f.Begin();f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId));Record(f,f.Source.InstanceId);var unobserved=Done(f,next);
        Check(unobserved.Requests.Single().Targets.Single().Observation=="unobserved","missing post-choice commit invented target endpoint");
        var old=f.Begin();Record(f,f.Source.InstanceId);var table=typeof(AiStrategyPlanner).GetField("matches",Hidden)!.GetValue(f.Planner)!;object?[] a=[f.Game,null];table.GetType().GetMethod("TryGetValue")!.Invoke(table,a);var lease=a[1]!.GetType().GetField("OutcomeLease",Hidden)!.GetValue(a[1])!;var window=lease.GetType().GetField("Choices",Hidden)!.GetValue(lease)!;
        f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId));var accessor=GameServiceProvider.ContextAccessor;
        try{GameServiceProvider.ContextAccessor=new GameContextAccessor();window.GetType().GetMethod("Observe",Hidden)!.Invoke(window,[lease,f.Game]);}finally{GameServiceProvider.ContextAccessor=accessor;}
        var unavailable=Done(f,old);Check(unavailable.Requests.Single().Targets.Single().Observation=="unavailable","missing native context did not remain unknown");
    }
    static void TerminalAndRetrieval()
    {
        using var f=new AiActionOutcomeTests.Fixture();var target=f.AddCard(3,CardZone.Field,f.Enemy);var root=f.Begin();Record(f,target.InstanceId);f.Commit(new PpBoostRequestedEvent(f.Own.PlayerId),new DamageEvent(target,1,f.Source,DamageKind.Ability),new GameEndEvent(f.Own.PlayerId,f.Enemy.PlayerId,"fixture"));
        Check(!File.Exists(f.HistoryFile),"terminal target saved before root completed");Done(f,root);
        var saved=JsonSerializer.Deserialize<AiStrategyPlanner.Lesson[]>(File.ReadAllText(f.HistoryFile),Json)!.Single();Check(saved.CompletedActions!.Single().ChoiceTargets!.Requests.Single().Targets.Single().After?.Health==1,"terminal persistence lost native target outcome");
        var state=f.Snapshot();state.Players[0].Hand.Clear();state.Players[0].Field.Clear();
        var stamp=new AiDefinitionStamp("catalog","engine",3,0,0,true,[]);saved=saved with{DefinitionRules=stamp,Attacks=[]};
        var retrieval=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiLessonRetrieval")!;
        AiExperienceRetrievalFact Select(AiStrategyPlanner.Lesson lesson,GameStateDto snapshot){var selection=retrieval.GetMethod("Select",Hidden)!.Invoke(null,[new[]{lesson},"Neutral",stamp,snapshot,f.Own.PlayerId,(Func<AiStrategyPlanner.Lesson[],string,AiStrategyPlanner.Lesson[]>)((l,c)=>l),"test",Array.Empty<int>()])!;return (AiExperienceRetrievalFact)selection.GetType().GetProperty("Fact")!.GetValue(selection)!;}
        Check(Select(saved,state).Matches.Single().EnemyCardIds.SequenceEqual(new[]{3}),"completed selected target did not support relevant retrieval");
        state.Players[1].Field.Clear();state.Players[1].Hand.Add(new(){CardId=3});Check(Select(saved,state).RelevantSelected==0,"current enemy hidden hand affected target retrieval");
        var method=typeof(AiStrategyPlanner).GetMethod("EvidenceForModel",Hidden)!;var packed=JsonSerializer.SerializeToElement(method.Invoke(null,[saved]),Json).GetProperty("completedActions").GetProperty("samples")[0];
        Check(packed.GetProperty("choiceTargets").GetProperty("requests")[0].GetProperty("targets")[0].GetProperty("after").GetProperty("health").GetInt32()==1,"target outcome not delivered to model evidence");
        var legacy=saved with{CompletedActions=[saved.CompletedActions.Single() with{ChoiceTargets=null}]};Check(JsonSerializer.SerializeToElement(method.Invoke(null,[legacy]),Json).GetProperty("completedActions").GetProperty("samples")[0].GetProperty("choiceTargets").ValueKind==JsonValueKind.Null,"old null target evidence was invented");
    }
    static void FocusedPack()
    {
        var requests=new[]{1,2,7,8}.Select(i=>new AiObservedTargetRequest(i,null,"target","none",4,Enumerable.Range(1,4).Select(j=>new AiObservedChosenTarget(new(i*100+j,"enemy","field",j,1,1,1),null,"not-visible")).ToArray())).ToArray();
        var facts=new AiObservedChoiceOutcomes(8,4,4,requests);var t=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiTargetOutcomes")!;
        var packed=JsonSerializer.SerializeToElement(t.GetMethod("ForModel",Hidden)!.Invoke(null,[facts,new[]{202}]),Json);
        Check(packed.GetProperty("submissionsIncluded").GetInt32()==2&&packed.GetProperty("retainedSubmissionsNotIncluded").GetInt32()==2,"target model excerpt not bounded");
        Check(packed.GetProperty("requests")[0].GetProperty("targets")[0].GetProperty("before").GetProperty("cardId").GetInt32()==202&&packed.GetProperty("requests").EnumerateArray().All(r=>r.GetProperty("targetsIncluded").GetInt32()==2),"matched middle target clipped out of model excerpt");
        var identical=new AiObservedChosenTarget(new(2,"own","hand",0,0,1,1),null,"unobserved");
        var duplicates=new AiObservedChoiceOutcomes(1,1,0,[new(1,null,"x","none",2,[identical,identical])]);
        var same=JsonSerializer.SerializeToElement(t.GetMethod("ForModel",Hidden)!.Invoke(null,[duplicates,Array.Empty<int>()]),Json).GetProperty("requests")[0];
        Check(same.GetProperty("targetsIncluded").GetInt32()==2&&same.GetProperty("targets").GetArrayLength()==2,"equal-looking observations were merged or count invented");
    }
}
