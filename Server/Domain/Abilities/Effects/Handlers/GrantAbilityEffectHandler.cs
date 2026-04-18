using System;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Common;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.GrantAbility)]
    public class GrantAbilityEffectHandler : EffectHandlerBase<GrantAbilityEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public GrantAbilityEffectHandler(
            ILogger<GrantAbilityEffectHandler> logger,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, GrantAbilityEffectParams parameters)
        {
            const string effectName = "GrantAbilityEffect";

            if (parameters.AbilityConfigs == null || parameters.AbilityConfigs.Count == 0)
            {
                return new GameStateActionResult { Success = true };
            }

            var effectKey = BuildEffectKey(effect);

            Func<TargetInfo, GameContext, DomainOperationResult> grantAbilityAction =
                (targetInfo, _) =>
                {
                    if (targetInfo.Type != TargetType.Card || targetInfo.Card == null)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "GrantAbility effect can only be applied to cards." };
                    }

                    var result = new DomainOperationResult { Success = true };
                    result.GeneratedEvents.Add(new GrantAbilityEvent(
                        parameters.AbilityConfigs,
                        targetInfo.Card,
                        context.SourceCard,
                        parameters.DurationType,
                        parameters.DurationParameter,
                        effectKey,
                        parameters.CanStack,
                        parameters.RemoveCondition));

                    return result;
                };

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                grantAbilityAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for grant ability effect, or no valid targets found.";
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
