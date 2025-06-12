using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Actions
{
    /// <summary>
    /// DTO for the attack action.
    /// </summary>
    public class AttackActionDto
    {
        /// <summary>
        /// The unique identifier (UID) of the attacking unit on the player's field.
        /// </summary>
        public Guid AttackerUid { get; set; } = Guid.Empty;

        /// <summary>
        /// The unique identifier (UID) of the target being attacked.
        /// Can be an opponent's unit UID or a special identifier for the opponent's leader (e.g., "OpponentLeader").
        /// </summary>
        public Guid TargetUid { get; set; } = Guid.Empty;
    }
}