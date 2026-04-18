using System;
using ShadowverseBeyond.Server.Dtos.SignalR;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    public class ActStateUpdatedEventDto : GameEventDto
    {
        public Guid TargetCardInstanceId { get; set; }
        public int ActsThisTurn { get; set; }
        public int MaxActsPerTurn { get; set; }

        public ActStateUpdatedEventDto(Guid sourcePlayerId, Guid targetCardInstanceId, int actsThisTurn, int maxActsPerTurn)
            : base(sourcePlayerId, "ActStateUpdated")
        {
            TargetCardInstanceId = targetCardInstanceId;
            ActsThisTurn = actsThisTurn;
            MaxActsPerTurn = maxActsPerTurn;
        }
    }
}
