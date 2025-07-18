//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
// - 实体关系图: 文档/04-数据库设计/01-实体关系图.md
//
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using System.Linq;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    /// <summary>
    /// 运行时卡牌能力表示。
    /// 【架构最终版】职责：作为效果链（Effect Chain）的模板和工厂。
    /// </summary>
    public class Ability : IAbility // Implement IAbility
    {
        /// <summary>
        /// 静态配置引用
        /// </summary>
        public AbilityConfig Config { get; }

        // RuntimeId is part of IAbility
        public Guid RuntimeId { get; } // Already exists, ensure it's initialized (done in constructor)

        /// <summary>
        /// Gets the definition ID of the ability, linking to its static configuration.
        /// </summary>
        public long DefinitionId => Config.Id; // Implement DefinitionId

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
        /// Gets the configuration for how this ability is triggered.
        /// This now directly implements the IAbility interface member.
        /// </summary>
        public TriggerConfig TriggerConfig => Trigger.Config; // Already matches IAbility

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
        /// 【废弃】不再使用运行时的Effect包装类。
        /// public List<Effect> Effects { get; }
        /// </summary>

        /// <summary>
        /// Gets or sets the card instance that is the source of this ability, if applicable.
        /// Null if the ability is not sourced from a specific card (e.g., system abilities).
        /// This now directly implements the IAbility interface member.
        /// </summary>
        public CardInstance? SourceCard { get; set; } // Already matches IAbility


        /// <summary>
        /// 总触发次数
        /// </summary>
        public int TotalTriggerCount { get; private set; }

        /// <summary>
        /// 上次触发时间
        /// </summary>
        public DateTime? LastTriggerTime { get; private set; }

        private readonly IConditionFactory _conditionFactory;

        /// <summary>
        /// 构造函数，从AbilityConfig创建运行时Ability
        /// </summary>
        /// <param name="config">静态配置</param>
        /// <param name="conditionFactory">用于创建 Condition 实例的工厂</param>
        /// <param name="sourceCard">源卡牌实例</param>
        public Ability(
            AbilityConfig config,
            IConditionFactory conditionFactory,
            CardInstance? sourceCard = null)
        {
            RuntimeId = Guid.NewGuid(); // Initialize RuntimeId here as it's part of the interface
            Config = config ?? throw new ArgumentNullException(nameof(config));
            _conditionFactory = conditionFactory ?? throw new ArgumentNullException(nameof(conditionFactory));
            SourceCard = sourceCard;
            // State is initialized by its property declaration.

            // 创建运行时组件
            Trigger = new Runtime.Trigger(config.Trigger);

            Conditions = config.Conditions
                .Select(c => _conditionFactory.CreateCondition(c))
                .ToList();

            Targets = config.Targets
                .Select(t => new Target(t))
                .ToList();
        }

        /// <summary>
        /// 检查能力是否可以触发（包含区域检查优化）
        /// </summary>
        public Task<bool> CanTrigger(IGameEvent gameEvent, GameContext context)
        {
            // 移除 IsActive 和 State 检查
            // 激活状态由 AbilityManager 根据游戏上下文（如卡牌区域、沉默状态等）判断
            // 此处主要依赖 TriggerConfig 进行事件类型和区域匹配
            // 以及其他必要的上下文检查（例如，SourceCard 是否被沉默等，如果这些检查适合放在 CanTrigger）
            if (SourceCard != null && SourceCard.IsSilenced)
            {
                return Task.FromResult(false); // 被沉默的卡牌能力不能触发
            }
            return Task.FromResult(Trigger.CanTrigger(gameEvent, SourceCard));
        }

        /// <summary>
        /// 检查所有条件是否满足
        /// </summary>
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
        /// 解析所有目标，遵循IAbility接口的新契约。
        /// 此方法现在负责收集并返回所有解析到的目标。
        /// </summary>
        public async Task<List<TargetInfo>> ResolveTargetsAsync(GameContext context)
        {
            var allResolvedTargets = new List<TargetInfo>();
            foreach (var target in Targets)
            {
                // 调用每个运行时Target的解析方法，它会更新自己的ResolvedTargets属性并返回它们
                var resolvedTargetsForCurrentSelector = await target.ResolveTargetsAsync(context);
                if (resolvedTargetsForCurrentSelector != null)
                {
                    allResolvedTargets.AddRange(resolvedTargetsForCurrentSelector);
                }
            }
            return allResolvedTargets;
        }

        /// <summary>
        /// 【架构最终版】根据预先解析好的目标创建效果列表。
        /// 此方法现在是纯粹的“效果工厂”，不再包含条件检查或目标解析逻辑。
        /// </summary>
        /// <param name="context">统一的游戏上下文</param>
        /// <param name="resolvedTargets">由外部调用者（如EventProcessor）传入的、已经解析完毕的目标列表</param>
        /// <returns>一个包含所有待执行效果的列表</returns>
        public Task<List<Effect>> CreateEffectsAsync(GameContext context, List<TargetInfo> resolvedTargets)
        {
            var resultingEffects = new List<Effect>();

            // 根据配置，为每个EffectConfig创建一个独立的Effect请求
            var sortedEffectConfigs = Config.Effects
                .OrderBy(e => e.ExecutionOrder)
                .ToList();

            // 从上下文中获取AbilityManager，用于创建连锁能力
            var abilityManager = context.ServiceProvider.GetRequiredService<AbilityManager>();

            foreach (var effectConfig in sortedEffectConfigs)
            {
                var effectRequest = new Effect(
                    effectConfig,
                    context.RequestingPlayerId,
                    resolvedTargets, // 使用传入的已解析目标
                    this.RuntimeId, // parentAbilityId
                    context.TriggeringEvent?.EventId ?? Guid.Empty, // triggeredEventId
                    this.SourceCard?.InstanceId // sourceCardId
                );

                // --- 核心修改：生成时填充能力链 ---
                if (effectConfig.ChainedAbilities.Any())
                {
                    foreach (var chainedConfig in effectConfig.ChainedAbilities)
                    {
                        if (this.SourceCard != null)
                        {
                            // 使用从上下文中获取的AbilityManager
                            var chainedAbility = abilityManager.CreateAbilityFromConfig(chainedConfig, this.SourceCard);
                            if (chainedAbility != null)
                            {
                                // 将父Effect的完成事件作为触发事件
                                // 注意：这部分逻辑根据设计文档应在GameService中处理，
                                // 此处仅负责创建和附加能力实例。
                                // chainedAbility.TriggeredEvent = new EffectCompletedEvent(effectRequest.Id);
                                effectRequest.ChainedAbilities.Add(chainedAbility);
                            }
                        }
                    }
                }
                // --- 修改结束 ---

                resultingEffects.Add(effectRequest);
            }

            return Task.FromResult(resultingEffects);
        }

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
        public void RecordTrigger(GameContext context)
        {
            TotalTriggerCount++;
            LastTriggerTime = DateTime.UtcNow;
            Trigger.RecordTrigger();
        }

        /// <summary>
        /// 重置回合状态
        /// </summary>
        public void ResetTurnState(GameContext context)
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
        public void UpdateRuntimeState(GameContext context)
        {
            foreach (var target in Targets)
            {
                if (!target.AreTargetsValid(context))
                    target.MarkForResolution();
            }
        }

        // Implement missing interface methods for trigger counts
        /// <summary>
        /// Gets the number of times this ability has been triggered during the current turn.
        /// </summary>
        public int GetTriggerCountThisTurn(GameContext context)
        {
            return Trigger.CurrentTurnTriggerCount;
        }

        /// <summary>
        /// Gets the total number of times this ability has been triggered during the current game.
        /// </summary>
        public int GetTriggerCountThisGame(GameContext context)
        {
            return TotalTriggerCount;
        }
    }
}
