using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

internal static class AiAbilityScoring
{
    internal static double Score(PlayerStateDto player, Guid actor, CardInstanceDto source, bool fusion, int cost,
        IReadOnlyList<EffectConfig> effects, IReadOnlyList<CardInstanceDto> targets, string? targetKey)
    {
        var enabled = effects.Where(e => e.IsEnabled).ToArray();
        var score = fusion ? 2.5 : Math.Max(.45, 1.5 - .7 * cost);
        var handSpace = GameConstants.Hand.MaxHandSize - player.Hand.Count + (fusion ? 1 : 0);
        if (fusion && targets.Count > 0)
            score -= targets.Min(card => MaterialValue(player, card));

        foreach (var effect in enabled)
        {
            score += effect.Type switch
            {
                EffectType.Draw => player.DeckCount > 1 && handSpace > 0 ? 2.4 : -2.5,
                EffectType.Discover or EffectType.GetToken => handSpace > 0 ? 2 : -2,
                EffectType.Summon or EffectType.Reanimate => player.Field.Count < GameConstants.Field.MaxFieldSize ? 3 : -2.5,
                EffectType.Heal when targets.Count == 0 => player.LeaderCard is { } leader
                    ? Math.Min(2, (leader.MaxHealth - leader.CurrentHealth) * .3) : 0,
                EffectType.Transform => 2,
                _ => 0,
            };
        }

        // A single direct effect with known selectable targets can reuse the same scoring as the choice prompt.
        if (!fusion && enabled.Length == 1 && targets.Count > 0 && targetKey != null)
        {
            var direct = AiTargetScoring.FindEffects(enabled, targetKey);
            if (direct.Count > 0)
                score += Math.Clamp(targets.Max(card => AiTargetScoring.Score(card, actor, direct)), -5, 8);
        }

        // A charge on an affordable source can be realized this turn.
        if (fusion && source.CurrentCost <= player.CurrentPP) score += .6;
        return score;
    }

    internal static double FragileHandPriority(PlayerStateDto player, CardInstanceDto card)
    {
        var texts = card.EffectDescriptions
            .Concat(card.BuffSummaries.SelectMany(buff => new[] { buff.Description }.Concat(buff.Details)
                .Concat(buff.GrantedAbilities.SelectMany(ability => new[] { ability.Name, ability.Description }))));
        if (!texts.Any(text => text.Contains("使用其他", StringComparison.Ordinal)
            && text.Contains("舍弃这张", StringComparison.Ordinal)))
            return 0;
        return card.CardId == 1474120 && !player.Emblem.Any(emblem => emblem.CardId == 1474276) ? 4 : 1.5;
    }

    internal static double PlannedMaterialValue(PlayerStateDto player, CardInstanceDto card, AiStrategyPlanner.TurnPlan? plan)
    {
        var copies = Math.Max(1, player.Hand.Count(other => other.CardId == card.CardId));
        var retention = plan?.Hold.Contains(card.CardId) == true ? 1.25
            : plan?.Prioritize.Contains(card.CardId) == true ? .85 : 0;
        return MaterialValue(player, card) + retention / copies;
    }

    internal static double MaterialBias(PlayerStateDto player, IReadOnlyList<CardInstanceDto> materials, AiStrategyPlanner.TurnPlan? plan) =>
        plan == null || materials.Count == 0 ? 0
            : materials.Min(card => MaterialValue(player, card)) - materials.Min(card => PlannedMaterialValue(player, card, plan));

    internal static double MaterialValue(PlayerStateDto player, CardInstanceDto card)
    {
        var duplicates = player.Hand.Count(other => other.CardId == card.CardId) > 1;
        var availableSoon = card.CurrentCost <= Math.Min(10, player.MaxPP + 1);
        var value = 1 + (availableSoon ? 1.2 : 0) - (duplicates ? .8 : 0);
        if (card.Type == CardType.Follower)
            value += Math.Min(1.5, (card.CurrentAttack + card.CurrentHealth) * .12);
        if (card.Keywords.Contains(CardKeyword.Storm)) value += 2;
        if (card.FusionAbilities.Count > 0) value += 1.2;
        // Preserve the first affordable engine piece until its emblem has been established.
        if (card.CardId == 1474120 && card.CurrentCost <= player.CurrentPP
            && !player.Emblem.Any(emblem => emblem.CardId == 1474276) && !duplicates)
            value += 3;
        return value + AiEternalEngine.Retention(player,card);
    }
}
