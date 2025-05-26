using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that damage has been dealt to a target (unit or player leader).
    /// </summary>
    public class DamageDealtEventDto : GameEventDto
    {
        public override string Type => "DamageDealt";

        /// <summary>
        /// The unique identifier (UID) of the entity that received the damage (unit or leader identifier).
        /// </summary>
        public string TargetUid { get; set; } = string.Empty;

        /// <summary>
        /// The amount of damage dealt.
        /// </summary>
        public int DamageAmount { get; set; }

        /// <summary>
        /// The identifier of the player whose entity received damage.
        /// Needed for client-side logic (e.g., showing damage animation on correct side).
        /// </summary>
        public string TargetPlayerId { get; set; } = string.Empty;


        /// <summary>
        /// Optional: The unique identifier (UID) of the source of the damage (e.g., attacking unit UID, spell card UID).
        /// Can be null if the source is ambiguous or not relevant (e.g., end-of-turn effect).
        /// </summary>
        public string? SourceUid { get; set; }

        /// <summary>
        /// Optional: The identifier of the player who owns the source of the damage.
        /// </summary>
        public string? SourcePlayerId { get; set; }

        /// <summary>
        /// Optional: Describes the type of damage (e.g., "Attack", "Spell", "Effect").
        /// Helps client determine appropriate visual/sound effect.
        /// </summary>
        public string DamageType { get; set; } = "Generic"; // Default to Generic
    }
}