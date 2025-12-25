using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating an update to a player's maximum Super Evolution Points (SEP).
    /// </summary>
    public class SepMaxUpdateEventDto : GameEventDto
    {
        /// <summary>
        /// The player's maximum SEP amount after the update.
        /// </summary>
        public int MaxSep { get; set; }

        /// <summary>
        /// The player's current SEP amount after the update.
        /// </summary>
        public int CurrentSep { get; set; }

        public SepMaxUpdateEventDto(Guid playerId, int currentSep, int maxSep)
            : base(playerId, "SepMaxUpdate")
        {
            CurrentSep = currentSep;
            MaxSep = maxSep;
        }
    }
}
