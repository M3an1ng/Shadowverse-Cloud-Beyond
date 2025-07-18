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
    /// 消失效果处理器
    /// 使目标消失，与摧毁效果不同的是不触发死亡相关效果（如LastWords）
    /// </summary>
    public class VanishEffectHandler : EffectHandlerBase<VanishEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public VanishEffectHandler(ILogger<VanishEffectHandler> logger, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, VanishEffectParams parameters)
        {
            const string effectName = "VanishEffect";
            Logger.LogDebug("{EffectName}: Starting with VanishReason={Reason}.", effectName, parameters.VanishReason);

            Func<CardInstance, GameContext, Task<DomainOperationResult>> cardVanishAction =
                async (card, ctx) =>
                {
                    var ownerPlayer = ctx.CurrentGameState.GetPlayerState(card.OwnerPlayerId);
                    if (ownerPlayer == null)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "Owner player not found." };
                    }

                    // Delegate the core logic to the domain entity
                    var operationResult = ownerPlayer.VanishCard(card);
                    await Task.CompletedTask;
                    return operationResult;
                };

            return await _targetProcessingService.ProcessTargetsAsync(
                context,
                null,
                cardVanishAction,
                effectName);
        }
    }
}
