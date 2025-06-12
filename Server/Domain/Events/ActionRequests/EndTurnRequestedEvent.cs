using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ShadowverseBeyond.Server.Domain.Events.ActionRequests
{
    /// <summary>
    /// Represents a player's request to end their turn.
    /// This event triggers the turn ending sequence within the event loop.
    /// </summary>
    public class EndTurnRequestedEvent : GameEventBase
    {
        public Guid PlayerId { get; }

        // Properties required by IGameEvent
        public new PlayerStateDto? SourcePlayer { get; init; } = null;
        public new CardInstance? SourceCard { get; init; } = null;
        public new Dictionary<string, object> Parameters { get; init; } = new();

        public EndTurnRequestedEvent(Guid playerId)
            : base(nameof(EndTurnRequestedEvent))
        {
            PlayerId = playerId;
        }

        public override Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            // Placeholder: The actual logic is handled by the EventProcessor reacting to this event type.
            _logger.LogDebug("Executing EndTurnRequestedEvent for Player {PlayerId}", PlayerId);
            return Task.FromResult(new List<IGameEvent>()); // Return empty list
        }

        public GameEventDto? ToDto(IMapper mapper)
        {
            _logger.LogWarning("Attempted to convert EndTurnRequestedEvent to DTO. This event type is typically not broadcast.");
            return null;
        }

        private static readonly ILogger _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<EndTurnRequestedEvent>();
    }
}