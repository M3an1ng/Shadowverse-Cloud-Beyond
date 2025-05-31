using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating an update to a player's hand.
    /// Can represent drawing, discarding, or adding cards via effects.
    /// </summary>
    public class HandUpdateEventDto : GameEventDto
    {
        /// <summary>
        /// The total number of cards currently in the player's hand after the update.
        /// Essential for the opponent client to display the correct hand size.
        /// </summary>
        public int HandCount { get; set; }

        /// <summary>
        /// Optional: A list of cards that were *added* to the hand in this update.
        /// Only sent to the owner of the hand. Contains enough info for the client to render the new card(s).
        /// We might need a simplified CardInfo DTO for this if CardDto is too heavy.
        /// For now, let's assume CardDto is usable or create a simple placeholder.
        /// </summary>
        public List<CardDto>? DrawnCards { get; set; } // Only sent to the owner

        /// <summary>
        /// Optional: The UID of a card that was *removed* from the hand (e.g., discarded due to hand size limit).
        /// </summary>
        public string? DiscardedCardUid { get; set; }

        /// <summary>
        /// Optional: Indicates if the discard was due to hand size limit (overdraw/burn).
        /// </summary>
        public bool IsBurned { get; set; }

        // Added constructor according to standard fix pattern
        public HandUpdateEventDto(string playerId, int handCount, List<CardDto>? drawnCards = null, string? discardedCardUid = null, bool isBurned = false)
            : base(playerId, "HandUpdate") // Call base with required params
        {
            HandCount = handCount;
            DrawnCards = drawnCards;
            DiscardedCardUid = discardedCardUid;
            IsBurned = isBurned;
        }

        // Removed override Type and local PlayerId properties
        // Consider adding CardId if DiscardedCardUid is present for graveyard visuals.
    }
}