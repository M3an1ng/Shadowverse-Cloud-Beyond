using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Effects;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using ShadowverseBeyond.Server.Domain.Abilities.JsonConverters;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects
{
    /// <summary>
    /// 效果配置类
    /// 定义能力的具体效果，支持复杂的效果组合和DSL语法
    /// </summary>
    public class EffectConfig
    {
        private Dictionary<string, ValueConfig> _parameters = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// 效果唯一标识
        /// </summary>
        //public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 效果名称
        /// </summary>
        //public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 效果类型
        /// 效果类型
        /// </summary>
        public EffectType Type { get; set; }

        /// <summary>
        /// <summary>
        /// 效果的执行顺序，数值越小越先执行。
        /// 用于处理同一能力内多个效果的执行次序。
        /// </summary>
        public int ExecutionOrder { get; set; } = 0; // Default to 0 if not specified

        /// <summary>
        /// 效果重复次数。0 表示只执行一次；1 表示额外再执行 1 次，以此类推。
        /// </summary>
        public int RepeatCount { get; set; } = 0;

        /// <summary>
        /// 动态重复次数配置（可选）。
        /// 当提供该值时，执行阶段会优先解析此 ValueConfig 作为 RepeatCount（向下取整并最小为 0）。
        /// </summary>
        public ValueConfig? RepeatCountValue { get; set; }

        /// <summary>
        /// 重复执行的条件（为空则默认满足）。
        /// </summary>
        public ConditionConfig? RepeatCondition { get; set; }

        /// 效果数值
        /// </summary>
        //public ValueConfig Amount { get; set; } = new();

        /// <summary>
        /// 效果参数
        /// </summary>
        // M12 V1.1: 使用 ValueConfig 以支持动态参数值
        public Dictionary<string, ValueConfig> Parameters
        {
            get => _parameters;
            set
            {
                if (value == null)
                {
                    _parameters = new Dictionary<string, ValueConfig>(StringComparer.OrdinalIgnoreCase);
                    return;
                }

                // Validate case-insensitive duplicates by rehydrating with the comparer.
                _parameters = new Dictionary<string, ValueConfig>(value, StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// 目标选择器：用于从父级能力已解析的目标池(ResolvedTargets)中选择目标。
        /// 规则为“取并”，每个字符串是一个目标池 key（如 "primary" / "target1"）。
        /// 支持通配："*" / "ResolvedTargets.*" / "ResolvedTargets.All" 表示全量。
        /// 如果为空，则默认使用父能力的所有已解析目标。
        /// </summary>
        [JsonConverter(typeof(TargetSelectorListConverter))]
        public List<string> TargetSelector { get; set; } = new();

        /// <summary>
        /// 失败时的处理策略
        /// </summary>
        public FailureHandling OnFailure { get; set; } = FailureHandling.CompleteWithFailure;

        /// <summary>
        /// 效果表达式（DSL语法）
        /// 例如: "damage(target, self.attack * 2)"
        /// </summary>
        //public string Expression { get; set; } = string.Empty;

        /// <summary>
        /// 子效果列表（用于复合效果）
        /// </summary>
        //public List<EffectConfig> SubEffects { get; set; } = new();

        /// <summary>
        /// 是否可以被取消
        /// </summary>
        //public bool CanBeCanceled { get; set; } = false;

        /// <summary>
        /// 取消条件
        /// </summary>
        //public List<ConditionConfig> CancelConditions { get; set; } = new();

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 定义当此效果成功执行后，如何构建 EffectCompletedEvent.ExecutionResults 的内容。
        /// 键 (string): 将作为 ExecutionResults 中的参数名。
        /// 值 (string): 一个DSL表达式，用于从效果的执行上下文中计算或提取该参数的值。
        /// </summary>
        public Dictionary<string, string>? Outputs { get; set; }

        /// <summary>
        /// 定义在此效果成功执行后，需要接续触发的能力链。
        /// 详见设计文档: 04-04-能力链与系统能力重构设计.md
        /// </summary>
        public List<AbilityConfig> ChainedAbilities { get; set; } = new();
    }
}
