using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating an update to a player's maximum Play Points (PP).
    /// </summary>
    public class PpMaxUpdateEventDto : GameEventDto
    {
        public PpMaxUpdateEventDto(Guid playerId, int currentPp, int maxPp)
            : base(playerId, "PpMaxUpdate")
        {
            CurrentPp = currentPp;
            MaxPp = maxPp;
        }

        public int CurrentPp { get; set; }
        public int MaxPp { get; set; }
    }
}

