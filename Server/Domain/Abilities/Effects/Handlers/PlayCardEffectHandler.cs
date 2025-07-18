using Microsoft.Extensions.Logging;
using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;

using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Exceptions;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// 【重构】出牌效果处理器。
    /// 此处理器遵循新的架构，使用 ITargetProcessingService 来处理作为目标的玩家，
    /// 并调用 PlayerState 中新增的领域方法 ExecutePlayCard 来执行核心逻辑。
    /// </summary>
    public class PlayCardEffectHandler : EffectHandlerBase<PlayCardEffectParams>
    {
        private readonly IGameDataProvider _gameDataProvider;
        private readonly ITargetProcessingService _targetProcessingService;

        public PlayCardEffectHandler(
            ILogger<PlayCardEffectHandler> logger,
            IGameDataProvider gameDataProvider,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _gameDataProvider = gameDataProvider ?? throw new ArgumentNullException(nameof(gameDataProvider));
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, PlayCardEffectParams parameters)
        {
            const string effectName = "PlayCardEffect";

            var playerState = context.RequestingPlayer;
            if (playerState == null)
                return new GameStateActionResult { Success = false, ErrorMessage = "Requesting player not found." };

            var cardInHand = playerState.Hand.FirstOrDefault(c => c.InstanceId == parameters.CardInstanceId);
            if (cardInHand == null)
                return new GameStateActionResult { Success = false, ErrorMessage = "Card not found in hand." };


            // 定义对目标（玩家）执行的操作
            Func<PlayerState, GameContext, Task<DomainOperationResult>> playerAction =
                async (player, ctx) =>
                {

                    // 调用PlayerState中的新领域方法
                    var operationResult = player.ExecutePlayCard(cardInHand);

                    await Task.CompletedTask;

                    return operationResult;
                };

            // 使用服务来处理这个单一的玩家目标
            return await _targetProcessingService.ProcessTargetsAsync(
                context,
                playerAction,
                null, // No card action
                effectName);
        }


    }
}
