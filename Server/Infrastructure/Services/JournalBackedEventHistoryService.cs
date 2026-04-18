using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using System;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Infrastructure.Services
{
    /// <summary>
    /// Keeps an in-memory cache for fast lookup of event history during the current process lifetime.
    /// </summary>
    public class JournalBackedEventHistoryService : IEventHistoryService
    {
        private readonly InMemoryEventHistoryService _memory;
        private readonly ILogger<JournalBackedEventHistoryService> _logger;

        public JournalBackedEventHistoryService(
            InMemoryEventHistoryService memory,
            ILogger<JournalBackedEventHistoryService> logger)
        {
            _memory = memory;
            _logger = logger;
        }

        public Task StoreEventAsync(string gameId, IGameEvent gameEvent)
            => _memory.StoreEventAsync(gameId, gameEvent);

        public IReadOnlyList<IGameEvent> GetEvents(string gameId)
            => _memory.GetEvents(gameId);

        public async Task<IGameEvent?> GetEventByIdAsync(string gameId, Guid eventId)
        {
            var cached = await _memory.GetEventByIdAsync(gameId, eventId);
            if (cached == null)
            {
                _logger.LogDebug("EventHistory lookup missed in memory. GameId={GameId} EventId={EventId}", gameId, eventId);
            }

            return cached;
        }

        public Task ClearHistoryForGameAsync(string gameId)
            => _memory.ClearHistoryForGameAsync(gameId);
    }
}
