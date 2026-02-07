using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event emitted at the end of a transaction to broadcast updated card instance snapshots.
    /// </summary>
    [GameEventMetadata("卡牌实例更新", "事务结束时批量推送受影响卡牌的完整快照。", EventType.CardInstanceUpdated, visibility: EventMetadataVisibility.System)]
    public class CardInstanceUpdatedEvent : GameEventBase
    {
        [GameEventParameter("更新卡牌", "事务结束后需要同步的卡牌实例列表。")]
        public IReadOnlyList<CardInstance> UpdatedCards { get; }

        [GameEventParameter("更新卡牌ID", "更新卡牌实例ID列表。")]
        public IReadOnlyList<Guid> UpdatedCardInstanceIds { get; }

        public CardInstanceUpdatedEvent(IReadOnlyList<CardInstance> updatedCards, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.CardInstanceUpdated, eventId, timestamp)
        {
            UpdatedCards = updatedCards?.Where(c => c != null).ToList() ?? new List<CardInstance>();
            UpdatedCardInstanceIds = UpdatedCards.Select(c => c.InstanceId).ToList();
            foreach (var card in UpdatedCards)
            {
                Target.Add(TargetInfo.ForCard(card));
            }
        }
    }
}
