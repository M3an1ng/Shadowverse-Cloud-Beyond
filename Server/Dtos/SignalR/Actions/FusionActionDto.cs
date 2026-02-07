using System;
using System.Collections.Generic;

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

        /// <summary>
        /// The unique identifiers (UIDs) of the material cards to be fused.
        /// </summary>
        public List<Guid> FusedCardUids { get; set; } = new();
    }
}
