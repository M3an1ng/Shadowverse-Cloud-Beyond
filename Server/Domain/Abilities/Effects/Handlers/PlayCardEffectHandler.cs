using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// 出牌效果处理器。
    /// 此处理器遵循标准效果处理流程，对目标玩家执行出牌操作。
    /// </summary>
    [EffectHandler(EffectType.Play)]
    public class PlayCardEffectHandler : EffectHandlerBase<PlayCardEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public PlayCardEffectHandler(ILogger<PlayCardEffectHandler> logger, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, PlayCardEffectParams parameters)
        {
            const string effectName = "PlayCardEffect";

            Func<TargetInfo, GameContext, DomainOperationResult> playCardAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != Common.TargetType.Player || targetInfo.Player == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a player or player data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "PlayCard effect can only be applied to players." };
                    }

                    var playerState = targetInfo.Player;
                    var cardInHand = playerState.Hand.FirstOrDefault(c => c.InstanceId == parameters.CardInstanceId);
                    if (cardInHand == null)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = $"Card with InstanceId {parameters.CardInstanceId} not found in hand for player {playerState.PlayerId}." };
                    }

                    var result = new DomainOperationResult();
                    var evt = new PlayCardEvent(playerState, cardInHand);
                    result.GeneratedEvents.Add(evt);
                    result.Success = true;
                    return result;
                };

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                playCardAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for play card effect, or no valid targets found.";
            }

            return serviceResult;
        }
    }
}
