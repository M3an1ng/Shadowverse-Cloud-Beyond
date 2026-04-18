using System;
using System.Collections.Generic;
using System.Linq;

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
        /// The unique identifiers of the buff instances that were removed in this batch.
        /// </summary>
        public IReadOnlyList<Guid> BuffIds { get; set; } = Array.Empty<Guid>();

        /// <summary>
        /// The unique identifier of the target card instance that had the buff removed.
        /// </summary>
        public Guid TargetCardInstanceId { get; set; } = Guid.Empty;

        /// <summary>
        /// The unique identifiers of the target card instances affected by this batch.
        /// </summary>
        public IReadOnlyList<Guid> TargetCardInstanceIds { get; set; } = Array.Empty<Guid>();

        /// <summary>
        /// The unique identifier of the source card instance that originally applied the buff (if any).
        /// </summary>
        public Guid? SourceCardInstanceId { get; set; }

        /// <summary>
        /// The unique identifiers of the source card instances represented by this batch.
        /// </summary>
        public IReadOnlyList<Guid?> SourceCardInstanceIds { get; set; } = Array.Empty<Guid?>();

        /// <summary>
        /// The definition ID of the buff (if it was a predefined buff template).
        /// </summary>
        public string? BuffDefinitionId { get; set; }

        /// <summary>
        /// The definition IDs of the buffs in this batch.
        /// </summary>
        public IReadOnlyList<string?> BuffDefinitionIds { get; set; } = Array.Empty<string?>();

        /// <summary>
        /// The type of buff that was removed (e.g., "ModifyStats", "AddKeyword").
        /// </summary>
        public string BuffType { get; set; } = string.Empty;

        /// <summary>
        /// The types of buffs removed in this batch.
        /// </summary>
        public IReadOnlyList<string> BuffTypes { get; set; } = Array.Empty<string>();

        /// <summary>
        /// The reason for buff removal (e.g., "expired", "silenced", "replaced", "manual").
        /// </summary>
        public string RemovalReason { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether this DTO represents multiple removals grouped into one event.
        /// </summary>
        public bool IsBatch { get; set; }

        /// <summary>
        /// The number of removed buffs represented by this event.
        /// </summary>
        public int RemovedCount { get; set; }

        // Constructor
        public BuffRemovedEventDto(
            Guid sourcePlayerId,
            string removalReason,
            IReadOnlyList<Guid> buffIds,
            IReadOnlyList<Guid> targetCardInstanceIds,
            IReadOnlyList<Guid?>? sourceCardInstanceIds = null,
            IReadOnlyList<string?>? buffDefinitionIds = null,
            IReadOnlyList<string>? buffTypes = null)
            : base(sourcePlayerId, "BuffRemoved")
        {
            BuffIds = buffIds ?? Array.Empty<Guid>();
            BuffId = BuffIds.FirstOrDefault();
            TargetCardInstanceIds = targetCardInstanceIds ?? Array.Empty<Guid>();
            TargetCardInstanceId = TargetCardInstanceIds.FirstOrDefault();
            RemovalReason = removalReason;
            SourceCardInstanceIds = sourceCardInstanceIds ?? Array.Empty<Guid?>();
            SourceCardInstanceId = SourceCardInstanceIds.FirstOrDefault();
            BuffDefinitionIds = buffDefinitionIds ?? Array.Empty<string?>();
            BuffDefinitionId = BuffDefinitionIds.FirstOrDefault();
            BuffTypes = buffTypes ?? Array.Empty<string>();
            BuffType = BuffTypes.FirstOrDefault() ?? "Unknown";
            RemovedCount = BuffIds.Count;
            IsBatch = RemovedCount > 1;
        }
    }
}
