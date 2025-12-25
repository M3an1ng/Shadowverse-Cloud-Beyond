using ShadowverseBeyond.Server.Dtos.SignalR.Events; // For GameEventDto
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
// Ensure correct using directive is present and recognized
using System.Collections.Generic;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Abilities.Common;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Represents the result of processing a single game event.
    /// It contains the abilities that were activated by the event and the DTOs for client broadcast.
    /// </summary>
    public class ProcessorResult
    {
        /// <summary>
        /// A list of abilities that were activated by the processed event.
        /// The GameService will be responsible for creating the enhanced context and executing these abilities.
        /// </summary>
        public List<IAbility> ActivatedAbilities { get; set; } = new();

        /// <summary>
        /// A list of DTOs generated during event processing (e.g., for the initial event, AbilityTriggeredEvent)
        /// that should be broadcast to clients.
        /// </summary>
        public List<GameEventDto> ResultingDtos { get; set; } = new();
    }

    /// <summary>
    /// Defines the contract for processing game events.
    /// Its primary role is to identify which abilities are triggered by an event and generate the necessary DTOs for broadcasting.
    /// It no longer executes the abilities directly.
    /// </summary>
    public interface IEventProcessor
    {
        /// <summary>
        /// Processes a single game event, identifies triggered abilities, and generates DTOs for broadcasting.
        /// </summary>
        /// <param name="initialEvent">The initial game event to process.</param>
        /// <param name="context">The current game context.</param>
        /// <returns>A ProcessorResult containing the list of activated abilities and DTOs for broadcasting.</returns>
        Task<ProcessorResult> ProcessEventAsync(IGameEvent initialEvent, GameContext context);
    }
}