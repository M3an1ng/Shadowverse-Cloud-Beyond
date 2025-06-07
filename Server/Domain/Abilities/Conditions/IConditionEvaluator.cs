using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Abilities.Conditions
{
    /// <summary>
    /// Defines the contract for evaluating conditions within the ability system.
    /// Implementations of this interface will contain the logic to parse and evaluate
    /// condition expressions (defined in ConditionConfig) based on the runtime AbilityContext.
    /// </summary>
    public interface IConditionEvaluator
    {
        /// <summary>
        /// Asynchronously evaluates a condition based on the provided configuration and context.
        /// </summary>
        /// <param name="config">The static configuration defining the condition to evaluate.</param>
        /// <param name="context">The runtime context containing game state, triggering event, source card, etc.</param>
        /// <returns>True if the condition is met, false otherwise.</returns>
        Task<bool> EvaluateAsync(ConditionConfig config, AbilityContext context);
    }
}