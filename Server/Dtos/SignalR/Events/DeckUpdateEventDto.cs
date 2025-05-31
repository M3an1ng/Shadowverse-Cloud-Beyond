using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating an update to a player's deck size.
    /// Primarily used to inform clients (especially the opponent) about the number
    /// of cards remaining in the deck.
    /// </summary>
    public class DeckUpdateEventDto : GameEventDto
    {
        // Constructor added to fix CS0506 and initialize properties
        public DeckUpdateEventDto(string playerId, int deckCount)
            : base(playerId, "DeckUpdate") // Call base constructor
        {
            DeckCount = deckCount;
        }

        // Removed override string Type => "DeckUpdate"; (causes CS0506)
        // Removed redundant PlayerId property (inherited from base)

        /// <summary>
        /// The total number of cards currently in the player's deck after the update.
        /// </summary>
        public int DeckCount { get; set; }

        // Consider adding information about specific cards added *to* the deck
        // if game mechanics allow (e.g., shuffling specific cards back).
        // However, this is less common and adds complexity.
    }
}