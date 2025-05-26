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
        public override string Type => "DeckUpdate";

        /// <summary>
        /// The identifier of the player whose deck was updated.
        /// </summary>
        public string PlayerId { get; set; } = string.Empty;

        /// <summary>
        /// The total number of cards currently in the player's deck after the update.
        /// </summary>
        public int DeckCount { get; set; }

        // Consider adding information about specific cards added *to* the deck
        // if game mechanics allow (e.g., shuffling specific cards back).
        // However, this is less common and adds complexity.
    }
}