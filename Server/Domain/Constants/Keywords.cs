// File: Server/Domain/Constants/Keywords.cs
//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 动态效果与Buff系统: 文档/05-游戏系统/10-动态效果与Buff系统.md
//

using System.ComponentModel;
using System;

namespace ShadowverseBeyond.Server.Domain.Constants
{
    public enum CardKeyword
    {
        [Description("无")]
        None = 0,
        [Description("疾馳")]
        Storm = 1,
        [Description("守护")]
        Ward = 2,
        [Description("必杀")]
        Bane = 3,
        [Description("突进")]
        Rush = 4,
        [Description("潜行")]
        Ambush = 5,
        [Description("吸血")]
        Drain = 6,
        [Description("倒数")]
        Countdown = 7,
        [Description("灵气")]
        Aura = 8,
        [Description("威懾")]
        Pressure = 9,
        [Description("無法攻擊")]
        Shackle = 10,
        [Description("屏障")]
        Shield = 11,
        [Description("无法被破坏")]
        DestroyResist = 20,
        [Description("无法被消失")]
        VanishResist = 21,
        [Description("免费进化")]
        FreeEvolve = 22,
        [Description("无法进化")]
        BanEvolve = 23,
    }

    public enum EffectKeyword
    {
        [Description("无")]
        None = 0,
        [Description("系统")]
        System = 1,
        [Description("入场曲")]
        Fanfare = 101,
        [Description("谢幕曲")]
        Lastword = 102,
        [Description("攻击时")]
        AttackEffect = 103,
        [Description("交战时")]
        FightEffect = 104,
        [Description("进化时")]
        EvolveEffect = 105,
        [Description("连击")]
        Combo = 106,
        [Description("协作")]
        Rally = 107,
        [Description("魔力增幅")]
        SpellBoost = 108,
        [Description("觉醒")]
        Awake = 109,
        [Description("死灵术")]
        Necromance = 110,
        [Description("复仇")]
        Venge = 111,
        [Description("共鸣")]
        Reson = 112,
        [Description("渴望")]
        Eager = 113,
        [Description("土之秘术")]
        EarthRitual = 114,
        [Description("爆能强化")]
        Enhance = 115,
        [Description("葬送")]
        Bury = 116,
        [Description("亡者召还")]
        Reanimate = 117,
        [Description("激奏")]
        Accelerate = 118,
        [Description("结晶")]
        Crystalize = 119,
        [Description("漫游")]
        Travel = 120,
        [Description("间奏")]
        Intromezzo = 121,
        [Description("狂乱")]
        Wrath = 122,
        [Description("抉择")]
        Choose = 123
    }

    /// <summary>
    /// 定义游戏中所有关键字的常量
    /// 确保关键字名称的一致性和类型安全
    /// </summary>
    public static class Keywords
    {
        // 字符串常量已移除，请使用 CardKeyword 和 EffectKeyword 枚举

        public static EffectKeyword ParseEffectKeywordOrDefault(string? keyword, EffectKeyword defaultValue = EffectKeyword.None)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return defaultValue;
            }

            return Enum.TryParse<EffectKeyword>(keyword, ignoreCase: true, out var parsed)
                ? parsed
                : defaultValue;
        }

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
