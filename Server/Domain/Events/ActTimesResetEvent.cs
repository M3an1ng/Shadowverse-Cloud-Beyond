using System;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Events
{
    [GameEventMetadata("启动次数重置", "当卡牌本回合已启动次数被重置时触发。", EventType.ActTimesReset, visibility: EventMetadataVisibility.Hidden)]
    public class ActTimesResetEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("目标卡牌", "启动次数被重置的卡牌。", DslDisplayTypeName = "事件源")]
        public CardInstance? TargetCard => Source?.Card;

        public ActTimesResetEvent(CardInstance targetCard, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.ActTimesReset, eventId, timestamp)
        {
            Source = TargetInfo.ForCard(targetCard);
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var card = TargetCard;
            if (card == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "ActTimesResetEvent.Commit: Target card is null." };
            }

            var oldActsThisTurn = card.ActsThisTurn;
            card.ResetActionUsage(CardActionUsageType.Act);

            var result = new DomainOperationResult { Success = true };
            if (oldActsThisTurn != card.ActsThisTurn)
            {
                result.GeneratedEvents.Add(new ActStateUpdatedEvent(card, card.ActsThisTurn, card.MaxActsPerTurn));
            }

            return result;
        }

        public string GetAnnouncement(GameState gameState)
        {
            var target = TargetCard;
            if (target == null)
            {
                return string.Empty;
            }

            return $"{AnnouncementTokens.Card(target)}的启动次数被重置。";
        }
    }
}
