using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating an update to a player's Play Points (PP).
    /// Typically sent at the start of the turn or after playing a card.
    /// </summary>
    public class PpUpdateEventDto : GameEventDto
    {
        public override string Type => "PpUpdate";

        /// <summary>
        /// The identifier of the player whose PP was updated.
        /// </summary>
        public string PlayerId { get; set; } = string.Empty;

        /// <summary>
        /// The player's current PP amount after the update.
        /// </summary>
        public int CurrentPp { get; set; }

        /// <summary>
        /// The player's maximum PP amount after the update.
        /// </summary>
        public int MaxPp { get; set; }
    }
}