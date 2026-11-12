using System.Runtime.CompilerServices;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;

namespace ShadowverseBeyond.Server.Services;

public sealed record AiCombatProjectionDecisionFact(int Turn,string Before,string After,int? SourceCardId,int? TargetCardId,
    bool OldLethal,bool NewLethal,string CommittedEvent);
public sealed record AiCombatProjectionMatchFacts(long EligibleDecisions,long SearchComparisons,long DifferentFirstSteps,
    long DifferentLethalClaims,long SubmittedChangedSteps,long CommittedChangedSteps,AiCombatProjectionDecisionFact[] Examples) {public string Baseline {get;init;}="disabled";}

// Offline policy comparison and bounded telemetry. Native game rules and daily matches do not store this state.
internal static class AiCombatEvaluation
{
    private sealed class State(Guid candidate,bool previous=false)
    {
        internal readonly Guid Candidate=candidate;
        internal readonly bool Previous=previous;
        internal readonly object Sync=new();
        internal long Eligible,Comparisons,Different,Lethal,Submitted,Committed;
        internal readonly Dictionary<Guid,Pending> Pending=[];
        internal readonly List<AiCombatProjectionDecisionFact> Examples=[];
    }
    internal sealed record Decision(AiTacticalSearch.Plan? Before,AiTacticalSearch.Plan? After,bool FirstChanged,bool LethalChanged,int Turn);
    private sealed record Pending(Guid Actor,object Data,AiStrategyPlanner.ActionOption Selected,Decision Decision)
    {internal bool Matched;}
    private static readonly ConditionalWeakTable<GameState,State> States=new();
    internal static void Start(GameState game,Guid candidate)
    {
        if(!game.IsLocalAiTraining||game.GetPlayerState(candidate)==null)throw new InvalidOperationException("Comparison requires a native local training seat");
        States.Add(game,new(candidate));
    }
    internal static void StartPrevious(GameState game,Guid candidate)
    {
        if(!game.IsLocalAiTraining||game.GetPlayerState(candidate)==null)throw new InvalidOperationException("Comparison requires a native local training seat");
        States.Add(game,new(candidate,true));
    }
    internal static bool ComparePrevious(GameState game,Guid actor)=>game.IsLocalAiTraining&&States.TryGetValue(game,out var state)&&state.Previous&&state.Candidate==actor;
    internal static bool ExpandedEnabled(GameState game,Guid actor)=>!game.IsLocalAiTraining||!States.TryGetValue(game,out var state)||!state.Previous||state.Candidate==actor;
    internal static bool Enabled(GameState game,Guid actor,bool configured)=>game.IsLocalAiTraining&&States.TryGetValue(game,out var state)?state.Previous||state.Candidate==actor:configured;
    internal static bool NeedsComparison(GameState game,Guid actor)=>game.IsLocalAiTraining&&States.TryGetValue(game,out var state)&&state.Candidate==actor;
    internal static Decision? Record(GameState game,Guid actor,GameStateDto snapshot,long revision,AiTacticalSearch.Plan? before,AiTacticalSearch.Plan? after)
    {
        if(!NeedsComparison(game,actor)||game.StateRevision!=revision||game.CurrentPlayerId!=actor||game.IsGameOver)return null;
        var state=States.GetValue(game,_=>throw new InvalidOperationException("Missing comparison"));
        bool Same(AiTacticalSearch.Move? a,AiTacticalSearch.Move? b)=>a==null&&b==null||AiTacticalCandidates.Same(a,b);
        var result=new Decision(before,after,!Same(before?.First,after?.First),(before?.ProjectsLethal??false)!=(after?.ProjectsLethal??false),snapshot.Players.Single(p=>p.PlayerId==actor).TurnNumber);
        lock(state.Sync){state.Eligible++;state.Comparisons++;if(result.FirstChanged)state.Different++;if(result.LethalChanged)state.Lethal++;}
        return result;
    }
    private static bool IsRequest(AiTacticalSearch.Move move,string kind,object data)=>kind==move.Type&&((kind,data) switch {
        ("PerformAttack",AttackActionDto attack)=>move.Source.InstanceId==attack.AttackerUid&&move.Target?.InstanceId==attack.TargetUid,
        ("PerformEvolve",EvolveActionDto evolve)=>Guid.TryParse(evolve.UnitUid,out var id)&&move.Source.InstanceId==id,
        ("PerformSuperEvolve",SuperEvolveActionDto evolve)=>Guid.TryParse(evolve.UnitUid,out var id)&&move.Source.InstanceId==id,
        ("PlayCard",PlayCardActionDto play)=>move.Source.InstanceId==play.CardUid&&move.Data is PlayCardActionDto expected&&play.PlayMode==expected.PlayMode&&play.PaidCost==expected.PaidCost,
        ("EndTurn",_)=>true,_=>false});
    internal static IDisposable? BeginSubmit(GameState game,Guid actor,Decision? decision,string kind,object data)
    {
        if(decision is not {FirstChanged:true,After.First:{ } move}||!NeedsComparison(game,actor)||!IsRequest(move,kind,data))return null;
        var state=States.GetValue(game,_=>throw new InvalidOperationException("Missing comparison"));
        var pending=new Pending(actor,data,new(kind,move.Source.CardId,move.Target?.CardId,null,0),decision);
        lock(state.Sync){state.Submitted++;state.Pending[actor]=pending;}
        return new Lease(()=>{lock(state.Sync){if(state.Pending.TryGetValue(actor,out var current)&&ReferenceEquals(current,pending))state.Pending.Remove(actor);}});
    }
    internal static void Observe(GameState game,IReadOnlyList<IGameEvent> events)
    {
        if(!game.IsLocalAiTraining||!States.TryGetValue(game,out var state))return;
        lock(state.Sync)foreach(var pending in state.Pending.Values) {
            if(pending.Matched)continue;
            var committed=events.FirstOrDefault(item=>AiCommittedAction.Matches(pending.Selected,pending.Data,pending.Actor,item,null));
            if(committed==null)continue;
            pending.Matched=true;state.Committed++;
            if(state.Examples.Count<16)state.Examples.Add(new(pending.Decision.Turn,pending.Decision.Before?.First.Type??"none",pending.Selected.Kind,
                pending.Selected.CardId,pending.Selected.TargetCardId,pending.Decision.Before?.ProjectsLethal??false,pending.Decision.After?.ProjectsLethal??false,committed.EventType.ToString()));
        }
    }
    internal static AiCombatProjectionMatchFacts? Snapshot(GameState game)
    {
        if(!States.TryGetValue(game,out var state))return null;
        lock(state.Sync)return new(state.Eligible,state.Comparisons,state.Different,state.Lethal,state.Submitted,state.Committed,state.Examples.ToArray()){Baseline=state.Previous?"previous-finite-coverage":"disabled"};
    }
    private sealed class Lease(Action release):IDisposable
    {private Action? remaining=release;public void Dispose()=>Interlocked.Exchange(ref remaining,null)?.Invoke();}
}
