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

        // Properties required by IGameEvent
        public new PlayerStateDto? SourcePlayer { get; init; } = null;
        public new CardInstance? SourceCard { get; init; } = null;
        public new Dictionary<string, object> Parameters { get; init; } = new();

        public MulliganSubmittedEvent(Guid playerId, List<Guid> cardUidsToReplace)
            : base(nameof(MulliganSubmittedEvent))
        {
            PlayerId = playerId;
            CardUidsToReplace = cardUidsToReplace ?? new List<Guid>(); // Ensure list is not null
        }

        public override Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            // MulliganSubmittedEvent 的执行逻辑应该通过 EventProcessor 中的特殊处理来调用 MulliganEffectHandler
            // 这里只记录事件执行，不直接执行换牌逻辑
            _logger.LogDebug("Executing MulliganSubmittedEvent for Player {PlayerId}, replacing {Count} cards.",
                PlayerId, CardUidsToReplace.Count);

            // 返回空列表，实际的 Mulligan 处理由 EventProcessor 通过 MulliganEffectHandler 处理
            return Task.FromResult(new List<IGameEvent>());
        }

        public GameEventDto? ToDto(IMapper mapper)
        {
            _logger.LogWarning("Attempted to convert MulliganSubmittedEvent to DTO. This event type is typically not broadcast.");
            return null;
        }

        private static readonly ILogger _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<MulliganSubmittedEvent>();
    }
}