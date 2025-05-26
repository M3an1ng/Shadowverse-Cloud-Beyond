using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event dispatched when any board entity's (Player or Unit) health changes.
    /// </summary>
    public class HealthChangedEventDto : GameEventDto
    {
        public override string Type => "HealthChanged";

        /// <summary>
        /// The unique identifier of the entity whose health changed.
        /// </summary>
        public string TargetUid { get; set; } = string.Empty;

        /// <summary>
        /// The new health value of the entity after the change.
        /// </summary>
        public int NewHealth { get; set; }

        /// <summary>
        /// The amount by which the health changed (negative for damage, positive for healing).
        /// </summary>
        public int ChangeAmount { get; set; }

        /// <summary>
        /// Constructor setting the event type.
        /// </summary>
        public HealthChangedEventDto()
        {
        }
    }
}
