using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;

namespace ShadowverseBeyond.Server.Services;

// Follow only an explicitly checked line, reconciling each committed step with the real state.
internal static class AiPlayLineExecution
{
    internal sealed record Continuation(AiPlayCandidates.Candidate[] Steps,PlayerStateDto BeforeOwn,
        PlayerStateDto BeforeEnemy,AiStrategyPlanner.HandLossRule[] Rules)
    {internal AiPpRefundFacts.Refund[] Refunds{get;init;}=[];}
    internal sealed record AdvanceResult(AiStrategyPlanner.TurnPlan Plan,string Status,string? Reason,int CardId,string Mode)
    {internal bool RefundReconciled{get;init;}}

    internal static Continuation? Bind(AiStrategyPlanner.TurnPlan plan,IReadOnlyList<AiPlayCandidates.Candidate> candidates,
        PlayerStateDto own,PlayerStateDto enemy,IReadOnlyList<AiStrategyPlanner.HandLossRule> rules,IReadOnlyList<AiPpRefundFacts.Refund>? refunds=null)
    {
        if(!plan.LineConstraintsChecked || plan.Plays is not {Length:>1})return null;
        var steps=plan.Plays.Select(step=>candidates.SingleOrDefault(candidate=>candidate.HandIndex==step.HandIndex
            &&candidate.Original.CardId==step.CardId&&candidate.Mode.ToString()==step.Mode&&candidate.Cost==step.Cost)).ToArray();
        return steps.Any(step=>step==null)?null:new(steps.OfType<AiPlayCandidates.Candidate>().ToArray(),own,enemy,rules.ToArray()){Refunds=(refunds??[]).ToArray()};
    }
    internal static AdvanceResult Advance(AiStrategyPlanner.TurnPlan plan,GameStateDto after,Guid actor,string actionType,object actionData,AiPlayDrawEvidence.Facts? draws=null,AiConditionalRefundEvidence.Facts? refundEvidence=null)
    {
        var line=plan.PlayLine!;var step=line.Steps[0];
        AdvanceResult Stop(string reason)=>new(plan with {Play=null,Plays=null,PlayBinding=null,PlayLine=null,LineConstraintsChecked=false,ConditionalBudgetSteps=[]},
            "interrupted",reason,step.Original.CardId,step.Mode.ToString());
        if(actionType!="PlayCard" || actionData is not PlayCardActionDto action || action.CardUid!=step.Original.InstanceId
            ||action.PlayMode!=step.Mode || action.PaidCost!=step.Cost)return Stop("实际优先执行了其他动作，原出牌路线中止。");
        var own=after.Players.FirstOrDefault(player=>player.PlayerId==actor);
        var enemy=after.Players.FirstOrDefault(player=>player.PlayerId!=actor);
        // Caller advances only after the native root completed. There is no stale future intent after the last step.
        if(own!=null&&enemy!=null&&line.Steps.Length==1)return new(plan with{Play=null,Plays=null,PlayBinding=null,PlayLine=null,LineConstraintsChecked=false,ConditionalBudgetSteps=[]},"finished",null,step.Original.CardId,step.Mode.ToString());
        if(own==null||enemy==null||after.CurrentPlayerId!=actor||after.Phase!=GamePhase.Main
            ||own.TurnNumber!=line.BeforeOwn.TurnNumber)return Stop("回合或对局阶段已经变化。");
        var before=line.BeforeOwn;var beforeEnemy=line.BeforeEnemy;
        static int Pp(PlayerStateDto player)=>player.CurrentPP+(player.IsPpBoostArmed?GameConstants.PlayPoints.PpBoostAmount:0);
        var refunded=AiConditionalRefundEvidence.Matches(refundEvidence,plan,own,enemy);
        if((!refunded&&Pp(own)!=Pp(before)-step.Cost) || own.MaxPP!=before.MaxPP ||own.CurrentEP!=before.CurrentEP||own.CurrentSEP!=before.CurrentSEP)
            return Stop("真实资源变化超出原路线的逐笔费用预期。");
        var remainingIndexes=AiPlaySequence.RemainingHand(before,step.HandIndex,line.Rules);
        var expectedHand=remainingIndexes.Select(index=>before.Hand[index]).ToList();
        var observedDraw=AiPlayDrawEvidence.Matches(draws,step.Original.InstanceId,before,own,expectedHand);
        if((!SameCards(expectedHand,own.Hand)||own.DeckCount!=before.DeckCount)&&!observedDraw
            ||enemy.DeckCount!=beforeEnemy.DeckCount||enemy.Hand.Count!=beforeEnemy.Hand.Count)
            return Stop("真实手牌、抽牌或附加效果发生了未预期变化。");
        var expectedLeader=refunded?before.LeaderCard! with{CurrentHealth=before.LeaderCard.CurrentHealth-refundEvidence!.ActualSelfDamage}:before.LeaderCard;
        var expectedEnemyField=refunded?beforeEnemy.Field.Where(c=>c.InstanceId!=refundEvidence!.Destroyed).ToList():beforeEnemy.Field;
        if(!SameLeader(expectedLeader,own.LeaderCard,false)||!SameLeader(beforeEnemy.LeaderCard,enemy.LeaderCard,true)
            ||!SameCards(before.Emblem,own.Emblem)||!SameCards(beforeEnemy.Emblem,enemy.Emblem)
            ||!SameCards(expectedEnemyField,enemy.Field))return Stop("主战者或公开场面变化需要重新判断。");
        var newBody=own.Field.FirstOrDefault(card=>card.InstanceId==step.Original.InstanceId);
        if(step.View.Type is CardType.Follower or CardType.Amulet)
        {
            if(newBody==null||own.Field.Last().InstanceId!=newBody.InstanceId||newBody.CardId!=step.PlayedCardId||newBody.Type!=step.View.Type
                ||newBody.CurrentAttack!=step.View.CurrentAttack||newBody.CurrentHealth!=step.View.CurrentHealth||newBody.MaxHealth!=step.View.MaxHealth
                ||newBody.CurrentCountdown!=step.View.CurrentCountdown||newBody.IsEvolved||newBody.IsSuperEvolved
                ||AiStrategyPlanner.CardEffectKey(newBody)!=AiStrategyPlanner.CardEffectKey(step.View)
                ||!newBody.Keywords.Order().SequenceEqual(step.View.Keywords.Order()))return Stop("新入场卡的真实形态或状态超出原路线预期。");
        }
        else if(newBody!=null)return Stop("法术结算产生了未预期的战场实体。");
        if(!SameCards(before.Field,own.Field.Where(card=>card.InstanceId!=step.Original.InstanceId).ToList()))
            return Stop("原有战场卡片发生了未预期变化。");
        var pending=line.Steps.Skip(1).Select(candidate=> {
            var index=own.Hand.FindIndex(card=>card.InstanceId==candidate.Original.InstanceId);
            return index<0?null:candidate with {HandIndex=index,Original=own.Hand[index]};
        }).ToArray();
        if(pending.Any(candidate=>candidate==null))return Stop("计划中的后续手牌已不在手中。");
        var next=pending.OfType<AiPlayCandidates.Candidate>().ToArray();
        if(next.Length==0)return new(plan with {Play=null,Plays=null,PlayBinding=null,PlayLine=null,LineConstraintsChecked=false,ConditionalBudgetSteps=[]},
            "finished",null,step.Original.CardId,step.Mode.ToString());
        var advice=new List<AiStrategyPlanner.PlayAdvice>();
        for(var i=0;i<next.Length;i++) {
            var hint=plan.Plays?.ElementAtOrDefault(i+1)?.Target;
            var rebased=hint==null?null:AiPlannedTargets.Rebase(hint,before,beforeEnemy,own,enemy);
            if(hint!=null&&rebased==null)return Stop("目标建议对应的实例已离开可见区域，重新判断。");
            var candidate=next[i];advice.Add(new(candidate.HandIndex,candidate.Original.CardId,candidate.Mode.ToString(),candidate.Cost,"use",rebased,plan.Plays?.ElementAtOrDefault(i+1)?.Choices));
        }
        var rules=line.Rules.Select(rule=>new AiStrategyPlanner.HandLossRule(
            own.Hand.FindIndex(card=>card.InstanceId==before.Hand[rule.DiscardedHandIndex].InstanceId),
            rule.WhenPlayedHandIndexes.Select(index=>own.Hand.FindIndex(card=>card.InstanceId==before.Hand[index].InstanceId)).Where(index=>index>=0).ToArray(),
            rule.AbilityDefinitionId)).Where(rule=>rule.DiscardedHandIndex>=0).ToArray();
        var updated=plan with {Play=advice[0],Plays=advice.ToArray(),
            PlayBinding=new(advice[0],next[0].Original.InstanceId,AiStrategyPlanner.PositionKey(own,enemy)){PlayedCardId=next[0].PlayedCardId},
            PlayLine=new(next,own,enemy,rules)};
        var remainingRefunds=line.Refunds.Select(r=>r with{HandIndex=own.Hand.FindIndex(c=>c.InstanceId==before.Hand[r.HandIndex].InstanceId)}).Where(r=>r.HandIndex>=0).ToArray();
        updated=updated with{PlayLine=updated.PlayLine! with{Refunds=remainingRefunds}};
        if(!AiPlaySequence.Validate(updated,next,rules,own,conditionalRefunds:remainingRefunds).Valid)return Stop("剩余路线不再满足费用或已知自动舍弃约束。");
        return new(updated,observedDraw||refunded?"needs-revalidation":"continued",null,step.Original.CardId,step.Mode.ToString()){RefundReconciled=refunded};
    }
    private static bool SameCards(IReadOnlyList<CardInstanceDto> before,IReadOnlyList<CardInstanceDto> after)=>
        before.Count==after.Count&&before.Zip(after).All(pair=>pair.First.InstanceId==pair.Second.InstanceId
            &&AiStrategyPlanner.CardKey(pair.First)==AiStrategyPlanner.CardKey(pair.Second));
    private static bool SameLeader(CardInstanceDto? before,CardInstanceDto? after,bool allowDamage)
    {
        if(before==null||after==null)return before==after;
        if(before.InstanceId!=after.InstanceId)return false;
        if(allowDamage&&after.CurrentHealth<=before.CurrentHealth)after=after with {CurrentHealth=before.CurrentHealth};
        return AiStrategyPlanner.CardKey(before)==AiStrategyPlanner.CardKey(after);
    }
}
