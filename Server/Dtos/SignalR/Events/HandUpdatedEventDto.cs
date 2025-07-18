using ShadowverseBeyond.Server.Dtos;         // For CardInstanceDto
using ShadowverseBeyond.Server.Dtos.SignalR; // For GameEventDto
using System.Diagnostics.CodeAnalysis; // For [SetsRequiredMembers]

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that a player's hand has been updated.
    /// This is typically sent after mulligan, drawing cards, or other hand modifications.
    /// </summary>
    public class HandUpdatedEventDto : GameEventDto
    {
        /// <summary>
        /// The ID of the player whose hand was updated.
        /// </summary>
        public Guid PlayerId { get; init; }

        /// <summary>
        /// The complete updated hand state containing all cards currently in the player's hand.
        /// </summary>
        public List<CardInstanceDto> UpdatedHand { get; init; } = new List<CardInstanceDto>();

        /// <summary>
        /// Constructor for HandUpdatedEventDto.
        /// </summary>
        /// <param name="playerId">The ID of the player whose hand was updated</param>
        /// <param name="updatedHand">The complete updated hand state</param>
        [SetsRequiredMembers]
        public HandUpdatedEventDto(Guid playerId, List<CardInstanceDto> updatedHand)
            : base(playerId, "HandUpdated") // Call base constructor
        {
            PlayerId = playerId;
            UpdatedHand = updatedHand ?? new List<CardInstanceDto>();
        }
    }
}
