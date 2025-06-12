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
    /// 回合开始效果处理器
    /// 处理回合开始操作，遵循统一架构：Handler负责验证和事件生成，领域实体负责状态修改
    /// </summary>
    public class TurnStartEffectHandler : EffectHandlerBase<TurnStartEffectParams>
    {
        private readonly IMapper _mapper;
        private readonly ITargetProcessingService _targetProcessingService;

        public TurnStartEffectHandler(
            ILogger logger,
            IMapper mapper,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        /// <summary>
        /// 执行回合开始效果
        /// </summary>
        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, TurnStartEffectParams parameters)
        {
            const string effectName = "TurnStart";
            Logger.LogDebug("[TurnStartEffectHandler] Starting turn start execution for player {PlayerId}", parameters.StartingPlayerId);

            // 定义处理单个PlayerState的Action委托
            Func<PlayerState, GameContext, Task<IndividualActionResult>> turnStartAction =
                async (playerState, ctx) =>
                {
                    var individualEvents = new List<IGameEvent>();

                    // 验证回合开始前置条件
                    try
                    {
                        ValidateTurnStartPreconditions(ctx.CurrentGameState, playerState, parameters);
                    }
                    catch (GameLogicException ex)
                    {
                        Logger.LogInformation("{EffectName}: Turn start validation failed for player {PlayerId}: {Reason}", 
                            effectName, playerState.PlayerId, ex.Message);
                        return new IndividualActionResult 
                        { 
                            TargetId = TargetId.Player(playerState.PlayerId), 
                            Success = false, 
                            Message = ex.Message 
                        };
                    }

                    // 使用GameState的领域方法执行回合开始
                    var turnStartEvents = ctx.CurrentGameState.StartPlayerTurn(playerState.PlayerId);
                    individualEvents.AddRange(turnStartEvents);

                    // 生成回合开始事件
                    var playerDto = _mapper.Map<PlayerStateDto>(playerState);
                    var turnStartEvent = new TurnStartEvent(playerDto, parameters.TurnNumber);
                    individualEvents.Add(turnStartEvent);

                    Logger.LogInformation("{EffectName}: Successfully started turn for player {PlayerId}. Turn number: {TurnNumber}", 
                        effectName, playerState.PlayerId, parameters.TurnNumber);

                    return new IndividualActionResult
                    {
                        TargetId = TargetId.Player(playerState.PlayerId),
                        Success = true,
                        Message = $"Turn started for player {playerState.PlayerId}.",
                        GeneratedEvents = individualEvents
                    };
                };

            // 使用ITargetProcessingService处理目标
            return await _targetProcessingService.ProcessTargetsAsync(
                context,
                TargetIdType.Player,      // 期望Player目标
                turnStartAction,          // PlayerState action
                null,                     // 不需要CardInstance action
                effectName);              // 效果名称用于日志
        }

        /// <summary>
        /// 验证回合开始前置条件
        /// </summary>
        private static void ValidateTurnStartPreconditions(
            GameState gameState,
            PlayerState startingPlayer,
            TurnStartEffectParams parameters)
        {
            if (!parameters.ForceStartTurn)
            {
                // 游戏状态验证
                if (gameState.IsGameOver)
                    throw new GameLogicException("Game is already over.");

                // 阶段验证
                if (gameState.Phase == GamePhase.Mulligan)
                    throw new GameLogicException("Cannot start turn during mulligan phase.");

                // 回合验证
                if (gameState.CurrentPlayerId == startingPlayer.PlayerId)
                    throw new GameLogicException($"Player {startingPlayer.PlayerId} is already the current player.");
            }
        }
    }

    /// <summary>
    /// 回合开始事件
    /// </summary>
    public class TurnStartEvent : GameEventBase
    {
        public PlayerStateDto StartingPlayer { get; }
        public int TurnNumber { get; }

        public TurnStartEvent(PlayerStateDto startingPlayer, int turnNumber)
            : base("turnStart")
        {
            StartingPlayer = startingPlayer;
            TurnNumber = turnNumber;
            
            SourcePlayer = startingPlayer;
            Parameters["startingPlayerId"] = startingPlayer.PlayerId;
            Parameters["turnNumber"] = turnNumber;
        }

        public override async Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            // 回合开始事件的执行逻辑已经在Handler中完成
            // 这里只是事件记录，不需要额外的执行逻辑
            await Task.CompletedTask;
            return new List<IGameEvent>();
        }
    }
}
