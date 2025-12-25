using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    public class CardsFusedEventDto : GameEventDto
    {
        public int FusedCount { get; set; }
        public List<Guid> FusedCardInstanceIds { get; set; } = new();

        public CardsFusedEventDto(Guid sourcePlayerId, List<Guid> fusedCardInstanceIds)
            : base(sourcePlayerId, "CardsFused")
        {
            FusedCardInstanceIds = fusedCardInstanceIds ?? new List<Guid>();
            FusedCount = FusedCardInstanceIds.Count;
        }
    }
}

