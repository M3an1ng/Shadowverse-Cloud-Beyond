using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AutoMapper;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Exceptions;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// 换牌效果处理器
    /// 处理换牌操作，遵循统一架构：Handler负责验证和事件生成，领域实体负责状态修改
    /// </summary>
    public class MulliganEffectHandler : EffectHandlerBase<MulliganEffectParams>
    {
        private readonly IMapper _mapper;
        private readonly ITargetProcessingService _targetProcessingService;

        public MulliganEffectHandler(
            ILogger logger,
            IMapper mapper,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        /// <summary>
        /// 执行换牌效果
        /// </summary>
        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, MulliganEffectParams parameters)
        {
            const string effectName = "Mulligan";
            Logger.LogDebug("[MulliganEffectHandler] Starting mulligan execution for player {PlayerId}", parameters.PlayerId);

            // 定义处理单个PlayerState的Action委托
            Func<PlayerState, GameContext, Task<IndividualActionResult>> mulliganAction =
                async (playerState, ctx) =>
                {
                    var individualEvents = new List<IGameEvent>();

                    // 验证换牌前置条件
                    try
                    {
                        ValidateMulliganPreconditions(ctx.CurrentGameState, playerState, parameters);
                    }
                    catch (GameLogicException ex)
                    {
                        Logger.LogInformation("{EffectName}: Mulligan validation failed for player {PlayerId}: {Reason}", 
                            effectName, playerState.PlayerId, ex.Message);
                        return new IndividualActionResult 
                        { 
                            TargetId = TargetId.Player(playerState.PlayerId), 
                            Success = false, 
                            Message = ex.Message 
                        };
                    }

                    // 使用PlayerState的领域方法执行换牌
                    var mulliganEvents = playerState.PerformMulligan(parameters.CardUidsToReplace);
                    individualEvents.AddRange(mulliganEvents);

                    // 生成换牌完成事件
                    var playerDto = _mapper.Map<PlayerStateDto>(playerState);
                    var mulliganEvent = new MulliganCompletedEvent(playerDto, parameters.CardUidsToReplace);
                    individualEvents.Add(mulliganEvent);

                    Logger.LogInformation("{EffectName}: Successfully completed mulligan for player {PlayerId}. Replaced {CardCount} cards.", 
                        effectName, playerState.PlayerId, parameters.CardUidsToReplace.Count);

                    return new IndividualActionResult
                    {
                        TargetId = TargetId.Player(playerState.PlayerId),
                        Success = true,
                        Message = $"Mulligan completed for player {playerState.PlayerId}.",
                        GeneratedEvents = individualEvents
                    };
                };

            // 使用ITargetProcessingService处理目标
            return await _targetProcessingService.ProcessTargetsAsync(
                context,
                TargetIdType.Player,      // 期望Player目标
                mulliganAction,           // PlayerState action
                null,                     // 不需要CardInstance action
                effectName);              // 效果名称用于日志
        }

        /// <summary>
        /// 验证换牌前置条件
        /// </summary>
        private static void ValidateMulliganPreconditions(
            GameState gameState,
            PlayerState mulliganPlayer,
            MulliganEffectParams parameters)
        {
            if (!parameters.ForceMulligan)
            {
                // 游戏状态验证
                if (gameState.IsGameOver)
                    throw new GameLogicException("Game is already over.");

                // 阶段验证
                if (gameState.Phase != GamePhase.Mulligan)
                    throw new GameLogicException("Mulligan can only be performed during mulligan phase.");

                // 换牌状态验证
                if (mulliganPlayer.HasCompletedMulligan)
                    throw new GameLogicException($"Player {mulliganPlayer.PlayerId} has already completed mulligan.");

                // 卡牌数量验证
                if (parameters.CardUidsToReplace.Count > mulliganPlayer.Hand.Count)
                    throw new GameLogicException("Cannot replace more cards than in hand.");
            }
        }
    }

    /// <summary>
    /// 换牌完成事件
    /// </summary>
    public class MulliganCompletedEvent : GameEventBase
    {
        public PlayerStateDto Player { get; }
        public List<Guid> ReplacedCardUids { get; }

        public MulliganCompletedEvent(PlayerStateDto player, List<Guid> replacedCardUids)
            : base("mulliganCompleted")
        {
            Player = player;
            ReplacedCardUids = replacedCardUids;
            
            SourcePlayer = player;
            Parameters["playerId"] = player.PlayerId;
            Parameters["replacedCardCount"] = replacedCardUids.Count;
        }

        public override async Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            // 换牌事件的执行逻辑已经在Handler中完成
            // 这里只是事件记录，不需要额外的执行逻辑
            await Task.CompletedTask;
            return new List<IGameEvent>();
        }
    }
}
