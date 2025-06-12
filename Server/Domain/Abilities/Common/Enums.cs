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
    /// 比较操作符枚举
    /// </summary>
    public enum ComparisonOperator
    {
        Equals,
        NotEquals,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual,
        Contains,
        NotContains,
        In,
        NotIn
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

        /// <summary>
        /// 随机值
        /// </summary>
        Random
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
        Immediate,

        /// <summary>
        /// 事件发生时
        /// </summary>
        On,

        /// <summary>
        /// 事件发生后
        /// </summary>
        After,

        /// <summary>
        /// 事件中断
        /// </summary>
        Intercept
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

    /// <summary>
    /// 目标类型枚举
    /// </summary>
    public enum TargetType
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
        Self,
        Filtered,
        FromVariable,
        TriggeringEventData,
        Composite,
        PlayerChoice
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
