using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that a unit has been destroyed and removed from the field.
    /// </summary>
    public class UnitDestroyedEventDto : GameEventDto
    {
        public override string Type => "UnitDestroyed";

        /// <summary>
        /// The unique identifier (UID) of the unit that was destroyed.
        /// </summary>
        public string UnitUid { get; set; } = string.Empty;

        /// <summary>
        /// The identifier of the player who owned the destroyed unit.
        /// </summary>
        public string PlayerId { get; set; } = string.Empty;

        /// <summary>
        /// Optional: Indicates the reason for destruction (e.g., "Combat", "Effect", "Spell").
        /// </summary>
        public string Reason { get; set; } = "Unknown";

        // Could potentially include the CardId of the destroyed unit if needed by client
        // for graveyard visuals or other logic.
    }
}