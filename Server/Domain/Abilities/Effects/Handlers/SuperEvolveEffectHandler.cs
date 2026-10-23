using System;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.SuperEvolve)]
    public class SuperEvolveEffectHandler : EffectHandlerBase<SuperEvolveEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public SuperEvolveEffectHandler(
            ILogger<SuperEvolveEffectHandler> logger,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, SuperEvolveEffectParams parameters)
        {
            const string effectName = "SuperEvolveEffect";
            Logger.LogDebug("{EffectName}: Starting super evolve effect processing.", effectName);

            Func<TargetInfo, GameContext, DomainOperationResult> createSuperEvolveActionEventAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Card == null)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "SuperEvolve target is not a valid card." };
                    }

                    if (targetInfo.Card.CurrentZone != CardZone.Field)
                    {
                        Logger.LogWarning("{EffectName}: Target card zone is not Field.", effectName);
                        return new DomainOperationResult { Success = true };
                    }

                    var cardToEvolve = targetInfo.Card;
                    var ownerPlayer = ctx.CurrentGameState.GetPlayerState(cardToEvolve.OwnerPlayerId);
                    if (ownerPlayer == null)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "Owner player not found for target card." };
                    }
                    return cardToEvolve.ApplySuperEvolution(ownerPlayer, ctx.SourceCard, parameters.IsManualEvolution);
                };

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                createSuperEvolveActionEventAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for super evolve effect, or no valid targets found.";
            }

            return serviceResult;
        }
    }
}
