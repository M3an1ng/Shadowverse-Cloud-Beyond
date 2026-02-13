using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.Abilities.Targets
{
    /// <summary>
    /// A temporary, isolated context for a single, atomic target selection operation.
    /// It holds a reference to the immutable GameContext and a mutable dictionary
    /// of targets that have been selected so far within the same operation.
    /// This allows subsequent target queries to depend on the results of previous selections
    /// without mutating the actual game state, enabling a fully atomic and cancellable
    /// multi-target selection experience for the user.
    /// </summary>
    public class TargetSelectionContext
    {
        /// <summary>
        /// A reference to the immutable, real-time game context.
        /// </summary>
        public GameContext GameContext { get; }

        /// <summary>
        /// A dictionary to store named lists of targets that have been resolved so far within this context.
        /// The key is the name of the target group (e.g., "primary", "secondary"), corresponding to a key
        /// in `AbilityConfig.Targets`. The value is the list of resolved targets for that group.
        /// This dictionary is populated during the ability's target resolution and consumed by the
        /// effect's `TargetSelector` keys.
        /// </summary>
        public Dictionary<string, List<TargetInfo>> ResolvedTargets { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TargetSelectionContext"/> class with pre-resolved targets.
        /// </summary>
        /// <param name="gameContext">The current game context.</param>
        /// <param name="initialResolvedTargets">A dictionary of targets that have already been resolved.</param>
        public TargetSelectionContext(GameContext gameContext, Dictionary<string, List<TargetInfo>>? initialResolvedTargets)
        {
            GameContext = gameContext;
            ResolvedTargets = initialResolvedTargets != null
                ? CloneResolvedTargets(initialResolvedTargets)
                : CloneResolvedTargets(gameContext.ResolvedTargets);
        }

        private static Dictionary<string, List<TargetInfo>> CloneResolvedTargets(
            IReadOnlyDictionary<string, List<TargetInfo>>? source)
        {
            if (source == null || source.Count == 0)
            {
                return new Dictionary<string, List<TargetInfo>>();
            }

            return source.ToDictionary(
                pair => pair.Key,
                pair => pair.Value?.Where(target => target != null).ToList() ?? new List<TargetInfo>());
        }
    }
}
