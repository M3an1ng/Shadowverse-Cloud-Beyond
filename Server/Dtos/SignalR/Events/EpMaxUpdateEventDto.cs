using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating an update to a player's maximum Evolution Points (EP).
    /// </summary>
    public class EpMaxUpdateEventDto : GameEventDto
    {
        public EpMaxUpdateEventDto(Guid playerId, int currentEp, int maxEp)
            : base(playerId, "EpMaxUpdate")
        {
            CurrentEp = currentEp;
            MaxEp = maxEp;
        }

        public int CurrentEp { get; set; }
        public int MaxEp { get; set; }
    }
}

