using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Dtos.Enums; // Added for CardZone

using ShadowverseBeyond.Server.Services.Abstractions; // Added for IBuffService
namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    using ShadowverseBeyond.Server.Domain.Buffs;
    using ShadowverseBeyond.Server.Domain.Abilities.Common;
    using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
    using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
    using ShadowverseBeyond.Server.Domain.Effects;

    [EffectHandler(EffectType.ModifyStatus)]
    public class ModifyStatsEffectHandler : EffectHandlerBase<ModifyStatsEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public ModifyStatsEffectHandler(ILogger<ModifyStatsEffectHandler> logger,
                                        ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, ModifyStatsEffectParams parameters)
        {
            const string effectName = "ModifyStatsEffect";

            // The action for each target is now simply to create a StatChangedEvent.
            // The event's Commit method will handle the rest of the logic.
            Func<TargetInfo, GameContext, DomainOperationResult> modifyStatsAction =
                (targetInfo, ctx) =>
                {
                    var result = new DomainOperationResult();
                    if (targetInfo.Type != Common.TargetType.Card || targetInfo.Card == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a card or card data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "ModifyStats effect can only be applied to cards." };
                    }

                    var card = targetInfo.Card;

                    // Create the event with the modifier values from the effect parameters.
                    var effectKey = BuildEffectKey(effect);
                    var statChangedEvent = new StatChangedEvent(
                        targetCard: card,
                        attackModifier: parameters.AttackModifier,
                        healthModifier: parameters.HealthModifier,
                        costModifier: parameters.CostModifier,
                        durationType: parameters.DurationType,
                        durationParameter: parameters.DurationParameter,
                        modifierLayer: parameters.ModifierLayer,
                        effectKey: effectKey,
                        canStack: parameters.CanStack
                    );

                    result.GeneratedEvents.Add(statChangedEvent);
                    result.Success = true;
                    return result;
                };

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                modifyStatsAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for modify stats effect, or no valid targets found.";
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

        // CreateEffectCompletedEvent method is removed as this responsibility has moved to EffectExecutor.
    }
}
