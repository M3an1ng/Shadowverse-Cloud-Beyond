using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Actions
{
    public class SubmitTargetChoiceActionDto
    {
        /// <summary>
        /// The unique identifier for the pending choice this DTO is meant to resolve.
        /// </summary>
        public Guid PendingChoiceId { get; set; }

        /// <summary>
        /// The unique identifier of the ability that is requesting a target.
        /// This is needed to find the correct pending action on the server.
        /// </summary>
        public Guid SourceAbilityRuntimeId { get; set; }

        /// <summary>
        /// The name of the target group for which the choice is being submitted (e.g., "primary").
        /// </summary>
        public string TargetKey { get; set; } = string.Empty;

        /// <summary>
        /// A list of the unique instance IDs of the targets chosen by the player.
        /// </summary>
        public List<Guid> ChosenTargetIds { get; set; } = new();

        /// <summary>
        /// If true, indicates that the player is cancelling the choice selection
        /// rather than submitting a choice (even an empty one).
        /// </summary>
        public bool WasCancelled { get; set; } = false;
    }
}