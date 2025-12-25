using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AutoMapper;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;

using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.Evolve)]
    public class EvolveEffectHandler : EffectHandlerBase<EvolveEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;
        public EvolveEffectHandler(
            ILogger<EvolveEffectHandler> logger,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, EvolveEffectParams parameters)
        {
            const string effectName = "EvolveEffect";
            Logger.LogDebug("{EffectName}: Starting evolution effect processing.", effectName);

            Func<TargetInfo, GameContext, DomainOperationResult> createEvolveActionEventAction =
                (targetInfo, ctx) =>
                {
                    var result = new DomainOperationResult();
                    if (targetInfo.Card == null)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "Evolve target is not a valid card." };
                    }

                    var cardToEvolve = targetInfo.Card;
                    var ownerPlayer = ctx.CurrentGameState.GetPlayerState(cardToEvolve.OwnerPlayerId);
                    if (ownerPlayer == null)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "Owner player not found for target card." };
                    }

                    return ownerPlayer.EvolveCard(cardToEvolve);
                };

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                createEvolveActionEventAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for evolve effect, or no valid targets found.";
            }

            return serviceResult;
        }
    }
}
