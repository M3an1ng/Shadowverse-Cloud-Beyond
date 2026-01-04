using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    public class PpBoostStateUpdatedEventDto : GameEventDto
    {
        public int PpBoostUsesAvailable { get; set; }

        public PpBoostStateUpdatedEventDto(Guid playerId, int ppBoostUsesAvailable)
            : base(playerId, "PpBoostStateUpdated")
        {
            PpBoostUsesAvailable = ppBoostUsesAvailable;
        }
    }
}
