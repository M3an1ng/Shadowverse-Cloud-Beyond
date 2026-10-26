using ShadowverseBeyond.Server.Dtos.SignalR.Events; // For GameEventDto
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
// Ensure correct using directive is present and recognized
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Represents the result of processing a single game event.
    /// It contains the DTOs generated for client broadcast.
    /// </summary>
    public class ProcessorResult
    {
        /// <summary>
        /// A list of DTOs generated during event processing (e.g., for the initial event, AbilityTriggeredEvent)
        /// that should be broadcast to clients.
        /// </summary>
        public List<GameEventDto> ResultingDtos { get; set; } = new();
    }

    /// <summary>
    /// Defines the contract for processing game events.
    /// Its primary role is to convert game events into DTOs for broadcasting.
    /// </summary>
    public interface IEventProcessor
    {
        /// <summary>
        /// Processes a single game event and generates DTOs for broadcasting.
        /// </summary>
        /// <param name="initialEvent">The initial game event to process.</param>
        /// <param name="context">The current game context.</param>
        /// <returns>A ProcessorResult containing any DTOs generated during processing.</returns>
        Task<ProcessorResult> ProcessEventAsync(IGameEvent initialEvent, GameContext context);
    }
}
