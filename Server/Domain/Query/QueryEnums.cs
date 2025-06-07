using System.Text.Json.Serialization;

namespace ShadowverseBeyond.Server.Domain.Query
{
    /// <summary>
    /// 指定查询的目标是卡牌实例还是卡牌定义。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum QueryType
    {
        Instance, // 查询游戏对局中的卡牌实例 (CardInstance)
        Definition // 查询全局的卡牌定义 (CardDefinition)
    }

    /// <summary>
    /// 定义卡牌查询的初始来源池类型。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SourcePoolType
    {
        // Instance Pools
        PlayerHand,       // 指定玩家的手牌
        PlayerBoard,      // 指定玩家的战场
        PlayerDeck,       // 指定玩家的牌库
        PlayerGraveyard,  // 指定玩家的墓地
        PlayerBanish,     // 指定玩家的放逐区 (如果需要)
        OpponentHand,     // 对手的手牌 (通常不可见，但某些效果可能需要)
        OpponentBoard,    // 对手的战场
        OpponentDeck,     // 对手的牌库
        OpponentGraveyard,// 对手的墓地
        OpponentBanish,   // 对手的放逐区
        BothBoards,       // 双方战场
        CurrentAbilityTargets, // 当前能力已经选择的目标 (用于效果链)
        TriggeringCard,   // 触发当前能力的卡牌实例

        // Definition Pools
        AllCards,         // 所有卡牌定义
        // Future: SpecificTraitCards (e.g., "Dragoncraft Follower Definitions")
    }

    /// <summary>
    /// 定义筛选条件的类型。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum FilterType
    {
        // Common Filters (Applicable to both Instance & Definition where sensible)
        CardType,       // 卡牌类型 (Follower, Spell, Amulet)
        Trait,          // 特性/标签 (e.g., Officer, Commander, Dragoncraft, Machina)
        Cost,           // 费用
        Attack,         // 攻击力
        Health,         // 生命值
        CardName,       // 卡牌名称 (完全匹配或包含)
        CardId,         // 卡牌定义ID
        IsEvolved,      // 是否已进化 (Instance only)

        // Instance-Specific Filters
        Location,       // 当前位置 (Hand, Board, Deck, Graveyard) - Often implied by SourcePool
        IsDamaged,      // 是否受伤
        CanAttack,      // 是否可攻击
        HasKeyword,     // 是否拥有特定关键字 (e.g., Ward, Drain, Rush, Bane, Storm) - 需要 Keyword 系统支持
        HasEffect,      // 是否拥有特定效果/状态 (e.g., "Cannot be targeted by spells") - 需要 EffectState 系统支持
        Player,     // 控制者 (Self, Opponent) - Often implied by SourcePool
        AttackCountThisTurn, // 本回合攻击次数
        IsTargetable,   // 是否可被（特定类型的效果）指定为目标
        CanEvolve,      // 是否可进化 (Instance only)

        // Definition-Specific Filters
        Rarity,         // 稀有度
        CardSet,        // 卡包 (如果需要)

        // Logical Filters
        And,            // 逻辑与 (组合多个筛选器)
        Or,             // 逻辑或 (组合多个筛选器)
        Not             // 逻辑非 (反转一个筛选器)
    }

    /// <summary>
    /// 定义用于比较数值的运算符。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ComparisonOperator
    {
        EqualTo,            // 等于 (==)
        NotEqualTo,         // 不等于 (!=)
        GreaterThan,        // 大于 (>)
        GreaterThanOrEqualTo, // 大于或等于 (>=)
        LessThan,           // 小于 (<)
        LessThanOrEqualTo,  // 小于或等于 (<=)
        Contains,           // 包含 (用于集合或字符串)
        NotContains         // 不包含 (用于集合或字符串)
    }

    /// <summary>
    /// 定义排序方向。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OrderingDirection
    {
        Ascending,  // 升序
        Descending // 降序
    }

    /// <summary>
    /// 定义对筛选结果列表进行选择/后处理的策略。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SelectionStrategy // Renamed from PostProcessingType
    {
        First,          // 选择列表开头的指定数量
        Last,           // 选择列表末尾的指定数量
        Random,         // 随机选择指定数量
        LowestAttack,   // 选择攻击力最低的
        HighestAttack,  // 选择攻击力最高的
        LowestHealth,   // 选择生命值最低的
        HighestHealth,  // 选择生命值最高的
        LowestCost,     // 选择费用最低的
        HighestCost     // 选择费用最高的
    }
}