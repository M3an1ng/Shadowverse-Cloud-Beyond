using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Actions
{
    /// <summary>
    /// DTO for the evolve action.
    /// </summary>
    public class EvolveActionDto
    {
        /// <summary>
        /// The unique identifier (UID) of the unit on the player's field being evolved.
        /// </summary>
        public string UnitUid { get; set; } = string.Empty;
    }
}