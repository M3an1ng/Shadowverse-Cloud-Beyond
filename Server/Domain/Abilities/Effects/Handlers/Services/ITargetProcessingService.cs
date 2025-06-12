using System;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services
{
    /// <summary>
    /// Defines a service responsible for processing effect targets in a generic way.
    /// </summary>
    public interface ITargetProcessingService
    {
        /// <summary>
        /// Applies an effect action to a list of targets, enforcing the expected target type.
        /// </summary>
        /// <param name="context">The game context.</param>
        /// <param name="expectedTargetType">The type of target the effect should apply to.</param>
        /// <param name="playerAction">Action to perform if the target is a PlayerState (and matches expected type).</param>
        /// <param name="cardAction">Action to perform if the target is a CardInstance (and matches expected type).</param>
        /// <param name="effectName">Name of the effect for logging purposes.</param>
        /// <returns>An aggregated GameStateActionResult.</returns>
        Task<GameStateActionResult> ProcessTargetsAsync(
            GameContext context,
            TargetIdType expectedTargetType,
            Func<PlayerState, GameContext, Task<IndividualActionResult>>? playerAction,
            Func<CardInstance, GameContext, Task<IndividualActionResult>>? cardAction,
            string effectName);
    }
}