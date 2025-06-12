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

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// 出牌效果处理器
    /// 处理玩家出牌的核心逻辑，包括验证、费用扣除、卡牌移动等
    /// </summary>
    public class PlayCardEffectHandler : EffectHandlerBase<PlayCardEffectParams>
    {
        private readonly IMapper _mapper;
        private readonly IGameDataProvider _gameDataProvider;

        public PlayCardEffectHandler(
            ILogger logger,
            IMapper mapper,
            IGameDataProvider gameDataProvider)
            : base(logger)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _gameDataProvider = gameDataProvider ?? throw new ArgumentNullException(nameof(gameDataProvider));
        }

        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, PlayCardEffectParams parameters)
        {
            const string effectName = "PlayCardEffect";
            Logger.LogDebug("{EffectName}: Starting with CardInstanceId={CardId}, TargetId={TargetId}.",
                           effectName, parameters.CardInstanceId, parameters.TargetId);

            // 验证参数
            if (parameters.CardInstanceId == Guid.Empty)
            {
                Logger.LogError("{EffectName}: CardInstanceId cannot be empty.", effectName);
                return new GameStateActionResult { Success = false, ErrorMessage = "CardInstanceId cannot be empty." };
            }

            // 获取卡牌定义
            var cardDefinition = _gameDataProvider.GetCardDefinition(parameters.CardDefinitionId);
            if (cardDefinition == null)
            {
                Logger.LogError("{EffectName}: Card definition not found for ID {CardDefinitionId}.",
                               effectName, parameters.CardDefinitionId);
                return new GameStateActionResult { Success = false, ErrorMessage = "Card definition not found." };
            }

            // 获取玩家状态
            var playerState = context.RequestingPlayer;
            if (playerState == null)
            {
                Logger.LogError("{EffectName}: Requesting player not found.", effectName);
                return new GameStateActionResult { Success = false, ErrorMessage = "Requesting player not found." };
            }

            // 查找手牌中的卡牌
            var cardInHand = playerState.Hand.FirstOrDefault(c => c.InstanceId == parameters.CardInstanceId);
            if (cardInHand == null)
            {
                Logger.LogError("{EffectName}: Card {CardInstanceId} not found in player's hand.",
                               effectName, parameters.CardInstanceId);
                return new GameStateActionResult { Success = false, ErrorMessage = "Card not found in hand." };
            }

            try
            {
                // 执行出牌逻辑
                var result = await ExecutePlayCardLogic(context, playerState, cardInHand, cardDefinition, parameters);

                Logger.LogInformation("{EffectName}: Successfully played card {CardName} ({CardInstanceId}).",
                                     effectName, cardDefinition.Name, parameters.CardInstanceId);

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "{EffectName}: Error playing card {CardInstanceId}.", effectName, parameters.CardInstanceId);
                return new GameStateActionResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// 执行出牌的核心逻辑
        /// </summary>
        private async Task<GameStateActionResult> ExecutePlayCardLogic(
            GameContext context,
            PlayerState playerState,
            CardInstance cardInHand,
            CardDefinition cardDefinition,
            PlayCardEffectParams parameters)
        {
            var events = new List<IGameEvent>();

            // 1. 验证出牌前置条件
            ValidatePlayCardPreconditions(context.CurrentGameState, playerState, cardDefinition, parameters);

            // 2. 解析卡牌类型
            if (!Enum.TryParse<CardType>(cardDefinition.Type, true, out var cardType))
            {
                throw new GameLogicException($"Unknown card type: {cardDefinition.Type}");
            }

            // 3. 使用PlayerState的领域方法执行出牌操作
            var playCardEvents = playerState.PlayCard(cardInHand, cardDefinition, cardType);
            events.AddRange(playCardEvents);

            // 5. 生成出牌事件
            var playerDto = _mapper.Map<PlayerStateDto>(playerState);
            var playCardEvent = new PlayCardEvent(playerDto, cardInHand);
            events.Add(playCardEvent);

            await Task.CompletedTask; // 出牌逻辑是同步的

            return new GameStateActionResult
            {
                Success = true,
                GeneratedEvents = events,
                ErrorMessage = $"Successfully played {cardDefinition.Name}."
            };
        }

        /// <summary>
        /// 验证出牌前置条件
        /// </summary>
        private static void ValidatePlayCardPreconditions(
            GameState gameState,
            PlayerState playerState,
            CardDefinition cardDefinition,
            PlayCardEffectParams parameters)
        {
            if (!parameters.ForcePlay)
            {
                // 游戏状态验证
                if (gameState.IsGameOver)
                    throw new GameLogicException("Game is already over.");

                if (gameState.CurrentPlayerId != playerState.PlayerId)
                    throw new GameLogicException($"It's not player {playerState.PlayerId}'s turn.");

                // 费用验证
                if (playerState.CurrentPlayPoints < cardDefinition.Cost)
                    throw new GameLogicException($"Not enough PP to play {cardDefinition.Name}. Requires {cardDefinition.Cost}, has {playerState.CurrentPlayPoints}.");

                // 场地空间验证（仅对随从）
                if (Enum.TryParse<CardType>(cardDefinition.Type, true, out var cardType) && cardType == CardType.Follower)
                {
                    if (playerState.Field.Count >= GameConstants.Field.MaxFieldSize)
                        throw new GameLogicException($"Field is full. Cannot play {cardDefinition.Name}.");
                }
            }
        }


    }
}
