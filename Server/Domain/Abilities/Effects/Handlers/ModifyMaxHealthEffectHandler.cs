using System;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.AdjustMaxHealth)]
    public class ModifyMaxHealthEffectHandler : EffectHandlerBase<ModifyMaxHealthEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public ModifyMaxHealthEffectHandler(
            ILogger<ModifyMaxHealthEffectHandler> logger,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, ModifyMaxHealthEffectParams parameters)
        {
            const string effectName = "AdjustMaxHealthEffect";

            Func<TargetInfo, GameContext, DomainOperationResult> action =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != ShadowverseBeyond.Server.Domain.Abilities.Common.TargetType.Card || targetInfo.Card == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a card or card data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "AdjustMaxHealth effect can only be applied to cards." };
                    }

                    if (!parameters.HealthModifier.HasValue)
                    {
                        return new DomainOperationResult { Success = true };
                    }

                    var effectKey = BuildEffectKey(effect);
                    var statChangedEvent = new StatChangedEvent(
                        targetCard: targetInfo.Card,
                        attackModifier: null,
                        healthModifier: parameters.HealthModifier.Value,
                        costModifier: null,
                        durationType: parameters.DurationType,
                        durationParameter: parameters.DurationParameter,
                        modifierLayer: parameters.ModifierLayer,
                        effectKey: effectKey,
                        canStack: parameters.CanStack,
                        removeCondition: parameters.RemoveCondition,
                        preserveCurrentHealthOnMaxHealthChange: true);

                    var result = new DomainOperationResult { Success = true };
                    result.GeneratedEvents.Add(statChangedEvent);
                    return result;
                };

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                action,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for adjust max health effect, or no valid targets found.";
            }

            return serviceResult;
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
