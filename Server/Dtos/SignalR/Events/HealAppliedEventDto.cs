using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that healing has been applied to a target (unit or player leader).
    /// </summary>
    public class HealAppliedEventDto : GameEventDto
    {
        /// <summary>
        /// The unique identifier (UID) of the entity that received the healing (unit or leader identifier).
        /// </summary>
        public Guid TargetUid { get; set; } = Guid.Empty;

        /// <summary>
        /// The amount of healing applied.
        /// </summary>
        public int HealAmount { get; set; }

        /// <summary>
        /// The identifier of the player whose entity received healing.
        /// Needed for client-side logic (e.g., showing heal animation on correct side).
        /// </summary>
        public Guid TargetPlayerId { get; set; } = Guid.Empty;

        /// <summary>
        /// Optional: The unique identifier (UID) of the source of the healing.
        /// </summary>
        public Guid? SourceUid { get; set; }

        /// <summary>
        /// Optional: Describes the type of healing (e.g., "Effect", "Spell").
        /// </summary>
        public string HealType { get; set; } = "Generic";

        public HealAppliedEventDto(Guid sourcePlayerId, Guid targetUid, int healAmount, Guid targetPlayerId, Guid? sourceUid, string healType)
            : base(sourcePlayerId, "HealApplied")
        {
            TargetUid = targetUid;
            HealAmount = healAmount;
            TargetPlayerId = targetPlayerId;
            SourceUid = sourceUid;
            HealType = healType;
        }
    }
}
