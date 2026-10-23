using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Services.Abstractions;

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

            if (!TryResolveAbilityConfigs(context, parameters, out var abilityConfigs, out var errorMessage))
            {
                return new GameStateActionResult { Success = false, ErrorMessage = errorMessage };
            }

            if (abilityConfigs.Count == 0)
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
                        abilityConfigs,
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

        private static bool TryResolveAbilityConfigs(
            GameContext context,
            GrantAbilityEffectParams parameters,
            out List<AbilityConfig> abilityConfigs,
            out string? errorMessage)
        {
            abilityConfigs = new List<AbilityConfig>();
            errorMessage = null;

            if (parameters.SourceMode == GrantAbilitySourceMode.InlineConfigs)
            {
                abilityConfigs = parameters.AbilityConfigs ?? new List<AbilityConfig>();
                return true;
            }

            if (parameters.SourceMode != GrantAbilitySourceMode.AbilityDefinitionIds)
            {
                errorMessage = $"Unsupported grant ability source mode: {parameters.SourceMode}.";
                return false;
            }

            if (parameters.AbilityDefinitionIds == null || parameters.AbilityDefinitionIds.Count == 0)
            {
                return true;
            }

            var dataProvider = context.ServiceProvider.GetService<IGameDataProvider>();
            if (dataProvider == null)
            {
                errorMessage = "GrantAbility effect requires IGameDataProvider when using ability definition IDs.";
                return false;
            }

            foreach (var abilityId in parameters.AbilityDefinitionIds)
            {
                var config = dataProvider.GetAbilityConfig(abilityId);
                if (config == null)
                {
                    errorMessage = $"GrantAbility effect could not find ability definition {abilityId}.";
                    return false;
                }

                abilityConfigs.Add(config);
            }

            return true;
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
