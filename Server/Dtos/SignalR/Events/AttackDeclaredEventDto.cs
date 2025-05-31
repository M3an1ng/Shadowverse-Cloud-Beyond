using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that an attack has been declared between two entities.
    /// This typically precedes DamageDealt events resulting from the combat.
    /// </summary>
    public class AttackDeclaredEventDto : GameEventDto
    {
        // Constructor added to fix CS0506 and initialize properties
        public AttackDeclaredEventDto(string playerId, string attackerUid, string targetUid)
            : base(playerId, "AttackDeclared") // Call base constructor
        {
            AttackerUid = attackerUid;
            TargetUid = targetUid;
        }

        // Removed override string Type => "AttackDeclared"; (causes CS0506)
        // Removed redundant PlayerId property (inherited from base)

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