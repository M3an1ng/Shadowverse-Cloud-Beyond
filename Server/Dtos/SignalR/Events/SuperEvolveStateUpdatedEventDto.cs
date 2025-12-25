using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    public class SuperEvolveStateUpdatedEventDto : GameEventDto
    {
        public bool CanSuperEvolve { get; set; }

        public SuperEvolveStateUpdatedEventDto(Guid playerId, bool canSuperEvolve)
            : base(playerId, "SuperEvolveStateUpdated")
        {
            CanSuperEvolve = canSuperEvolve;
        }
    }
}
