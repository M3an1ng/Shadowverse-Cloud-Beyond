using System;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Events
{
    [GameEventMetadata("攻击次数重置", "当卡牌本回合已攻击次数被重置时触发。", EventType.AttackTimesReset, visibility: EventMetadataVisibility.Hidden)]
    public class AttackTimesResetEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("目标卡牌", "攻击次数被重置的卡牌。", DslDisplayTypeName = "事件源")]
        public CardInstance? TargetCard => Source?.Card;

        public AttackTimesResetEvent(CardInstance targetCard, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.AttackTimesReset, eventId, timestamp)
        {
            Source = TargetInfo.ForCard(targetCard);
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var card = TargetCard;
            if (card == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "AttackTimesResetEvent.Commit: Target card is null." };
            }

            var oldCanAttack = card.CanAttack;
            card.ResetActionUsage(CardActionUsageType.Attack);

            var result = new DomainOperationResult { Success = true };
            if (oldCanAttack != card.CanAttack)
            {
                result.GeneratedEvents.Add(new AttackStateUpdatedEvent(card, card.CanAttack));
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

            return $"{AnnouncementTokens.Card(target)}的攻击次数被重置。";
        }
    }
}
