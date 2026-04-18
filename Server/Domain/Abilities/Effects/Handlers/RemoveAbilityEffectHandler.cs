using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Removes (suppresses) abilities from target cards.
    /// </summary>
    [EffectHandler(EffectType.RemoveAbility)]
    public class RemoveAbilityEffectHandler : EffectHandlerBase<RemoveAbilityEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;
        private readonly IBuffFactory _buffFactory;
        private readonly IBuffService _buffService;

        public RemoveAbilityEffectHandler(
            ILogger<RemoveAbilityEffectHandler> logger,
            ITargetProcessingService targetProcessingService,
            IBuffFactory buffFactory,
            IBuffService buffService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
            _buffFactory = buffFactory ?? throw new ArgumentNullException(nameof(buffFactory));
            _buffService = buffService ?? throw new ArgumentNullException(nameof(buffService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, RemoveAbilityEffectParams parameters)
        {
            const string effectName = "RemoveAbilityEffect";
            var currentAbility = context.GetCurrentAbility();

            if (!ShouldApplySuppression(parameters, currentAbility))
            {
                return new GameStateActionResult { Success = true };
            }

            var effectKey = BuildEffectKey(effect);

            Func<TargetInfo, GameContext, DomainOperationResult> removeAbilityAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != TargetType.Card || targetInfo.Card == null)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "RemoveAbility effect can only be applied to cards." };
                    }

                    var result = new DomainOperationResult { Success = true };
                    var card = targetInfo.Card;

                    var sourceAbility = ctx.GetCurrentAbility();
                    var sourceCard = ctx.SourceCard;
                    var ownerPlayer = ctx.CurrentGameState.GetPlayerState(sourceCard?.OwnerPlayerId ?? ctx.RequestingPlayerId);
                    var turnNumber = ownerPlayer?.TurnNumber ?? 0;

                    var suppressionEffect = new AbilitySuppressionBuffEffect(
                        parameters.RemoveMode,
                        parameters.AbilityDefinitionIds,
                        parameters.AbilityKeyword,
                        sourceAbility?.RuntimeId);
                    var effects = new List<IBuffEffect> { suppressionEffect };

                    if (parameters.RemoveMode == RemoveAbilityMode.All)
                    {
                        var capturedKeywords = _buffService.GetEffectiveKeywords(card, ctx)
                            .Where(keyword => keyword != CardKeyword.None)
                            .Distinct()
                            .ToList();
                        if (capturedKeywords.Count > 0)
                        {
                            effects.Add(new RemoveKeywordBuffEffect(capturedKeywords));
                        }
                    }

                    var suppressedAbilities = AbilitySuppressionEvaluator.CollectSuppressedAbilities(
                        ctx.AbilityManager.GetAbilitiesForCard(card.InstanceId),
                        new[] { suppressionEffect });
                    foreach (var suppressedAbility in suppressedAbilities)
                    {
                        if (suppressedAbility is Ability concreteAbility)
                        {
                            concreteAbility.InvalidateQueuedEffects("remove_ability");
                        }
                    }

                    Buff buff;
                    if (sourceAbility == null)
                    {
                        buff = new Buff(
                            sourceAbilityId: Guid.Empty,
                            targetCardInstanceId: card.InstanceId,
                            currentTurn: turnNumber,
                            effects: effects,
                            durationType: parameters.DurationType,
                            durationParameter: parameters.DurationParameter,
                            removalCondition: parameters.RemoveCondition);
                    }
                    else
                    {
                        buff = _buffFactory.CreateCompositeBuff(
                            target: card,
                            sourceCard: sourceCard,
                            sourceAbility: sourceAbility,
                            effects: effects,
                            durationType: parameters.DurationType,
                            durationParameter: parameters.DurationParameter,
                            currentTurn: turnNumber,
                            removeCondition: parameters.RemoveCondition);
                    }

                    if (!string.IsNullOrWhiteSpace(effectKey) && buff.SourceAbilityId != Guid.Empty)
                    {
                        buff.EffectKey = effectKey;
                    }

                    result.Merge(_buffService.ApplyBuffProcess(buff, ctx, parameters.CanStack));
                    return result;
                };

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                removeAbilityAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for remove ability effect, or no valid targets found.";
            }

            return serviceResult;
        }

        private static bool ShouldApplySuppression(RemoveAbilityEffectParams parameters, IAbility? currentAbility)
        {
            if (parameters.RemoveMode == RemoveAbilityMode.All)
            {
                return true;
            }

            if (parameters.RemoveMode == RemoveAbilityMode.Self)
            {
                return currentAbility != null && currentAbility.RuntimeId != Guid.Empty;
            }

            if (parameters.RemoveMode == RemoveAbilityMode.Id)
            {
                return parameters.AbilityDefinitionIds != null && parameters.AbilityDefinitionIds.Count > 0;
            }

            return parameters.RemoveMode == RemoveAbilityMode.Keyword
                   && parameters.AbilityKeyword != AbilityKeyword.None;
        }

        private static string? BuildEffectKey(Effect effect)
        {
            if (!effect.sourceAbilityId.HasValue)
            {
                return null;
            }

            return $"{effect.sourceAbilityId.Value:N}:{effect.EffectIndex}";
        }
    }
}
