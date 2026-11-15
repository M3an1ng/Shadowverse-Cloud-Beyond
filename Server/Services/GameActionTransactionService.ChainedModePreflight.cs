using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services
{
    public partial class GameActionTransactionService
    {
        private readonly record struct ChainedModeKey(Guid PlayEventId, string EffectPath);
        private sealed record PreparedChainedMode(int Count, string OptionConfigs, IReadOnlyList<int> Indexes);

        private async Task<PausableTransactionResult> PrepareChainedModeAsync(
            GameContext context, PlayCardEvent play, Ability rootAbility)
        {
            var noChoice = PausableTransactionResult.Success(new List<IGameEvent>());
            if (_activePreflightSession == null || play.PlayingPlayer is not { IsAI: false } player
                || player.PlayerId != context.RequestingPlayerId || context.CurrentGameState.IsCombatSandbox
                || play.AlternateCardDefinitionId.HasValue || rootAbility.SourceCard == null
                || rootAbility.TriggerConfig.RelationType != TriggerRelationType.TargetSelf
                || !rootAbility.Config.Effects.Any(effect => effect.IsEnabled && effect.ChainedAbilities.Count > 0))
            {
                return noChoice;
            }

            var knownModes = new List<(Effect Effect, GameContext Context)>();
            var rootContext = context with { SourceCard = rootAbility.SourceCard, SourceAbility = rootAbility };
            if (!FindVisibleChainedModes(rootAbility, rootContext, player.BloodPool, bloodWasSpent: false, depth: 0, knownModes)
                || knownModes.Count != 1)
            {
                return noChoice;
            }

            var (effect, choiceContext) = knownModes[0];
            var decision = await PrepareDecisionEventAsync(effect, choiceContext);
            if (decision.Result.ShouldStop || decision.DecisionEvent is not ChooseEvent choose
                || decision.DecisionAbility is not { } choiceAbility)
            {
                return noChoice;
            }

            var options = choiceAbility.GetDecisionOptions(effect.TriggeredEventId, effect.EffectIndex, Ability.DecisionMode.Choose);
            var optionConfigs = SerializeModeOptions(options);
            var resolved = await ResolveDecisionEventIfNeededAsync(choiceContext, choose, DecisionResolutionPhase.ChainedPreflight);
            if (resolved.ShouldStop)
            {
                return resolved;
            }

            var key = new ChainedModeKey(play.EventId, EnsureSequenceScopePath(effect, choiceAbility));
            _activePreflightSession.ChainedModes[key] = new PreparedChainedMode(
                choose.SelectionCount, optionConfigs, choose.SelectedOptionIds.ToList());
            return noChoice;
        }

        private bool FindVisibleChainedModes(
            Ability ability, GameContext context, int blood, bool bloodWasSpent, int depth,
            List<(Effect Effect, GameContext Context)> modes)
        {
            var config = ability.Config;
            if (!config.IsEnabled) return true;
            if (depth > 12 || config.Targets.Values.Any(target => target.IsEnabled)
                || config.Auras.Any(aura => aura.IsEnabled) || ability.HasKeyword(AbilityKeyword.Intromezzo)
                || ability.TriggerConfig.LimitSameNameOncePerTransaction)
            {
                return false;
            }

            var conditions = config.Conditions.Where(condition => condition.IsEnabled).ToList();
            if (conditions.Any(condition => !PlayTargetPreview.CanPreviewCondition(condition.Expression, allowBloodPool: !bloodWasSpent)))
                return false;
            var evaluator = context.ServiceProvider.GetRequiredService<IConditionEvaluator>();
            if (!conditions.All(condition => evaluator.Evaluate(condition, context))) return true;

            var effects = config.Effects.Where(effect => effect.IsEnabled).ToList();
            if (effects is not [var step] || step.RepeatCount != 0 || step.RepeatCountValue != null
                || step.RepeatCondition?.IsEnabled == true || step.TargetSelector.Count != 0)
            {
                return false;
            }

            var effect = new Effect(step, context.RequestingPlayerId, null, ability.RuntimeId,
                context.TriggeringEvent!.EventId, ability.SourceCard?.InstanceId)
            {
                SourceAbility = ability,
                SummaryRootAbilityId = ability.SummaryRootAbilityId
            };
            var effectContext = context with { CurrentEffect = effect };
            if (step.Type == EffectType.Choose)
            {
                if (depth == 0 || !IsStaticDecisionParameter(step, "abilityConfigs")
                    || !IsStaticDecisionParameter(step, "selectionCount"))
                {
                    return false;
                }

                modes.Add((effect, effectContext));
                return true;
            }

            // Preview a mode through an empty step or an affordable, fixed payment from the
            // player's own blood pool. Neither gameplay effects nor reactions run during this walk.
            if (step.Outputs is { Count: > 0 } || step.ChainedAbilities.Count == 0) return false;
            if (step.Type == EffectType.Sacrifice)
            {
                if (step.Parameters.GetValueOrDefault("Amount") is not StaticValueConfig amount
                    || step.Parameters.GetValueOrDefault("PlayerScope") is not StaticValueConfig playerScope)
                {
                    return false;
                }
                var resolver = context.ServiceProvider.GetRequiredService<IValueSourceResolver>();
                if (!string.Equals(playerScope.Resolve(context, resolver)?.ToString(), "self", StringComparison.OrdinalIgnoreCase)
                    || !int.TryParse(amount.Resolve(context, resolver)?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var cost)
                    || cost <= 0 || cost > blood)
                {
                    return false;
                }
                blood -= cost;
                bloodWasSpent = true;
            }
            else if (step.Type != EffectType.None || step.Parameters.Count != 0)
            {
                return false;
            }

            var children = CreateChainedAbilitiesForExecution(effect, effectContext);
            if (!children.Success) return false;

            var completion = new EffectCompletedEvent(step.Type,
                new Dictionary<string, object>(), Array.Empty<TargetInfo>(), Array.Empty<IGameEvent>(),
                Array.Empty<CardInstance>(), Array.Empty<CardInstanceSnapshot>(), new Dictionary<string, object?>(),
                parentEffectSucceeded: true, sourceCard: ability.SourceCard);
            foreach (var candidate in children.Abilities.OfType<Ability>())
            {
                var childContext = effectContext with
                {
                    SourceCard = candidate.SourceCard,
                    SourceAbility = candidate,
                    TriggeringEvent = completion
                };
                if (!FindVisibleChainedModes(candidate, childContext, blood, bloodWasSpent, depth + 1, modes))
                    return false;
            }
            return true;
        }

        private void TrackChainedModeRoot(Effect parent, Effect child)
        {
            if (_activePreflightSession is not { ChainedModes.Count: > 0 } session) return;
            var rootEvent = session.ChainedModeRootEventsByEffect.GetValueOrDefault(parent.Id, parent.TriggeredEventId);
            if (session.ActionRootEventIds.Contains(rootEvent))
            {
                session.ChainedModeRootEventsByEffect[child.Id] = rootEvent;
            }
        }

        private bool TryTakePreparedChainedMode(Effect effect, Ability ability, ChooseEvent choose,
            IReadOnlyList<Ability.DecisionOptionSnapshot> options)
        {
            if (_activePreflightSession is not { ChainedModes.Count: > 0 } session
                || !session.ChainedModeRootEventsByEffect.TryGetValue(effect.Id, out var rootEvent))
            {
                return false;
            }

            var key = new ChainedModeKey(rootEvent, EnsureSequenceScopePath(effect, ability));
            // The real effect always prepares its own options. If a preceding reaction changed
            // the active branch or its inputs, the normal settlement prompt will handle it.
            if (!session.ChainedModes.Remove(key, out var prepared)
                || prepared.Count != choose.SelectionCount
                || prepared.OptionConfigs != SerializeModeOptions(options)
                || prepared.Indexes.Count != Math.Min(choose.SelectionCount, options.Count))
            {
                return false;
            }

            choose.SetSelectedOptionIndexes(prepared.Indexes);
            return true;
        }

        private static string SerializeModeOptions(IReadOnlyList<Ability.DecisionOptionSnapshot> options)
            => JsonSerializer.Serialize(options.Select(option => option.Config));
    }
}
