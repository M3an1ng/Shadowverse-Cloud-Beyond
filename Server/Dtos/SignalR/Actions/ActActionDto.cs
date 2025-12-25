using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Actions
{
    /// <summary>
    /// DTO for the Act (activated ability) request action.
    /// </summary>
    public class ActActionDto
    {
        /// <summary>
        /// The unique identifier (UID) of the card on the player's field to activate.
        /// </summary>
        public Guid CardUid { get; set; } = Guid.Empty;
    }
}

