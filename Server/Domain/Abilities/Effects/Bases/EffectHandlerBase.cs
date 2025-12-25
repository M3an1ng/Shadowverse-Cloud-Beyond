using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
// Removed using for DSL.Parser, DSL.Evaluation, and Reflection as they are no longer needed here.

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases
{
    /// <summary>
    /// Base class for all effect handlers, providing common functionality like logging.
    /// </summary>
    /// <typeparam name="TParams">The type of parameters this handler expects.</typeparam>
    public abstract class EffectHandlerBase<TParams> : IEffectHandler<TParams> where TParams : BaseEffectParams
    {
        protected ILogger Logger { get; }

        protected EffectHandlerBase(ILogger logger) // Removed DslParser and DslEvaluator
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Abstract method to be implemented by specific effect handlers
        /// containing the core logic for applying the effect.
        /// </summary>
        /// <param name="effect">The effect DTO containing dynamic data like selected targets.</param>
        /// <param name="context">The game context.</param>
        /// <param name="parameters">The validated and resolved effect parameters.</param>
        /// <returns>The result of the game state action.</returns>
        public abstract GameStateActionResult Apply(Effect effect, GameContext context, TParams parameters);

        // CreateEffectCompletedEvent method is removed as this responsibility moves to EffectExecutor.
    }


    /// <summary>
    /// Represents the interface for a handler that applies a specific game effect.
    /// </summary>
    /// <typeparam name="TParams">The type of the parameter object required by this effect handler, inheriting from BaseEffectParams.</typeparam>
    public interface IEffectHandler<in TParams> where TParams : BaseEffectParams
    {
        /// <summary>
        /// Applies the effect using the provided context and strongly-typed parameters.
        /// </summary>
        /// <param name="effect">The effect DTO containing dynamic data like selected targets.</param>
        /// <param name="context">The current game context.</param>
        /// <param name="parameters">The strongly-typed parameters specific to this effect (already resolved).</param>
        /// <returns>The result of the effect application.</returns>
        GameStateActionResult Apply(Effect effect, GameContext context, TParams parameters);

        // Execute is typically part of the base class for orchestration.
        // If handlers are resolved directly via IEffectHandler and need Execute,
        // its signature would also change to:
        // GameStateActionResult Execute(TParams parameters, GameContext context);
    }
}