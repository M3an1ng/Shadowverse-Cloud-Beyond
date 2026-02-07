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

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.ResetAttackTimes)]
    public class ResetAttackTimesEffectHandler : EffectHandlerBase<ResetAttackTimesEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public ResetAttackTimesEffectHandler(
            ILogger<ResetAttackTimesEffectHandler> logger,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, ResetAttackTimesEffectParams parameters)
        {
            const string effectName = "ResetAttackTimesEffect";

            Func<TargetInfo, GameContext, DomainOperationResult> action =
                (targetInfo, _) =>
                {
                    if (targetInfo.Type != Common.TargetType.Card || targetInfo.Card == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a card or card data is missing. TargetType: {TargetType}",
                            effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "ResetAttackTimes effect can only be applied to cards." };
                    }

                    var card = targetInfo.Card;
                    if (!card.IsFollower)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "ResetAttackTimes effect can only be applied to follower cards." };
                    }

                    var result = new DomainOperationResult { Success = true };
                    result.GeneratedEvents.Add(new AttackTimesResetEvent(card));
                    return result;
                };

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                action,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for reset attack times effect, or no valid targets found.";
            }

            return serviceResult;
        }
    }
}
