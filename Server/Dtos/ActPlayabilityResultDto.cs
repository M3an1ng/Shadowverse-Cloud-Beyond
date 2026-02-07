using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos
{
    /// <summary>
    /// DTO for returning the result of an Act (activated ability) playability check.
    /// </summary>
    public class ActPlayabilityResultDto
    {
        /// <summary>
        /// Indicates if the card can currently use Act.
        /// </summary>
        public bool IsPlayable { get; set; }

        /// <summary>
        /// The PP cost required to use Act.
        /// </summary>
        public int ActCost { get; set; }

        /// <summary>
        /// Indicates if using Act requires the player to select a target.
        /// </summary>
        public bool RequiresTargetSelection { get; set; }

        /// <summary>
        /// If target selection is required, indicates if there are currently valid targets available.
        /// </summary>
        public bool HasValidTargets { get; set; }

        /// <summary>
        /// If the card is not playable (IsPlayable == false), provides a brief reason.
        /// </summary>
        public string FailureReason { get; set; } = string.Empty;

        /// <summary>
        /// If target selection is required and valid targets exist, provides a list of their Instance IDs.
        /// Used for UI highlighting.
        /// </summary>
        public List<Guid> ValidTargetIds { get; set; } = new();

        /// <summary>
        /// Per-ability playability info when multiple Act abilities exist.
        /// </summary>
        public List<ActAbilityPlayabilityDto> Abilities { get; set; } = new();
    }
}
