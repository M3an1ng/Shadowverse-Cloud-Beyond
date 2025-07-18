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

        public EvolveRequestedEvent(Guid playerId, EvolveActionDto actionDto)
            : base(EventType.EvolveRequested)
        {
            PlayerId = playerId;
            ActionDto = actionDto;
        }


        public GameEventDto? ToDto(IMapper mapper)
        {
            _logger.LogWarning("Attempted to convert EvolveRequestedEvent to DTO. This event type is typically not broadcast.");
            return null;
        }

        private static readonly ILogger _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<EvolveRequestedEvent>();
    }
}