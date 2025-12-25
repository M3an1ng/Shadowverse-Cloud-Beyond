using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Effects;
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
        /// TODO: 移除所有ExecutionOrder的调用，依靠事件入队顺序即可
        /// </summary>
        public int ExecutionOrder { get; set; } = 0; // Default to 0 if not specified

        /// 效果数值
        /// </summary>
        //public ValueConfig Amount { get; set; } = new();

        /// <summary>
        /// 效果参数
        /// </summary>
        // M12 V1.1: 使用 ValueConfig 以支持动态参数值
        public Dictionary<string, ValueConfig> Parameters { get; set; } = new();

        /// <summary>
        /// 一个DSL查询字符串，用于从父级能力已解析的目标池(ResolvedTargets)中，为当前效果选择其自己的目标。
        /// 这允许效果对已选定的目标进行二次筛选和组合。
        /// 例如: "ResolvedTargets.primary" 或 "Filter(ResolvedTargets.secondary, c => c.Attack > 3)"
        /// 如果为空，则默认使用父能力的所有已解析目标。
        /// </summary>
        public string TargetSelector { get; set; } = string.Empty;

        /// <summary>
        /// 失败时的处理策略
        /// </summary>
        //public FailureStrategy OnFailure { get; set; } = FailureStrategy.Continue;

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
        //public bool IsEnabled { get; set; } = true;

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
