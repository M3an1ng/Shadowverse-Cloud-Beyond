using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Auras;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Events.Services;

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
        public IAuraManager AuraManager { get; }
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
        public bool IsAuraRefresh { get; init; }

        [DslExposable("触发事件", Description = "触发当前能力的游戏事件")]
        public IGameEvent? TriggeringEvent { get; init; }

        [DslExposable("本卡牌", Description = "当前效果的持有卡牌")]
        public CardInstance? SourceCard { get; init; }

        public IAbility? SourceAbility { get; init; }

        /// <summary>
        /// Invocation-scoped execution overrides applied when an effect explicitly
        /// triggers another runtime ability.
        /// </summary>
        public AbilityInvocationOptions InvocationOptions { get; init; } = AbilityInvocationOptions.None;

        [DslExposable("本能力", Description = "当前处理的能力实例")]
        public Ability? Ability => SourceAbility as Ability;

        [DslExposable("活跃玩家", Description = "发起当前动作的玩家")]
        public PlayerState RequestingPlayer => this.GetRequestingPlayer();

        [DslExposable("闲置玩家", Description = "非当前动作的玩家")]
        public PlayerState IdlePlayer => this.GetIdlePlayer();

        [DslExposable("我方玩家", Description = "本卡牌归属的玩家")]
        public PlayerState Owner => SourceCard != null ? this.GetSourceCardOwner() : this.GetRequestingPlayer();

        [DslExposable("敌方玩家", Description = "本卡牌敌对的玩家")]
        public PlayerState Opponent => SourceCard != null ? this.GetSourceCardOpponent() : this.GetIdlePlayer();

        [DslExposable("当前效果", Description = "当前执行中的效果")]
        public Effect? CurrentEffect { get; init; }

        /// <summary>
        /// Runtime-only context used to scope a single attack flow.
        /// </summary>
        public AttackFlowContext? AttackFlow { get; init; }

        [DslExposable("事件历史", Description = "当前对局的事件历史读取器")]
        public EventHistoryReader EventHistory { get; }

        public IReadOnlyDictionary<string, List<TargetInfo>> ResolvedTargets { get; init; } = new Dictionary<string, List<TargetInfo>>();

        [DslExposable("目标池", Description = "当前能力已解析的卡牌目标池")]
        public IReadOnlyDictionary<string, List<CardInstance>> ResolvedTargetCards =>
            ExtractCardTargets(ResolvedTargets);

        public IReadOnlyList<TargetInfo> SelectedTargets =>
            (IReadOnlyList<TargetInfo>?)CurrentEffect?.SelectedTargets ?? Array.Empty<TargetInfo>();

        //[DslExposable("效果目标", Description = "当前效果携带的已选卡牌列表")]
        //public IReadOnlyList<CardInstance> SelectedTargetCards =>
        //    ExtractCardTargets(CurrentEffect?.SelectedTargets);

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
            AttackFlowContext? attackFlow = null,
            IBuffService? buffService = null,
            bool isCancellable = false,
            bool isAuraRefresh = false,
            IReadOnlyDictionary<string, List<TargetInfo>>? resolvedTargets = null)
        {
            ServiceProvider = serviceProvider;
            Mapper = mapper;
            CurrentGameState = currentGameState;
            RequestingPlayerId = requestingPlayerId;
            CancellationToken = cancellationToken;
            TriggeringEvent = triggeringEvent;
            SourceCard = sourceCard;
            CurrentEffect = currentEffect;
            AttackFlow = attackFlow;
            IsCancellable = isCancellable;
            IsAuraRefresh = isAuraRefresh;
            ResolvedTargets = resolvedTargets ?? new Dictionary<string, List<TargetInfo>>();

            // Get managers directly from the GameState
            AbilityManager = currentGameState.AbilityManager;
            BuffManager = currentGameState.BuffManager;
            AuraManager = currentGameState.AuraManager ?? NullAuraManager.Instance;
            BuffService = serviceProvider.GetRequiredService<IBuffService>();
            EventHistory = new EventHistoryReader(serviceProvider.GetRequiredService<IEventHistoryService>(), currentGameState.GameId);

            // Set the ambient context upon creation.
            _accessor = accessor;
            _accessor.GameContext = this;
        }

        private static IReadOnlyList<CardInstance> ExtractCardTargets(IEnumerable<TargetInfo>? targets)
        {
            if (targets == null)
            {
                return Array.Empty<CardInstance>();
            }

            return targets
                .Where(target => target.Type == TargetType.Card && target.Card != null)
                .Select(target => target.Card!)
                .ToList();
        }

        private static IReadOnlyDictionary<string, List<CardInstance>> ExtractCardTargets(IReadOnlyDictionary<string, List<TargetInfo>> targets)
        {
            if (targets == null || targets.Count == 0)
            {
                return new Dictionary<string, List<CardInstance>>();
            }

            return targets.ToDictionary(
                pair => pair.Key,
                pair => pair.Value
                    .Where(target => target.Type == TargetType.Card && target.Card != null)
                    .Select(target => target.Card!)
                    .ToList());
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
