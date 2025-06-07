using System.Collections.Generic;
using System.Threading.Tasks; // Added for Task
using ShadowverseBeyond.Server.Domain.Abilities; // Added for AbilityContext

namespace ShadowverseBeyond.Server.Domain.Abilities.Targets
{
    /// <summary>
    /// Defines the contract for target selectors within the ability system.
    /// Implementations determine valid targets based on configuration and runtime context.
    /// Based on M11 Target Selection System Design Document (adapted for AbilityContext).
    /// </summary>
    public interface ITargetSelector
    {
        /// <summary>
        /// Asynchronously selects targets based on the provided configuration and context.
        /// </summary>
        /// <param name="config">The static configuration defining the target selection rules.</param>
        /// <param name="context">The runtime context containing game state, triggering event, source card, etc.</param>
        /// <returns>A list of selected target identifiers (TargetId). Returns an empty list if no targets are selected or valid.</returns>
        Task<List<TargetId>> SelectTargetsAsync(TargetConfig config, AbilityContext context);
    }
}
