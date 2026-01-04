using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events; // EventType, GameContext
using ShadowverseBeyond.Server.Dtos.Enums; // CardZone
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // DomainOperationResult
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event triggered when a card is vanished from the game.
    /// </summary>
    [GameEventMetadata("消失", "当一张卡牌从游戏中消失时触发的事件。", EventType.Vanish)]
    public class CardVanishedEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("原始区域", "卡牌消失前所在的区域。")]
        public CardZone OriginalZone { get; }

        [GameEventParameter("消失原因", "卡牌消失的原因。")]
        public string VanishReason { get; }

        [GameEventParameter("消失的卡牌", "被消失的卡牌。")]
        public CardInstance? VanishedCard => Source?.Card;

        public CardVanishedEvent(CardInstance card, CardZone originalZone, string vanishReason = "Effect", Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.Vanish, eventId, timestamp)
        {
            Source = TargetInfo.ForCard(card);
            OriginalZone = originalZone;
            VanishReason = vanishReason;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var card = VanishedCard;
            if (card == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "CardVanishedEvent.Commit: card is null." };
            }
            var owner = context.CurrentGameState.GetPlayerState(card.OwnerPlayerId);
            if (owner == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"CardVanishedEvent.Commit: owner {card.OwnerPlayerId} not found." };
            }

            // Vanish is represented as moving the card into the removed-from-game zone.
            // Important: Commit MUST NOT call a method that enqueues another CardVanishedEvent to avoid recursion.
            var result = new DomainOperationResult { Success = true };
            result.GeneratedEvents.Add(new CardMovedEvent(card, card.CurrentZone, CardZone.Removed, CardMoveReason.Vanished));
            return result;
        }

        public string GetAnnouncement(GameState gameState)
        {
            var targetId = VanishedCard?.InstanceId;
            return targetId.HasValue
                ? $"vanish target={gameState.InGameIds.Format(InGameIdKind.Card, targetId.Value)}"
                : "vanish";
        }
    }
}
