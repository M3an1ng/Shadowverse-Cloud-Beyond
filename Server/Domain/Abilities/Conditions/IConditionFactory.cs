using ShadowverseBeyond.Server.Domain.Abilities.Runtime; // For Condition
using ShadowverseBeyond.Server.Domain.Abilities.Conditions; // For ConditionConfig

namespace ShadowverseBeyond.Server.Domain.Abilities.Conditions
{
    /// <summary>
    /// Factory interface responsible for creating Condition instances.
    /// This encapsulates the dependencies needed for condition evaluation (e.g., IDslConditionEvaluator).
    /// </summary>
    public interface IConditionFactory
    {
        /// <summary>
        /// Creates a runtime Condition instance from its configuration.
        /// </summary>
        /// <param name="config">The static configuration for the condition.</param>
        /// <returns>A new Condition instance.</returns>
        Condition CreateCondition(ConditionConfig config);
    }
}