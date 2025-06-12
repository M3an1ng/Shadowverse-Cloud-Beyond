using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that silence has been removed from a target.
    /// </summary>
    public class SilenceRemovedEventDto : GameEventDto
    {
        /// <summary>
        /// The unique identifier of the target card instance that had silence removed.
        /// </summary>
        public Guid TargetCardInstanceId { get; set; } = Guid.Empty;

        // Constructor
        public SilenceRemovedEventDto(
            Guid sourcePlayerId,
            Guid targetCardInstanceId)
            : base(sourcePlayerId, "SilenceRemoved")
        {
            TargetCardInstanceId = targetCardInstanceId;
        }
    }
}
