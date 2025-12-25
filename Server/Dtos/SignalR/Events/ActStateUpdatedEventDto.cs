using System;
using ShadowverseBeyond.Server.Dtos.SignalR;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    public class ActStateUpdatedEventDto : GameEventDto
    {
        public Guid TargetCardInstanceId { get; set; }
        public int ActLastUsedTurnNumber { get; set; }

        public ActStateUpdatedEventDto(Guid sourcePlayerId, Guid targetCardInstanceId, int actLastUsedTurnNumber)
            : base(sourcePlayerId, "ActStateUpdated")
        {
            TargetCardInstanceId = targetCardInstanceId;
            ActLastUsedTurnNumber = actLastUsedTurnNumber;
        }
    }
}

