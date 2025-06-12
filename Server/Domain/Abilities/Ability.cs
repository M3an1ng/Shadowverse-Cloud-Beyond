//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
// - 实体关系图: 文档/04-数据库设计/01-实体关系图.md
//
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions; // Keep for ConditionConfig, IConditionFactory
using ShadowverseBeyond.Server.Domain.Abilities.Runtime; // Keep for Trigger, Condition, Target, Effect, EffectExecutionContext
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // Keep for EffectConfig, IEffectExecutor
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // Keep for Target
using ShadowverseBeyond.Server.Dtos; // Keep for GameStateDto
using Microsoft.Extensions.Logging; // Keep for temporary logger creation if needed, or remove if logger is injected elsewhere
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    /// <summary>
    /// 运行时卡牌能力表示
    /// 从AbilityConfig创建，包含运行时状态和行为
    /// </summary>
    public class Ability
    {
        /// <summary>
        /// 静态配置引用
        /// </summary>
        public AbilityConfig Config { get; }

        /// <summary>
        /// 能力的唯一标识符
        /// </summary>
        public long AbilityId => Config.Id.GetHashCode();

        /// <summary>
        /// 关联的卡牌ID
        /// </summary>
        public int CardId => Config.CardId;

        /// <summary>
        /// 能力名称
        /// </summary>
        public string Name => Config.Name;

        /// <summary>
        /// 能力描述
        /// </summary>
        public string Description => Config.Description;

        /// <summary>
        /// 能力标签，用于标识能力类型（如"Fanfare", "LastWords", "Rush"等）
        /// </summary>
        public string AbilityTag => Config.AbilityTag;

        /// <summary>
        /// 能力类型
        /// </summary>
        public AbilityType Type => Config.Type;

        /// <summary>
        /// 运行时触发器
        /// </summary>
        public Trigger Trigger { get; }

        /// <summary>
        /// 运行时条件列表
        /// </summary>
        public List<Condition> Conditions { get; }

        /// <summary>
        /// 运行时目标列表
        /// </summary>
        public List<Target> Targets { get; }

        /// <summary>
        /// 运行时效果列表
        /// </summary>
        public List<Effect> Effects { get; }

        /// <summary>
        /// 源卡牌实例（运行时设置）
        /// </summary>
        public CardInstance? SourceCard { get; set; }

        /// <summary>
        /// 是否激活
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// 激活时间
        /// </summary>
        public DateTime? ActivationTime { get; private set; }

        /// <summary>
        /// 总触发次数
        /// </summary>
        public int TotalTriggerCount { get; private set; }

        /// <summary>
        /// 上次触发时间
        /// </summary>
        public DateTime? LastTriggerTime { get; private set; }

        /// <summary>
        /// 能力状态
        /// </summary>
        public AbilityState State { get; private set; } = AbilityState.Inactive;

        // Add private fields for new dependencies
        private readonly IConditionFactory _conditionFactory;
        private readonly Func<IEffectExecutor> _effectExecutorFactory;
        // Removed: _buffManager, _cardRepository, _functionRegistry

        /// <summary>
        /// 构造函数，从AbilityConfig创建运行时Ability
        /// </summary>
        /// <param name="config">静态配置</param>
        /// <param name="conditionFactory">用于创建 Condition 实例的工厂</param>
        /// <param name="effectExecutorFactory">用于获取 IEffectExecutor 的工厂函数</param>
        /// <param name="sourceCard">源卡牌实例</param>
        public Ability(
            AbilityConfig config,
            IConditionFactory conditionFactory, // Inject IConditionFactory
            Func<IEffectExecutor> effectExecutorFactory,   // Inject factory function for IEffectExecutor
            CardInstance? sourceCard = null)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            _conditionFactory = conditionFactory ?? throw new ArgumentNullException(nameof(conditionFactory)); // Store conditionFactory
            _effectExecutorFactory = effectExecutorFactory ?? throw new ArgumentNullException(nameof(effectExecutorFactory)); // Store effectExecutor factory
            SourceCard = sourceCard;

            // 创建运行时组件
            Trigger = new Runtime.Trigger(config.Trigger);

            // Use the injected factory to create Conditions
            Conditions = config.Conditions
                .Select(c => _conditionFactory.CreateCondition(c)) // Use factory
                .ToList();

            Targets = config.Targets
                .Select(t => new Target(t))
                .ToList();

            // Effects are still simple wrappers around EffectConfig for now
            Effects = config.Effects
                .Select(e => new Effect(e))
                .ToList();
        }

        /// <summary>
        /// 激活能力
        /// </summary>
        public void Activate()
        {
            if (State == AbilityState.Destroyed)
                throw new InvalidOperationException("Cannot activate a destroyed ability");

            State = AbilityState.Active;
            IsActive = true;
            ActivationTime = DateTime.UtcNow;
        }

        /// <summary>
        /// 停用能力
        /// </summary>
        public void Deactivate()
        {
            State = AbilityState.Inactive;
            IsActive = false;
        }

        /// <summary>
        /// 暂停能力
        /// </summary>
        public void Suspend()
        {
            if (State == AbilityState.Active)
            {
                State = AbilityState.Suspended;
                IsActive = false;
            }
        }

        /// <summary>
        /// 恢复能力
        /// </summary>
        public void Resume()
        {
            if (State == AbilityState.Suspended)
            {
                State = AbilityState.Active;
                IsActive = true;
            }
        }

        /// <summary>
        /// 禁用能力
        /// </summary>
        public void Disable()
        {
            State = AbilityState.Disabled;
            IsActive = false;
        }

        /// <summary>
        /// 销毁能力
        /// </summary>
        public void Destroy()
        {
            State = AbilityState.Destroyed;
            IsActive = false;

            // 重置所有运行时组件
            foreach (var condition in Conditions)
                condition.Reset();

            foreach (var target in Targets)
                target.Reset();

            foreach (var effect in Effects)
                effect.Reset();

            Trigger.ResetTurnCounters();
            Trigger.ResetEventFlags();
        }

        /// <summary>
        /// 检查能力是否可以触发（包含区域检查优化）
        /// </summary>
        /// <param name="gameEvent">游戏事件</param>
        /// <returns>是否可以触发</returns>
        public bool CanTrigger(IGameEvent gameEvent)
        {
            if (!IsActive || State != AbilityState.Active)
                return false;

            // 使用区域感知的触发器检查，传入源卡牌进行区域验证
            return Trigger.CanTrigger(gameEvent, SourceCard);
        }

        /// <summary>
        /// 检查所有条件是否满足
        /// </summary>
        /// <param name="context">统一的游戏上下文</param>
        /// <returns>条件是否满足</returns>
        public async Task<bool> CheckConditionsAsync(GameContext context)
        {
            foreach (var condition in Conditions)
            {
                if (!await condition.EvaluateAsync(context))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 解析所有目标
        /// </summary>
        /// <param name="context">统一的游戏上下文</param>
        /// <returns>解析的目标列表</returns>
        public async Task<List<CardInstance>> ResolveTargetsAsync(GameContext context)
        {
            var allTargets = new List<CardInstance>();

            foreach (var target in Targets)
            {
                var resolvedTargets = await target.ResolveTargetsAsync(context);
                allTargets.AddRange(resolvedTargets);
            }

            return allTargets;
        }

        /// <summary>
        /// 执行所有效果
        /// </summary>
        /// <param name="context">统一的游戏上下文</param>
        /// <returns>生成的事件列表</returns>
        public async Task<List<IGameEvent>> ExecuteEffectsAsync(GameContext context)
        {
            var allEvents = new List<IGameEvent>();

            // 首先检查能力的顶层条件是否满足
            if (!await CheckConditionsAsync(context))
            {
                // 如果顶层条件不满足，跳过所有效果的执行
                return allEvents;
            }

            // 按执行顺序排序
            var sortedEffects = Effects
                .Where(e => e.CanExecute(context))
                .OrderBy(e => e.Config.ExecutionOrder)
                .ToList();

            foreach (var effect in sortedEffects)
            {
                try
                {
                    effect.BeginExecution(context);

                    // 执行单个效果并收集生成的事件
                    var effectEvents = await ExecuteSingleEffectAsync(effect, context);
                    allEvents.AddRange(effectEvents);

                    effect.EndExecution(new Runtime.EffectExecutionResult
                    {
                        Success = true,
                        GeneratedEvents = effectEvents
                    });
                }
                catch (Exception ex)
                {
                    effect.EndExecution(new Runtime.EffectExecutionResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message
                    });

                    // 根据失败策略处理
                    if (effect.Config.OnFailure == FailureStrategy.Stop)
                        break;
                }
            }

            return allEvents;
        }

        /// <summary>
        /// 执行单个效果
        /// </summary>
        /// <param name="effect">运行时效果</param>
        /// <param name="context">统一的游戏上下文</param>
        /// <returns>生成的事件列表</returns>
        private async Task<List<IGameEvent>> ExecuteSingleEffectAsync(Effect effect, GameContext context)
        {
            // Use the injected IEffectExecutor
            return await _effectExecutor.ExecuteEffectAsync(effect.Config, context);
            // Note: The logic previously inside this method (creating dispatcher, handler, converting context)
            // should now reside within the implementation of IEffectExecutor.
        }

        // ConvertGameStateToDto method removed - use GameContext.CreateSnapshot() instead
        // This method was never used in the codebase and GameContext.CreateSnapshot() provides
        // the same functionality with proper AutoMapper integration

        /// <summary>
        /// 转换CardInstance列表到TargetId列表
        /// </summary>
        private static List<TargetId> ConvertTargetsToTargetIds(List<CardInstance> targets)
        {
            return targets.Select(t => TargetId.Card(t.InstanceId)).ToList();
        }

        /// <summary>
        /// 记录触发
        /// </summary>
        public void RecordTrigger()
        {
            TotalTriggerCount++;
            LastTriggerTime = DateTime.UtcNow;
            Trigger.RecordTrigger();
        }

        /// <summary>
        /// 重置回合状态
        /// </summary>
        public void ResetTurnState()
        {
            Trigger.ResetTurnCounters();
            Trigger.ResetEventFlags();

            foreach (var condition in Conditions)
                condition.ClearCache();

            foreach (var target in Targets)
                target.MarkForResolution();
        }

        /// <summary>
        /// 更新运行时状态
        /// </summary>
        public void UpdateRuntimeState()
        {
            Trigger.UpdateCooldownStatus();

            // 检查目标有效性
            foreach (var target in Targets)
            {
                if (!target.AreTargetsValid())
                    target.MarkForResolution();
            }
        }

        // Removed the temporary CreateRuntimeCondition method as Condition creation is now handled by IConditionFactory
    }
}
