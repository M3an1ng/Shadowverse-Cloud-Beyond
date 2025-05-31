using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event dispatched when any board entity's (Player or Unit) health changes.
    /// </summary>
    public class HealthChangedEventDto : GameEventDto
    {
        // Type property is inherited from GameEventDto and set in the constructor.

        /// <summary>
        /// The unique identifier of the entity whose health changed.
        /// </summary>
        public string TargetUid { get; set; } // Removed default value for required property

        /// <summary>
        /// The new health value of the entity after the change.
        /// </summary>
        public int NewHealth { get; set; }

        /// <summary>
        /// The amount by which the health changed (negative for damage, positive for healing).
        /// </summary>
        public int ChangeAmount { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthChangedEventDto"/> class.
        /// </summary>
        /// <param name="playerId">The ID of the player this event is relevant to.</param>
        /// <param name="targetUid">The unique identifier of the entity whose health changed.</param>
        /// <param name="newHealth">The new health value of the entity.</param>
        /// <param name="changeAmount">The amount the health changed.</param>
        public HealthChangedEventDto(string playerId, string targetUid, int newHealth, int changeAmount)
            : base(playerId, "HealthChanged")
        {
            this.TargetUid = targetUid;
            this.NewHealth = newHealth;
            this.ChangeAmount = changeAmount;
        }
    }
}
