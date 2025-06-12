using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that damage has been dealt to a target (unit or player leader).
    /// </summary>
    public class DamageDealtEventDto : GameEventDto
    {
        /// <summary>
        /// The unique identifier (UID) of the entity that received the damage (unit or leader identifier).
        /// </summary>
        public Guid TargetUid { get; set; } = Guid.Empty;

        /// <summary>
        /// The amount of damage dealt.
        /// </summary>
        public int DamageAmount { get; set; }

        /// <summary>
        /// The identifier of the player whose entity received damage.
        /// Needed for client-side logic (e.g., showing damage animation on correct side).
        /// </summary>
        public Guid TargetPlayerId { get; set; } = Guid.Empty;


        /// <summary>
        /// Optional: The unique identifier (UID) of the source of the damage (e.g., attacking unit UID, spell card UID).
        /// Can be null if the source is ambiguous or not relevant (e.g., end-of-turn effect).
        /// </summary>
        public Guid? SourceUid { get; set; }

        /// <summary>
        /// Optional: Describes the type of damage (e.g., "Attack", "Spell", "Effect").
        /// Helps client determine appropriate visual/sound effect.
        /// </summary>
        public string DamageType { get; set; } = "Generic"; // Default to Generic

        // Constructor
        public DamageDealtEventDto(Guid sourcePlayerId, Guid targetUid, int damageAmount, Guid targetPlayerId, Guid? sourceUid, string damageType)
            : base(sourcePlayerId, "DamageDealt") // Call base constructor
        {
            TargetUid = targetUid;
            DamageAmount = damageAmount;
            TargetPlayerId = targetPlayerId;
            SourceUid = sourceUid;
            DamageType = damageType;
            // Inherited SourcePlayerId is set by the base constructor call
        }
    }
}