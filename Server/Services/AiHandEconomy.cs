using System.Text.Json;
using System.Text.RegularExpressions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

// Scores hand opportunity costs from current effective rules; never executes a DSL expression.
internal static class AiHandEconomy
{
    internal sealed record Rule(double DiscardReward=0,int TemporaryDiscount=0,double PlayUtility=0);
    private static string Normalize(string text)=>Regex.Replace(text,"[\\s()]","").ToLowerInvariant();
    private static object? Literal(EffectConfig effect,string key)=>effect.Parameters.FirstOrDefault(p=>p.Key.Equals(key,StringComparison.OrdinalIgnoreCase)).Value is StaticValueConfig s?s.Value:null;
    private static int? Number(object? value)=>value is int i?i:value is JsonElement {ValueKind:JsonValueKind.Number} j&&j.TryGetInt32(out var n)?n:null;
    private static string? Text(object? value)=>value is string s?s:value is JsonElement {ValueKind:JsonValueKind.String} j?j.GetString():null;
    private static bool Simple(EffectConfig effect)=>effect.IsEnabled&&effect.RepeatCount==0&&effect.RepeatCountValue==null&&effect.RepeatCondition==null
        &&effect.ChainedAbilities.Count==0&&effect.Outputs?.Count is not >0;
    private static bool Plain(AbilityConfig ability,EventType ev)=>ability.IsEnabled&&ability.Type==AbilityType.Triggered&&ability.Trigger.IsEnabled
        &&ability.Trigger.EventTypes.SequenceEqual(new[]{ev})&&ability.Trigger.RelationType==TriggerRelationType.TargetSelf
        &&ability.Trigger.Timing==TriggerTiming.On&&ability.Trigger.CountLimit==null&&!ability.Trigger.LimitSameNameOncePerTransaction
        &&ability.Conditions.All(c=>!c.IsEnabled)&&ability.PreConditions.All(c=>!c.IsEnabled)&&ability.Properties.Count==0
        &&ability.Auras.All(a=>!a.IsEnabled)&&ability.AlternatePlay==null&&ability.ActivateCost==null;
    private static bool Pool(AbilityConfig ability,EffectConfig effect,string expression)=>effect.TargetSelector.Count==1
        &&ability.Targets.TryGetValue(effect.TargetSelector[0],out var target)&&target.IsEnabled&&target.PlayerChoiceOptions==null
        &&target.Query.Operations.Count==0&&Normalize(target.Query.SourcePoolExpression)==Normalize(expression);
    internal static Rule ReadRules(IEnumerable<AbilityConfig> configs,PlayerStateDto own,PlayerStateDto? enemy)
    {
        double reward=0;var discount=0;double utility=0;
        foreach(var ability in configs){
            var effects=ability.Effects.Where(e=>e.IsEnabled).OrderBy(e=>e.ExecutionOrder).ToArray();
            utility=Math.Clamp(utility+ChoiceSummonUtility(ability,own),-6,6);
            if(Plain(ability,EventType.Discard)&&ability.AbilityKeywords.Count==0&&effects.Length>0&&effects.All(Simple)
                &&ability.Trigger.ValidZones.All(z=>z==CardZone.Hand)
                &&effects.All(e=>e.Type==EffectType.Damage&&Pool(ability,e,"context.opponent.leader")&&AiTargetScoring.ReadAmount(e).HasValue
                    ||e.Type==EffectType.Heal&&Pool(ability,e,"context.owner.leader")&&AiTargetScoring.ReadAmount(e).HasValue)){
                foreach(var effect in effects){
                    if(effect.Type==EffectType.Damage&&Pool(ability,effect,"context.opponent.leader")
                        &&enemy?.LeaderCard is {} leader&&!leader.Keywords.Any(k=>k is CardKeyword.Shield or CardKeyword.EffectImmune)
                        &&AiTargetScoring.ReadAmount(effect) is {} damage)reward+=Math.Min(3,damage)*.9;
                    if(effect.Type==EffectType.Heal&&Pool(ability,effect,"context.owner.leader")&&own.LeaderCard is {} self
                        &&AiTargetScoring.ReadAmount(effect) is {} heal)reward+=Math.Min(Math.Max(0,self.MaxHealth-self.CurrentHealth),Math.Min(3,heal))*.8;
                }
            }
            // This exact shape discounts selected hand cards until end of turn, then discards the rest.
            if(!Plain(ability,EventType.PlayCard)||!ability.HasKeyword(AbilityKeyword.Fanfare)||effects.Length!=2||!effects.All(Simple)
                ||effects[0].Type!=EffectType.ModifyStatus||effects[1].Type!=EffectType.Discard
                ||effects[0].TargetSelector.Count!=1||effects[1].TargetSelector.Count!=1
                ||Text(Literal(effects[0],"DurationType"))!="EndOfTurn"||Number(Literal(effects[0],"CostModifier")) is not <0 or <-20)continue;
            var first=effects[0].TargetSelector[0];var second=effects[1].TargetSelector[0];
            if(!ability.Targets.TryGetValue(first,out var kept)||!ability.Targets.TryGetValue(second,out var discarded)
                ||!kept.IsEnabled||!discarded.IsEnabled||kept.Query.Operations.Count>0||discarded.Query.Operations.Count>0
                ||kept.PlayerChoiceOptions is not {MinCount:2,MaxCount:2}||discarded.PlayerChoiceOptions!=null
                ||Normalize(kept.Query.SourcePoolExpression)!=Normalize("context.owner.hand ~ context.sourceCard")
                ||Normalize(discarded.Query.SourcePoolExpression)!=Normalize($"(context.owner.hand ~ context.sourceCard) ~ context.resolvedTargetCards.{first}"))continue;
            discount=Math.Max(discount,-Number(Literal(effects[0],"CostModifier"))!.Value);
        }
        return new(Math.Min(3,reward),discount,utility);
    }
    // This is a modest card-use heuristic, not a combat or survival proof. In particular it
    // does not invent the secondary summons/buffs of these followers' entry triggers.
    private static double ChoiceSummonUtility(AbilityConfig ability,PlayerStateDto own)
    {
        if(!Plain(ability,EventType.PlayCard)||ability.Targets.Any(t=>t.Value.IsEnabled))return 0;
        var effects=ability.Effects.Where(e=>e.IsEnabled).ToArray();
        if(effects.Length!=1||effects[0].Type!=EffectType.Choose||!Simple(effects[0])
            ||Number(Literal(effects[0],"SelectionCount")) is not int count||count is <1 or >4)return 0;
        var branches=AiGeneratedTokenTactics.Branches(effects[0]);if(branches.Length<count)return 0;
        var costs=new List<int>();
        foreach(var branch in branches){
            if(!Plain(branch,EventType.PlayCard)||branch.Targets.Any(t=>t.Value.IsEnabled))return 0;
            var fees=branch.Effects.Where(e=>e.IsEnabled).ToArray();
            if(fees.Length!=1||fees[0].Type!=EffectType.Sacrifice||fees[0].RepeatCount!=0||fees[0].RepeatCountValue!=null||fees[0].RepeatCondition!=null
                ||fees[0].TargetSelector.Count>0||fees[0].Outputs?.Count is >0||Text(Literal(fees[0],"PlayerScope"))!="self"
                ||Number(Literal(fees[0],"Amount")) is not int fee||fee<0||fees[0].ChainedAbilities.Count!=1)return 0;
            var child=fees[0].ChainedAbilities[0];
            if(!child.IsEnabled||child.Conditions.Any(c=>c.IsEnabled)||child.PreConditions.Any(c=>c.IsEnabled)||child.Targets.Any(t=>t.Value.IsEnabled)
                ||child.Auras.Any(a=>a.IsEnabled)||child.Properties.Count>0||child.CounterRegistrations.Count>0||child.PlayerCounterRegistrations.Count>0)return 0;
            var summons=child.Effects.Where(e=>e.IsEnabled).ToArray();
            if(summons.Length!=1||summons[0].Type!=EffectType.Summon||!Simple(summons[0])||summons[0].TargetSelector.Count>0
                ||Number(Literal(summons[0],"SummonCount"))!=1||Text(Literal(summons[0],"PlayerScope"))!="self"
                ||Text(Literal(summons[0],"CardSourceMode"))!="definition")return 0;
            var ids=Literal(summons[0],"CardDefinitionId");
            int[] list=ids is IEnumerable<int> values?values.ToArray():ids is JsonElement {ValueKind:JsonValueKind.Array} json
                ?json.EnumerateArray().Select(v=>Number(v)??0).ToArray():[];
            if(list.Length!=1||GameDataProvider.Instance.GetCardDefinition(list[0]) is not {Type:CardType.Follower,Attack:>=0,Health:>0})return 0;
            costs.Add(fee);
        }
        if(costs.Min()>own.BloodPool)return -6; // Every offered branch currently fails its resource payment.
        return own.Field.Count+count<=GameConstants.Field.MaxFieldSize&&costs.OrderByDescending(c=>c).Take(count).Sum()<=own.BloodPool
            ?Math.Min(6,count*3):0;
    }
    internal static Dictionary<Guid,Rule> Read(GameContext? context,PlayerStateDto own,PlayerStateDto? enemy)
    {
        var result=new Dictionary<Guid,Rule>();if(!AiRecoveryPolicy.Enabled||context==null||context.CurrentGameState.LegacyAiPlayerId==own.PlayerId)return result;
        foreach(var card in own.Hand){
            var actual=context.CurrentGameState.GetPlayerState(own.PlayerId)?.Hand.SingleOrDefault(c=>c.InstanceId==card.InstanceId);
            if(actual==null||actual.IsSilenced||actual.CurrentDefinitionId!=card.CardId||actual.CurrentCost!=card.CurrentCost)continue;
            try{
                var rules=context.AbilityManager.GetEffectiveAbilitiesForCard(actual.InstanceId,context).ToArray();
                if(rules.Any(a=>a is not Ability))continue;
                result[card.InstanceId]=ReadRules(rules.Cast<Ability>().Select(a=>a.Config),own,enemy);
            }catch(Exception e) when(e is not OperationCanceledException and not OutOfMemoryException){/* Missing facts give no extra credit. */}
        }
        return result;
    }
    internal static double DiscardValue(PlayerStateDto own,CardInstanceDto card,AiStrategyPlanner.TurnPlan? plan,Rule? rule)
    {
        if(!AiRecoveryPolicy.Enabled)return AiAbilityScoring.PlannedMaterialValue(own,card,plan);
        var value=AiAbilityScoring.PlannedMaterialValue(own,card,plan)-(rule?.DiscardReward??0);
        // Once the refill engine exists, an unaffordable card has a real cost each turn it blocks empty hand.
        if(AiEternalEngine.Active(own)&&AiEternalEngine.Established(own)&&card.CurrentCost>own.CurrentPP+1)
            value-=1.4;
        return value;
    }
    internal static double PlayBias(PlayerStateDto own,CardInstanceDto card,int paidCost,Rule? rule)
    {
        if(!AiRecoveryPolicy.Enabled)return 0;
        var utility=rule?.PlayUtility??0;
        if(rule is not {TemporaryDiscount:>0}||!AiEternalEngine.Active(own)||!AiEternalEngine.Established(own))return utility;
        var remaining=own.Hand.Where(c=>c.InstanceId!=card.InstanceId).ToArray();
        var pp=own.CurrentPP-paidCost;
        return utility+(remaining.Length>0&&pp>=0&&remaining.All(c=>Math.Max(0,c.CurrentCost-rule.TemporaryDiscount)>pp)?-4:0);
    }
}
