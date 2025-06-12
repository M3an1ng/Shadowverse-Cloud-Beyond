using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
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
    /// Represents a player's request to evolve a unit.
    /// This event triggers the core evolution logic (validation, EP cost, stat changes) within the event loop.
    /// </summary>
    public class EvolveRequestedEvent : GameEventBase
    {
        public Guid PlayerId { get; }
        public EvolveActionDto ActionDto { get; }

        // Properties required by IGameEvent
        public new PlayerStateDto? SourcePlayer { get; init; } = null;
        public new CardInstance? SourceCard { get; init; } = null; // Could be the unit being evolved
        public new Dictionary<string, object> Parameters { get; init; } = new();

        public EvolveRequestedEvent(Guid playerId, EvolveActionDto actionDto)
            : base(nameof(EvolveRequestedEvent))
        {
            PlayerId = playerId;
            ActionDto = actionDto;
            // Potentially set SourceCard based on ActionDto.UnitUid if needed immediately
        }

        public override Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            // Placeholder: The actual logic is handled by the EventProcessor reacting to this event type.
            _logger.LogDebug("Executing EvolveRequestedEvent for Player {PlayerId}, UnitUID {UnitUid}",
                PlayerId, ActionDto.UnitUid);
            return Task.FromResult(new List<IGameEvent>()); // Return empty list
        }

        public GameEventDto? ToDto(IMapper mapper)
        {
            _logger.LogWarning("Attempted to convert EvolveRequestedEvent to DTO. This event type is typically not broadcast.");
            return null;
        }

        private static readonly ILogger _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<EvolveRequestedEvent>();
    }
}