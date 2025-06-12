using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases
{
    /// <summary>
    /// Base class for all effect handlers, providing common functionality like logging.
    /// </summary>
    /// <typeparam name="TParams">The type of parameters this handler expects.</typeparam>
    public abstract class EffectHandlerBase<TParams> : IEffectHandler<TParams> where TParams : BaseEffectParams
    {
        protected ILogger Logger { get; }

        protected EffectHandlerBase(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes the effect asynchronously. This method orchestrates parameter validation
        /// and calls the specific ApplyAsync implementation.
        /// </summary>
        /// <param name="parameters">The effect parameters.</param>
        /// <param name="context">The game context.</param>
        /// <returns>The result of the game state action.</returns>
        public async Task<GameStateActionResult> ExecuteAsync(TParams parameters, GameContext context)
        {
            // Basic validation (can be expanded)
            if (parameters == null)
            {
                Logger.LogError("ExecuteAsync called with null parameters for handler {HandlerType}.", this.GetType().Name);
                return new GameStateActionResult { Success = false, ErrorMessage = "Effect parameters cannot be null." };
            }
            if (context == null)
            {
                Logger.LogError("ExecuteAsync called with null context for handler {HandlerType}.", this.GetType().Name);
                return new GameStateActionResult { Success = false, ErrorMessage = "Game context cannot be null." };
            }

            // Call the specific implementation with corrected parameter order
            return await ApplyAsync(context, parameters); // Corrected order
        }

        /// <summary>
        /// Abstract method to be implemented by specific effect handlers
        /// containing the core logic for applying the effect.
        /// Matches the IEffectHandler interface definition.
        /// </summary>
        /// <param name="context">The game context.</param> // Corrected parameter order
        /// <param name="parameters">The validated effect parameters.</param> // Corrected parameter order
        /// <returns>The result of the game state action.</returns>
        // Corrected signature to match IEffectHandler<TParams>
        public abstract Task<GameStateActionResult> ApplyAsync(GameContext context, TParams parameters);
    }


    /// <summary>
    /// Represents the interface for a handler that applies a specific game effect.
    /// Uses a generic type parameter for strongly-typed effect parameters,
    /// which must inherit from BaseEffectParams.
    /// </summary>
    /// <typeparam name="TParams">The type of the parameter object required by this effect handler, inheriting from BaseEffectParams.</typeparam>
    public interface IEffectHandler<in TParams> where TParams : BaseEffectParams // Constraint: Must inherit from BaseEffectParams
    {
        /// <summary>
        /// Asynchronously applies the effect using the provided context and strongly-typed parameters.
        /// </summary>
        /// <param name="context">The current game context, containing game state, source card, selected targets etc.</param>
        /// <param name="parameters">The strongly-typed parameters specific to this effect.</param>
        /// <returns>A task representing the asynchronous operation, containing the result of the effect application.</returns>
        Task<GameStateActionResult> ApplyAsync(GameContext context, TParams parameters);

        // Consider adding properties if needed, e.g.,
        // EffectType HandledEffectType { get; }
    }
}