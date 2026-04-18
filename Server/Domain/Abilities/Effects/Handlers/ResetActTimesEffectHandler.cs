using System;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.ResetActTimes)]
    public class ResetActTimesEffectHandler : EffectHandlerBase<ResetActTimesEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public ResetActTimesEffectHandler(
            ILogger<ResetActTimesEffectHandler> logger,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, ResetActTimesEffectParams parameters)
        {
            const string effectName = "ResetActTimesEffect";

            Func<TargetInfo, GameContext, DomainOperationResult> action =
                (targetInfo, _) =>
                {
                    if (targetInfo.Type != Common.TargetType.Card || targetInfo.Card == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a card or card data is missing. TargetType: {TargetType}",
                            effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "ResetActTimes effect can only be applied to cards." };
                    }

                    var card = targetInfo.Card;
                    if (card.CurrentZone != CardZone.Field)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "ResetActTimes effect can only be applied to cards on the field." };
                    }

                    var result = new DomainOperationResult { Success = true };
                    result.GeneratedEvents.Add(new ActTimesResetEvent(card));
                    return result;
                };

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                action,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for reset act times effect, or no valid targets found.";
            }

            return serviceResult;
        }
    }
}
