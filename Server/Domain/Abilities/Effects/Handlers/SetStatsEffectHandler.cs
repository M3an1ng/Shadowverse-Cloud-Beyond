using System;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.SetStatus)]
    public class SetStatsEffectHandler : EffectHandlerBase<SetStatsEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public SetStatsEffectHandler(
            ILogger<SetStatsEffectHandler> logger,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, SetStatsEffectParams parameters)
        {
            const string effectName = "SetStatsEffect";

            Func<TargetInfo, GameContext, DomainOperationResult> setStatsAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != TargetType.Card || targetInfo.Card == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a card or card data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "SetStats effect can only be applied to cards." };
                    }

                    var card = targetInfo.Card;
                    if (parameters.AttackModifier == null
                        && parameters.HealthModifier == null
                        && parameters.CostModifier == null)
                    {
                        return new DomainOperationResult { Success = true };
                    }

                    var effectKey = BuildEffectKey(effect);
                    var statChangedEvent = new StatChangedEvent(
                        targetCard: card,
                        attackModifier: parameters.AttackModifier,
                        healthModifier: parameters.HealthModifier,
                        costModifier: parameters.CostModifier,
                        durationType: parameters.DurationType,
                        durationParameter: parameters.DurationParameter,
                        modifierLayer: ModifierLayer.Set,
                        effectKey: effectKey,
                        canStack: parameters.CanStack,
                        removeCondition: parameters.RemoveCondition
                    );

                    var result = new DomainOperationResult { Success = true };
                    result.GeneratedEvents.Add(statChangedEvent);
                    return result;
                };

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                setStatsAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for set stats effect, or no valid targets found.";
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
