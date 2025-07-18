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
        /// TODO: 由string改为枚举类
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// 效果目标配置
        /// TODO: 这里的目标配置应该是对于从Target处继承下来的上下文中的目标列表的进一步选择，而非TargetConfig，需要修改
        /// </summary>
        public TargetConfig Target { get; set; } = new(); // Changed TargetConfig to TargetConfig

        /// <summary>
        /// <summary>
        /// 效果的执行顺序，数值越小越先执行。
        /// 用于处理同一能力内多个效果的执行次序。
        /// TODO: 移除所有ExecutionOrder的调用，依靠事件入队顺序即可
        /// </summary>
        public int ExecutionOrder { get; set; } = 0; // Default to 0 if not specified

        /// 效果数值
        /// </summary>
        public ValueConfig Amount { get; set; } = new();

        /// <summary>
        /// 效果参数
        /// </summary>
        // M12 V1.1: 使用 ValueConfig 以支持动态参数值
        public Dictionary<string, ValueConfig> Parameters { get; set; } = new();

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
        /// 是否可以被取消
        /// </summary>
        public bool CanBeCanceled { get; set; } = false;

        /// <summary>
        /// 取消条件
        /// </summary>
        public List<ConditionConfig> CancelConditions { get; set; } = new();

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
