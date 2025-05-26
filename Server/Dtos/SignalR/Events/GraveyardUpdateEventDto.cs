using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating an update to a player's graveyard (destroyed units, used spells).
    /// </summary>
    public class GraveyardUpdateEventDto : GameEventDto
    {
        public override string Type => "GraveyardUpdate";

        /// <summary>
        /// The identifier of the player whose graveyard was updated.
        /// </summary>
        public string PlayerId { get; set; } = string.Empty;

        /// <summary>
        /// The total number of cards currently in the player's graveyard after the update.
        /// Important for mechanics interacting with graveyard count (Necromancy in Shadowverse).
        /// </summary>
        public int GraveyardCount { get; set; }

        /// <summary>
        /// Optional: Information about the card that was just *added* to the graveyard.
        /// Useful for displaying the card entering the graveyard animation/log.
        /// Might need a simplified CardInfo DTO if CardDto is too heavy.
        /// </summary>
        public CardDto? CardAdded { get; set; }

        /// <summary>
        /// Optional: The UID of a card that was *removed* from the graveyard (e.g., due to a resurrection effect).
        /// </summary>
        public string? CardRemovedUid { get; set; }

        // Consider adding the CardId of the removed card if needed for visual tracking.
    }
}