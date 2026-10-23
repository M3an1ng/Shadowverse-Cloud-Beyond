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
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.Tuck)]
    public class TuckEffectHandler : EffectHandlerBase<TuckEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public TuckEffectHandler(ILogger<TuckEffectHandler> logger, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, TuckEffectParams parameters)
        {
            const string effectName = "TuckEffect";
            Logger.LogDebug("{EffectName}: Starting.", effectName);

            var validTargets = effect.SelectedTargets
                .Where(target => target.Type == Common.TargetType.Card
                                 && target.Card != null
                                 && target.Card.CurrentZone == CardZone.Field)
                .ToList();

            if (validTargets.Count == 0)
            {
                return new GameStateActionResult
                {
                    Success = false,
                    ErrorMessage = "Tuck effect has no valid field targets."
                };
            }

            Func<TargetInfo, GameContext, DomainOperationResult> tuckAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Card?.CurrentZone != CardZone.Field)
                    {
                        Logger.LogWarning("{EffectName}: Target card zone is not Field.", effectName);
                        return new DomainOperationResult { Success = true };
                    }

                    var result = new DomainOperationResult { Success = true };
                    result.GeneratedEvents.Add(new LeaveFieldEvent(
                        targetInfo.Card!,
                        LeaveFieldKind.Tuck,
                        EventReasonSource.Ability,
                        ctx.SourceCard,
                        deckInsertMode: parameters.InsertMode));
                    return result;
                };

            return _targetProcessingService.ProcessTargets(
                context,
                validTargets,
                tuckAction,
                effectName);
        }
    }
}
