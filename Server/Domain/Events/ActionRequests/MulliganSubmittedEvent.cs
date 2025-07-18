using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using System; // Added for Guid
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace ShadowverseBeyond.Server.Domain.Events.ActionRequests
{
    /// <summary>
    /// Represents a player's submission of cards to replace during the mulligan phase.
    /// This event triggers the core mulligan logic within the event loop.
    /// </summary>
    public class MulliganSubmittedEvent : GameEventBase
    {
        public Guid PlayerId { get; }
        public List<Guid> CardUidsToReplace { get; } // Changed from CardInstanceId to Guid based on GameHub likely usage

        public MulliganSubmittedEvent(Guid playerId, List<Guid> cardUidsToReplace)
            : base(EventType.MulliganSubmitted)
        {
            PlayerId = playerId;
            CardUidsToReplace = cardUidsToReplace ?? new List<Guid>(); // Ensure list is not null
        }


        public GameEventDto? ToDto(IMapper mapper)
        {
            _logger.LogWarning("Attempted to convert MulliganSubmittedEvent to DTO. This event type is typically not broadcast.");
            return null;
        }

        private static readonly ILogger _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<MulliganSubmittedEvent>();
    }
}