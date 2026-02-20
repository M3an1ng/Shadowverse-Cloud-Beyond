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
        [Description("使用卡牌")]
        PlayCard,
        /// <summary>
        /// 卡牌进入战场时触发（包括出牌召唤、效果召唤等）。
        /// </summary>
        [Description("召唤")]
        Summon,
        /// <summary>
        /// 卡牌从战场或手牌等区域离开游戏（通常进入墓地）时触发。
        /// </summary>
        [Description("破坏")]
        Destroy,
        /// <summary>
        /// 卡牌从战场或手牌等区域离开游戏时（不进入墓地）触发。
        /// </summary>
        [Description("消失")]
        Vanish,
        /// <summary>
        /// 卡牌被公开时触发（公开其卡面信息）。
        /// </summary>
        [Description("公开")]
        CardRevealed,
        /// <summary>
        /// 卡牌进化时触发。
        /// </summary>
        [Description("进化")]
        Evolve,
        /// <summary>
        /// 卡牌超进化时触发（继承进化触发，额外提供超进化触发）。
        /// </summary>
        [Description("超进化")]
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
        [Description("弃牌")]
        Discard,
        /// <summary>
        /// 卡牌被添加到手牌时触发（例如，通过效果生成）。
        /// </summary>
        [Description("加入手牌")]
        AddCardToHand,

        // --- 战斗相关事件 ---
        /// <summary>
        /// 单位宣告攻击时触发（在伤害计算前）。
        /// </summary>
        [Description("攻击")]
        Attack,
        /// <summary>
        /// 单位宣告攻击，并已消耗攻击次数时触发（在伤害计算前）。
        /// </summary>
        [Description("攻击宣告")]
        AttackDeclared,
        /// <summary>
        /// 单位或玩家的生命值发生改变时触发。
        /// </summary>
        [Description("生命值变更")]
        HealthChanged,
        /// <summary>
        /// 单位的攻击力或最大生命值发生改变时触发。
        /// </summary>
        [Description("属性变更")]
        StatChanged,
        /// <summary>
        /// 攻击状态（CanAttack）发生改变时触发。
        /// </summary>
        [Description("攻击状态更新")]
        AttackStateUpdated,
        /// <summary>
        /// 单位每回合可攻击次数发生改变时触发。
        /// </summary>
        [Description("攻击次数更新")]
        AttackTimesUpdated,
        /// <summary>
        /// 单位本回合已攻击次数被重置时触发。
        /// </summary>
        [Description("攻击次数重置")]
        AttackTimesReset,
        /// <summary>
        /// 玩家的可进化状态发生改变时触发。
        /// </summary>
        [Description("进化状态更新")]
        EvolveStateUpdated,
        /// <summary>
        /// 玩家的可超进化状态发生改变时触发。
        /// </summary>
        [Description("超进化状态更新")]
        SuperEvolveStateUpdated,

        // --- 回合与阶段事件 ---
        /// <summary>
        /// 玩家回合开始时触发。
        /// </summary>
        [Description("回合开始")]
        TurnStart,
        /// <summary>
        /// 玩家回合结束时触发。
        /// </summary>
        [Description("回合结束")]
        TurnEnd,
        /// <summary>
        /// 特定游戏阶段开始时触发（例如：开始阶段、结束阶段）。
        /// </summary>
        [Description("阶段开始")]
        PhaseStart,
        /// <summary>
        /// 特定游戏阶段结束时触发。
        /// </summary>
        [Description("阶段结束")]
        PhaseEnd,
        /// <summary>
        /// 游戏阶段转换完成时触发。
        /// </summary>
        [Description("阶段切换")]
        PhaseTransition,

        // --- 资源相关事件 ---
        /// <summary>
        /// 玩家PP（Play Point）当前值发生改变时触发。
        /// </summary>
        [Description("PP变更")]
        PpUpdated,
        /// <summary>
        /// 玩家PP（Play Point）最大值发生改变时触发。
        /// </summary>
        [Description("PP上限变更")]
        PpMaxUpdated,
        /// <summary>
        /// 玩家PP+1可用次数发生改变时触发。
        /// </summary>
        [Description("PP+1状态更新")]
        PpBoostStateUpdated,
        /// <summary>
        /// 玩家EP（Evolve Point）当前值发生改变时触发。
        /// </summary>
        [Description("EP变更")]
        EpUpdated,
        /// <summary>
        /// 玩家EP（Evolve Point）最大值发生改变时触发。
        /// </summary>
        [Description("EP上限变更")]
        EpMaxUpdated,
        /// <summary>
        /// 玩家SEP（Super Evolve Point）当前值发生改变时触发。
        /// </summary>
        [Description("SEP变更")]
        SepUpdated,
        /// <summary>
        /// 玩家SEP（Super Evolve Point）最大值发生改变时触发。
        /// </summary>
        [Description("SEP上限变更")]
        SepMaxUpdated,
        /// <summary>
        /// 玩家牌库数量发生改变时触发。
        /// </summary>
        [Description("牌库变更")]
        DeckUpdated,
        /// <summary>
        /// 玩家墓地数量发生改变时触发。
        /// </summary>
        [Description("墓地变更")]
        GraveyardUpdated,

        // --- 状态与效果事件 ---
        /// <summary>
        /// Buff（增益/减益效果）被施加到单位或玩家时触发。
        /// </summary>
        [Description("附加增益")]
        BuffApplied,
        /// <summary>
        /// Buff从单位或玩家身上移除时触发。
        /// </summary>
        [Description("移除增益")]
        BuffRemoved,
        /// <summary>
        /// 关键字（如突进、守护）被赋予单位时触发。
        /// </summary>
        [Description("附加关键字")]
        KeywordApplied,
        /// <summary>
        /// 关键字从单位身上移除时触发。
        /// </summary>
        [Description("移除关键字")]
        KeywordRemoved,
        /// <summary>
        /// 沉默效果被施加到单位时触发。
        /// </summary>
        [Description("附加沉默")]
        SilenceApplied,
        /// <summary>
        /// 沉默效果从单位身上移除时触发。
        /// </summary>
        [Description("移除沉默")]
        SilenceRemoved,

        // --- 游戏流程事件 ---
        /// <summary>
        /// 玩家使用EP执行了进化动作后触发，用于通知本回合进化权已消耗。
        /// </summary>
        [Description("玩家完成进化")]
        PlayerEvolved,
        /// <summary>
        /// 整场游戏开始时触发。
        /// </summary>
        [Description("对局开始")]
        GameStart,
        /// <summary>
        /// 整场游戏结束时触发。
        /// </summary>
        [Description("对局结束")]
        GameEnd,
        /// <summary>
        /// 玩家完成换牌时触发。
        /// </summary>
        [Description("换牌完成")]
        MulliganCompleted,
        /// <summary>
        /// 玩家手牌更新时触发。
        /// </summary>
        [Description("手牌更新")]
        HandUpdated,
        /// <summary>
        /// 事务结束时的卡牌实例批量更新事件。
        /// </summary>
        [Description("卡牌实例更新")]
        CardInstanceUpdated,
        /// <summary>
        /// 玩家提交换牌请求时触发。
        /// </summary>
        [Description("提交换牌")]
        MulliganSubmitted,
        /// <summary>
        /// 玩家请求攻击时触发。
        /// </summary>
        [Description("请求攻击")]
        AttackRequested,
        /// <summary>
        /// 玩家请求结束回合时触发。
        /// </summary>
        [Description("请求结束回合")]
        EndTurnRequested,
        /// <summary>
        /// 玩家请求进化时触发。
        /// </summary>
        [Description("请求进化")]
        EvolveRequested,
        /// <summary>
        /// 玩家请求超进化时触发。
        /// </summary>
        [Description("请求超进化")]
        SuperEvolveRequested,
        /// <summary>
        /// 玩家请求使用PP+1时触发。
        /// </summary>
        [Description("请求PP+1")]
        PpBoostRequested,
        /// <summary>
        /// 玩家请求出牌时触发。
        /// </summary>
        [Description("请求出牌")]
        PlayCardRequested,


        // --- 能力系统事件 ---
        /// <summary>
        /// 当一个能力成功触发其效果时广播（用于能力间交互）。
        /// </summary>
        [Description("效果触发")]
        EffectInvoked,
        /// <summary>
        /// 抉择结果事件（用于记录抉择效果的最终选择）。
        /// </summary>
        [Description("抉择结果")]
        Choose,
        /// <summary>
        /// 随机结果事件（用于记录随机效果的最终选择）。
        /// </summary>
        [Description("随机结果")]
        Random,
        /// <summary>
        /// 依序结果事件（用于记录依序效果的最终选择）。
        /// </summary>
        [Description("依序结果")]
        Sequence,
        /// <summary>
        /// 当一个能力被触发时广播。
        /// </summary>
        [Description("能力触发")]
        AbilityTriggered,
        /// <summary>
        /// 倒计时结束时触发。
        /// </summary>
        [Description("倒计时结束")]
        CountdownFinished,
        /// <summary>
        /// 倒计时减少时触发。
        /// </summary>
        [Description("倒计时减少")]
        CountdownTick,
        /// <summary>
        /// 治疗效果时触发。
        /// </summary>
        [Description("治疗")]
        Heal,
        /// <summary>
        /// 单位交战时触发。
        /// </summary>
        [Description("交战")]
        Clash,

        [Description("返回")]
        Return,

        /// <summary>
        /// 统一离场前置路由事件。
        /// </summary>
        [Description("离场")]
        LeaveField,

        /// <summary>
        /// 将卡牌从战场洗回牌库时触发。
        /// </summary>
        [Description("洗回牌库")]
        Tuck,

        /// <summary>
        /// 将手牌洗回牌库时触发。
        /// </summary>
        [Description("洗回牌堆")]
        Shuffle,

        [Description("变形")]
        Transform,

        // --- 其他/通用事件 ---
        /// <summary>
        /// 通用计数器改变事件。
        /// </summary>
        [Description("计数器变更")]
        CounterChanged,
        /// <summary>
        /// 区域（手牌、战场、墓地等）中的卡牌发生变化。
        /// </summary>
        [Description("区域变更")]
        ZoneChanged,
        /// <summary>
        /// 卡牌在不同区域之间移动时触发。
        /// </summary>
        [Description("卡牌移动")]
        CardMoved,
        /// <summary>
        /// 卡牌因手牌已满而被爆牌时触发。
        /// </summary>
        [Description("爆牌")]
        CardBurned,
        /// <summary>
        /// 卡牌因战场已满而无法进入战场时触发。
        /// </summary>
        [Description("战场已满")]
        CardPacked,
        /// <summary>
        /// 玩家试图抽牌但牌库为空时触发。
        /// </summary>
        [Description("空牌库抽牌")]
        DeckEmptyOnDraw,
        /// <summary>
        /// 效果处理器完成其主要操作后触发的聚合事件。
        /// </summary>
        [Description("效果完成")]
        EffectCompleted,
        /// <summary>
        /// An internal event used to resume a transaction with a player's choice.
        /// </summary>
        [Description("玩家选择提交")]
        PlayerChoiceSubmitted,
        /// <summary>
        /// 未知或自定义事件类型。
        /// </summary>
        [Description("未知事件")]
        Unknown,

        /// <summary>
        /// 创建卡牌实例并放置到指定区域（可用于效果生成、调试注入等）。
        /// </summary>
        [Description("创建卡牌")]
        CreateCard,

        /// <summary>
        /// 复制卡牌实例并放置到指定区域。
        /// </summary>
        [Description("复制卡牌")]
        CopyCard,

        /// <summary>
        /// 一张卡牌成功发动了 Act（启动）能力（用于触发 Activated 能力）。
        /// </summary>
        [Description("启动")]
        Act,

        /// <summary>
        /// 玩家请求发动场上卡牌的 Act（启动）能力。
        /// </summary>
        [Description("请求启动")]
        ActRequested,

        /// <summary>
        /// 玩家以 Enhance（爆能）模式打出卡牌时触发。
        /// </summary>
        [Description("爆能")]
        Enhance,

        /// <summary>
        /// 一张卡牌成功发动了 Fusion（融合）能力（用于触发 Fusion 能力）。
        /// </summary>
        [Description("融合")]
        Fusion,

        /// <summary>
        /// 玩家请求发动手牌中的 Fusion（融合）能力。
        /// </summary>
        [Description("请求融合")]
        FusionRequested,

        /// <summary>
        /// 场上卡牌的 Act 可用性/已使用状态发生变化（用于驱动客户端 UI）。
        /// </summary>
        [Description("启动状态更新")]
        ActStateUpdated,
        [Description("加入牌库")]
        AddDeck,
        /// <summary>
        /// 纹章被添加到玩家纹章区时触发。
        /// </summary>
        [Description("添加纹章")]
        AddEmblem,

        /// <summary>
        /// 玩家使用法术后触发的魔力增幅事件。
        /// </summary>
        [Description("魔力增幅")]
        SpellBoost,

        /// <summary>
        /// 战斗伤害结算时触发。
        /// </summary>
        [Description("战斗结算")]
        CombatResolution,

        /// <summary>
        /// 战斗伤害处理完成后统一结算破坏时触发。
        /// </summary>
        [Description("战斗破坏结算")]
        CombatDestroyResolution,

        /// <summary>
        /// 卡牌获得能力时触发。
        /// </summary>
        [Description("获得能力")]
        AbilityGranted,

        /// <summary>
        /// 融合效果完成结算并实际消耗素材时触发。
        /// </summary>
        [Description("融合结算")]
        CardFused,

    }


    public enum EffectAbility
    {
        None = 0, SetResult = 1, KeepCard = 2, TurnStart = 3, TurnEnd = 4,
        Play = 5, Cover = 6, Attack = 7, Evolve = 8, Fusion = 9, Act = 10,
        Accelerate = 11, Crystalize = 12, SetAttackTimes = 13,
        ResetAttackTimes = 15,
        ChangeFlag = 90,
        Random = 91,
        Sequence = 148,

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
