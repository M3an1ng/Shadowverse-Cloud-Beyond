// File: ShadowverseBeyond.Server.Dtos.SignalR.PpUpdateEventDto.cs
using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR
{
    /// <summary>
    /// Event DTO representing an update to a player's Play Points (PP).
    /// </summary>
    public class PpUpdateEventDto : GameEventDto
    {
        /// <summary>
        /// ID of the player whose PP was updated.
        /// </summary>
        public string PlayerId { get; set; }

        /// <summary>
        /// The player's current PP after the update.
        /// </summary>
        public int CurrentPp { get; set; }

        /// <summary>
        /// The player's maximum PP after the update.
        /// </summary>
        public int MaxPp { get; set; }

        public PpUpdateEventDto(string playerId, int currentPp, int maxPp)
            : base(playerId, "PpUpdate") // Pass playerId as sourcePlayerId, set EventType
        {
            PlayerId = playerId;
            CurrentPp = currentPp;
            MaxPp = maxPp;
        }
    }
}