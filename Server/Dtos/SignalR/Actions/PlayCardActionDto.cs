using System;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Actions
{
    /// <summary>
    /// DTO for the play card action.
    /// </summary>
    public class PlayCardActionDto
    {
        /// <summary>
        /// The unique identifier (UID) of the card instance being played from the hand.
        /// Using string for flexibility, but could be Guid if UIDs are Guids.
        /// </summary>
        public Guid CardUid { get; set; } = Guid.Empty;

        /// <summary>
        /// A list of unique identifiers (UIDs) for the targets, if the card requires them.
        /// This supports both single-target and multi-target effects.
        /// The list can be null or empty if no targets are required or selected.
        /// </summary>
        public List<Guid>? TargetUids { get; set; }

        /// <summary>
        /// The selected play mode for this action (Base or Enhance).
        /// </summary>
        public PlayMode? PlayMode { get; set; }

        /// <summary>
        /// The runtime ID of the specific ability driving this play mode (optional, e.g., Enhance).
        /// </summary>
        public Guid? AbilityRuntimeId { get; set; }

        /// <summary>
        /// The cost the client expects to pay for the selected mode.
        /// </summary>
        public int? PaidCost { get; set; }

        // Potential future additions:
        // public int? SelectedOption { get; set; } // For cards with choices
        // public List<string> MultiTargetUids { get; set; } // For multi-target effects
    }
}
