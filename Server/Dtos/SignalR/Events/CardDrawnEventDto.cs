// File: Server/Dtos/SignalR/Events/CardDrawnEventDto.cs
using ShadowverseBeyond.Server.Dtos.SignalR; // Using the namespace of the base class

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Represents an event signaling that a player has drawn a specific card.
    /// </summary>
    public class CardDrawnEventDto : GameEventDto
    {
        // The 'Type' property ("CardDrawn") is inherited and set via the base constructor.

        /// <summary>
        /// The unique instance ID (UID) of the card that was drawn.
        /// </summary>
        public string DrawnCardUid { get; set; }

        /// <summary>
        /// The definition ID (CardId) of the card that was drawn.
        /// Useful for looking up base card properties on the client.
        /// </summary>
        public string DrawnCardId { get; set; } // CardDefinition ID is string (e.g., "F001")

        // Constructor - Calls the base constructor correctly
        public CardDrawnEventDto(string sourcePlayerId, string drawnCardUid, string drawnCardId) // Changed type to string
            : base(sourcePlayerId, "CardDrawn") // Pass sourcePlayerId and the event type string "CardDrawn"
        {
            // Base constructor handles setting SourcePlayerId and Type properties.
            // Only need to set properties specific to this derived class.
            DrawnCardUid = drawnCardUid;
            DrawnCardId = drawnCardId; // Now assigns string to string
        }
    }
}