using System;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.SetCountdown)]
    public class SetCountdownEffectHandler : EffectHandlerBase<SetCountdownEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public SetCountdownEffectHandler(
            ILogger<SetCountdownEffectHandler> logger,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, SetCountdownEffectParams parameters)
        {
            const string effectName = "SetCountdownEffect";

            Func<TargetInfo, GameContext, DomainOperationResult> action =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != TargetType.Card || targetInfo.Card == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a card or card data is missing. TargetType: {TargetType}",
                            effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "SetCountdown effect can only be applied to cards." };
                    }

                    var card = targetInfo.Card;
                    if (!card.CurrentCountdown.HasValue)
                    {
                        return new DomainOperationResult { Success = true };
                    }

                    var result = new DomainOperationResult { Success = true };
                    result.GeneratedEvents.Add(new CountdownTickEvent(card, parameters.ChangeAmount));
                    return result;
                };

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                action,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for set countdown effect, or no valid targets found.";
            }

            return serviceResult;
        }
    }
}
