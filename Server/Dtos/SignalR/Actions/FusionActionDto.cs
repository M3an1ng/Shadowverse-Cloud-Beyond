using System;
namespace ShadowverseBeyond.Server.Dtos.SignalR.Actions
{
    /// <summary>
    /// DTO for the Fusion request action.
    /// </summary>
    public class FusionActionDto
    {
        /// <summary>
        /// The unique identifier (UID) of the card in hand that is invoking Fusion.
        /// </summary>
        public Guid CardUid { get; set; } = Guid.Empty;

        /// <summary>
        /// The runtime ID of the specific Fusion ability to execute (optional).
        /// </summary>
        public Guid? AbilityRuntimeId { get; set; }

    }
}
