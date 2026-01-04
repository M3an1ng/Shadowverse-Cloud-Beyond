using System.ComponentModel;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// 定义游戏中可能发生的各种事件类型。
    /// 用于触发器监听、事件处理和日志记录。
    /// </summary>
    public enum EventType
    {
        // --- 卡牌生命周期事件 ---
        /// <summary>
        /// 卡牌从手牌被打出时触发。
        /// </summary>
        [Description("卡牌打出")]
        PlayCard,
        /// <summary>
        /// 卡牌进入战场时触发（包括出牌召唤、效果召唤等）。
        /// </summary>
        [Description("卡牌召唤")]
        Summon,
        /// <summary>
        /// 卡牌从战场或手牌等区域离开游戏（通常进入墓地）时触发。
        /// </summary>
        [Description("卡牌死亡")]
        Destroy,
        /// <summary>
        /// 卡牌从战场或手牌等区域离开游戏时（不进入墓地）触发。
        /// </summary>
        [Description("卡牌消失")]
        Vanish,
        /// <summary>
        /// 卡牌被公开时触发（公开其卡面信息）。
        /// </summary>
        [Description("卡牌公开")]
        CardRevealed,
        /// <summary>
        /// 卡牌进化时触发。
        /// </summary>
        [Description("卡牌进化")]
        Evolve,
        /// <summary>
        /// 卡牌超进化时触发（继承进化触发，额外提供超进化触发）。
        /// </summary>
        [Description("卡牌超进化")]
        SuperEvolve,
        /// <summary>
        /// 玩家从牌库抽牌时触发。
        /// </summary>
        [Description("抽牌")]
        Draw,
        /// <summary>
        /// 造成伤害时触发。
        /// </summary>
        [Description("造成伤害")]
        Damage,
        /// <summary>
        /// 卡牌从手牌被弃置时触发。
        /// </summary>
        Discard,
        /// <summary>
        /// 卡牌被添加到手牌时触发（例如，通过效果生成）。
        /// </summary>
        AddCardToHand,

        // --- 战斗相关事件 ---
        /// <summary>
        /// 单位宣告攻击时触发（在伤害计算前）。
        /// </summary>
        Attack,
        /// <summary>
        /// 单位宣告攻击，并已消耗攻击次数时触发（在伤害计算前）。
        /// </summary>
        AttackDeclared,
        /// <summary>
        /// 单位或玩家的生命值发生改变时触发。
        /// </summary>
        HealthChanged,
        /// <summary>
        /// 单位的攻击力或最大生命值发生改变时触发。
        /// </summary>
        StatChanged,
        /// <summary>
        /// 攻击状态（CanAttack）发生改变时触发。
        /// </summary>
        AttackStateUpdated,
        /// <summary>
        /// 单位每回合可攻击次数发生改变时触发。
        /// </summary>
        AttackTimesUpdated,
        /// <summary>
        /// 玩家的可进化状态发生改变时触发。
        /// </summary>
        EvolveStateUpdated,
        /// <summary>
        /// 玩家的可超进化状态发生改变时触发。
        /// </summary>
        SuperEvolveStateUpdated,

        // --- 回合与阶段事件 ---
        /// <summary>
        /// 玩家回合开始时触发。
        /// </summary>
        TurnStart,
        /// <summary>
        /// 玩家回合结束时触发。
        /// </summary>
        TurnEnd,
        /// <summary>
        /// 特定游戏阶段开始时触发（例如：开始阶段、结束阶段）。
        /// </summary>
        PhaseStart,
        /// <summary>
        /// 特定游戏阶段结束时触发。
        /// </summary>
        PhaseEnd,
        /// <summary>
        /// 游戏阶段转换完成时触发。
        /// </summary>
        PhaseTransition,

        // --- 资源相关事件 ---
        /// <summary>
        /// 玩家PP（Play Point）当前值发生改变时触发。
        /// </summary>
        PpUpdated,
        /// <summary>
        /// 玩家PP（Play Point）最大值发生改变时触发。
        /// </summary>
        PpMaxUpdated,
        /// <summary>
        /// 玩家PP+1可用次数发生改变时触发。
        /// </summary>
        PpBoostStateUpdated,
        /// <summary>
        /// 玩家EP（Evolve Point）当前值发生改变时触发。
        /// </summary>
        EpUpdated,
        /// <summary>
        /// 玩家EP（Evolve Point）最大值发生改变时触发。
        /// </summary>
        EpMaxUpdated,
        /// <summary>
        /// 玩家SEP（Super Evolve Point）当前值发生改变时触发。
        /// </summary>
        SepUpdated,
        /// <summary>
        /// 玩家SEP（Super Evolve Point）最大值发生改变时触发。
        /// </summary>
        SepMaxUpdated,
        /// <summary>
        /// 玩家牌库数量发生改变时触发。
        /// </summary>
        DeckUpdated,
        /// <summary>
        /// 玩家墓地数量发生改变时触发。
        /// </summary>
        GraveyardUpdated,

        // --- 状态与效果事件 ---
        /// <summary>
        /// Buff（增益/减益效果）被施加到单位或玩家时触发。
        /// </summary>
        BuffApplied,
        /// <summary>
        /// Buff从单位或玩家身上移除时触发。
        /// </summary>
        BuffRemoved,
        /// <summary>
        /// 关键字（如突进、守护）被赋予单位时触发。
        /// </summary>
        KeywordApplied,
        /// <summary>
        /// 关键字从单位身上移除时触发。
        /// </summary>
        KeywordRemoved,
        /// <summary>
        /// 沉默效果被施加到单位时触发。
        /// </summary>
        SilenceApplied,
        /// <summary>
        /// 沉默效果从单位身上移除时触发。
        /// </summary>
        SilenceRemoved,

        // --- 游戏流程事件 ---
        /// <summary>
        /// 玩家使用EP执行了进化动作后触发，用于通知本回合进化权已消耗。
        /// </summary>
        PlayerEvolved,
        /// <summary>
        /// 整场游戏开始时触发。
        /// </summary>
        GameStart,
        /// <summary>
        /// 整场游戏结束时触发。
        /// </summary>
        GameEnd,
        /// <summary>
        /// 玩家完成换牌时触发。
        /// </summary>
        MulliganCompleted,
        /// <summary>
        /// 玩家手牌更新时触发。
        /// </summary>
        HandUpdated,
        /// <summary>
        /// 玩家提交换牌请求时触发。
        /// </summary>
        MulliganSubmitted,
        /// <summary>
        /// 玩家请求攻击时触发。
        /// </summary>
        AttackRequested,
        /// <summary>
        /// 玩家请求结束回合时触发。
        /// </summary>
        EndTurnRequested,
        /// <summary>
        /// 玩家请求进化时触发。
        /// </summary>
        EvolveRequested,
        /// <summary>
        /// 玩家请求超进化时触发。
        /// </summary>
        SuperEvolveRequested,
        /// <summary>
        /// 玩家请求使用PP+1时触发。
        /// </summary>
        PpBoostRequested,
        /// <summary>
        /// 玩家请求出牌时触发。
        /// </summary>
        PlayCardRequested,


        // --- 能力系统事件 ---
        /// <summary>
        /// 当一个能力成功触发其效果时广播（用于能力间交互）。
        /// </summary>
        EffectInvoked,
        /// <summary>
        /// 当一个能力被触发时广播。
        /// </summary>
        AbilityTriggered,
        /// <summary>
        /// 倒计时结束时触发。
        /// </summary>
        CountdownFinished,
        /// <summary>
        /// 倒计时减少时触发。
        /// </summary>
        CountdownTick,
        /// <summary>
        /// 治疗效果时触发。
        /// </summary>
        Heal,
        /// <summary>
        /// 单位交战时触发。
        /// </summary>
        Clash,

        Return,

        /// <summary>
        /// 将卡牌从战场洗回牌库时触发。
        /// </summary>
        [Description("洗回牌库")]
        Tuck,

        Transform,

        // --- 其他/通用事件 ---
        /// <summary>
        /// 通用计数器改变事件。
        /// </summary>
        CounterChanged,
        /// <summary>
        /// 区域（手牌、战场、墓地等）中的卡牌发生变化。
        /// </summary>
        ZoneChanged,
        /// <summary>
        /// 卡牌在不同区域之间移动时触发。
        /// </summary>
        CardMoved,
        /// <summary>
        /// 卡牌因手牌已满而被爆牌时触发。
        /// </summary>
        CardBurned,
        /// <summary>
        /// 玩家试图抽牌但牌库为空时触发。
        /// </summary>
        DeckEmptyOnDraw,
        /// <summary>
        /// 效果处理器完成其主要操作后触发的聚合事件。
        /// </summary>
        EffectCompleted,
        /// <summary>
        /// An internal event used to resume a transaction with a player's choice.
        /// </summary>
        PlayerChoiceSubmitted,
        /// <summary>
        /// 未知或自定义事件类型。
        /// </summary>
        Unknown,

        /// <summary>
        /// 创建卡牌实例并放置到指定区域（可用于效果生成、调试注入等）。
        /// </summary>
        CreateCard,

        /// <summary>
        /// 复制卡牌实例并放置到指定区域。
        /// </summary>
        CopyCard,

        /// <summary>
        /// 一张卡牌成功发动了 Act（启动）能力（用于触发 Activated 能力）。
        /// </summary>
        Act,

        /// <summary>
        /// 玩家请求发动场上卡牌的 Act（启动）能力。
        /// </summary>
        ActRequested,

        /// <summary>
        /// 一张卡牌成功发动了 Fusion（融合）能力（用于触发 Fusion 能力）。
        /// </summary>
        Fusion,

        /// <summary>
        /// 玩家请求发动手牌中的 Fusion（融合）能力。
        /// </summary>
        FusionRequested,

        /// <summary>
        /// 场上卡牌的 Act 可用性/已使用状态发生变化（用于驱动客户端 UI）。
        /// </summary>
        ActStateUpdated,
        AddDeck,
        /// <summary>
        /// 纹章被添加到玩家纹章区时触发。
        /// </summary>
        AddEmblem,

        /// <summary>
        /// 玩家使用法术后触发的魔力增幅事件。
        /// </summary>
        SpellBoost,

    }


    public enum EffectAbility
    {
        None = 0, SetResult = 1, KeepCard = 2, TurnStart = 3, TurnEnd = 4,
        Play = 5, Cover = 6, Attack = 7, Evolve = 8, Fusion = 9, Act = 10,
        Accelerate = 11, Crystalize = 12, SetAttackTimes = 13,
        ChangeFlag = 90,
        Random = 91,

        SetKeyword = 100,
        Draw = 101,
        Summon = 102,
        Damage = 103,
        Heal = 104,
        Destroy = 105,
        Vanish = 106,
        Return = 107,
        Transform = 108,
        SetStatus = 109,
        RemoveBuff = 110,

        GetToken = 111,
        SpellBoost = 112,
        SetCost = 113,
        Ramp = 114,
        AddEffect = 115,
        RemoveEffect = 116,
        SetGrave = 117,
        SetCountdown = 118,
        AddDeck = 119,
        Hybrid = 120,

        Bury = 121,
        Reanimate = 122,
        Discard = 123,
        Travel = 124,
        Reveal = 125,
        SetDamage = 126,
        SetHeal = 127,
        SetLeave = 128,
        SetPP = 129,
        SetEP = 130,
        SetCharge = 131,
        SetValue = 132,
        Choose = 133,
        AddBuff = 134,
    }
}
