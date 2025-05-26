using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating an update to a player's Evolution Points (EP).
    /// Typically sent at the start of specific turns or after evolving a unit.
    /// </summary>
    public class EpUpdateEventDto : GameEventDto
    {
        public override string Type => "EpUpdate";

        /// <summary>
        /// The identifier of the player whose EP was updated.
        /// </summary>
        public string PlayerId { get; set; } = string.Empty;

        /// <summary>
        /// The player's current EP amount after the update.
        /// </summary>
        public int CurrentEp { get; set; }

        /// <summary>
        /// The player's maximum EP amount after the update (usually doesn't change mid-game, but included for completeness).
        /// </summary>
        public int MaxEp { get; set; }
    }
}