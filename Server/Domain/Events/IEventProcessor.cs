using ShadowverseBeyond.Server.Dtos.SignalR.Events; // For GameEventDto
// Ensure correct using directive is present and recognized
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Defines the contract for processing game events and triggering associated abilities.
    /// </summary>
    public interface IEventProcessor
    {
        /// <summary>
        /// Processes a single game event, executes it, checks for triggered abilities,
        /// executes those abilities, and collects all resulting events.
        /// </summary>
        /// <param name="initialEvent">The initial game event to process.</param>
        /// <param name="context">The current game context.</param>
        /// <returns>A list of DTOs representing all events that occurred during processing.</returns>
        // Restore simple type name now that namespace is correct
        Task<(List<IGameEvent> NewDomainEvents, List<GameEventDto> ResultingDtos)> ProcessEventAsync(IGameEvent initialEvent, GameContext context);

        // Potentially add methods for registering/unregistering abilities if needed by the DI approach,
        // although currently EventProcessor seems to get abilities via AbilityManager in its constructor.
        // void RegisterAbility(Abilities.Ability ability);
        // void UnregisterAbility(Abilities.Ability ability);
    }
}