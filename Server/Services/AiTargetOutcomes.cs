using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

public sealed record AiObservedTargetState(int CardId,string Side,string Zone,int Index,int Cost,int? Attack,int? Health,string[]? Keywords=null,int KeywordsOmitted=0);
public sealed record AiObservedChosenTarget(AiObservedTargetState Before,AiObservedTargetState? After,string Observation);
public sealed record AiObservedTargetRequest(int Submission,int? SourceCardId,string Key,string AdviceStatus,int TargetsSubmitted,
    AiObservedChosenTarget[] Targets);
public sealed record AiObservedChoiceOutcomes(int SubmissionsObserved,int SubmissionsRetained,int SubmissionsOmitted,
    AiObservedTargetRequest[] Requests,
    string Scope="Instrumented normal AI card-target submissions within this completed native root; other selector branches may be unobserved, and zero does not prove no choices. Before is selection time, after is the last observed commit. Responses/other effects may intervene; no per-card causal attribution. Not-visible does not prove destruction, discard or death. Only own hand and public field/leader are observed. At most four submissions, opening/recent halves, and four targets each. Old null records were not collected."){public int ObservationFailures{get;init;}}

// Observation only: never resolves a query, changes a choice or looks inside enemy hidden zones.
internal static class AiTargetOutcomes
{
    private sealed class Pending(int ordinal,int? source,string key,string status,int submitted,List<(Guid Uid,AiObservedTargetState Before)> targets)
    {
        internal readonly int Ordinal=ordinal,Submitted=submitted;
        internal readonly int? Source=source;
        internal readonly string Key=key,Status=status;
        internal readonly List<(Guid Uid,AiObservedTargetState Before)> Targets=targets;
        internal AiObservedChosenTarget[]? After;
    }
    internal sealed class Window
    {
        private int observed,failures;
        private readonly List<Pending> pending=[];
        internal void Capture(AiActionOutcomes.Lease lease,GameState game,GameStateDto snapshot,PlayerChoiceRequest request,Ability? source,IReadOnlyList<Guid> selected,string? status)
        {
            try{CaptureCore(lease,game,snapshot,request,source,selected,status);}
            catch(Exception e)when(e is not OperationCanceledException and not OutOfMemoryException){failures++;}
        }
        private void CaptureCore(AiActionOutcomes.Lease lease,GameState game,GameStateDto snapshot,PlayerChoiceRequest request,Ability? source,IReadOnlyList<Guid> selected,string? status)
        {
            if(lease.Request.Completion.IsCompleted||request.RequestingPlayerId!=lease.Request.PlayerId||snapshot.GameId!=game.GameId
                ||game.GetPlayerState(request.RequestingPlayerId)?.TurnNumber!=lease.Turn)return;
            observed++;
            var own=snapshot.Players.FirstOrDefault(p=>p.PlayerId==lease.Request.PlayerId);var enemy=snapshot.Players.FirstOrDefault(p=>p.PlayerId!=lease.Request.PlayerId);
            if(own==null||enemy==null||selected.Count>64||selected.Any(id=>!request.SelectableTargetIds.Contains(id)))return;
            var targets=selected.Take(4).Select(uid=>(Uid:uid,Before:Read(snapshot,lease.Request.PlayerId,uid))).Where(t=>t.Before!=null).Select(t=>(t.Uid,t.Before!)).ToList();
            var card=source?.SourceCard;
            var visibleSource=card==null?null:Read(snapshot,lease.Request.PlayerId,card.InstanceId);
            // A played spell may be off the board already. Use its known pre-submission identity,
            // never its current hidden definition after an unseen move/transformation.
            var sourceId=visibleSource?.CardId??(card?.OwnerPlayerId==lease.Request.PlayerId&&lease.ActorUid==card.InstanceId?lease.Action.CardId:null);
            // Only program status codes are retained, never free-form model reasons or arbitrary identifiers.
            var safeStatus=status is "selected" or "rejected" or "blocked" or "other-key"?status:"none";
            var key=request.TargetKey is {Length:<=64} k?k:"";
            if(pending.Count==4)pending.RemoveAt(2);
            pending.Add(new(observed,sourceId,key,safeStatus,selected.Count,targets));
        }
        internal void Observe(AiActionOutcomes.Lease lease,GameState game)
        {
            if(lease.Request.Completion.IsCompleted)return;
            var memo=new Dictionary<Guid,(AiObservedTargetState? State,bool Known)>();
            foreach(var sample in pending)sample.After=sample.Targets.Select(t=>{
                try{if(!memo.TryGetValue(t.Uid,out var observed)){observed=Read(game,lease.Request.PlayerId,t.Uid);memo[t.Uid]=observed;}
                    var (state,known)=observed;
                    return new AiObservedChosenTarget(t.Before,state,!known?"unavailable":state==null?"not-visible":"visible");}
                catch(Exception e)when(e is not OperationCanceledException and not OutOfMemoryException){failures++;return new AiObservedChosenTarget(t.Before,null,"unavailable");}
            }).ToArray();
        }
        internal AiObservedChoiceOutcomes? Snapshot()=>observed==0&&failures==0?null:new(observed,pending.Count,observed-pending.Count,pending.Select(p=>
            new AiObservedTargetRequest(p.Ordinal,p.Source,p.Key,p.Status,p.Submitted,p.After??p.Targets.Select(t=>new AiObservedChosenTarget(t.Before,null,"unobserved")).ToArray())).ToArray()){ObservationFailures=failures};
    }
    internal static object? ForModel(AiObservedChoiceOutcomes? facts,int[]? focus=null)
    {
        if(facts==null)return null;var ids=(focus??[]).ToHashSet();
        bool Relevant(AiObservedChosenTarget t)=>ids.Contains(t.Before.CardId)||t.After!=null&&ids.Contains(t.After.CardId);
        var chosen=facts.Requests.Where(r=>r.Targets.Any(Relevant)).Concat(facts.Requests.Reverse()).DistinctBy(r=>r.Submission).Take(2).OrderBy(r=>r.Submission).ToArray();
        return new{facts.SubmissionsObserved,facts.SubmissionsRetained,facts.SubmissionsOmitted,facts.ObservationFailures,submissionsIncluded=chosen.Length,
            retainedSubmissionsNotIncluded=facts.Requests.Length-chosen.Length,requests=chosen.Select(r=>{
                var targets=r.Targets.Select((t,i)=>(t,i)).Where(x=>Relevant(x.t)).Concat(r.Targets.Select((t,i)=>(t,i)).Reverse()).DistinctBy(x=>x.i).Take(2).OrderBy(x=>x.i).Select(x=>x.t).ToArray();
                return new{r.Submission,r.SourceCardId,r.Key,r.AdviceStatus,r.TargetsSubmitted,targetsRetained=r.Targets.Length,targetsIncluded=targets.Length,targets};})};
    }
    private static AiObservedTargetState? Read(GameStateDto snapshot,Guid actor,Guid uid)
    {
        foreach(var player in snapshot.Players){var side=player.PlayerId==actor?"own":"enemy";
            AiObservedTargetState? Find(IReadOnlyList<CardInstanceDto> cards,string zone){for(var i=0;i<cards.Count;i++){
                var c=cards[i];if(c.OwnerPlayerId!=player.PlayerId||c.InstanceId!=uid&&!(zone=="leader"&&uid==player.PlayerId))continue;
                var keys=c.Keywords.Select(k=>k.ToString()).Distinct().Order().ToArray();
                return new(c.CardId,side,zone,i,c.CurrentCost,c.Type==CardType.Follower?c.CurrentAttack:null,c.Type is CardType.Follower or CardType.Leader?c.CurrentHealth:null,keys.Take(32).ToArray(),Math.Max(0,keys.Length-32));
            }return null;}
            if(side=="own"&&Find(player.Hand,"hand") is {} hand)return hand;
            if(Find(player.Field,"field") is {} field)return field;
            if(player.LeaderCard!=null&&Find([player.LeaderCard],"leader") is {} leader)return leader;
        }return null;
    }
    private static (AiObservedTargetState? State,bool Known) Read(GameState game,Guid actor,Guid uid)
    {
        var context=GameServiceProvider.CurrentGameContext;
        if(context==null||!ReferenceEquals(context.CurrentGameState,game))return(null,false);
        foreach(var player in new[]{game.GetPlayerState(actor),game.GetOpponentPlayerState(actor)}){
            if(player==null)return(null,false);var side=player.PlayerId==actor?"own":"enemy";
            AiObservedTargetState? Find(IReadOnlyList<CardInstance> cards,string zone){for(var i=0;i<cards.Count;i++){
                var c=cards[i];if(c.OwnerPlayerId!=player.PlayerId||c.InstanceId!=uid&&!(zone=="leader"&&uid==player.PlayerId))continue;
                var keys=c.GetKeywords(context).Select(k=>k.ToString()).Distinct().Order().ToArray();
                return new(c.CurrentDefinitionId,side,zone,i,c.GetCurrentCost(context),c.CardType==CardType.Follower?c.GetCurrentAttack(context):null,
                    c.CardType is CardType.Follower or CardType.Leader?c.GetMaxHealth(context)-c.Damage:null,keys.Take(32).ToArray(),Math.Max(0,keys.Length-32));
            }return null;}
            if(side=="own"&&Find(player.Hand,"hand") is {} hand)return(hand,true);
            if(Find(player.Field,"field") is {} field)return(field,true);
            if(player.Leader!=null&&Find([player.Leader],"leader") is {} leader)return(leader,true);
        }return(null,true);
    }
}
