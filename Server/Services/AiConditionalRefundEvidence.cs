using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;

namespace ShadowverseBeyond.Server.Services;

// A conditional arithmetic route is not executable credit. Reconcile the completed native root first.
internal static class AiConditionalRefundEvidence
{
    internal sealed record Facts(Guid Source,Guid Actor,Guid Leader,Guid Destroyed,int PpAfterPayment,int PpAfterRefund,int ActualSelfDamage);
    internal static AiPpRefundFacts.Refund? ForStep(AiStrategyPlanner.PlayAdvice step,IReadOnlyList<AiPpRefundFacts.Refund> refunds)
    {
        if(step.Mode!="Base"||step.Target is not {Side:"enemy",Zone:"field",Index:>=0,CardId:>0} target)return null;
        var matches=refunds.Where(r=>r.HandIndex==step.HandIndex&&r.CardId==step.CardId&&r.BaseCost==step.Cost
            &&r.DestroyTargetKey==target.Key&&(!target.AbilityDefinitionId.HasValue||target.AbilityDefinitionId==r.AbilityDefinitionId)).ToArray();
        return matches.Length==1?matches[0]:null;
    }
    internal sealed class Recorder(Guid actor,PlayCardActionDto action,Guid leader,Guid removed,int payment,int refund,int damage)
    {
        private readonly HashSet<Guid> events=[];
        private bool matched,paid,restored,destroyed,hit,invalid;
        internal void Observe(IReadOnlyList<IGameEvent> committed)
        {
            foreach(var item in committed){
                if(!events.Add(item.EventId))continue;if(events.Count>512){invalid=true;return;}
                if(item is PlayCardEvent play&&play.Source?.Player?.PlayerId==actor){
                    if(play.PlayedCard?.InstanceId==action.CardUid&&play.PlayMode==action.PlayMode&&play.PaidCost==action.PaidCost)matched=true;
                    else invalid=true;
                }
                if(item is PpUpdateEvent pp&&pp.TargetPlayer?.PlayerId==actor){if(pp.CurrentPp==payment)paid=true;if(pp.CurrentPp==refund)restored=true;}
                if(item is DestroyEvent destroy&&destroy.DestroySource?.InstanceId==action.CardUid&&destroy.DestroyTarget?.InstanceId==removed)destroyed=true;
                if(item is DamageEvent hurt&&hurt.DamageSource?.InstanceId==action.CardUid&&hurt.DamageTarget?.InstanceId==leader&&hurt.ActualDamage==damage)hit=true;
            }
        }
        internal Facts? Snapshot()=>!invalid&&matched&&paid&&restored&&destroyed&&hit?new(action.CardUid,actor,leader,removed,payment,refund,damage):null;
    }
    internal static Recorder? Begin(AiStrategyPlanner.TurnPlan? plan,Guid actor,string type,object data)
    {
        if(plan?.PlayLine is not {} line||plan.Play is not {} advice||type!="PlayCard"||data is not PlayCardActionDto action
            ||line.BeforeOwn.IsPpBoostArmed||line.BeforeOwn.LeaderCard is not {} leader||line.Steps[0].Original.InstanceId!=action.CardUid
            ||action.PlayMode!=line.Steps[0].Mode||action.PaidCost!=line.Steps[0].Cost)return null;
        var rule=ForStep(advice,line.Refunds);var target=advice.Target==null?null:AiPlannedTargets.Resolve(advice.Target,line.BeforeOwn,line.BeforeEnemy);
        if(rule==null||target==null||line.BeforeOwn.BloodPool<rule.PaidBlood||leader.CurrentHealth<=rule.OwnDamage)return null;
        var payment=line.BeforeOwn.CurrentPP-advice.Cost;
        return payment<0?null:new(actor,action,leader.InstanceId,target.Uid,payment,Math.Min(line.BeforeOwn.MaxPP,payment+rule.RestoreUpTo),rule.OwnDamage);
    }
    internal static bool Matches(Facts? facts,AiStrategyPlanner.TurnPlan plan,PlayerStateDto own,PlayerStateDto enemy)
    {
        if(facts==null||plan.PlayLine is not {} line||plan.Play is not {} advice)return false;
        var before=line.BeforeOwn;var rule=ForStep(advice,line.Refunds);
        if(rule==null||facts.Actor!=own.PlayerId||facts.Source!=line.Steps[0].Original.InstanceId||before.IsPpBoostArmed||own.IsPpBoostArmed
            ||before.LeaderCard?.InstanceId!=facts.Leader||own.LeaderCard?.InstanceId!=facts.Leader
            ||facts.PpAfterPayment!=before.CurrentPP-advice.Cost||facts.PpAfterRefund!=Math.Min(before.MaxPP,facts.PpAfterPayment+rule.RestoreUpTo)
            ||own.CurrentPP!=facts.PpAfterRefund||facts.ActualSelfDamage!=rule.OwnDamage
            ||own.LeaderCard.CurrentHealth!=before.LeaderCard.CurrentHealth-rule.OwnDamage
            ||own.BloodPool!=before.BloodPool-rule.PaidBlood+rule.OwnDamage)return false;
        var target=advice.Target==null?null:AiPlannedTargets.Resolve(advice.Target,before,line.BeforeEnemy);
        return target?.Uid==facts.Destroyed&&line.BeforeEnemy.Field.Any(c=>c.InstanceId==facts.Destroyed)
            &&enemy.Field.All(c=>c.InstanceId!=facts.Destroyed);
    }
}
