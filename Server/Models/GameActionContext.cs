using ShadowverseBeyond.Server.Dtos.SignalR; // Correct namespace for GameEventDto
using System.Collections.Generic;
using System.Linq;

namespace ShadowverseBeyond.Server.Models // Define a namespace
{
    public class GameActionContext
    {
        public List<GameEventDto> Events { get; } = new List<GameEventDto>();

        // Optional: Can add other context-specific data if needed later
        // public Guid GameId { get; init; }
        // public string PlayerId { get; init; }

        public void AddEvent(GameEventDto? gameEvent) // Allow nullable for convenience
        {
            if (gameEvent is not null) // Use 'is not null' for nullable check
            {
                // Use null-forgiving operator (!) as 'is not null' check wasn't enough for compiler flow analysis here.
                Events.Add(gameEvent!);
            }
        }

        public void AddEvents(IEnumerable<GameEventDto>? gameEvents)
        {
            if (gameEvents is not null) // Use 'is not null' for nullable check
            {
                // Use 'is not null' in Where clause for consistency
                Events.AddRange(gameEvents.Where(e => e is not null));
            }
        }
    }
}