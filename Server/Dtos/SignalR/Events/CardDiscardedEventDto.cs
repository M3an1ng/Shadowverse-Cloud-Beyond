//
// 相关文档:
// - 游戏事件系统: 文档/05-游戏系统/04-事件驱动架构.md
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
//
using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that a card has been discarded from a player's hand.
    /// This can happen due to hand size limits, card effects, or other game mechanics.
    /// </summary>
    public class CardDiscardedEventDto : GameEventDto
    {
        /// <summary>
        /// The unique instance ID (UID) of the card that was discarded.
        /// </summary>
        public string DiscardedCardUid { get; set; } = string.Empty;

        /// <summary>
        /// The definition ID (CardId) of the card that was discarded.
        /// Useful for looking up base card properties on the client.
        /// </summary>
        public string DiscardedCardId { get; set; } = string.Empty;

        /// <summary>
        /// The name of the discarded card for display purposes.
        /// </summary>
        public string CardName { get; set; } = string.Empty;

        /// <summary>
        /// The reason why the card was discarded.
        /// Examples: "HandSizeLimit", "CardEffect", "PlayerChoice", "Random"
        /// </summary>
        public string DiscardReason { get; set; } = "Unknown";

        /// <summary>
        /// The identifier of the player who owned the discarded card.
        /// </summary>
        public Guid OwnerPlayerId { get; set; } = Guid.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="CardDiscardedEventDto"/> class.
        /// </summary>
        /// <param name="sourcePlayerId">The ID of the player who initiated the discard action (may be different from owner).</param>
        /// <param name="discardedCardUid">The unique instance ID of the discarded card.</param>
        /// <param name="discardedCardId">The definition ID of the discarded card.</param>
        /// <param name="cardName">The name of the discarded card.</param>
        /// <param name="ownerPlayerId">The ID of the player who owned the card.</param>
        /// <param name="discardReason">The reason for the discard.</param>
        public CardDiscardedEventDto(
            Guid sourcePlayerId,
            string discardedCardUid,
            string discardedCardId,
            string cardName,
            Guid ownerPlayerId,
            string discardReason = "Unknown")
            : base(sourcePlayerId, "CardDiscarded")
        {
            DiscardedCardUid = discardedCardUid;
            DiscardedCardId = discardedCardId;
            CardName = cardName;
            OwnerPlayerId = ownerPlayerId;
            DiscardReason = discardReason;
        }
    }
}
