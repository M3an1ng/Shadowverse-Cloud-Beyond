using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Actions
{
    /// <summary>
    /// DTO for the super evolve action.
    /// </summary>
    public class SuperEvolveActionDto
    {
        /// <summary>
        /// The unique identifier (UID) of the unit on the player's field being super evolved.
        /// </summary>
        public string UnitUid { get; set; } = string.Empty;
    }
}
