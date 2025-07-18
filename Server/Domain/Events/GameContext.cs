using AutoMapper; // Added for IMapper
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Entities; // For GameState, PlayerState, CardInstance
using ShadowverseBeyond.Server.Domain.Abilities; // For Ability, AbilityConfig
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // For TargetId
using ShadowverseBeyond.Server.Dtos.SignalR.Events; // For GameEventDto
using ShadowverseBeyond.Server.Dtos; // For GameStateDto

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// 统一的游戏上下文，集成了能力系统相关的Context功能。
    /// 提供对完整游戏状态领域对象的访问，以及事件处理、能力执行、查询等操作所需的所有上下文信息。
    ///
    /// 此类替代了以下分散的Context类：
    /// - AbilityContext: 能力执行上下文
    /// - QueryContext: 查询上下文
    /// - EvaluationContext: DSL评估上下文
    /// - ConditionEvaluationContext: 条件评估上下文
    /// - TargetResolutionContext: 目标解析上下文
    ///
    /// 注意：GameActionContext保持独立，用于轻量级的事件收集场景。
    /// </summary>
    public class GameContext
    {
        private readonly IMapper _mapper; // Added IMapper field

        // === 核心游戏状态 ===

        /// <summary>
        /// 完整的、可变的游戏状态领域对象。
        /// 注意：这是实际的游戏状态，不是快照。使用时需要谨慎，避免意外修改。
        /// </summary>
        public GameState CurrentGameState { get; }

        /// <summary>
        /// 触发当前处理流程的游戏事件（如果有）。
        /// </summary>
        public IGameEvent? TriggeringEvent { get; }

        /// <summary>
        /// 拥有正在执行的能力的卡牌实例（如果适用）。
        /// 通常与 CurrentAbility?.SourceCard 相同。
        /// </summary>
        public CardInstance? SourceCard { get; }

        /// <summary>
        /// 发起动作或触发事件的玩家ID。
        /// </summary>
        public Guid RequestingPlayerId { get; }

        // === 只读访问器（防止意外修改） ===

        /// <summary>
        /// 发起请求的玩家状态（只读访问）。从 CurrentGameState 派生。
        /// </summary>
        public PlayerState RequestingPlayer => CurrentGameState.GetPlayerState(RequestingPlayerId)
            ?? throw new InvalidOperationException($"Requesting player with ID {RequestingPlayerId} not found in game state.");

        /// <summary>
        /// 对手玩家状态（只读访问）。从 CurrentGameState 派生。
        /// </summary>
        public PlayerState OpponentPlayer => CurrentGameState.GetOpponentPlayerState(RequestingPlayerId)
            ?? throw new InvalidOperationException($"Opponent for player with ID {RequestingPlayerId} not found in game state.");

        // === 能力执行相关 ===

        /// <summary>
        /// 当前正在处理的能力实例（如果适用）。
        /// 可在能力处理过程中设置。
        /// </summary>
        public Ability? CurrentAbility { get; set; }

        /// <summary>
        /// 当前正在处理的能力配置。从 CurrentAbility 派生。
        /// </summary>
        public AbilityConfig? CurrentAbilityConfig => CurrentAbility?.Config;

        // === 处理过程中的可变状态（受控访问） ===

        /// <summary>
        /// 在目标选择阶段填充的选定目标列表。
        /// </summary>
        private readonly List<TargetInfo> _selectedTargets = new();

        /// <summary>
        /// 选定目标的只读视图。
        /// </summary>
        public ReadOnlyCollection<TargetInfo> SelectedTargets => _selectedTargets.AsReadOnly();

        /// <summary>
        /// 在效果执行前填充的已解析参数字典。
        /// 键是参数名称（string），值是解析的数据（object）。
        /// </summary>
        private readonly Dictionary<string, object> _resolvedParameters = new();

        /// <summary>
        /// 已解析参数的只读视图。
        /// </summary>
        public IReadOnlyDictionary<string, object> ResolvedParameters => _resolvedParameters.AsReadOnly();

        /// <summary>
        /// 临时变量字典，用于存储能力执行流程中的临时数据。
        /// 这些变量通常只在单个能力解析链的持续时间内存在。
        /// </summary>
        private readonly Dictionary<string, object> _temporaryVariables = new();

        /// <summary>
        /// 临时变量的只读视图。
        /// </summary>
        public IReadOnlyDictionary<string, object> TemporaryVariables => _temporaryVariables.AsReadOnly();

        /// <summary>
        /// (新增) 当前效果处理期间累积的原子事件列表。
        /// 此列表由 EffectHandlerBase 在效果处理的生命周期内管理（填充与清空）。
        /// DSL 可以通过隐式上下文直接访问此属性 (例如 `CurrentEffectAtomicEvents.Filter(...)`)。
        /// </summary>
        public IReadOnlyList<IGameEvent> GeneratedEvents { get; internal set; } = Array.Empty<IGameEvent>();

        /// <summary>
        /// 目标选择结果字典。
        /// 键是目标标识符（string，例如 "target_1"），值是选定对象的列表（例如 List<CardInstance>）。
        /// </summary>
        private readonly Dictionary<string, List<object>> _targets = new();

        /// <summary>
        /// 目标选择结果的只读视图。
        /// </summary>
        public IReadOnlyDictionary<string, List<object>> Targets => _targets.AsReadOnly();

        // === 服务访问（替代EvaluationContext功能） ===

        /// <summary>
        /// 卡牌仓储服务，用于获取卡牌定义。
        /// </summary>

        /// <summary>
        /// 当前正在处理的项目（用于DSL评估）。
        /// 可以是CardInstance、PlayerState等任何对象。
        /// </summary>
        public object? CurrentItem { get; set; }

        /// <summary>
        /// (新增) .NET Core的依赖注入服务提供者。
        /// 用于在需要时动态解析服务，例如在能力效果中创建新的能力实例。
        /// </summary>
        public IServiceProvider ServiceProvider { get; }

        // === 构造函数 ===

        /// <summary>
        /// 初始化统一的GameContext的新实例。
        /// </summary>
        /// <param name="gameState">当前游戏状态领域对象。必需。</param>
        /// <param name="requestingPlayerId">发起请求的玩家ID。必需。</param>
        /// <param name="triggeringEvent">触发事件（可选）。</param>
        /// <param name="sourceCard">源卡牌实例（可选）。</param>
        /// <param name="currentAbility">当前能力实例（可选）。</param>
        /// <param name="cardRepository">卡牌仓储服务（可选）。</param>
        /// <param name="functionRegistry">函数注册表（可选）。</param>
        /// <param name="abilityTriggerService">能力触发服务。必需。</param>
        /// <param name="mapper">AutoMapper实例。必需。</param>
        /// <param name="serviceProvider">服务提供者实例。必需。</param>
        public GameContext(
            GameState gameState,
            Guid requestingPlayerId,
            IMapper mapper, // Inject IMapper
            IServiceProvider serviceProvider, // Inject IServiceProvider
            IGameEvent? triggeringEvent = null,
            CardInstance? sourceCard = null,
            Ability? currentAbility = null
)
        {
            CurrentGameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
            RequestingPlayerId = requestingPlayerId == Guid.Empty ? throw new ArgumentException("RequestingPlayerId cannot be empty", nameof(requestingPlayerId)) : requestingPlayerId;
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper)); // Store IMapper
            ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider)); // Store IServiceProvider
            TriggeringEvent = triggeringEvent;
            SourceCard = sourceCard;
            CurrentAbility = currentAbility;

            // 提前验证requestingPlayerId在gameState中存在，以便早期捕获错误
            _ = RequestingPlayer;
            _ = OpponentPlayer;
        }

        // === 便利方法 ===

        /// <summary>
        /// 根据实例ID查找卡牌实例。
        /// 搜索所有玩家的手牌、场上单位、墓地等区域。
        /// </summary>
        /// <param name="instanceId">卡牌实例ID</param>
        /// <returns>找到的卡牌实例，如果未找到则返回null</returns>
        public CardInstance? FindCardInstance(Guid instanceId)
        {
            if (instanceId == Guid.Empty)
                return null;

            // 搜索所有玩家的所有区域
            foreach (var player in new[] { CurrentGameState.Player1, CurrentGameState.Player2 })
            {
                if (player == null) continue;

                // 搜索手牌
                var cardInHand = player.Hand.FirstOrDefault(c => c.InstanceId == instanceId);
                if (cardInHand != null) return cardInHand;

                // 搜索场上单位
                var cardOnField = player.Field.FirstOrDefault(c => c.InstanceId == instanceId);
                if (cardOnField != null) return cardOnField;

                // 搜索墓地
                var cardInGraveyard = player.Graveyard.FirstOrDefault(c => c.InstanceId == instanceId);
                if (cardInGraveyard != null) return cardInGraveyard;

                // 搜索牌库
                var cardInDeck = player.Deck.FirstOrDefault(c => c.InstanceId == instanceId);
                if (cardInDeck != null) return cardInDeck;

                // 检查主战者卡牌
                if (player.Leader?.InstanceId == instanceId)
                    return player.Leader;
            }

            return null;
        }



        /// <summary>
        /// 清除所有临时状态（目标、参数、变量等）。
        /// 通常在能力执行完成后调用。
        /// </summary>
        public void ClearTemporaryState()
        {
            _selectedTargets.Clear();
            _resolvedParameters.Clear();
            _temporaryVariables.Clear();
            _targets.Clear();
            CurrentItem = null;
            GeneratedEvents = Array.Empty<IGameEvent>(); // 清理累积的原子事件
        }

        // === 状态修改方法（受控访问） ===

        /// <summary>
        /// 添加选定目标。
        /// </summary>
        /// <param name="target">要添加的目标</param>
        public void AddSelectedTarget(TargetInfo target)
        {
            _selectedTargets.Add(target);
        }

        /// <summary>
        /// 设置已解析参数。
        /// </summary>
        /// <param name="key">参数名称</param>
        /// <param name="value">参数值</param>
        public void SetResolvedParameter(string key, object value)
        {
            _resolvedParameters[key] = value;
        }

        /// <summary>
        /// 设置临时变量。
        /// </summary>
        /// <param name="key">变量名称</param>
        /// <param name="value">变量值</param>
        public void SetTemporaryVariable(string key, object value)
        {
            _temporaryVariables[key] = value;
        }

        /// <summary>
        /// 设置目标选择结果。
        /// </summary>
        /// <param name="targetId">目标标识符</param>
        /// <param name="targets">选定的目标列表</param>
        public void SetTargets(string targetId, List<object> targets)
        {
            _targets[targetId] = targets;
        }

        /// <summary>
        /// 创建此GameContext的快照DTO，用于传递给需要不可变状态的组件。
        /// </summary>
        /// <returns>游戏状态DTO快照</returns>
        public GameStateDto CreateSnapshot()
        {
            return _mapper.Map<GameStateDto>(CurrentGameState); // Use AutoMapper
        }
    }
}
