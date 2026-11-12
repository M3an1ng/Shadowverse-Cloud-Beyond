using System.Text.Json;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Extensions;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

internal static class AiLeaderRecovery
{
    private const int DiligentStudy = 1474226;
    private const string OwnHandBoost = "context.owner.hand.queryself.type==\"follower\"||self.type==\"amulet\"||self.type==\"spell\"";

    internal static async Task<AiTacticalSearch.Plan?> FindAsync(GameContext context, GameStateDto snapshot, Guid actor,
        IGameService games, CancellationToken token, AiLeaderRecoveryEvaluation? evaluation = null, bool scopeReactions = true)
    {
        var game = context.CurrentGameState;
        var revision = game.StateRevision;
        var catalog = GameDataProvider.Instance.DataRevision;
        var own = snapshot.Players.Single(player => player.PlayerId == actor);
        var enemy = snapshot.Players.Single(player => player.PlayerId != actor);
        var leader = own.LeaderCard;
        if (game.CurrentPlayerId != actor || leader is not { CurrentHealth: > 0 } || leader.CurrentHealth >= leader.MaxHealth
            || leader.HasKeyword(CardKeyword.EffectImmune) || context.BuffManager.GetBuffsForCard(leader.InstanceId).Any(buff => buff.IsActive)
            || !AiTacticalSearch.HasVisibleLethalThreat(own, enemy)) return null;

        evaluation?.Check();
        var incoming = AiTacticalSearch.EstimateBoardDamage(enemy.Field, own.Field, leader);
        var stableField = game.AllField.All(unit => (unit.CardType != CardType.Follower || unit.CurrentHealth > 0)
            && unit.CurrentCountdown is not <= 0 && (!unit.HasKeyword(CardKeyword.Evidence) || unit.Evidence > 0));
        var routes = new List<AiTacticalSearch.Plan>();
        foreach (var view in own.Field.Concat(own.Hand))
        {
            token.ThrowIfCancellationRequested();
            var card = game.FindCardInstance(view.InstanceId);
            if (card == null || card.IsSilenced || card.CurrentDefinitionId != view.CardId || card.OwnerPlayerId != actor
                || card.CurrentZone != view.Zone || context.BuffManager.GetBuffsForCard(card.InstanceId).Any(buff => buff.IsActive)) continue;
            var inHand = view.Zone == CardZone.Hand;
            var evolve = view.Type == CardType.Follower && view.CanEvolve && card.CanEvolve() && own.IsEvolutionUnlocked
                && own.CanEvolve && !view.HasKeyword(CardKeyword.BanEvolve)
                && (view.HasKeyword(CardKeyword.FreeEvolve) || own.CurrentEP >= GameConstants.Evolution.EvolutionPointCost);
            if ((!inHand || view.Type != CardType.Spell) && !evolve) continue;
            if (inHand && (own.IsPpBoostArmed || view.CurrentCost > own.CurrentPP
                || evolve && own.Field.Count >= GameConstants.Field.MaxFieldSize)) continue;

            var runtime = context.AbilityManager.GetEffectiveAbilitiesForCard(card.InstanceId, context).ToArray();
            if (runtime.Any(ability => ability is not Ability)) continue;
            var configs = runtime.Cast<Ability>().Select(ability => ability.Config).Where(config => config.IsEnabled).ToArray();
            var amount = Read(configs, evolve, inHand, own.DeckCount);
            if (amount <= 0 || Math.Min(leader.MaxHealth, leader.CurrentHealth + amount) <= incoming) continue;
            evaluation?.NativeCandidate();
            bool Harmless(CardInstance other, AbilityConfig ability) =>
                (evolve || scopeReactions) && other.CurrentZone == CardZone.Hand && other.OwnerPlayerId == actor
                    && CostOnlySpellBoost(ability, scopeReactions)
                || scopeReactions && AiRecoveryReactionScope.CannotReact(ability, card, evolve, inHand, stableField);
            if (AiGeneratedTokenTactics.HasInterference(context, card, Harmless,
                evaluation == null ? null : (other, ability) => evaluation.ObserveBlock(card, other, ability)))
            {
                evaluation?.ReactionBlock();
                continue;
            }

            var cost = 0;
            var steps = new List<AiTacticalSearch.Move>();
            if (inHand)
            {
                var playable = await games.GetCardPlayabilityForModeAsync(game.GameId, actor, card.InstanceId, PlayMode.Base, token);
                var mode = playable.PlayModes.FirstOrDefault(item => item.Mode == PlayMode.Base);
                if (!playable.IsPlayable || playable.SelectedPlayMode != PlayMode.Base || playable.RequiresTargetSelection
                    || mode is not { IsAvailable: true, EffectiveCost: >= 0 } || mode.EffectiveCost > own.CurrentPP) continue;
                cost = mode.EffectiveCost;
                steps.Add(new("PlayCard", new PlayCardActionDto { CardUid = card.InstanceId, PlayMode = PlayMode.Base, PaidCost = cost }, view));
            }
            var ep = own.CurrentEP;
            if (evolve)
            {
                steps.Add(new("PerformEvolve", new EvolveActionDto { UnitUid = card.InstanceId.ToString() }, view));
                if (!view.HasKeyword(CardKeyword.FreeEvolve)) ep -= GameConstants.Evolution.EvolutionPointCost;
            }
            var recovered = Math.Min(leader.MaxHealth - leader.CurrentHealth, amount);
            routes.Add(new(steps[0], recovered - cost * .25 - (own.CurrentEP - ep), false, true)
            {
                Steps = steps, LeaderRecovery = recovered,
                Outcome = new(leader.CurrentHealth + recovered, enemy.LeaderCard?.CurrentHealth, own.CurrentPP - cost,
                    ep, own.CurrentSEP, own.Field.Count + (evolve && inHand ? 1 : 0), enemy.Field.Count)
            });
        }
        if (game.StateRevision != revision || GameDataProvider.Instance.DataRevision != catalog) return null;
        var best = routes.OrderBy(route => route.Steps.Count).ThenByDescending(route => route.Gain).FirstOrDefault();
        if (best != null) evaluation?.Verified();
        return best;
    }

    internal static int Read(AbilityConfig[] configs, bool evolve, bool inHand, int deckCount)
    {
        var eventType = evolve ? EventType.Evolve : EventType.PlayCard;
        var zone = evolve ? CardZone.Field : CardZone.Hand;
        var amount = 0;
        foreach (var ability in configs)
        {
            if (evolve && !inHand && ability.Trigger.EventTypes.SequenceEqual([EventType.PlayCard])
                && ability.Trigger.RelationType == TriggerRelationType.TargetSelf && ability.Auras.All(aura => !aura.IsEnabled)) continue;
            if (evolve && inHand && EntryAddsStudy(ability)) continue;
            if (!Plain(ability, eventType, zone)) return 0;
            foreach (var effect in ability.Effects.Where(effect => effect.IsEnabled))
            {
                if (!Simple(effect)) return 0;
                if (effect.Type == EffectType.Heal && effect.Parameters.Count == 1 && Target(ability, effect) == "context.owner.leader"
                    && Integer(Literal(effect, "HealAmount")) is int value && value is > 0 and <= 20)
                    amount += value;
                else if (evolve && effect.Type == EffectType.SpellBoost && effect.Parameters.Count == 0
                    && Target(ability, effect) == OwnHandBoost) continue;
                else if (!evolve && effect.Type == EffectType.Draw && effect.TargetSelector.Count == 0
                    && Integer(Literal(effect, "DrawAmount")) is int draws && draws == 1 && deckCount > draws
                    && effect.Parameters.Count == 1 && ability.Targets.All(pair => !pair.Value.IsEnabled)) continue;
                else return 0;
            }
        }
        return Math.Min(20, amount);
    }

    private static bool EntryAddsStudy(AbilityConfig ability)
    {
        if (!Plain(ability, EventType.PlayCard, CardZone.Hand) || ability.Targets.Any(pair => pair.Value.IsEnabled)
            || ability.Effects.Where(effect => effect.IsEnabled).ToArray() is not { Length: 1 } effects
            || effects[0] is not { Type: EffectType.GetToken } effect || !Simple(effect)
            || Text(Literal(effect, "PlayerScope")) != "self" || Text(Literal(effect, "CardSourceMode")) != "definition"
            || Integer(Literal(effect, "Amount")) != 1 || !IsStudy(Literal(effect, "CardDefinitionId"))) return false;
        var study = GameDataProvider.Instance.GetAbilityConfigs(DiligentStudy)?.Where(config => config.IsEnabled).ToArray();
        return study is { Length: 2 } && study.Count(config => Plain(config, EventType.PlayCard, CardZone.Hand)) == 1
            && study.Any(config => CostOnlySpellBoost(config));
    }

    private static bool CostOnlySpellBoost(AbilityConfig config, bool allowPermanent = false)
    {
        if (!Plain(config, EventType.SpellBoost, CardZone.Hand)
            || config.Effects.Where(item => item.IsEnabled).ToArray() is not { Length: 1 } changes) return false;
        var change = changes[0];
        if (change.Type != EffectType.ModifyStatus || !Simple(change) || change.Parameters.Count != 4
            || Integer(Literal(change, "CostModifier")) != -1 || Integer(Literal(change, "DurationParameter")) != 0
            || Text(Literal(change, "ModifierLayer")) != "additive") return false;
        var duration = Text(Literal(change, "DurationType"));
        var selfTarget = Target(config, change) == "context.sourcecard";
        if (duration == "OwnerTurnEnd" && selfTarget) return true;
        return allowPermanent && string.Equals(duration, "permanent", StringComparison.OrdinalIgnoreCase)
            && (selfTarget || change.TargetSelector.Count == 0 && config.Targets.All(pair => !pair.Value.IsEnabled));
    }

    private static bool Plain(AbilityConfig config, EventType type, params CardZone[] zones) => config.IsEnabled
        && config.Type == AbilityType.Triggered && config.Trigger.IsEnabled && config.Trigger.EventTypes.SequenceEqual([type])
        && config.Trigger.Timing == TriggerTiming.On && config.Trigger.RelationType == TriggerRelationType.TargetSelf
        && zones.Any(config.Trigger.IsValidZone) && config.Trigger.CountLimit == null && !config.Trigger.LimitSameNameOncePerTransaction
        && config.Conditions.All(condition => !condition.IsEnabled) && config.PreConditions.All(condition => !condition.IsEnabled)
        && config.Auras.All(aura => !aura.IsEnabled) && config.Properties.Count == 0 && config.CounterRegistrations.Count == 0
        && config.PlayerCounterRegistrations.Count == 0 && config.ActivateCost == null && config.AlternatePlay == null
        && (config.AbilityKeywords.Count == 0 || config.AbilityKeywords.Count == 1
            && (type == EventType.PlayCard && config.HasKeyword(AbilityKeyword.Fanfare)
                || type == EventType.SpellBoost && config.HasKeyword(AbilityKeyword.SpellBoost)));

    private static bool Simple(EffectConfig effect) => effect.RepeatCount == 0 && effect.RepeatCountValue == null
        && effect.RepeatCondition == null && effect.ChainedAbilities.Count == 0 && effect.Outputs?.Count is not > 0;

    private static string? Target(AbilityConfig config, EffectConfig effect)
    {
        var enabled = config.Targets.Where(pair => pair.Value.IsEnabled).ToArray();
        if (enabled.Any(pair => pair.Value.PlayerChoiceOptions != null || pair.Value.RequireValidTarget
            || pair.Value.Query.Operations.Count != 0)) return null;
        var target = effect.TargetSelector.Count == 0 && enabled.Length == 1 ? enabled[0].Value
            : effect.TargetSelector.Count == 1 ? enabled.FirstOrDefault(pair => pair.Key == effect.TargetSelector[0]).Value : null;
        return target == null ? null : string.Concat(target.Query.SourcePoolExpression.Where(ch => !char.IsWhiteSpace(ch) && ch is not '(' and not ')')).ToLowerInvariant();
    }

    private static object? Literal(EffectConfig effect, string key) => effect.Parameters
        .FirstOrDefault(pair => pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value is StaticValueConfig value ? value.Value : null;
    private static string? Text(object? value) => value is JsonElement { ValueKind: JsonValueKind.String } json ? json.GetString() : value as string;
    private static int? Integer(object? value) => value is int number ? number
        : value is JsonElement { ValueKind: JsonValueKind.Number } json && json.TryGetInt32(out var parsed) ? parsed : null;
    private static bool IsStudy(object? value) => value is IEnumerable<int> ids ? ids.SequenceEqual([DiligentStudy])
        : value is JsonElement { ValueKind: JsonValueKind.Array } json && json.GetArrayLength() == 1 && Integer(json[0]) == DiligentStudy;
}
