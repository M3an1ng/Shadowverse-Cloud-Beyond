using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    public class PpBoostStateUpdatedEventDto : GameEventDto
    {
        public int PpBoostUsesAvailable { get; set; }
        public bool IsArmed { get; set; }

        public PpBoostStateUpdatedEventDto(Guid playerId, int ppBoostUsesAvailable, bool isArmed)
            : base(playerId, "PpBoostStateUpdated")
        {
            PpBoostUsesAvailable = ppBoostUsesAvailable;
            IsArmed = isArmed;
        }
    }
}
