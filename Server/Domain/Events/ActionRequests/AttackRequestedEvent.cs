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
    /// Represents a player's request to perform an attack.
    /// This event triggers the core attack logic (validation, damage calculation, counter-attack) within the event loop.
    /// </summary>
    public class AttackRequestedEvent : GameEventBase
    {
        public Guid PlayerId { get; }
        public AttackActionDto ActionDto { get; }

        public AttackRequestedEvent(Guid playerId, AttackActionDto actionDto)
            : base(EventType.AttackRequested)
        {
            PlayerId = playerId;
            ActionDto = actionDto;
        }


        public GameEventDto? ToDto(IMapper mapper)
        {
            _logger.LogWarning("Attempted to convert AttackRequestedEvent to DTO. This event type is typically not broadcast.");
            return null;
        }

        private static readonly ILogger _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<AttackRequestedEvent>();
    }
}