using System.ComponentModel;

namespace ShadowverseBeyond.Server.Domain.Abilities.Common
{
    /// <summary>
    /// 能力类型枚举
    /// </summary>
    public enum AbilityType
    {
        /// <summary>
        /// 触发型能力（如Fanfare, LastWords等）
        /// </summary>
        Triggered,

        /// <summary>
        /// 持续效果能力
        /// </summary>
        Continuous,

        /// <summary>
        /// 激活型能力
        /// </summary>
        Activated,

        /// <summary>
        /// 关键字能力（如Rush, Taunt, Ward等）
        /// </summary>
        Keyword
    }

    /// <summary>
    /// 逻辑操作符枚举
    /// </summary>
    public enum LogicOperator
    {
        And,
        Or,
        Not,
        Xor
    }

    /// <summary>
    /// 条件类型枚举
    /// </summary>
    public enum ConditionType
    {
        /// <summary>
        /// 简单条件（属性比较）
        /// </summary>
        Simple,

        /// <summary>
        /// 复合条件（多个条件组合）
        /// </summary>
        Composite,

        /// <summary>
        /// 表达式条件（DSL表达式）
        /// </summary>
        Expression,

        /// <summary>
        /// 自定义条件（通过代码实现）
        /// </summary>
        Custom,

        /// <summary>
        /// 范围条件（值在某个范围内）
        /// </summary>
        Range,

        /// <summary>
        /// 集合条件（值在集合中）
        /// </summary>
        Collection,

        /// <summary>
        /// 正则表达式条件
        /// </summary>
        Regex
    }

    /// <summary>
    /// 值类型枚举
    /// </summary>
    public enum ValueType
    {
        /// <summary>
        /// 静态值
        /// </summary>
        Static,

        /// <summary>
        /// 动态值（从游戏状态获取）
        /// </summary>
        Dynamic,

        /// <summary>
        /// 公式计算值
        /// </summary>
        Formula,

        /// <summary>
        /// 变量值
        /// </summary>
        Variable,

    }

    /// <summary>
    /// 触发范围枚举
    /// </summary>
    public enum TriggerScope
    {
        /// <summary>
        /// 自身触发
        /// </summary>
        Self,

        /// <summary>
        /// 其他单位触发
        /// </summary>
        Other,

        /// <summary>
        /// 友方单位触发
        /// </summary>
        Ally,

        /// <summary>
        /// 敌方单位触发
        /// </summary>
        Enemy,

        /// <summary>
        /// 全局触发
        /// </summary>
        Global,

        /// <summary>
        /// 任意触发
        /// </summary>
        Any
    }

    /// <summary>
    /// 触发时序枚举
    /// </summary>
    public enum TriggerTiming
    {
        /// <summary>
        /// 事件发生前
        /// </summary>
        [Description("之前")]
        Before,

        /// <summary>
        /// 事件发生时
        /// </summary>
        [Description("时")]
        On,

        /// <summary>
        /// 事件发生后
        /// </summary>
        [Description("之后")]
        After,
    }

    /// <summary>
    /// 能力状态枚举
    /// </summary>
    public enum AbilityState
    {
        /// <summary>
        /// 未激活
        /// </summary>
        Inactive,

        /// <summary>
        /// 已激活
        /// </summary>
        Active,

        /// <summary>
        /// 已暂停
        /// </summary>
        Suspended,

        /// <summary>
        /// 已禁用
        /// </summary>
        Disabled,

        /// <summary>
        /// 已销毁
        /// </summary>
        Destroyed
    }

    // Add the TargetType enum (can be in this file or a separate one)
    public enum TargetType
    {
        Undefined,
        Player,
        Card, // Renamed from CardInstance for conciseness and alignment with TargetInfo
        None // Represents a system-level or non-specific source
    }

    /// <summary>
    /// 目标类型枚举
    /// </summary>
    public enum TargetConfigType
    {
        /// <summary>
        /// 自身
        /// </summary>
        Self,

        /// <summary>
        /// 敌方目标
        /// </summary>
        Enemy,

        /// <summary>
        /// 友方目标
        /// </summary>
        Ally,

        /// <summary>
        /// 任意目标
        /// </summary>
        Any,
    }

    public enum SelectorType
    {
        Filtered,
        PlayerChoice,
        Composite
    }

    /// <summary>
    /// 选择方法枚举
    /// </summary>
    public enum SelectionMethod
    {
        /// <summary>
        /// 全部选择
        /// </summary>
        All,

        /// <summary>
        /// 随机选择
        /// </summary>
        Random,

        /// <summary>
        /// 按顺序选取
        /// </summary>
        First,

        /// <summary>
        /// 按逆序选取
        /// </summary>
        Last,

        /// <summary>
        /// 玩家选择
        /// </summary>
        PlayerChoice
    }

    /// <summary>
    /// 失败处理策略枚举
    /// </summary>
    public enum FailureStrategy
    {
        /// <summary>
        /// 继续执行
        /// </summary>
        Continue,

        /// <summary>
        /// 停止执行
        /// </summary>
        Stop,

        /// <summary>
        /// 重试
        /// </summary>
        Retry,

        /// <summary>
        /// 跳过
        /// </summary>
        Skip
    }

    /// <summary>
    /// 排序方向枚举
    /// </summary>
    public enum SortDirection
    {
        /// <summary>
        /// 升序
        /// </summary>
        Ascending,

        /// <summary>
        /// 降序
        /// </summary>
        Descending
    }

    /// <summary>
    /// Defines how long a buff lasts or how it is removed.
    /// </summary>
    public enum DurationType
    {
        /// <summary> Lasts indefinitely until explicitly removed or silenced. </summary>
        Permanent,
        /// <summary> Removed at the end of the current turn (or specified player's turn). </summary>
        EndOfTurn,
        /// <summary> Removed at the start of the next turn (or specified player's turn). </summary>
        StartOfNextTurn,
        /// <summary> Removed when a specific game event occurs (e.g., "OnAttack"). Parameter holds event details. </summary>
        SpecificEvent,
        /// <summary> Lasts for a specific number of turns. Parameter holds the number. </summary>
        LimitedTurns,
        /// <summary> Removed based on a TriggerConfig and optional ConditionConfig. </summary>
        ConditionalRemoval
    }
}
/// <summary>
/// Defines the relationship between the ability's owner and the event's participants (Source/Target)
/// required for the ability to trigger.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum TriggerRelationType
{
    /// <summary>
    /// The trigger condition is met regardless of the source or target. (Default)
    /// </summary>
    [Description("任意")]
    Any,

    /// <summary>
    /// Triggers if the ability's owner is the source of the event.
    /// </summary>
    [Description("仅限自己为事件源时")]
    SourceSelf,

    /// <summary>
    /// Triggers if the source of the event is any entity other than the ability's owner.
    /// </summary>
    [Description("仅限自己不是事件源时")]
    SourceOther,

    /// <summary>
    /// Triggers if the ability's owner is the target of the event.
    /// </summary>
    [Description("仅限自己为事件目标时")]
    TargetSelf,

    /// <summary>
    /// Triggers if the target of the event is any entity other than the ability's owner.
    /// </summary>
    [Description("仅限自己不是事件目标时")]
    TargetOther
}