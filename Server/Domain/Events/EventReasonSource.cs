using System.ComponentModel;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Classifies event/effect reasons to avoid string-based ambiguity.
    /// </summary>
    public enum EventReasonSource
    {
        [Description("系统")]
        System = 0,
        [Description("能力")]
        Ability = 1
    }
}
