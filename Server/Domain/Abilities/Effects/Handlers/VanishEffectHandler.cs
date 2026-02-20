using Microsoft.Extensions.Logging;
using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;

using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// 消失效果处理器
    /// 使目标消失，与破坏效果不同的是不触发死亡相关效果（如LastWords）
    /// </summary>
    [EffectHandler(EffectType.Vanish)]
    public class VanishEffectHandler : EffectHandlerBase<VanishEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public VanishEffectHandler(ILogger<VanishEffectHandler> logger, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, VanishEffectParams parameters)
        {
            const string effectName = "VanishEffect";
            Logger.LogDebug("{EffectName}: Starting with VanishReason={Reason}.", effectName, EventReasonSource.Ability);

            Func<TargetInfo, GameContext, DomainOperationResult> vanishAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != Common.TargetType.Card || targetInfo.Card == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a card or card data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "Vanish effect can only be applied to cards." };
                    }

                    var card = targetInfo.Card;
                    var ownerPlayer = ctx.CurrentGameState.GetPlayerState(card.OwnerPlayerId);
                    if (ownerPlayer == null)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "Owner player not found." };
                    }

                    var result = new DomainOperationResult { Success = true };
                    result.GeneratedEvents.Add(new LeaveFieldEvent(
                        card,
                        LeaveFieldKind.Vanish,
                        EventReasonSource.Ability,
                        ctx.SourceCard));
                    return result;
                };

            return _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                vanishAction,
                effectName);
        }
    }
}
