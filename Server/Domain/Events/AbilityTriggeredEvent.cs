using System;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Collections.Generic;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// 能力触发事件，用于播报能力关键字
    /// 基于最新Event驱动架构设计
    /// </summary>
    [GameEventMetadata("能力触发", "当一个能力被触发时发生的事件。", EventType.AbilityTriggered)]
    public class AbilityTriggeredEvent : GameEventBase
    {
        public IAbility TriggeredAbility { get; }

        [GameEventParameter("触发的能力", "被触发的能力对象。")]
        public Ability? Ability => TriggeredAbility as Ability;

        [GameEventParameter("触发事件类型", "导致能力触发的原始事件类型。")]
        public EventType TriggerEventType { get; }

        [GameEventParameter("能力关键字", "能力的关键字列表。")]
        public IReadOnlyList<AbilityKeyword> AbilityKeywords { get; }

        [GameEventParameter("能力拥有者", "拥有此能力的卡牌。", DslDisplayTypeName = "事件源")]
        public CardInstance? AbilityOwner => Source?.Card;

        public AbilityTriggeredEvent(IAbility triggeredAbility, IGameEvent triggeringEvent, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.AbilityTriggered, eventId, timestamp)
        {
            TriggeredAbility = triggeredAbility;
            TriggerEventType = triggeringEvent.EventType;
            AbilityKeywords = triggeredAbility.Keywords?.ToList() ?? new List<AbilityKeyword>();

            // Set the source as the ability owner if available
            if (triggeredAbility.SourceCard != null)
            {
                Source = TargetInfo.ForCard(triggeredAbility.SourceCard);
            }
        }
    }
}
