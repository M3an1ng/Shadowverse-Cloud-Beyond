using System.ComponentModel;

namespace ShadowverseBeyond.Server.Domain.Events
{
    public enum CardMoveReason
    {
        [Description("未知")]
        Unknown = 0,
        [Description("破坏")]
        Destroyed = 1,
        [Description("弃置")]
        Discarded = 2,
        [Description("爆牌")]
        Burned = 3,
        [Description("法术使用")]
        SpellPlayed = 4,
        [Description("变形")]
        Transform = 5,
        [Description("召唤")]
        Summoned = 6,
        [Description("消失")]
        Vanished = 7,
        [Description("回手")]
        ReturnedToHand = 8,
        [Description("洗回牌堆")]
        ShuffledIntoDeck = 9,
        [Description("抽牌")]
        Drawn = 10,
        [Description("添加到手牌")]
        AddedToHand = 11,
        [Description("添加到牌堆")]
        AddedToDeck = 12,
        [Description("满场")]
        Packed = 13,
        [Description("卡组替换")]
        DeckReplaced = 14,
    }
}
