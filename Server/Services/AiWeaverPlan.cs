using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

// User-provided deck role, guarded by the current native configuration. No hidden deck inspection.
internal static class AiWeaverPlan
{
    internal const int Weaver=1474173,Call=1475138;
    private sealed class Benchmark(Guid candidate)
    {
        internal Guid Candidate { get; } = candidate;
        internal int Preparations;
    }

    private static readonly ConditionalWeakTable<GameState, Benchmark> Benchmarks = new();

    internal static void BindBenchmark(GameState game, Guid candidate) => Benchmarks.Add(game, new(candidate));

    internal static bool PreparationEnabled(GameState game, Guid actor, bool configured) =>
        Benchmarks.TryGetValue(game, out var benchmark) ? benchmark.Candidate == actor : configured;

    internal static void RecordPreparation(GameState game, Guid actor)
    {
        if (Benchmarks.TryGetValue(game, out var benchmark) && benchmark.Candidate == actor)
            Interlocked.Increment(ref benchmark.Preparations);
    }

    internal static int PreparationCount(GameState game) =>
        Benchmarks.TryGetValue(game, out var benchmark) ? Volatile.Read(ref benchmark.Preparations) : 0;

    private static string Normal(string text)=>Regex.Replace(text,"[\\s()]","").ToLowerInvariant();
    private static bool Simple(EffectConfig e)=>e.IsEnabled&&e.RepeatCount==0&&e.RepeatCountValue==null&&e.RepeatCondition==null&&e.ChainedAbilities.Count==0;
    private static string? Text(EffectConfig e,string key)=>e.Parameters.GetValueOrDefault(key) is StaticValueConfig s?s.Value is JsonElement{ValueKind:JsonValueKind.String} j?j.GetString():s.Value as string:null;
    internal static bool RulesValid()
    {
        var p=GameDataProvider.Instance;
        if(p.GetCardDefinition(Weaver) is not {Type:CardType.Follower,Cost:1}||p.GetCardDefinition(Call) is not {Type:CardType.Spell,Cost:5})return false;
        var own=p.GetAbilityConfigs(Weaver)?.Any(OwnEvolutionBoost);
        var summon=p.GetAbilityConfigs(Call)?.Any(a=>a.IsEnabled&&a.Type==AbilityType.Triggered&&a.Trigger.IsEnabled&&a.Trigger.EventTypes.SequenceEqual(new[]{EventType.PlayCard})
            &&a.Trigger.RelationType==TriggerRelationType.TargetSelf&&a.Conditions.All(c=>!c.IsEnabled)&&a.PreConditions.All(c=>!c.IsEnabled)
            &&a.Targets.Values.Any(t=>t.IsEnabled&&t.PlayerChoiceOptions==null&&t.Query.Operations.Count==0&&Normal(t.Query.SourcePoolExpression)=="context.owner.deck.queryself.type==\"follower\".queryself.currentcost==1.takerandom1")
            &&a.Effects.Where(e=>e.IsEnabled).ToArray() is {Length:2} effects&&effects.All(Simple)&&effects[0].Type==EffectType.Summon&&effects[1].Type==EffectType.Draw
            &&string.Equals(Text(effects[0],"CardSourceMode"),"Instance",StringComparison.OrdinalIgnoreCase)&&Text(effects[0],"PlayerScope")=="self");
        return own==true&&summon==true;
    }
    private static bool OwnEvolutionBoost(AbilityConfig config) =>
        config.IsEnabled && config.Type == AbilityType.Triggered && config.Trigger.IsEnabled
        && config.Trigger.Timing == TriggerTiming.On && config.Trigger.EventTypes.SequenceEqual(new[] { EventType.Evolve })
        && config.Trigger.ValidZones.Contains(CardZone.Field) && config.Trigger.CountLimit?.MaxPerTurn == 1
        && config.PreConditions.All(condition => !condition.IsEnabled)
        && config.Conditions.Where(condition => condition.IsEnabled).Select(condition => Normal(condition.Expression))
            .SequenceEqual(new[] { "context.triggeringevent.evolvedcard.ownerplayerid==context.owner.playerid" })
        && config.Effects.Where(effect => effect.IsEnabled).ToArray() is { Length: 1 } effects
        && Simple(effects[0]) && effects[0].Type == EffectType.SpellBoost
        && config.Targets.Values.Any(target => target.IsEnabled
            && Normal(target.Query.SourcePoolExpression) == "context.owner.hand.queryself.type==\"follower\"||self.type==\"spell\"||self.type==\"amulet\"");

    private static bool DiscountsCall(AbilityConfig config) =>
        config.IsEnabled && config.Type == AbilityType.Triggered && config.Trigger.IsEnabled
        && config.Trigger.Timing == TriggerTiming.On && config.Trigger.EventTypes.SequenceEqual(new[] { EventType.SpellBoost })
        && config.Trigger.RelationType == TriggerRelationType.TargetSelf && config.Trigger.ValidZones.Contains(CardZone.Hand)
        && config.Conditions.All(condition => !condition.IsEnabled) && config.PreConditions.All(condition => !condition.IsEnabled)
        && config.Targets.Values.Any(target => target.IsEnabled && target.Query.Operations.Count == 0
            && Normal(target.Query.SourcePoolExpression) == "context.sourcecard")
        && config.Effects.Where(effect => effect.IsEnabled).ToArray() is { Length: 1 } effects
        && Simple(effects[0]) && effects[0].Type == EffectType.ModifyStatus
        && effects[0].Parameters.GetValueOrDefault("costModifier") is StaticValueConfig modifier
        && modifier.Value?.ToString() == "-1" && Text(effects[0], "DurationType") == "permanent"
        && Text(effects[0], "ModifierLayer") == "additive";

    private static bool CanPrepare(PlayerStateDto player) => player.CanEvolve
        && player.Field.Count <= GameConstants.Field.MaxFieldSize - 2
        && !player.Field.Any(card => card.CardId == Weaver)
        && player.Hand.Any(card => card.CardId == Call && card.CurrentCost > 0 && card.CurrentCost <= player.CurrentPP);

    internal static Guid? PrepareBeforeEvolution(GameContext context, PlayerStateDto player)
    {
        if (!CanPrepare(player) || !player.Hand.Any(card => card.CardId == Weaver && card.CurrentCost is >= 0 and <= 1)
            || context.CurrentGameState.GetPlayerState(player.PlayerId) is not { } own || !Has(own.InitialDeckDefinitionIds))
            return null;

        bool Active(CardInstanceDto view, Func<AbilityConfig, bool> matches)
        {
            var card = context.CurrentGameState.FindCardInstance(view.InstanceId);
            return card is { IsSilenced: false, CurrentZone: CardZone.Hand } && card.OwnerPlayerId == player.PlayerId
                && context.AbilityManager.GetEffectiveAbilitiesForCard(card.InstanceId, context)
                    .OfType<Ability>().Any(ability => matches(ability.Config));
        }

        if (!player.Hand.Any(card => card.CardId == Call && card.CurrentCost > 0 && card.CurrentCost <= player.CurrentPP
            && Active(card, DiscountsCall))) return null;
        return player.Hand.FirstOrDefault(card => card.CardId == Weaver && card.CurrentCost is >= 0 and <= 1
            && Active(card, OwnEvolutionBoost))?.InstanceId;
    }

    internal static bool Has(IEnumerable<int> deck)=>deck.Contains(Weaver)&&deck.Contains(Call)&&RulesValid();
    internal static int[] OriginalPool(IEnumerable<int> deck)=>deck.Distinct().Where(id=>GameDataProvider.Instance.GetCardDefinition(id) is {Type:CardType.Follower,Cost:1}).Order().ToArray();
    internal static string[] Principles(IEnumerable<int> deck)
    {
        var ids=deck.ToArray();if(!Has(ids))return [];
        return ["当前原生规则：#1474173只在己方战场上的随从进化时发动魔力增幅，每个能力实例每回合最多1次；对手随从进化不会触发。实际仍须核对沉默、区域和已用次数。",
            "用户战术定位：#1475138是可被魔力增幅降费的5费法术，用于从牌堆拉取当前费用为1的从者，主要拉#1474173，不是要求尽量把织法拿上手。手牌或场上的织法不属于牌堆召唤池；过多上手会减少可拉目标。",
            OriginalPool(ids).SequenceEqual(new[]{Weaver})?"当前原始牌组中，原始1费从者只有#1474173，因此#1475138承担稳定拉织法的任务；牌堆被其他能力改动或费用变化后不能保证。调度优先低费过牌/增幅组件与一张仰慕，不追求多张织法上手。已有仰慕时可把织法换回，多张织法不全留。":"当前原始牌组还有其他1费从者，仰慕不是确定拉织法；按实际构筑与当前费用重新判断。"];
    }
    internal static List<string>? Mulligan(PlayerStateDto own,IReadOnlyList<int>? deck)
    {
        if(deck==null||!Has(deck)||!OriginalPool(deck).SequenceEqual(new[]{Weaver}))return null;
        var keep=new HashSet<Guid>();var call=own.Hand.FirstOrDefault(c=>c.CardId==Call);
        if(call!=null)keep.Add(call.InstanceId);
        var oneWeaver=call==null?own.Hand.FirstOrDefault(c=>c.CardId==Weaver):null;
        if(oneWeaver!=null)keep.Add(oneWeaver.InstanceId);
        foreach(var c in own.Hand.Where(c=>c.CardId is not (Weaver or Call)&&c.Cost<=3))keep.Add(c.InstanceId);
        return own.Hand.Where(c=>!keep.Contains(c.InstanceId)).Select(c=>c.InstanceId.ToString()).ToList();
    }
    internal static AiStrategyPlanner.DeckGuide Correct(AiStrategyPlanner.DeckGuide guide,IEnumerable<int> deck)
    {
        var ids=deck.ToArray();if(!Has(ids))return guide;
        var stable=OriginalPool(ids).SequenceEqual(new[]{Weaver});
        var weaver="织法只在己方随从进化时触发魔力增幅，每个能力实例每回合最多1次；对手进化不会触发。";
        var tutor=stable?"仰慕的召唤主要用于从牌堆稳定拉织法，当前原始构筑的1费从者只有织法；不追求多张织法上手，已有仰慕时可把织法换回，保留牌堆中的召唤目标。":"仰慕的召唤从牌堆随机拉当前1费从者；构筑有其他1费从者时不能保证拉到织法。";
        var rest=string.Concat(Regex.Split(guide.GamePlan,"(?<=[。；;])").Where(s=>!s.Contains("织法",StringComparison.Ordinal)&&!s.Contains("#1474173",StringComparison.Ordinal)&&!s.Contains("仰慕",StringComparison.Ordinal)&&!s.Contains("#1475138",StringComparison.Ordinal)));
        var text=weaver+tutor+rest;
        return guide with{GamePlan=text.Length<=1600?text:text[..1600],Cards=guide.Cards.Select(c=>c.Id==Weaver?c with{Role="己方进化增幅组件，可不消耗EP进化。主要由仰慕从牌堆拉取，不追求多张上手。"}:c.Id==Call?c with{Role="可增幅降费的5费召唤/过牌法术，主要从牌堆拉织法；手牌中的织法不是拉取对象，牌堆没目标时不能承担该铺场作用。"}:c).ToArray(),RuleCorrections=["已按当前原生规则与用户战术说明校正织法及仰慕的召唤。"]};
    }
    internal static object? ForModel(IEnumerable<int> deck,PlayerStateDto own)
    {
        var ids=deck.ToArray();
        if (!Has(ids)) return null;
        var considerOrder = CanPrepare(own) && own.Hand.Any(card => card.CardId == Weaver && card.CurrentCost is >= 0 and <= 1);
        return new{source="current-native-rules-and-user-tutor-role",ownEvolutionsOnly=true,boostLimitPerAbilityPerTurn=1,
            initialWeaverCopies=ids.Count(id=>id==Weaver),weaverInHand=own.Hand.Count(c=>c.CardId==Weaver),weaverOnField=own.Field.Count(c=>c.CardId==Weaver),
            callHandIndexes=own.Hand.Select((c,i)=>(c,i)).Where(x=>x.c.CardId==Call).Select(x=>x.i),originalOneCostFollowers=OriginalPool(ids),
            considerWeaverBeforeOrdinaryEvolution=considerOrder,
            ordinaryEvolutionOrderHint=considerOrder ? "若本回合本来就要普通进化，先核对合法出牌中的织法与仰慕、手牌当前费用及空位。织法在场后再进化可先增幅仰慕，仍要核对临时能力与紧急战斗。此提示不要求额外使用进化点，不保证进化或后续出牌合法。" : null,
            scope="Known original deck composition and current visible own hand/field only, not remaining hidden deck contents/order. Copies in hand/field are not available to the summon. Current costs/deck modifications may alter the pool."};
    }
}
internal static class AiDeckPrinciples
{
    internal static string[] For(IEnumerable<int> deck){var ids=deck.ToArray();return AiEternalEngine.Principles(ids).Concat(AiWeaverPlan.Principles(ids)).ToArray();}
}
