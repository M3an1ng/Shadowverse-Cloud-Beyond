using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    public class HealthChangedEventDto : GameEventDto
    {
        public Guid CardInstanceId { get; set; }
        public int NewHealth { get; set; }
        public int DamageTaken { get; set; }

        public HealthChangedEventDto(Guid sourcePlayerId, Guid cardInstanceId, int newHealth, int damageTaken)
            : base(sourcePlayerId, "HealthChanged")
        {
            CardInstanceId = cardInstanceId;
            NewHealth = newHealth;
            DamageTaken = damageTaken;
        }
    }
}
