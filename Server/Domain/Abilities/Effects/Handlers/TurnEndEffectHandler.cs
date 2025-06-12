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
    /// 回合结束效果处理器
    /// 处理回合结束操作，遵循统一架构：Handler负责验证和事件生成，领域实体负责状态修改
    /// </summary>
    public class TurnEndEffectHandler : EffectHandlerBase<TurnEndEffectParams>
    {
        private readonly IMapper _mapper;
        private readonly ITargetProcessingService _targetProcessingService;

        public TurnEndEffectHandler(
            ILogger logger,
            IMapper mapper,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        /// <summary>
        /// 执行回合结束效果
        /// </summary>
        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, TurnEndEffectParams parameters)
        {
            const string effectName = "TurnEnd";
            Logger.LogDebug("[TurnEndEffectHandler] Starting turn end execution for player {PlayerId}", parameters.EndingPlayerId);

            // 定义处理单个PlayerState的Action委托
            Func<PlayerState, GameContext, Task<IndividualActionResult>> turnEndAction =
                async (playerState, ctx) =>
                {
                    var individualEvents = new List<IGameEvent>();

                    // 验证回合结束前置条件
                    try
                    {
                        ValidateTurnEndPreconditions(ctx.CurrentGameState, playerState, parameters);
                    }
                    catch (GameLogicException ex)
                    {
                        Logger.LogInformation("{EffectName}: Turn end validation failed for player {PlayerId}: {Reason}",
                            effectName, playerState.PlayerId, ex.Message);
                        return new IndividualActionResult
                        {
                            TargetId = TargetId.Player(playerState.PlayerId),
                            Success = false,
                            Message = ex.Message
                        };
                    }

                    // 使用GameState的领域方法执行回合结束
                    var turnEndEvents = ctx.CurrentGameState.EndTurn(playerState.PlayerId);
                    individualEvents.AddRange(turnEndEvents);

                    // 生成回合结束事件
                    var playerDto = _mapper.Map<PlayerStateDto>(playerState);
                    var turnEndEvent = new TurnEndEvent(playerDto, parameters.TurnNumber);
                    individualEvents.Add(turnEndEvent);

                    Logger.LogInformation("{EffectName}: Successfully ended turn for player {PlayerId}. New current player: {NewCurrentPlayerId}",
                        effectName, playerState.PlayerId, ctx.CurrentGameState.CurrentPlayerId);

                    return new IndividualActionResult
                    {
                        TargetId = TargetId.Player(playerState.PlayerId),
                        Success = true,
                        Message = $"Turn ended for player {playerState.PlayerId}.",
                        GeneratedEvents = individualEvents
                    };
                };

            // 使用ITargetProcessingService处理目标
            return await _targetProcessingService.ProcessTargetsAsync(
                context,
                TargetIdType.Player,      // 期望Player目标
                turnEndAction,            // PlayerState action
                null,                     // 不需要CardInstance action
                effectName);              // 效果名称用于日志
        }

        /// <summary>
        /// 验证回合结束前置条件
        /// </summary>
        private static void ValidateTurnEndPreconditions(
            GameState gameState,
            PlayerState endingPlayer,
            TurnEndEffectParams parameters)
        {
            if (!parameters.ForceEndTurn)
            {
                // 游戏状态验证
                if (gameState.IsGameOver)
                    throw new GameLogicException("Game is already over.");

                // 回合验证
                if (gameState.CurrentPlayerId != endingPlayer.PlayerId)
                    throw new GameLogicException($"It's not player {endingPlayer.PlayerId}'s turn.");

                // 阶段验证
                if (gameState.Phase == GamePhase.Mulligan)
                    throw new GameLogicException("Cannot end turn during mulligan phase.");
            }
        }
    }

    /// <summary>
    /// 回合结束事件
    /// </summary>
    public class TurnEndEvent : GameEventBase
    {
        public PlayerStateDto EndingPlayer { get; }
        public int TurnNumber { get; }

        public TurnEndEvent(PlayerStateDto endingPlayer, int turnNumber)
            : base("turnEnd")
        {
            EndingPlayer = endingPlayer;
            TurnNumber = turnNumber;

            SourcePlayer = endingPlayer;
            Parameters["endingPlayerId"] = endingPlayer.PlayerId;
            Parameters["turnNumber"] = turnNumber;
        }

        public override async Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            // 回合结束事件的执行逻辑已经在Handler中完成
            // 这里只是事件记录，不需要额外的执行逻辑
            await Task.CompletedTask;
            return new List<IGameEvent>();
        }
    }
}
