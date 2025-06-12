namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// 定义游戏中可能发生的各种事件类型。
    /// 用于触发器监听、事件处理和日志记录。
    /// </summary>
    public enum GameEventType
    {
        // --- 卡牌生命周期事件 ---
        /// <summary>
        /// 卡牌从手牌被打出时触发。
        /// </summary>
        CardPlayed,
        /// <summary>
        /// 卡牌进入战场时触发（包括出牌召唤、效果召唤等）。
        /// </summary>
        CardSummoned,
        /// <summary>
        /// 卡牌从战场或手牌等区域离开游戏（通常进入墓地）时触发。
        /// </summary>
        CardDestroyed,
        /// <summary>
        /// 卡牌进化时触发。
        /// </summary>
        CardEvolved,
        /// <summary>
        /// 玩家从牌库抽牌时触发。
        /// </summary>
        CardDrawn,
        /// <summary>
        /// 卡牌从手牌被弃置时触发。
        /// </summary>
        CardDiscarded,

        // --- 战斗相关事件 ---
        /// <summary>
        /// 单位宣告攻击时触发（在伤害计算前）。
        /// </summary>
        AttackDeclared,
        /// <summary>
        /// 造成伤害时触发（包括战斗伤害、效果伤害）。
        /// </summary>
        DamageDealt,
        /// <summary>
        /// 单位或玩家受到伤害时触发。
        /// </summary>
        DamageTaken,
        /// <summary>
        /// 单位或玩家的生命值发生改变时触发。
        /// </summary>
        HealthChanged,
        /// <summary>
        /// 单位的攻击力或最大生命值发生改变时触发。
        /// </summary>
        StatChanged,

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

        // --- 资源相关事件 ---
        /// <summary>
        /// 玩家PP（Play Point）发生改变时触发。
        /// </summary>
        PpChanged,
        /// <summary>
        /// 玩家EP（Evolve Point）发生改变时触发。
        /// </summary>
        EpChanged,
        /// <summary>
        /// 玩家手牌数量发生改变时触发。
        /// </summary>
        HandCountChanged,
        /// <summary>
        /// 玩家牌库数量发生改变时触发。
        /// </summary>
        DeckCountChanged,

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
        KeywordGranted,
        /// <summary>
        /// 关键字从单位身上移除时触发。
        /// </summary>
        KeywordRemoved,

        // --- 游戏流程事件 ---
        /// <summary>
        /// 整场游戏开始时触发。
        /// </summary>
        GameStart,
        /// <summary>
        /// 整场游戏结束时触发。
        /// </summary>
        GameEnd,

        // --- 能力系统事件 ---
        /// <summary>
        /// 当一个能力成功触发其效果时广播（用于能力间交互）。
        /// </summary>
        AbilityTriggered,

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
        /// 玩家做出选择时触发。
        /// </summary>
        PlayerChoiceMade,
        /// <summary>
        /// 未知或自定义事件类型。
        /// </summary>
        Unknown
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