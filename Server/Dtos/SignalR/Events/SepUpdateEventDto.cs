using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating an update to a player's Super Evolution Points (SEP).
    /// </summary>
    public class SepUpdateEventDto : GameEventDto
    {
        /// <summary>
        /// The player's current SEP amount after the update.
        /// </summary>
        public int CurrentSep { get; set; }

        /// <summary>
        /// The player's maximum SEP amount after the update.
        /// </summary>
        public int MaxSep { get; set; }

        public SepUpdateEventDto(Guid playerId, int currentSep, int maxSep)
            : base(playerId, "SepUpdate")
        {
            CurrentSep = currentSep;
            MaxSep = maxSep;
        }
    }
}
