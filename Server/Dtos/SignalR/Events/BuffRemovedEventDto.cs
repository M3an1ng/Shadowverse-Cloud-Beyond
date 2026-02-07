using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that a buff has been removed from a target.
    /// </summary>
    public class BuffRemovedEventDto : GameEventDto
    {
        /// <summary>
        /// The unique identifier of the buff instance that was removed.
        /// </summary>
        public Guid BuffId { get; set; } = Guid.Empty;

        /// <summary>
        /// The unique identifier of the target card instance that had the buff removed.
        /// </summary>
        public Guid TargetCardInstanceId { get; set; } = Guid.Empty;

        /// <summary>
        /// The unique identifier of the source card instance that originally applied the buff (if any).
        /// </summary>
        public Guid? SourceCardInstanceId { get; set; }

        /// <summary>
        /// The definition ID of the buff (if it was a predefined buff template).
        /// </summary>
        public string? BuffDefinitionId { get; set; }

        /// <summary>
        /// The type of buff that was removed (e.g., "ModifyStats", "AddKeyword").
        /// </summary>
        public string BuffType { get; set; } = string.Empty;

        /// <summary>
        /// The reason for buff removal (e.g., "expired", "silenced", "replaced", "manual").
        /// </summary>
        public string RemovalReason { get; set; } = string.Empty;

        // Constructor
        public BuffRemovedEventDto(
            Guid sourcePlayerId,
            Guid buffId,
            Guid targetCardInstanceId,
            string removalReason,
            Guid? sourceCardInstanceId = null,
            string? buffDefinitionId = null,
            string buffType = "Unknown")
            : base(sourcePlayerId, "BuffRemoved")
        {
            BuffId = buffId;
            TargetCardInstanceId = targetCardInstanceId;
            RemovalReason = removalReason;
            SourceCardInstanceId = sourceCardInstanceId;
            BuffDefinitionId = buffDefinitionId;
            BuffType = buffType;
        }
    }
}
