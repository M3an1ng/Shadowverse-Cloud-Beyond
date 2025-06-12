using System; // Added for Guid
using System.Collections.Generic;
// Assuming PlayabilityResultDto exists in Server.Dtos namespace based on design doc link
using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// DTO sent from the server to a client after an action completes,
    /// providing the updated playability status for all cards currently in that player's hand.
    /// Inherits from GameEventDto for potential unified broadcasting, though specific handling might be needed.
    /// </summary>
    public class HandPlayabilityUpdateDto : GameEventDto // Inherit for consistency? Or send separately? Design decision.
    {
        /// <summary>
        /// The ID of the player whose hand playability is being updated.
        /// </summary>
        public Guid PlayerId { get; set; } = Guid.Empty;

        /// <summary>
        /// A dictionary where the key is the CardInstanceId (unique ID of the card in hand)
        /// and the value is the PlayabilityResultDto for that card.
        /// </summary>
        public Dictionary<Guid, PlayabilityResultDto> PlayabilityResults { get; set; } = new Dictionary<Guid, PlayabilityResultDto>();

        // Constructor updated to match base class GameEventDto(string sourcePlayerId, string type)
        public HandPlayabilityUpdateDto(Guid playerId)
            : base(playerId, nameof(HandPlayabilityUpdateDto)) // Pass playerId as sourcePlayerId and class name as type
        {
            PlayerId = playerId; // Set the specific property for this DTO
        }

        // Parameterless constructor might be needed for deserialization or certain frameworks,
        // but it won't satisfy the base class requirements directly.
        // If needed, consider how to handle base class initialization (e.g., default values, throw exception).
        // For now, we only provide the parameterized constructor.
        // public HandPlayabilityUpdateDto() : base("UNKNOWN", nameof(HandPlayabilityUpdateDto)) {} // Example if parameterless is strictly needed
    }
}