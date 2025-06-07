//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
// - 实体关系图: 文档/04-数据库设计/01-实体关系图.md
//
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Dtos;
using Microsoft.Extensions.Logging;
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

        /// <summary>
        /// 构造函数，从AbilityConfig创建运行时Ability
        /// </summary>
        /// <param name="config">静态配置</param>
        /// <param name="sourceCard">源卡牌实例</param>
        public Ability(AbilityConfig config, CardInstance? sourceCard = null)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            SourceCard = sourceCard;

            // 创建运行时组件
            Trigger = new Runtime.Trigger(config.Trigger);

            // TODO: 需要依赖注入DslEvaluator和DslExpressionParser
            // 临时使用null，需要后续重构
            Conditions = config.Conditions
                .Select(c => CreateRuntimeCondition(c))
                .ToList();

            Targets = config.Targets
                .Select(t => new Target(t))
                .ToList();

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
        /// 检查能力是否可以触发
        /// </summary>
        /// <param name="gameEvent">游戏事件</param>
        /// <returns>是否可以触发</returns>
        public bool CanTrigger(IGameEvent gameEvent)
        {
            if (!IsActive || State != AbilityState.Active)
                return false;

            return Trigger.CanTrigger(gameEvent);
        }

        /// <summary>
        /// 检查所有条件是否满足
        /// </summary>
        /// <param name="context">条件评估上下文</param>
        /// <returns>条件是否满足</returns>
        public async Task<bool> CheckConditionsAsync(ConditionEvaluationContext context)
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
        /// <param name="context">目标解析上下文</param>
        /// <returns>解析的目标列表</returns>
        public async Task<List<CardInstance>> ResolveTargetsAsync(TargetResolutionContext context)
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
        /// <param name="context">效果执行上下文</param>
        /// <returns>生成的事件列表</returns>
        public async Task<List<IGameEvent>> ExecuteEffectsAsync(EffectExecutionContext context)
        {
            var allEvents = new List<IGameEvent>();

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

                    // TODO: 实际的效果执行逻辑
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
        /// <param name="context">执行上下文</param>
        /// <returns>生成的事件列表</returns>
        private async Task<List<IGameEvent>> ExecuteSingleEffectAsync(Effect effect, EffectExecutionContext context)
        {
            try
            {
                // 创建EffectHandler和GameStateActionDispatcher
                var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
                var dispatcherLogger = loggerFactory.CreateLogger<Effects.GameStateActionDispatcher>();
                var handlerLogger = loggerFactory.CreateLogger<Effects.EffectHandler>();

                var dispatcher = new Effects.GameStateActionDispatcher(dispatcherLogger);

                // TODO: 实现IValueSourceResolver - 临时使用null，需要后续实现
                var handler = new Effects.EffectHandler(dispatcher, null!, handlerLogger);

                // 转换EffectExecutionContext到AbilityContext
                var abilityContext = new AbilityContext
                {
                    CurrentGameState = ConvertGameStateToDto(context.GameState),
                    TriggeringEvent = context.TriggeringEvent,
                    SourceCard = context.SourceCard,
                    SelectedTargets = ConvertTargetsToTargetIds(context.SelectedTargets),
                    Parameters = context.Variables
                };

                // 执行效果
                var result = await handler.ExecuteAsync(effect.Config, abilityContext);

                // 转换生成的事件
                var gameEvents = new List<IGameEvent>();
                foreach (var eventObj in result.GeneratedEvents)
                {
                    if (eventObj is IGameEvent gameEvent)
                    {
                        gameEvents.Add(gameEvent);
                    }
                }

                return gameEvents;
            }
            catch (Exception ex)
            {
                // 记录错误并返回空列表
                // TODO: 添加适当的日志记录
                return new List<IGameEvent>();
            }
        }

        /// <summary>
        /// 转换GameState到GameStateDto（临时实现）
        /// </summary>
        private static GameStateDto? ConvertGameStateToDto(GameState? gameState)
        {
            // TODO: 实现实际的转换逻辑
            return gameState?.ToDto();
        }

        /// <summary>
        /// 转换CardInstance列表到TargetId列表
        /// </summary>
        private static List<TargetId> ConvertTargetsToTargetIds(List<CardInstance> targets)
        {
            return targets.Select(t => new TargetId(t.InstanceId.ToString())).ToList();
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

        /// <summary>
        /// 创建运行时条件（临时方法，需要依赖注入重构）
        /// </summary>
        /// <param name="config">条件配置</param>
        /// <returns>运行时条件</returns>
        private static Condition CreateRuntimeCondition(ConditionConfig config)
        {
            // TODO: 需要依赖注入IDslConditionEvaluator
            // 临时创建空的实例，需要后续重构
            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
            var dslEvaluatorLogger = loggerFactory.CreateLogger<DSL.Evaluation.DslEvaluator>();
            var dslParserLogger = loggerFactory.CreateLogger<DSL.Processing.DslExpressionParser>();
            var dslConditionEvaluatorLogger = loggerFactory.CreateLogger<Conditions.DslConditionEvaluator>();

            var dslEvaluator = new DSL.Evaluation.DslEvaluator(dslEvaluatorLogger);
            var dslParser = new DSL.Processing.DslExpressionParser(dslParserLogger);
            var dslConditionEvaluator = new Conditions.DslConditionEvaluator(dslEvaluator, dslParser, dslConditionEvaluatorLogger);

            return new Condition(config, dslConditionEvaluator);
        }
    }
}
