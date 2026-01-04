using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    public class FusionEventDto : GameEventDto
    {
        public int FusedCount { get; set; }
        public List<Guid> FusedCardInstanceIds { get; set; } = new();

        public FusionEventDto(Guid sourcePlayerId, List<Guid> fusedCardInstanceIds)
            : base(sourcePlayerId, "Fusion")
        {
            FusedCardInstanceIds = fusedCardInstanceIds ?? new List<Guid>();
            FusedCount = FusedCardInstanceIds.Count;
        }
    }
}
