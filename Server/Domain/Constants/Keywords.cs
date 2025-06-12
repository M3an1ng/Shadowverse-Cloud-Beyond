// File: Server/Domain/Constants/Keywords.cs
//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 动态效果与Buff系统: 文档/05-游戏系统/10-动态效果与Buff系统.md
//

namespace ShadowverseBeyond.Server.Domain.Constants
{
    public enum CardKeyword
    {
        None = 0, Storm = 1, Ward = 2, Bane = 3, Rush = 4, Ambush = 5, Drain = 6,
        Countdown = 7, Aura = 8, Pressure = 9, Freeze = 10, Shield = 11, Charge = 12,
        DestroyResist = 20,
        VanishResist = 21,
        FreeEvolve = 22,
        BanEvolve = 23,
    }

    public enum EffectKeyword
    {
        None = 0,
        Fanfare = 101,
        Lastword = 102,
        AttackEffect = 103,
        FightEffect = 104,
        EvolveEffect = 105,
        Combo = 106,
        Rally = 107,
        SpellBoost = 108,
        Awake = 109,
        Necromance = 110,
        Venge = 111,
        Reson = 112,
        Eager = 113,
        EarthRitual = 114,
        Enhance = 115,
        Bury = 116,
        Reanimate = 117,
        Accelerate = 118,
        Crystalize = 119,
        Travel = 120,
        Intromezzo = 121,
        Wrath = 122,
        Choose = 123
    }

    /// <summary>
    /// 定义游戏中所有关键字的常量
    /// 确保关键字名称的一致性和类型安全
    /// </summary>
    public static class Keywords
    {
        // 字符串常量已移除，请使用 CardKeyword 和 EffectKeyword 枚举

        /// <summary>
        /// 检查卡牌关键字是否为防御相关关键字
        /// </summary>
        /// <param name="keyword">卡牌关键字</param>
        /// <returns>如果是防御相关关键字则返回true</returns>
        public static bool IsDefenseKeyword(CardKeyword keyword)
        {
            return keyword switch
            {
                CardKeyword.Ward or CardKeyword.Ambush or CardKeyword.Shield => true,
                _ => false
            };
        }

        /// <summary>
        /// 检查效果关键字是否为能力触发关键字
        /// </summary>
        /// <param name="keyword">效果关键字</param>
        /// <returns>如果是能力触发关键字则返回true</returns>
        public static bool IsAbilityTriggerKeyword(EffectKeyword keyword)
        {
            // 注意: Lastword 在枚举中拼写不同
            return keyword switch
            {
                EffectKeyword.Fanfare or EffectKeyword.Lastword or EffectKeyword.AttackEffect or EffectKeyword.FightEffect or EffectKeyword.EvolveEffect => true,
                _ => false
            };
        }

        /// <summary>
        /// 检查卡牌关键字是否允许立即攻击（刚召唤就能攻击）
        /// </summary>
        /// <param name="keyword">卡牌关键字</param>
        /// <returns>如果允许立即攻击则返回true</returns>
        public static bool AllowsImmediateAttack(CardKeyword keyword)
        {
            return keyword switch
            {
                CardKeyword.Rush or CardKeyword.Storm => true,
                _ => false
            };
        }

        /// <summary>
        /// 检查卡牌关键字是否允许攻击主战者
        /// </summary>
        /// <param name="keyword">卡牌关键字</param>
        /// <returns>如果允许攻击主战者则返回true</returns>
        public static bool AllowsLeaderAttack(CardKeyword keyword)
        {
            return keyword switch
            {
                CardKeyword.Storm => true, // 疾走可以打脸
                CardKeyword.Rush => false, // 突进不能打脸
                _ => false // 其他关键字默认不能让单位在召唤回合打脸
            };
        }
    }
}
