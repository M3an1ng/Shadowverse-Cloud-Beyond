using System.ComponentModel;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Describes the intended legacy leave action to route to.
    /// </summary>
    public enum LeaveFieldKind
    {
        [Description("破坏")]
        Destroy = 0,

        [Description("消失")]
        Vanish = 1,

        [Description("返回手牌")]
        Return = 2,

        [Description("置入牌库")]
        Tuck = 3,

        [Description("无操作")]
        NoOp = 4
    }
}
