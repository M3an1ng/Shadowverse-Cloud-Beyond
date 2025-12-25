using System;
using System;
using System.Collections.Generic;
using System.Threading;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Represents a pure, immutable context for game event and effect processing, which also manages its own lifetime
    /// within the static GameServiceProvider. This class is designed to be used in a 'using' block to ensure
    /// that the ambient context is correctly set and cleared.
    /// </summary>
    [DslExposable("游戏上下文", Description = "代表当前游戏事件和效果处理的上下文环境")]
    public record GameContext : IDisposable
    {
        private readonly IGameContextAccessor _accessor;

        // Core Services are now passed directly, not resolved via IServiceProvider
        public IServiceProvider ServiceProvider { get; } // Kept for resolving other services if needed
        public IMapper Mapper { get; }
        public IAbilityManager AbilityManager { get; }
        public IBuffManager BuffManager { get; }
        public IBuffService BuffService { get; }

        /// <summary>
        /// The cancellation token for the current game processing loop.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        // Contextual Data
        [DslExposable("当前游戏状态", Description = "当前游戏的完整状态")]
        public GameState CurrentGameState { get; }

        public Guid RequestingPlayerId { get; }

        /// <summary>
        /// Indicates whether the current transaction/ability processing is cancellable (pre-commit).
        /// This is used to align interactive prompts (e.g., Choose) with the transaction phase.
        /// </summary>
        public bool IsCancellable { get; init; }

        [DslExposable("触发事件", Description = "触发当前能力的游戏事件")]
        public IGameEvent? TriggeringEvent { get; init; }

        [DslExposable("源卡牌", Description = "触发当前效果的源卡牌")]
        public CardInstance? SourceCard { get; init; }

        [DslExposable("请求玩家", Description = "发起当前动作的玩家")]
        public PlayerState RequestingPlayer => this.GetRequestingPlayer();

        [DslExposable("敌方玩家", Description = "未发起当前动作的玩家")]
        public PlayerState OpponentPlayer => this.GetOpponentPlayer();

        [DslExposable("源卡牌拥有者", Description = "源卡牌归属的玩家")]
        public PlayerState Owner => this.GetSourceCardOwner();

        [DslExposable("当前效果", Description = "当前正在处理的效果请求（用于连锁能力/输出读取等）")]
        public Effect? CurrentEffect { get; init; }

        [DslExposable("已选目标", Description = "当前效果携带的已选目标列表（连锁能力可用于复用或判断）")]
        public IReadOnlyList<TargetInfo> SelectedTargets =>
            (IReadOnlyList<TargetInfo>?)CurrentEffect?.SelectedTargets ?? Array.Empty<TargetInfo>();

        public GameContext(
            IServiceProvider serviceProvider,
            IMapper mapper,
            IGameContextAccessor accessor,
            GameState currentGameState,
            Guid requestingPlayerId,
            CancellationToken cancellationToken = default,
            IGameEvent? triggeringEvent = null,
            CardInstance? sourceCard = null,
            Effect? currentEffect = null,
            IBuffService? buffService = null,
            bool isCancellable = false)
        {
            ServiceProvider = serviceProvider;
            Mapper = mapper;
            CurrentGameState = currentGameState;
            RequestingPlayerId = requestingPlayerId;
            CancellationToken = cancellationToken;
            TriggeringEvent = triggeringEvent;
            SourceCard = sourceCard;
            CurrentEffect = currentEffect;
            IsCancellable = isCancellable;

            // Get managers directly from the GameState
            AbilityManager = currentGameState.AbilityManager;
            BuffManager = currentGameState.BuffManager;
            BuffService = serviceProvider.GetRequiredService<IBuffService>();

            // Set the ambient context upon creation.
            _accessor = accessor;
            _accessor.GameContext = this;
        }

        /// <summary>
        /// Clears the ambient context from the GameServiceProvider.
        /// </summary>
        public void Dispose()
        {
            _accessor.GameContext = null;
        }
    }
}
