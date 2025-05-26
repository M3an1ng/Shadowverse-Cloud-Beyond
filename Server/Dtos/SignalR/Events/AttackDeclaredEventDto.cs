using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that an attack has been declared between two entities.
    /// This typically precedes DamageDealt events resulting from the combat.
    /// </summary>
    public class AttackDeclaredEventDto : GameEventDto
    {
        public override string Type => "AttackDeclared";

        /// <summary>
        /// The identifier of the player whose unit is attacking.
        /// </summary>
        public string PlayerId { get; set; } = string.Empty;

        /// <summary>
        /// The unique identifier (UID) of the attacking unit.
        /// </summary>
        public string AttackerUid { get; set; } = string.Empty;

        /// <summary>
        /// The unique identifier (UID) of the target being attacked (unit or leader).
        /// </summary>
        public string TargetUid { get; set; } = string.Empty;
    }
}