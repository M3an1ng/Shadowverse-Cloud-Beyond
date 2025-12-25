using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Infrastructure.Services
{
    /// <summary>
    /// Keeps an in-memory cache for fast lookup, with a best-effort fallback to the persistent journal.
    /// </summary>
    public class JournalBackedEventHistoryService : IEventHistoryService
    {
        private readonly InMemoryEventHistoryService _memory;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IGameEventRehydrator _rehydrator;
        private readonly ILogger<JournalBackedEventHistoryService> _logger;

        public JournalBackedEventHistoryService(
            InMemoryEventHistoryService memory,
            IServiceScopeFactory scopeFactory,
            IGameEventRehydrator rehydrator,
            ILogger<JournalBackedEventHistoryService> logger)
        {
            _memory = memory;
            _scopeFactory = scopeFactory;
            _rehydrator = rehydrator;
            _logger = logger;
        }

        public Task StoreEventAsync(string gameId, IGameEvent gameEvent)
            => _memory.StoreEventAsync(gameId, gameEvent);

        public async Task<IGameEvent?> GetEventByIdAsync(string gameId, Guid eventId)
        {
            var cached = await _memory.GetEventByIdAsync(gameId, eventId);
            if (cached != null) return cached;

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var journal = scope.ServiceProvider.GetRequiredService<ICommittedEventJournal>();
                var recorded = await journal.FindByEventIdAsync(gameId, eventId);
                if (recorded == null) return null;

                var rehydrated = await _rehydrator.RehydrateAsync(gameId, recorded);
                if (rehydrated != null)
                {
                    await _memory.StoreEventAsync(gameId, rehydrated);
                }
                return rehydrated;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "EventHistory fallback to journal failed for GameId={GameId} EventId={EventId}", gameId, eventId);
                return null;
            }
        }

        public Task ClearHistoryForGameAsync(string gameId)
            => _memory.ClearHistoryForGameAsync(gameId);
    }
}
