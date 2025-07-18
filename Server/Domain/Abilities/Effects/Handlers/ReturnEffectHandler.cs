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
    /// 返回效果处理器
    /// 将目标卡牌返回到指定区域（通常是手牌或牌库）
    /// </summary>
    public class ReturnEffectHandler : EffectHandlerBase<ReturnEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public ReturnEffectHandler(ILogger<ReturnEffectHandler> logger, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, ReturnEffectParams parameters)
        {
            const string effectName = "ReturnEffect";
            Logger.LogDebug("{EffectName}: Starting with TargetZone={Zone}.", effectName, parameters.TargetZone);

            Func<CardInstance, GameContext, Task<DomainOperationResult>> cardReturnAction =
                async (card, ctx) =>
                {
                    var ownerPlayer = ctx.CurrentGameState.GetPlayerState(card.OwnerPlayerId);
                    if (ownerPlayer == null)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "Owner player not found." };
                    }

                    // Delegate the core logic to the domain entity
                    var operationResult = ownerPlayer.ReturnCardToHand(card);
                    await Task.CompletedTask;
                    return operationResult;
                };

            return await _targetProcessingService.ProcessTargetsAsync(
                context,
                null,
                cardReturnAction,
                effectName);
        }
    }
}
