using System;
using ShadowverseBeyond.Server.Dtos.SignalR;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    public class FusionStateUpdatedEventDto : GameEventDto
    {
        public Guid TargetCardInstanceId { get; set; }
        public int FusionsThisTurn { get; set; }
        public int MaxFusionsPerTurn { get; set; }

        public FusionStateUpdatedEventDto(Guid sourcePlayerId, Guid targetCardInstanceId, int fusionsThisTurn, int maxFusionsPerTurn)
            : base(sourcePlayerId, "FusionStateUpdated")
        {
            TargetCardInstanceId = targetCardInstanceId;
            FusionsThisTurn = fusionsThisTurn;
            MaxFusionsPerTurn = maxFusionsPerTurn;
        }
    }
}
