using ShadowverseBeyond.Server.Dtos.SignalR;
using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    public class AttackStateUpdatedEventDto : GameEventDto
    {
        public Guid TargetCardInstanceId { get; set; }
        public bool CanAttack { get; set; }

        public AttackStateUpdatedEventDto(Guid sourcePlayerId, Guid targetCardInstanceId, bool canAttack)
            : base(sourcePlayerId, "AttackStateUpdated")
        {
            TargetCardInstanceId = targetCardInstanceId;
            CanAttack = canAttack;
        }
    }
}