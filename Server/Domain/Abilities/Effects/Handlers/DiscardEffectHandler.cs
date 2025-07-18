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

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// 弃牌效果处理器
    /// 强制目标卡牌从其拥有者的手牌中被弃置
    /// </summary>
    public class DiscardEffectHandler : EffectHandlerBase<DiscardEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public DiscardEffectHandler(ILogger<DiscardEffectHandler> logger, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, DiscardEffectParams parameters)
        {
            const string effectName = "DiscardEffect";
            Logger.LogDebug("{EffectName}: Starting with DiscardReason={Reason}.", effectName, parameters.DiscardReason);

            Func<CardInstance, GameContext, Task<DomainOperationResult>> cardDiscardAction =
                (card, ctx) => // Made lambda synchronous as DiscardCard is sync
                {
                    var ownerPlayer = ctx.CurrentGameState.GetPlayerState(card.OwnerPlayerId);
                    if (ownerPlayer == null)
                    {
                        Logger.LogError("{EffectName}: Could not find owner PlayerState for card {CardId}.", effectName, card.InstanceId);
                        return Task.FromResult(new DomainOperationResult { Success = false, ErrorMessage = "Owner player not found." });
                    }

                    var operationResult = ownerPlayer.DiscardCard(card, parameters.DiscardReason);
                    return Task.FromResult(operationResult);
                };

            return await _targetProcessingService.ProcessTargetsAsync(
                context,
                null,
                cardDiscardAction,
                effectName);
        }

    }
}
