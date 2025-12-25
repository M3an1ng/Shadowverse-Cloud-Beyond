using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // DomainOperationResult
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.Enums; // CardZone
using ShadowverseBeyond.Server.Domain.Events.Metadata;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// 原子事实事件：当一张卡牌在不同区域之间移动时触发。
    /// </summary>
    [GameEventMetadata("卡牌移动", "当一张卡牌在不同区域之间移动时触发的原子事件。", EventType.CardMoved)]
    public class CardMovedEvent : GameEventBase, ICommittableEvent
    {
        [GameEventParameter("起始区域", "卡牌移动前所在的区域。")]
        public CardZone FromZone { get; }

        [GameEventParameter("目标区域", "卡牌移动后所在的区域。")]
        public CardZone ToZone { get; }

        [GameEventParameter("移动的卡牌", "被移动的卡牌。")]
        public CardInstance? MovedCard => Source?.Card;

        public CardMovedEvent(CardInstance card, CardZone fromZone, CardZone toZone)
            : base(EventType.CardMoved)
        {
            FromZone = fromZone;
            ToZone = toZone;
            Source = TargetInfo.ForCard(card);
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult();
            var card = MovedCard;
            if (card == null)
            {
                result.Success = false;
                result.ErrorMessage = "CardMovedEvent.Commit: Source card is null.";
                return result;
            }
            var owner = context.CurrentGameState.GetPlayerState(card.OwnerPlayerId);
            if (owner == null)
            {
                result.Success = false;
                result.ErrorMessage = $"CardMovedEvent.Commit: Owner player {card.OwnerPlayerId} not found.";
                return result;
            }

            // Execute internal state mutation on the aggregate
            bool ok = owner.ApplyCardZoneChange(card, FromZone, ToZone, null);
            if (!ok)
            {
                result.Success = false;
                result.ErrorMessage = $"Failed to apply card move from {FromZone} to {ToZone} for {card.InstanceId}.";
                return result;
            }

            // Derive follow-up notifications (can later be centralized)
            if (ToZone == CardZone.Hand || FromZone == CardZone.Hand)
                result.GeneratedEvents.Add(new HandUpdatedEvent(owner, owner.Hand.ToList()));
            if (ToZone == CardZone.Deck || FromZone == CardZone.Deck)
                result.GeneratedEvents.Add(new DeckCountChangedEvent(owner, owner.Deck.Count));
            if (ToZone == CardZone.Graveyard || FromZone == CardZone.Graveyard)
                result.GeneratedEvents.Add(new GraveyardUpdatedEvent(owner, owner.Graveyard.Count));

            result.Success = true;
            return result;
        }
    }
}

