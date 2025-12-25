using ShadowverseBeyond.Server.Domain.Abilities.Common;

using System.ComponentModel;

namespace ShadowverseBeyond.Server.Domain.Abilities.Targets
{
    /// <summary>
    /// 目标池来源类型枚举
    /// 根据M11目标选择系统设计文档实现
    /// </summary>
    public enum TargetPoolSource
    {
        /// <summary>
        /// 己方战场
        /// </summary>
        OwnField,

        /// <summary>
        /// 敌方战场
        /// </summary>
        OpponentField,

        /// <summary>
        /// 己方手牌
        /// </summary>
        OwnHand,

        /// <summary>
        /// 敌方手牌
        /// </summary>
        OpponentHand,

        /// <summary>
        /// 己方牌库
        /// </summary>
        OwnDeck,

        /// <summary>
        /// 敌方牌库
        /// </summary>
        OpponentDeck,

        /// <summary>
        /// 己方墓地
        /// </summary>
        OwnGraveyard,

        /// <summary>
        /// 敌方墓地
        /// </summary>
        OpponentGraveyard,

        /// <summary>
        /// 己方主战者
        /// </summary>
        OwnLeader,

        /// <summary>
        /// 敌方主战者
        /// </summary>
        OpponentLeader,

        /// <summary>
        /// 双方战场所有卡牌
        /// </summary>
        AllCardsInPlay,

        /// <summary>
        /// 游戏中所有区域的卡牌 (慎用，性能影响大)
        /// </summary>
        AllCards,

        /// <summary>
        /// 触发事件直接关联的目标
        /// </summary>
        TriggeringEventTargets,

        /// <summary>
        /// 能力来源卡牌自身
        /// </summary>
        ContextSourceCard,

        /// <summary>
        /// 使用自定义 DSL 查询
        /// </summary>
        DslQuery
    }

    /// <summary>
    /// 数量模式枚举
    /// 根据M11目标选择系统设计文档实现
    /// </summary>
    public enum CountMode
    {
        /// <summary>
        /// 必须选择精确数量的目标
        /// </summary>
        Exact,

        /// <summary>
        /// 最多选择指定数量的目标
        /// </summary>
        Max,

        /// <summary>
        /// 选择数量在指定范围内的目标
        /// </summary>
        Range,

        /// <summary>
        /// 选择所有符合条件的目标
        /// </summary>
        All
    }

    // 注意：排序方向使用现有的SortDirection枚举

    /// <summary>
    /// 失败处理方式枚举
    /// 根据M11目标选择系统设计文档实现
    /// </summary>
    public enum FailureHandling
    {
        /// <summary>
        /// 中止能力
        /// </summary>
        [Description("中止能力")]
        AbortAbility,

        /// <summary>
        /// 跳过效果
        /// </summary>
        [Description("跳过效果")]
        SkipEffect,

        /// <summary>
        /// 记录警告
        /// </summary>
        [Description("记录警告")]
        LogWarning
    }

    /// <summary>
    /// 过滤器类型枚举
    /// 根据M11目标选择系统设计文档实现
    /// </summary>
    public enum FilterType
    {
        /// <summary>
        /// 属性比较过滤器
        /// </summary>
        PropertyComparison,

        /// <summary>
        /// 特性检查过滤器
        /// </summary>
        HasTrait,

        /// <summary>
        /// 条件过滤器
        /// </summary>
        Condition,

        /// <summary>
        /// 特定卡牌检查过滤器
        /// </summary>
        IsSpecificCard,

        /// <summary>
        /// 相对位置过滤器
        /// </summary>
        RelativePosition
    }
}
