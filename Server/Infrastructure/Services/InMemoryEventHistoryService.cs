using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging; // 添加日志记录

namespace ShadowverseBeyond.Server.Infrastructure.Services // 或其他适当的命名空间
{
    public class InMemoryEventHistoryService : IEventHistoryService
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, IGameEvent>> _gameEventHistories = new();
        private readonly ILogger<InMemoryEventHistoryService> _logger;

        public InMemoryEventHistoryService(ILogger<InMemoryEventHistoryService> logger)
        {
            _logger = logger;
        }

        public Task StoreEventAsync(string gameId, IGameEvent gameEvent)
        {
            if (string.IsNullOrEmpty(gameId) || gameEvent == null)
            {
                _logger.LogWarning("Attempted to store event with null gameId or gameEvent.");
                return Task.CompletedTask; // 或者抛出ArgumentNullException
            }

            var gameHistory = _gameEventHistories.GetOrAdd(gameId, _ => new ConcurrentDictionary<Guid, IGameEvent>());
            gameHistory[gameEvent.EventId] = gameEvent;
            _logger.LogTrace("Stored event {EventId} ({EventType}) for game {GameId}.", gameEvent.EventId, gameEvent.EventType, gameId);
            return Task.CompletedTask;
        }

        public Task<IGameEvent?> GetEventByIdAsync(string gameId, Guid eventId)
        {
            if (string.IsNullOrEmpty(gameId) || eventId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to get event with null/empty gameId or empty eventId.");
                return Task.FromResult<IGameEvent?>(null);
            }

            if (_gameEventHistories.TryGetValue(gameId, out var gameHistory))
            {
                if (gameHistory.TryGetValue(eventId, out var gameEvent))
                {
                    _logger.LogTrace("Retrieved event {EventId} ({EventType}) for game {GameId}.", eventId, gameEvent.EventType, gameId);
                    return Task.FromResult<IGameEvent?>(gameEvent);
                }
            }
            _logger.LogDebug("Event {EventId} not found for game {GameId}.", eventId, gameId);
            return Task.FromResult<IGameEvent?>(null);
        }

        public Task ClearHistoryForGameAsync(string gameId)
        {
            if (string.IsNullOrEmpty(gameId))
            {
                _logger.LogWarning("Attempted to clear event history with null or empty gameId.");
                return Task.CompletedTask;
            }

            if (_gameEventHistories.TryRemove(gameId, out var removedHistory))
            {
                _logger.LogInformation("Cleared event history for game {GameId}. Removed {Count} events.", gameId, removedHistory.Count);
            }
            else
            {
                _logger.LogDebug("No event history found to clear for game {GameId}.", gameId);
            }
            return Task.CompletedTask;
        }
    }
}