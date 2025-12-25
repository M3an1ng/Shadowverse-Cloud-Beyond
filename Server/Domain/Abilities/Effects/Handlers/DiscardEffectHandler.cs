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
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// 弃牌效果处理器
    /// 强制目标卡牌从其拥有者的手牌中被弃置
    /// </summary>
    [EffectHandler(EffectType.Discard)]
    public class DiscardEffectHandler : EffectHandlerBase<DiscardEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public DiscardEffectHandler(ILogger<DiscardEffectHandler> logger, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, DiscardEffectParams parameters)
        {
            const string effectName = "DiscardEffect";
            Logger.LogDebug("{EffectName}: Starting with DiscardReason={Reason}.", effectName, parameters.DiscardReason);

            Func<TargetInfo, GameContext, DomainOperationResult> discardAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != Common.TargetType.Card || targetInfo.Card == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a card or card data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "Discard effect can only be applied to cards." };
                    }

                    var card = targetInfo.Card;
                    var ownerPlayer = ctx.CurrentGameState.GetPlayerState(card.OwnerPlayerId);
                    if (ownerPlayer == null)
                    {
                        Logger.LogError("{EffectName}: Could not find owner PlayerState for card {CardId}.", effectName, card.InstanceId);
                        return new DomainOperationResult { Success = false, ErrorMessage = "Owner player not found." };
                    }

                    var result = new DomainOperationResult { Success = true };
                    result.GeneratedEvents.Add(new CardDiscardedEvent(ownerPlayer, card, parameters.DiscardReason));
                    return result;
                };

            return _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                discardAction,
                effectName);
        }

    }
}
