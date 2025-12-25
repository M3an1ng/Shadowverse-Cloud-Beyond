using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    public class EvolveStateUpdatedEventDto : GameEventDto
    {
        // The PlayerId is inherited from GameEventDto's SourcePlayerId.
        public bool CanEvolve { get; set; }

        public EvolveStateUpdatedEventDto(Guid playerId, bool canEvolve)
            : base(playerId, "EvolveStateUpdated")
        {
            CanEvolve = canEvolve;
        }
    }
}