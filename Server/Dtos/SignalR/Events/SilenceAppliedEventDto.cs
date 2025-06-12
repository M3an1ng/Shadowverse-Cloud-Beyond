using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that silence has been applied to a target.
    /// </summary>
    public class SilenceAppliedEventDto : GameEventDto
    {
        /// <summary>
        /// The unique identifier of the target card instance that was silenced.
        /// </summary>
        public Guid TargetCardInstanceId { get; set; } = Guid.Empty;

        /// <summary>
        /// The unique identifier of the source card instance that applied the silence (if any).
        /// </summary>
        public Guid? SourceCardInstanceId { get; set; }

        /// <summary>
        /// The unique identifier of the source ability that applied the silence (if any).
        /// </summary>
        public long? SourceAbilityId { get; set; }

        /// <summary>
        /// Number of buffs that were removed due to silence.
        /// </summary>
        public int RemovedBuffCount { get; set; }

        // Constructor
        public SilenceAppliedEventDto(
            Guid sourcePlayerId,
            Guid targetCardInstanceId,
            Guid? sourceCardInstanceId = null,
            long? sourceAbilityId = null,
            int removedBuffCount = 0)
            : base(sourcePlayerId, "SilenceApplied")
        {
            TargetCardInstanceId = targetCardInstanceId;
            SourceCardInstanceId = sourceCardInstanceId;
            SourceAbilityId = sourceAbilityId;
            RemovedBuffCount = removedBuffCount;
        }
    }
}
