using ShadowverseBeyond.Server.Domain.Abilities.Common;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Domain.Abilities.Conditions
{
    /// <summary>
    /// 条件配置类
    /// 定义能力生效的条件，支持复杂的逻辑表达式和DSL语法
    /// </summary>
    public class ConditionConfig
    {
        /// <summary>
        /// 条件唯一标识
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 条件名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 逻辑操作符（用于复合条件，如果保留该类型）
        /// </summary>
        public LogicOperator Logic { get; set; } = LogicOperator.And;

        /// <summary>
        /// 条件表达式（DSL语法）
        /// 例如: "self.attack > target.health AND self.keywords.contains('Rush')"
        /// 这是定义条件逻辑的主要方式。
        /// </summary>
        public string Expression { get; set; } = string.Empty;

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        // /// <summary>
        // /// 创建简单条件 (已弃用，使用Expression类型替代)
        // /// </summary>
        // /// <param name="target">目标</param>
        // /// <param name="property">属性</param>
        // /// <param name="op">操作符</param>
        // /// <param name="value">值</param>
        // /// <returns>条件配置</returns>
        // public static ConditionConfig Simple(string target, string property, ComparisonOperator op, object value)
        // {
        //     return new ConditionConfig
        //     {
        //         Type = ConditionType.Simple,
        //         // Target = target, // Removed
        //         // Property = property, // Removed
        //         // Operator = op, // Removed
        //         // Value = new ValueConfig { Type = Common.ValueType.Static, StaticValue = value } // Removed
        //     };
        // }

        /// <summary>
        /// 创建表达式条件
        /// </summary>
        /// <param name="expression">条件表达式</param>
        /// <returns>条件配置</returns>
        public static ConditionConfig FromExpression(string expression)
        {
            return new ConditionConfig
            {
                Expression = expression
            };
        }

        // /// <summary>
        // /// 创建复合条件 (已弃用，使用Expression类型替代)
        // /// </summary>
        // /// <param name="conditions">子条件列表</param>
        // /// <param name="logic">逻辑操作符</param>
        // /// <returns>条件配置</returns>
        // public static ConditionConfig Composite(List<ConditionConfig> conditions, LogicOperator logic = LogicOperator.And)
        // {
        //     return new ConditionConfig
        //     {
        //         Type = ConditionType.Composite,
        //         // SubConditions = conditions, // Removed
        //         Logic = logic
        //     };
        // }

        /// <summary>
        /// 创建自定义条件
        /// </summary>
        /// <param name="customType">自定义类型</param>
        /// <param name="parameters">参数</param>
        /// <returns>条件配置</returns>
        // public static ConditionConfig Custom(string customType, Dictionary<string, object> parameters)
        // {
        //     return new ConditionConfig
        //     {
        //         Type = ConditionType.Custom,
        //         Parameters = parameters
        //     };
        // }
    }

    // 注意：ConditionType枚举已移至Server/Domain/Abilities/Common/Enums.cs
    // 请使用ShadowverseBeyond.Server.Domain.Abilities.Common命名空间中的ConditionType枚举

    /// <summary>
    /// 条件组配置
    /// 用于管理相关条件的分组
    /// </summary>
    public class ConditionGroupConfig
    {
        /// <summary>
        /// 组唯一标识
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 组名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 组内条件列表
        /// </summary>
        public List<ConditionConfig> Conditions { get; set; } = new();

        /// <summary>
        /// 组内逻辑操作符
        /// </summary>
        public LogicOperator InternalLogic { get; set; } = LogicOperator.And;

        /// <summary>
        /// 与其他组的逻辑操作符
        /// </summary>
        public LogicOperator ExternalLogic { get; set; } = LogicOperator.And;

        /// <summary>
        /// 组优先级
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;
    }

}
