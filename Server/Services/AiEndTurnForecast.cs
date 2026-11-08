using System.Text.Json;
using System.Text.RegularExpressions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Services;

// Conservative, read-only projection of currently available automatic end-turn damage.
// Unsupported composites, choices, random pools and dynamic damage stay with the real engine.
internal static class AiEndTurnForecast
{
    internal static async Task<AiTacticalSearch.EndTurnForecast> ReadAsync(GameContext context, PlayerStateDto player)
    {
        var damage = new List<AiTacticalSearch.PendingDamage>();
        var unsafeAttackers = new HashSet<Guid>();
        var query = context.ServiceProvider.GetRequiredService<IQueryExecutor>();
        var conditions = context.ServiceProvider.GetRequiredService<IConditionEvaluator>();
        var owner = context.CurrentGameState.GetPlayerState(player.PlayerId)!;
        var ending = new TurnEndEvent(owner);
        foreach (var view in player.Field.Concat(player.Emblem))
        {
            var card = context.CurrentGameState.FindCardInstance(view.InstanceId);
            if (card == null) continue;
            foreach (var ability in context.AbilityManager.GetEffectiveAbilitiesForCard(card.InstanceId, context).OfType<Ability>())
            {
                // An attack-triggered ability can change hand size, remove a source or rewrite a condition.
                // Do not assume the current end-turn projection survives such an attack.
                if (ability.TriggerConfig.EventTypes.Any(type => type is EventType.Attack or EventType.AttackDeclared or EventType.Clash or EventType.CombatResolution))
                    unsafeAttackers.UnionWith(player.Field.Select(unit => unit.InstanceId));
                if (!ability.TriggerConfig.EventTypes.Contains(EventType.TurnEnd) || ability.TriggerConfig.CountLimit != null) continue;
                // Only cache predicates invariant under our attack simulation: same hand size and turn owner.
                if (ability.Config.Conditions.Where(condition => condition.IsEnabled).Any(condition =>
                {
                    var expression = Regex.Replace(condition.Expression, @"[\s()]", "");
                    return !expression.Equals("true", StringComparison.OrdinalIgnoreCase)
                        && !expression.Equals("context.triggeringEvent.EndingPlayer.playerId==context.owner.playerId", StringComparison.OrdinalIgnoreCase)
                        && !Regex.IsMatch(expression, @"^countcontext\.owner\.hand(==|<=|>=|<|>)[0-9]+$", RegexOptions.IgnoreCase);
                })) continue;
                var effects = ability.Config.Effects.Where(effect => effect.IsEnabled).ToArray();
                if (effects.Length == 0 || effects.Any(effect => effect.Type != EffectType.Damage
                    || effect.RepeatCount != 0 || effect.RepeatCountValue != null || effect.RepeatCondition != null
                    || effect.ChainedAbilities.Count != 0 || !effect.Parameters.TryGetValue("DamageAmount", out var amount)
                    || amount is not StaticValueConfig)) continue;
                if (ability.Targets.Any(target => target.Config.IsEnabled
                    && (target.Config.PlayerChoiceOptions?.IsRequired == true || target.Config.Query.Operations.Count > 0
                        || JsonSerializer.Serialize(target.Config.Query).Contains("random", StringComparison.OrdinalIgnoreCase)
                        || JsonSerializer.Serialize(target.Config.Query).Contains("sample", StringComparison.OrdinalIgnoreCase)))) continue;
                var evaluation = context with { SourceCard = card, SourceAbility = ability, TriggeringEvent = ending };
                if (!await ability.CanTrigger(ending, evaluation)
                    || ability.Config.Conditions.Any(condition => condition.IsEnabled && !conditions.Evaluate(condition, evaluation))) continue;
                var targets = new Dictionary<string, List<TargetInfo>>();
                var valid = true;
                foreach (var target in ability.Targets.Where(target => target.Config.IsEnabled).OrderBy(target => target.Config.Order ?? 0))
                {
                    var found = query.ExecuteQuery(target.Config.Query, evaluation, targets);
                    if (!found.Success) { valid = false; break; }
                    targets[target.Key] = found.Results.OfType<CardInstance>().Select(TargetInfo.ForCard).ToList();
                }
                if (!valid) continue;
                foreach (var effect in effects.OrderBy(effect => effect.ExecutionOrder))
                {
                    if (effect.Parameters.TryGetValue("DistributionMode", out var mode)
                        && mode is StaticValueConfig staticMode
                        && !string.Equals(staticMode.Value?.ToString(), "perTarget", StringComparison.OrdinalIgnoreCase)) continue;
                    var amount = AiTargetScoring.ReadAmount(effect);
                    if (amount is not > 0 || amount > 1000 || amount != Math.Floor(amount.Value)
                        || !EffectTargetSelector.TrySelectTargets(effect.TargetSelector, targets, out var selected)) continue;
                    damage.Add(new(card.InstanceId, selected.Select(target => target.Card?.InstanceId ?? Guid.Empty)
                        .Where(id => id != Guid.Empty).Distinct().ToArray(), (int)amount.Value, player.Hand.Count));
                }
            }
        }
        return new(damage, unsafeAttackers);
    }
}
