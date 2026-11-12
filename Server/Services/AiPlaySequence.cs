using System.Text.RegularExpressions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

// Rule-derived hand constraints for a proposed sequence. Does not simulate arbitrary effects.
internal static class AiPlaySequence
{
    private static string Normalize(string? expression)=>Regex.Replace(expression ?? "",@"[\s()]","").ToLowerInvariant();

    internal static async Task<AiStrategyPlanner.HandLossRule[]> ReadHandLossAsync(GameContext context, PlayerStateDto player)
    {
        var owner=context.CurrentGameState.GetPlayerState(player.PlayerId)!;
        var evaluator=context.ServiceProvider.GetRequiredService<IConditionEvaluator>();
        var rules=new List<AiStrategyPlanner.HandLossRule>();
        foreach(var (view,index) in player.Hand.Select((card,index)=>(card,index)))
        {
            var source=owner.Hand.FirstOrDefault(card=>card.InstanceId==view.InstanceId);
            if(source==null)continue;
            foreach(var ability in context.AbilityManager.GetEffectiveAbilitiesForCard(source.InstanceId,context).OfType<Ability>())
            {
                var config=ability.Config;
                if(!config.IsEnabled || !config.Trigger.IsEnabled || !config.Trigger.EventTypes.Contains(EventType.PlayCard)
                    || config.Trigger.Timing!=TriggerTiming.On || config.Trigger.CountLimit!=null
                    || config.Trigger.LimitSameNameOncePerTransaction || config.Trigger.RelationType!=TriggerRelationType.TargetOther
                    || !config.Trigger.IsValidZone(CardZone.Hand) || config.Auras.Count>0
                    || config.PreConditions.Any(condition=>condition.IsEnabled))continue;
                // Restrict to predicates whose result is invariant over the simulated hand changes.
                if(config.Conditions.Where(condition=>condition.IsEnabled).Any(condition=>Normalize(condition.Expression) is not
                    ("true" or "context.triggeringevent.playingplayer.playerid==context.owner.playerid")))continue;
                var effects=config.Effects.Where(effect=>effect.IsEnabled).ToArray();
                if(effects.Length!=1 || effects[0].Type!=EffectType.Discard || effects[0].RepeatCount!=0
                    || effects[0].RepeatCountValue!=null || effects[0].RepeatCondition!=null || effects[0].ChainedAbilities.Count>0)continue;
                var targets=config.Targets.Where(target=>target.Value.IsEnabled).ToArray();
                if(targets.Length!=1 || Normalize(targets[0].Value.Query.SourcePoolExpression)!="context.sourcecard"
                    || targets[0].Value.Query.Operations.Count>0 || targets[0].Value.PlayerChoiceOptions?.IsRequired==true
                    || effects[0].TargetSelector.Any(key=>key!=targets[0].Key && key!="*"))continue;
                var when=new List<int>();
                foreach(var (other,playedIndex) in player.Hand.Select((card,index)=>(card,index)))
                {
                    if(other.InstanceId==source.InstanceId)continue;
                    var played=owner.Hand.FirstOrDefault(card=>card.InstanceId==other.InstanceId);
                    if(played==null)continue;
                    var ev=new PlayCardEvent(owner,played);
                    var evaluation=context with {SourceCard=source,SourceAbility=ability,TriggeringEvent=ev};
                    if(await ability.CanTrigger(ev,evaluation) && config.Conditions.Where(condition=>condition.IsEnabled)
                        .All(condition=>evaluator.Evaluate(condition,evaluation)))when.Add(playedIndex);
                }
                if(when.Count>0)rules.Add(new(index,when.ToArray(),config.Id));
            }
        }
        return rules.DistinctBy(rule=>string.Join(',',rule.WhenPlayedHandIndexes)+":"+rule.DiscardedHandIndex).ToArray();
    }

    internal static int[] RemainingHand(PlayerStateDto player,int playedIndex,IReadOnlyList<AiStrategyPlanner.HandLossRule> rules) =>
        Enumerable.Range(0,player.Hand.Count).Where(index=>index!=playedIndex
            && !rules.Any(rule=>rule.DiscardedHandIndex==index && rule.WhenPlayedHandIndexes.Contains(playedIndex))).ToArray();

    internal static AiStrategyPlanner.PlayAdvice[][] Reorderings(AiStrategyPlanner.TurnPlan plan,
        IReadOnlyList<AiPlayCandidates.Candidate> candidates,IReadOnlyList<AiStrategyPlanner.HandLossRule> rules,PlayerStateDto player,
        IReadOnlyList<AiSelectedHandLoss.Rule>? selectedHandLoss=null,IReadOnlyList<AiPpRefundFacts.Refund>? conditionalRefunds=null)
    {
        if(plan.Plays is not {Length:>1 and <=4} line || line.Any(step=>step==null))return [];
        var results=new List<AiStrategyPlanner.PlayAdvice[]>();
        void Visit(List<AiStrategyPlanner.PlayAdvice> prefix,List<AiStrategyPlanner.PlayAdvice> rest) {
            if(rest.Count==0) {
                var reordered=prefix.ToArray();
                if(Validate(plan with {Play=reordered[0],Plays=reordered},candidates,rules,player,selectedHandLoss,conditionalRefunds).Valid
                    &&!results.Any(existing=>existing.SequenceEqual(reordered)))results.Add(reordered);
                return;
            }
            for(var i=0;i<rest.Count;i++)Visit(prefix.Append(rest[i]).ToList(),rest.Where((_,index)=>index!=i).ToList());
        }
        Visit([],line.ToList());
        return results.Take(6).ToArray();
    }

    internal sealed record BudgetExample(AiStrategyPlanner.PlayAdvice[] Plays,int TotalCost,int RemainingPP,bool SearchComplete);

    // A bounded arithmetic aid, not an effect/target simulation or a recommendation to play these cards.
    internal static BudgetExample Example(AiPlayCandidates.Candidate first,IReadOnlyList<AiPlayCandidates.Candidate> candidates,
        IReadOnlyList<AiStrategyPlanner.HandLossRule> rules,PlayerStateDto player)
    {
        var budget=player.CurrentPP+(player.IsPpBoostArmed?Domain.Constants.GameConstants.PlayPoints.PpBoostAmount:0);
        AiStrategyPlanner.PlayAdvice Advice(AiPlayCandidates.Candidate card)=>new(card.HandIndex,card.Original.CardId,card.Mode.ToString(),card.Cost,"use");
        var cheapest=candidates.GroupBy(card=>card.HandIndex).Select(group=>group.OrderBy(card=>card.Cost)
            .ThenBy(card=>card.Mode).First()).OrderBy(card=>card.HandIndex).ToArray();
        var best=new[]{Advice(first)};var bestCost=first.Cost;var nodes=0;var complete=true;
        void Visit(List<AiStrategyPlanner.PlayAdvice> path,HashSet<int> remaining,int pp,int paid)
        {
            if(++nodes>4096){complete=false;return;}
            if(path.Count>best.Length || path.Count==best.Length && paid<bestCost){best=path.ToArray();bestCost=paid;}
            if(path.Count>=4)return;
            foreach(var card in cheapest.Where(card=>remaining.Contains(card.HandIndex)&&card.Cost<=pp))
            {
                var next=remaining.Where(index=>index!=card.HandIndex && !rules.Any(rule=>rule.DiscardedHandIndex==index
                    && rule.WhenPlayedHandIndexes.Contains(card.HandIndex))).ToHashSet();
                path.Add(Advice(card));Visit(path,next,pp-card.Cost,paid+card.Cost);path.RemoveAt(path.Count-1);
                if(!complete)return;
            }
        }
        Visit([Advice(first)],RemainingHand(player,first.HandIndex,rules).ToHashSet(),budget-first.Cost,first.Cost);
        return new(best,bestCost,budget-bestCost,complete);
    }

    internal sealed record Check(bool Valid,string? Error,int RemainingPP,int[] RemainingHandIndexes)
    {public int[] ConditionalBudgetSteps{get;init;}=[];public AiHandMaterialRules.Projection? HandMaterials{get;init;}}
    internal static Check Validate(AiStrategyPlanner.TurnPlan plan, IReadOnlyList<AiPlayCandidates.Candidate> candidates,
        IReadOnlyList<AiStrategyPlanner.HandLossRule> rules,PlayerStateDto player,
        IReadOnlyList<AiSelectedHandLoss.Rule>? selectedHandLoss=null,IReadOnlyList<AiPpRefundFacts.Refund>? conditionalRefunds=null)
    {
        var pp=player.CurrentPP+(player.IsPpBoostArmed?Domain.Constants.GameConstants.PlayPoints.PpBoostAmount:0);
        var remaining=Enumerable.Range(0,player.Hand.Count).ToHashSet();
        var blood=player.BloodPool;var health=player.LeaderCard?.CurrentHealth??0;var conditional=new List<int>();
        Check Fail(string error)=>new(false,error,pp,remaining.Order().ToArray());
        var line=plan.Plays;
        if(line==null || line.Length==0)return new(true,null,pp,remaining.Order().ToArray());
        if(line.Length>4)return Fail("plays至多包含4步当前手牌的出牌计划。复杂路线留在文字规划中，不要编造新手牌位置。");
        if(plan.Play!=null && !AiPlannedModes.Same(plan.Play,line[0]))return Fail("play必须与plays的第一步完全一致。");
        for(var i=0;i<line.Length;i++)
        {
            var step=line[i];
            if(step==null || step.Intent!="use")return Fail($"第{i+1}步不是有效的use出牌计划。");
            if(!remaining.Contains(step.HandIndex))return Fail($"第{i+1}步使用手牌位置{step.HandIndex}，但该牌已在之前步骤离开手牌（已使用、被自动舍弃或计划中被选为弃牌素材），不能继续使用。");
            var candidate=candidates.FirstOrDefault(card=>card.HandIndex==step.HandIndex && card.Original.CardId==step.CardId
                && card.Mode.ToString()==step.Mode && card.Cost==step.Cost);
            if(candidate==null)return Fail($"第{i+1}步的手牌、形态或费用不在当前引擎核对的候选中。");
            if(step.Cost>pp)return Fail($"第{i+1}步需要{step.Cost}PP，按当前候选逐笔支付后只剩{pp}PP；已识别的有条件返费仍不足或不适用，未知返费不计入可行预算。");
            var discarded=AiSelectedHandLoss.IntendedDiscard(step,player,selectedHandLoss??[]);
            if(discarded.HasValue&&!remaining.Contains(discarded.Value))return Fail($"第{i+1}步选择弃掉的手牌位置{discarded.Value}已在之前步骤离手，不能再将它作为弃牌素材。该检查只是意图约束，效果仍须实际结算。");
            pp-=step.Cost;
            remaining.Remove(step.HandIndex);
            if(discarded.HasValue)remaining.Remove(discarded.Value);
            foreach(var rule in rules.Where(rule=>rule.WhenPlayedHandIndexes.Contains(step.HandIndex)))
                remaining.Remove(rule.DiscardedHandIndex);
            var refund=AiConditionalRefundEvidence.ForStep(step,conditionalRefunds??[]);
            if(refund!=null&&!player.IsPpBoostArmed&&blood>=refund.PaidBlood&&health>refund.OwnDamage){
                pp=Math.Min(player.MaxPP,pp+refund.RestoreUpTo);blood=blood-refund.PaidBlood+refund.OwnDamage;health-=refund.OwnDamage;conditional.Add(i);
            }
        }
        return new(true,null,pp,remaining.Order().ToArray()){ConditionalBudgetSteps=conditional.ToArray()};
    }
}
