using System;
using System.Collections.Generic; // Added for List
using System.Linq; // Added for OfType, FirstOrDefault
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Dtos; // Added for CardZone
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters; // For HealEffectParams
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services; // For ITargetProcessingService
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases; // Added for EffectHandlerBase
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // For TargetInfo
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Handles the application of the Heal effect.
    /// </summary>
    [EffectHandler(EffectType.Heal)]
    public class HealEffectHandler : EffectHandlerBase<HealEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public HealEffectHandler(ILogger<HealEffectHandler> logger, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, HealEffectParams parameters)
        {
            const string effectName = "HealEffect";
            Logger.LogDebug("{EffectName}: Starting with HealAmount={Amount}.", effectName, parameters.HealAmount);

            if (parameters.HealAmount <= 0)
            {
                Logger.LogInformation("{EffectName}: HealAmount is {HealAmount}. No healing will be applied.", effectName, parameters.HealAmount);
                return new GameStateActionResult { Success = true };
            }

            Func<TargetInfo, GameContext, DomainOperationResult> healAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != Common.TargetType.Card || targetInfo.Card == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a card or card data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "Heal effect can only be applied to cards." };
                    }

                    var card = targetInfo.Card;
                    var operationResult = card.Heal(parameters.HealAmount, ctx.SourceCard);
                    return operationResult;
                };

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                healAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for heal effect, or no valid targets found.";
            }

            return serviceResult;
        }

        // CreateEffectCompletedEvent method is removed as this responsibility has moved to EffectExecutor.
    }
}