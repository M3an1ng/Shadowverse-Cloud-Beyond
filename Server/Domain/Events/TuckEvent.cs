using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// High-level tuck event: move a card from the field into its owner's deck.
    /// Commit delegates to AddCardToDeckEvent.
    /// </summary>
    [GameEventMetadata("洗回牌库", "当一张卡牌被洗回拥有者牌库时触发的事件。", EventType.Tuck)]
    public class TuckEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("洗回原因", "卡牌被洗回牌库的原因。")]
        public string Reason { get; }

        [GameEventParameter("洗回的卡牌", "被洗回牌库的卡牌。")]
        public CardInstance? TuckedCard => Target.FirstOrDefault()?.Card;

        public TuckEvent(CardInstance card, string reason = "Effect", Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.Tuck, eventId, timestamp)
        {
            Target.Add(TargetInfo.ForCard(card));
            Reason = reason;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var card = TuckedCard;
            if (card == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "TuckEvent.Commit: card is null." };
            }

            var owner = context.CurrentGameState.GetPlayerState(card.OwnerPlayerId);
            if (owner == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"TuckEvent.Commit: owner {card.OwnerPlayerId} not found." };
            }

            var result = new DomainOperationResult { Success = true };
            result.GeneratedEvents.Add(new AddCardToDeckEvent(owner, new List<CardInstance> { card }));
            return result;
        }

        public string GetAnnouncement(GameState gameState)
        {
            var targetId = TuckedCard?.InstanceId;
            return targetId.HasValue
                ? $"tuck target={gameState.InGameIds.Format(InGameIdKind.Card, targetId.Value)}"
                : "tuck";
        }
    }
}
