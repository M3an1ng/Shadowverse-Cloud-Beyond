using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.Common; // Re-added for SortDirection etc.
using ShadowverseBeyond.Server.Domain.Query; // Added for CardQueryConfig

namespace ShadowverseBeyond.Server.Domain.Abilities.Targets
{
    /// <summary>
    /// 目标选择器配置类
    /// 根据M11目标选择系统设计文档实现，支持JSON配置
    /// 简化版：直接嵌入 CardQueryConfig 来处理目标池、筛选、排序和数量
    /// </summary>
    public class TargetConfig
    {
        /// <summary>
        /// 选择器类型
        /// </summary>
        public Common.SelectorType SelectorType { get; set; } = Common.SelectorType.Self; // Default might change

        /// <summary>
        /// 选择器描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 卡牌查询配置，用于定义目标池、筛选、排序和数量限制。
        /// 主要用于 SelectorType.Self (隐式查询) 或其他需要查询的类型。
        /// PlayerChoice 类型也可能使用它来定义可选的目标池。
        /// </summary>
        public CardQueryConfig Query { get; set; } = new();

        // Removed TargetPool, Filters, Count, Ordering properties as their functionality
        // is now handled by the embedded CardQueryConfig 'Query'.

        /// <summary>
        /// 失败处理方式
        /// </summary>
        public FailureHandling OnFailure { get; set; } = FailureHandling.AbortAbility;

        /// <summary>
        /// 子选择器列表（用于Composite类型）
        /// </summary>
        public List<TargetConfig> SubSelectors { get; set; } = new();

        /// <summary>
        /// 变量名（用于FromVariable类型）
        /// </summary>
        public string? VariableName { get; set; }

        /// <summary>
        /// 玩家选择的具体配置（仅当 SelectorType 为 PlayerChoice 时有效）
        /// </summary>
        public PlayerChoiceConfig? PlayerChoiceOptions { get; set; }
    }

    // Removed inner classes TargetPoolConfig, TargetFilterConfig, ValueSourceConfig, CountConfig, OrderingConfig.
    // Their functionality is now handled by the embedded CardQueryConfig Query property.

    /// <summary>
    /// 玩家选择配置类
    /// </summary>
    public class PlayerChoiceConfig
    {
        /// <summary>
        /// 是否强制需要玩家选择
        /// </summary>
        public bool IsRequired { get; set; } = false;

        /// <summary>
        /// 选择提示信息
        /// </summary>
        public string? Prompt { get; set; }

        /// <summary>
        /// 是否可以取消选择
        /// </summary>
        public bool CanCancel { get; set; } = false;

        /// <summary>
        /// 如果只有一个目标是否自动选择
        /// </summary>
        public bool AutoSelectSingleIfPossible { get; set; } = true;
    }
}
