using ShadowverseBeyond.Server.Domain.Abilities.Runtime; // For EffectExecutionContext
using ShadowverseBeyond.Server.Domain.Events; // For IGameEvent and GameContext
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects
{
    /// <summary>
    /// Interface responsible for executing a single effect based on its configuration and context.
    /// This encapsulates the dependencies needed for effect execution (e.g., specific IEffectHandler<TParams> instances).
    /// </summary>
    public interface IEffectExecutor
    {
        /// <summary>
        /// 执行单个效果并返回完整的结果，包括事件和效果请求
        /// Executes a single effect and returns the complete result including events and effect requests.
        /// </summary>
        /// <param name="effectConfig">The configuration of the effect to execute.</param>
        /// <param name="context">The unified game context containing game state, source, targets, etc.</param>
        /// <returns>The complete game state action result including events and effect requests.</returns>
        GameStateActionResult ExecuteEffect(Effect effect, GameContext context);
    }
}