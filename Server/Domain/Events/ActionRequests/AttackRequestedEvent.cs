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

        // Properties required by IGameEvent
        public new PlayerStateDto? SourcePlayer { get; init; } = null;
        public new CardInstance? SourceCard { get; init; } = null; // Could be the attacking unit
        public new Dictionary<string, object> Parameters { get; init; } = new();

        public AttackRequestedEvent(Guid playerId, AttackActionDto actionDto)
            : base(nameof(AttackRequestedEvent))
        {
            PlayerId = playerId;
            ActionDto = actionDto;
            // Potentially set SourceCard based on ActionDto.AttackerUid if needed immediately
        }

        public override Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            // Placeholder: The actual logic is handled by the EventProcessor reacting to this event type.
            _logger.LogDebug("Executing AttackRequestedEvent for Player {PlayerId}, AttackerUID {AttackerUid}, TargetUID {TargetUid}",
                PlayerId, ActionDto.AttackerUid, ActionDto.TargetUid);
            return Task.FromResult(new List<IGameEvent>()); // Return empty list
        }

        public GameEventDto? ToDto(IMapper mapper)
        {
            _logger.LogWarning("Attempted to convert AttackRequestedEvent to DTO. This event type is typically not broadcast.");
            return null;
        }

        private static readonly ILogger _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<AttackRequestedEvent>();
    }
}