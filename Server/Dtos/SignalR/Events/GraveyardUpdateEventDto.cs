using System;
using ShadowverseBeyond.Server.Dtos; // Added for CardInstanceDto
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating an update to a player's graveyard (destroyed units, used spells).
    /// </summary>
    public class GraveyardUpdateEventDto : GameEventDto
    {
        // Type is now set in the base constructor
        // PlayerId is inherited from GameEventDto

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
        /// Might need a simplified CardInfo DTO if CardInstanceDto is too heavy.
        public CardInstanceDto? CardAdded { get; set; }

        /// <summary>
        /// Optional: The UID of a card that was *removed* from the graveyard (e.g., due to a resurrection effect).
        /// </summary>
        public string? CardRemovedUid { get; set; }

        // Consider adding the CardId of the removed card if needed for visual tracking.

        // Constructor to initialize properties and call base constructor
        public GraveyardUpdateEventDto(Guid playerId, int graveyardCount, CardInstanceDto? cardAdded = null, string? cardRemovedUid = null)
            : base(playerId, "GraveyardUpdate") // Set base PlayerId and Type
        {
            this.GraveyardCount = graveyardCount;
            this.CardAdded = cardAdded;
            this.CardRemovedUid = cardRemovedUid;
        }
    }
}