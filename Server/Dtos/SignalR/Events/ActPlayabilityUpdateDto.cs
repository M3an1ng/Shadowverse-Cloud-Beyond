using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// DTO sent from the server to a client after an action completes,
    /// providing the updated Act playability status for all cards currently on that player's field.
    /// </summary>
    public class ActPlayabilityUpdateDto : GameEventDto
    {
        /// <summary>
        /// The ID of the player whose Act playability is being updated.
        /// </summary>
        public Guid PlayerId { get; set; } = Guid.Empty;

        /// <summary>
        /// A dictionary where the key is the CardInstanceId (unique ID of the field card)
        /// and the value is the ActPlayabilityResultDto for that card.
        /// </summary>
        public Dictionary<Guid, ActPlayabilityResultDto> PlayabilityResults { get; set; } = new();

        public ActPlayabilityUpdateDto(Guid playerId)
            : base(playerId, nameof(ActPlayabilityUpdateDto))
        {
            PlayerId = playerId;
        }
    }
}
