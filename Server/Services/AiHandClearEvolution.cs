using System.Text.RegularExpressions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Extensions;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

internal static class AiHandClearEvolution
{
    private const int DiscardCount = 3;

    internal sealed record Projection(Guid Source, Guid[] Materials, int Heal, bool Ordinary, bool Super, bool RequiresDiscardChoice);

    internal static async Task<Projection[]> ReadAsync(GameContext context, PlayerStateDto player)
    {
        if (player.Hand.Count is < 1 or > DiscardCount || (!player.CanEvolve && !player.CanSuperEvolve))
            return [];
        var blockedHealing = player.LeaderCard is not { CurrentHealth: > 0 } leader || leader.HasKeyword(CardKeyword.EffectImmune)
            || context.BuffManager.GetBuffsForCard(leader.InstanceId).Any(buff => buff.IsActive);
        var game = context.CurrentGameState;
        var owner = game.GetPlayerState(player.PlayerId)!;
        var measurement = AiEvolutionEvaluation.For(game);
        if (measurement?.Candidate != player.PlayerId) measurement = null;
        if (blockedHealing && measurement == null) return [];
        var selection = context.ServiceProvider.GetRequiredService<TargetSelectionService>();
        var materials = player.Hand.Select(material => material.InstanceId).ToArray();
        var requiresDiscardChoice = materials.Length == DiscardCount;
        var routes = new List<Projection>();
        foreach (var unit in player.Field.Where(card => card.CanEvolve && !card.HasKeyword(CardKeyword.BanEvolve)))
        {
            var card = game.FindCardInstance(unit.InstanceId);
            if (card == null || card.IsSilenced || card.CurrentZone != CardZone.Field) continue;
            var effective = context.AbilityManager.GetEffectiveAbilitiesForCard(unit.InstanceId,context).ToArray();
            var evolution = effective.OfType<Ability>().Where(ability => ability.Config.IsEnabled && ability.TriggerConfig.IsEnabled
                && ability.TriggerConfig.IsValidZone(CardZone.Field)
                && ability.TriggerConfig.EventTypes.Any(type => type is EventType.Evolve or EventType.SuperEvolve or EventType.EvolveRequested))
                .ToArray();
            if (effective.Any(ability => ability is not Ability) || evolution is not [var ability] || !Matches(ability.Config))
                continue;
            measurement?.ObserveHandClearInput(owner.TurnNumber);
            if (blockedHealing || context.BuffManager.GetBuffsForCard(unit.InstanceId).Any(buff => buff.IsActive))
            {
                measurement?.BlockHandClear(owner.TurnNumber,unit.CardId,blockedHealing?"leader-heal-uncertain":"source-modified");
                continue;
            }
            if (AiGeneratedTokenTactics.HasInterference(context,card,IsUnrelatedReaction,
                measurement == null ? null : (other, config) => measurement.BlockHandClear(owner.TurnNumber,unit.CardId,
                    "public-or-own-hand-reaction",other.CurrentDefinitionId,config.Trigger.EventTypes.Select(value=>value.ToString()).ToArray())))
                continue;

            async Task<bool> CanClear(bool super)
            {
                var trigger = new EvolveEvent(card, TargetInfo.ForPlayer(owner), super, isManualEvolution:true);
                var evaluation = context with { SourceCard=card, SourceAbility=ability, TriggeringEvent=trigger };
                if (!await ability.CanTrigger(trigger,evaluation) || !ability.CheckConditions(evaluation)) return false;
                var targets = await selection.CheckTargetAvailabilityAsync(ability,evaluation);
                return targets.IsAvailable && targets.CanPlayWithoutTarget == !requiresDiscardChoice
                    && targets.ValidTargetIds is { } ids && ids.ToHashSet().SetEquals(materials);
            }
            var ordinary = player.CanEvolve && (player.CurrentEP >= GameConstants.Evolution.EvolutionPointCost || unit.HasKeyword(CardKeyword.FreeEvolve))
                && await CanClear(false);
            var super = player.CanSuperEvolve && player.CurrentSEP >= GameConstants.SuperEvolution.SuperEvolutionPointCost
                && await CanClear(true);
            if (ordinary || super) routes.Add(new(unit.InstanceId, materials, 3, ordinary, super, requiresDiscardChoice));
            else measurement?.BlockHandClear(owner.TurnNumber,unit.CardId,"no-legal-evolution-with-all-hand-targets");
        }
        return routes.ToArray();
    }

    private static bool IsUnrelatedReaction(CardInstance source, AbilityConfig ability)
    {
        var trigger = ability.Trigger;
        if (trigger.EventTypes.All(type => type is EventType.Attack or EventType.Clash or EventType.Act or EventType.PlayCard
            || (type is EventType.Evolve or EventType.SuperEvolve
                && trigger.RelationType is TriggerRelationType.TargetSelf or TriggerRelationType.SourceSelf)))
            return true;

        return IsNativeLeaderCounter(source, ability);
    }

    // These built-in leader abilities only write their own counter. Any ability listening to
    // that counter still goes through the separate reaction check.
    internal static bool IsNativeLeaderCounter(CardInstance source, AbilityConfig ability)
    {
        var trigger = ability.Trigger;
        if (!source.IsLeader || source.CurrentDefinitionId != GameConstants.Cards.LeaderCardId
            || trigger.Timing != TriggerTiming.On || trigger.EventTypes is not [var eventType]
            || ability.AbilityKeywords.Count != 0 || ability.Targets.Count != 0 || ability.Auras.Count != 0
            || ability.Effects is not [var effect] || !Simple(effect,EffectType.ModifyVariable,0)
            || effect.Parameters.Count != 5)
            return false;
        string? Value(string key) => (effect.Parameters.GetValueOrDefault(key) as StaticValueConfig)?.Value?.ToString();
        if (Value("Amount") != "1" || Value("Operation") != "add" || Value("TargetType") != "player" || Value("PlayerScope") != "self")
            return false;
        return (eventType, Value("CounterName")) is
            (EventType.Evolve, "进化的次数") or (EventType.Destroy, "破坏的纹章数" or "破坏的护符数")
            or (EventType.Summon, "自己已进入战场的创造物从者卡种类数");
    }

    private static string Compact(string text) => Regex.Replace(text, @"[\s()]", "").ToLowerInvariant();
    private static bool Matches(AbilityConfig ability)
    {
        var trigger = ability.Trigger;
        if (ability.Type != AbilityType.Triggered || !ability.HasKeyword(AbilityKeyword.EvolveEffect)
            || ability.AbilityKeywords.Count != 1 || !trigger.EventTypes.SequenceEqual([EventType.Evolve])
            || trigger.Timing != TriggerTiming.On || trigger.RelationType != TriggerRelationType.TargetSelf
            || trigger.CountLimit != null || trigger.LimitSameNameOncePerTransaction
            || ability.Auras.Count != 0 || ability.PreConditions.Count != 0
            || ability.Conditions is not [{ IsEnabled: true } condition]
            || Compact(condition.Expression) != "context.triggeringevent.ismanualevolution==true"
            || ability.Targets.Count != 2 || ability.Effects is not [var discard, var heal]
            || !Simple(discard, EffectType.Discard) || !Simple(heal, EffectType.Heal))
            return false;

        if (!ability.Targets.TryGetValue(discard.TargetSelector[0], out var hand)
            || !ability.Targets.TryGetValue(heal.TargetSelector[0], out var leader) || ReferenceEquals(hand, leader)
            || !hand.IsEnabled || !leader.IsEnabled || hand.Query.Operations.Count != 0 || leader.Query.Operations.Count != 0
            || hand.GetEffectiveTargetHandlingStrategy() != TargetHandlingStrategy.DoNotIntercept
            || hand.PlayerChoiceOptions is not { IsRequired: true, MinCount: DiscardCount, MaxCount: DiscardCount }
            || hand.PlayerChoiceOptions.IsInverseSelection || leader.PlayerChoiceOptions != null
            || Compact(hand.Query.SourcePoolExpression) != "context.owner.hand.queryself.type==\"follower\"||self.type==\"spell\"||self.type==\"amulet\""
            || Compact(leader.Query.SourcePoolExpression) != "context.owner.leader")
            return false;

        return discard.Parameters.Count == 0 && heal.Parameters.Count == 1
            && heal.Parameters.GetValueOrDefault("HealAmount") is StaticValueConfig literal && literal.Value?.ToString() == "3";
    }

    private static bool Simple(EffectConfig effect, EffectType type, int targetCount=1) => effect.Type == type && effect.IsEnabled
        && effect.RepeatCount == 0 && effect.RepeatCountValue == null && effect.RepeatCondition == null
        && effect.ChainedAbilities.Count == 0 && effect.Outputs is not { Count: > 0 } && effect.TargetSelector.Count == targetCount;
}
