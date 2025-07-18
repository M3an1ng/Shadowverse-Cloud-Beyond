using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Dtos;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// 能力触发事件，用于播报能力标签
    /// 基于最新Event驱动架构设计
    /// </summary>
    public class AbilityTriggeredEvent : GameEventBase
    {
        public IAbility TriggeredAbility { get; }
        public IGameEvent TriggeringEvent { get; }
        public string AbilityTag { get; }

        public AbilityTriggeredEvent(IAbility triggeredAbility, IGameEvent triggeringEvent)
            : base(EventType.AbilityTriggered)
        {
            TriggeredAbility = triggeredAbility;
            TriggeringEvent = triggeringEvent;
            AbilityTag = triggeredAbility.AbilityTag; // Preserve the ability tag
        }
    }
}
