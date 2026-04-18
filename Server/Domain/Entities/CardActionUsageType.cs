using System.ComponentModel;

namespace ShadowverseBeyond.Server.Domain.Entities
{
    public enum CardActionUsageType
    {
        [Description("攻击")]
        Attack = 1,
        [Description("启动")]
        Act = 2,
        [Description("融合")]
        Fusion = 3
    }
}
