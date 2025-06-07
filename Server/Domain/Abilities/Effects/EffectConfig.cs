using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects
{
    /// <summary>
    /// 效果配置类
    /// 定义能力的具体效果，支持复杂的效果组合和DSL语法
    /// </summary>
    public class EffectConfig
    {
        /// <summary>
        /// 效果唯一标识
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 效果名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 效果类型
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// 效果分类
        /// </summary>
        public EffectCategory Category { get; set; } = EffectCategory.Immediate;

        /// <summary>
        /// 效果目标配置
        /// </summary>
        public TargetConfig Target { get; set; } = new(); // Changed TargetConfig to TargetConfig

        /// <summary>
        /// 效果数值
        /// </summary>
        public ValueConfig Amount { get; set; } = new();

        /// <summary>
        /// 效果参数
        /// </summary>
        // M12 V1.1: 使用 ValueConfig 以支持动态参数值
        public Dictionary<string, ValueConfig> Parameters { get; set; } = new();

        /// <summary>
        /// 执行顺序
        /// </summary>
        public int ExecutionOrder { get; set; } = 0;

        /// <summary>
        /// 执行条件
        /// </summary>
        public List<ConditionConfig> ExecutionConditions { get; set; } = new();

        /// <summary>
        /// 失败时的处理策略
        /// </summary>
        public FailureStrategy OnFailure { get; set; } = FailureStrategy.Continue;

        /// <summary>
        /// 效果表达式（DSL语法）
        /// 例如: "damage(target, self.attack * 2)"
        /// </summary>
        public string Expression { get; set; } = string.Empty;

        /// <summary>
        /// 子效果列表（用于复合效果）
        /// </summary>
        public List<EffectConfig> SubEffects { get; set; } = new();

        /// <summary>
        /// 效果持续时间配置
        /// </summary>
        public EffectDurationConfig? Duration { get; set; }

        /// <summary>
        /// 效果触发配置（用于延迟效果）
        /// </summary>
        public EffectTriggerConfig? TriggerConfig { get; set; }

        /// <summary>
        /// 是否可以被取消
        /// </summary>
        public bool CanBeCanceled { get; set; } = false;

        /// <summary>
        /// 取消条件
        /// </summary>
        public List<ConditionConfig> CancelConditions { get; set; } = new();

        /// <summary>
        /// 效果优先级
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 创建伤害效果
        /// </summary>
        /// <param name="target">目标配置</param>
        /// <param name="amount">伤害数值</param>
        /// <returns>效果配置</returns>
        public static EffectConfig Damage(TargetConfig target, ValueConfig amount) // Changed TargetConfig to TargetConfig
        {
            return new EffectConfig
            {
                Type = StandardEffectTypes.Damage,
                Category = EffectCategory.Immediate,
                Target = target,
                Amount = amount
            };
        }

        /// <summary>
        /// 创建治疗效果
        /// </summary>
        /// <param name="target">目标配置</param>
        /// <param name="amount">治疗数值</param>
        /// <returns>效果配置</returns>
        public static EffectConfig Heal(TargetConfig target, ValueConfig amount) // Changed TargetConfig to TargetConfig
        {
            return new EffectConfig
            {
                Type = StandardEffectTypes.Heal,
                Category = EffectCategory.Immediate,
                Target = target,
                Amount = amount
            };
        }

        /// <summary>
        /// 创建抽牌效果
        /// </summary>
        /// <param name="target">目标配置</param>
        /// <param name="count">抽牌数量</param>
        /// <returns>效果配置</returns>
        public static EffectConfig Draw(TargetConfig target, ValueConfig count) // Changed TargetConfig to TargetConfig
        {
            return new EffectConfig
            {
                Type = StandardEffectTypes.DrawCard,
                Category = EffectCategory.Immediate,
                Target = target,
                Amount = count
            };
        }

        /// <summary>
        /// 创建召唤效果
        /// </summary>
        /// <param name="cardId">卡牌ID</param>
        /// <param name="count">召唤数量</param>
        /// <returns>效果配置</returns>
        public static EffectConfig Summon(int cardId, ValueConfig count)
        {
            return new EffectConfig
            {
                Type = StandardEffectTypes.SummonUnit,
                Category = EffectCategory.Immediate,
                Amount = count,
                // 假设 ValueConfig.Static 用于创建静态值
                Parameters = new Dictionary<string, ValueConfig> { ["cardId"] = ValueConfig.Static(cardId) }
            };
        }

        /// <summary>
        /// 创建属性修改效果
        /// </summary>
        /// <param name="target">目标配置</param>
        /// <param name="property">属性名</param>
        /// <param name="amount">修改数值</param>
        /// <param name="duration">持续时间</param>
        /// <returns>效果配置</returns>
        public static EffectConfig ModifyProperty(TargetConfig target, string property, ValueConfig amount, EffectDurationConfig? duration = null) // Changed TargetConfig to TargetConfig
        {
            var effectType = property.ToLower() switch
            {
                "attack" => StandardEffectTypes.ModifyAttack,
                "health" => StandardEffectTypes.ModifyHealth,
                _ => StandardEffectTypes.ModifyStats
            };

            return new EffectConfig
            {
                Type = effectType,
                Category = duration != null ? EffectCategory.Continuous : EffectCategory.Immediate,
                Target = target,
                Amount = amount,
                Duration = duration,
                // 假设 ValueConfig.Static 用于创建静态值
                Parameters = new Dictionary<string, ValueConfig> { ["property"] = ValueConfig.Static(property) }
            };
        }

        /// <summary>
        /// 创建复合效果
        /// </summary>
        /// <param name="subEffects">子效果列表</param>
        /// <returns>效果配置</returns>
        public static EffectConfig Composite(List<EffectConfig> subEffects)
        {
            return new EffectConfig
            {
                Type = "composite",
                Category = EffectCategory.Immediate,
                SubEffects = subEffects
            };
        }
    }

    // 注意：EffectCategory枚举已移至Server/Domain/Abilities/Common/Enums.cs
    // 请使用ShadowverseBeyond.Server.Domain.Abilities.Common命名空间中的EffectCategory枚举

    /// <summary>
    /// 效果持续时间配置
    /// </summary>
    public class EffectDurationConfig
    {
        /// <summary>
        /// 持续类型
        /// </summary>
        public DurationType Type { get; set; } = DurationType.Permanent;

        /// <summary>
        /// 持续回合数
        /// </summary>
        public int Turns { get; set; } = 1;

        /// <summary>
        /// 结束条件
        /// </summary>
        public List<ConditionConfig> EndConditions { get; set; } = new();

        /// <summary>
        /// 是否在回合结束时检查
        /// </summary>
        public bool CheckOnTurnEnd { get; set; } = true;

        /// <summary>
        /// 最大持续时间（毫秒）
        /// </summary>
        public int MaxDurationMs { get; set; } = -1; // -1表示无限制
    }

    /// <summary>
    /// 效果触发配置
    /// </summary>
    public class EffectTriggerConfig
    {
        /// <summary>
        /// 触发延迟（毫秒）
        /// </summary>
        public int DelayMs { get; set; } = 0;

        /// <summary>
        /// 触发条件
        /// </summary>
        public List<ConditionConfig> TriggerConditions { get; set; } = new();

        /// <summary>
        /// 触发事件类型
        /// </summary>
        public List<string> TriggerEvents { get; set; } = new();

        /// <summary>
        /// 最大触发次数
        /// </summary>
        public int MaxTriggers { get; set; } = 1;

        /// <summary>
        /// 当前触发次数
        /// </summary>
        public int CurrentTriggers { get; set; } = 0;
    }
}
