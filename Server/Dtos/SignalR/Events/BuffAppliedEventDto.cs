using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that a buff has been applied to a target.
    /// </summary>
    public class BuffAppliedEventDto : GameEventDto
    {
        /// <summary>
        /// The unique identifier of the buff instance that was applied.
        /// </summary>
        public Guid BuffId { get; set; } = Guid.Empty;

        /// <summary>
        /// The unique identifier of the target card instance that received the buff.
        /// </summary>
        public Guid TargetCardInstanceId { get; set; } = Guid.Empty;

        /// <summary>
        /// The unique identifier of the source card instance that applied the buff (if any).
        /// </summary>
        public Guid? SourceCardInstanceId { get; set; }

        /// <summary>
        /// The definition ID of the buff (if it's a predefined buff template).
        /// </summary>
        public string? BuffDefinitionId { get; set; }

        /// <summary>
        /// The type of buff that was applied (e.g., "ModifyStats", "AddKeyword").
        /// </summary>
        public string BuffType { get; set; } = string.Empty;

        /// <summary>
        /// The duration type of the buff (e.g., "Permanent", "UntilEndOfTurn").
        /// </summary>
        public string DurationType { get; set; } = string.Empty;

        /// <summary>
        /// The duration parameter (e.g., number of turns for LimitedTurns).
        /// </summary>
        public object? DurationParameter { get; set; }

        /// <summary>
        /// Whether this buff is non-stackable (replaces existing buffs from the same source).
        /// </summary>
        public bool IsNonStackable { get; set; }

        // Constructor
        public BuffAppliedEventDto(
            Guid sourcePlayerId,
            Guid buffId,
            Guid targetCardInstanceId,
            Guid? sourceCardInstanceId = null,
            string? buffDefinitionId = null,
            string buffType = "Unknown",
            string durationType = "Permanent",
            object? durationParameter = null,
            bool isNonStackable = false)
            : base(sourcePlayerId, "BuffApplied")
        {
            BuffId = buffId;
            TargetCardInstanceId = targetCardInstanceId;
            SourceCardInstanceId = sourceCardInstanceId;
            BuffDefinitionId = buffDefinitionId;
            BuffType = buffType;
            DurationType = durationType;
            DurationParameter = durationParameter;
            IsNonStackable = isNonStackable;
        }
    }
}
