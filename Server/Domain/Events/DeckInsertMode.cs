using System.ComponentModel;

namespace ShadowverseBeyond.Server.Domain.Events
{
    public enum DeckInsertMode
    {
        [Description("随机")]
        Random = 0,

        [Description("牌顶")]
        Top = 1,

        [Description("牌底")]
        Bottom = 2
    }
}
