using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Services.Abstractions;
using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Services
{
    public class EventHistoryMatchHistoryProjectionService : IMatchHistoryProjectionService
    {
        private const int MaxEntriesPerType = 300;

        private readonly IEventHistoryService _eventHistoryService;
        private readonly IGameDataProvider _gameDataProvider;
        private readonly ILogger<EventHistoryMatchHistoryProjectionService> _logger;

        public EventHistoryMatchHistoryProjectionService(
            IEventHistoryService eventHistoryService,
            IGameDataProvider gameDataProvider,
            ILogger<EventHistoryMatchHistoryProjectionService> logger)
        {
            _eventHistoryService = eventHistoryService;
            _gameDataProvider = gameDataProvider;
            _logger = logger;
        }

        public MatchHistorySyncEventDto BuildSnapshot(string gameId)
        {
            var snapshot = new MatchHistorySyncEventDto
            {
                GameId = gameId ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(gameId))
            {
                return snapshot;
            }

            IReadOnlyList<IGameEvent> events;
            try
            {
                events = _eventHistoryService.GetEvents(gameId) ?? Array.Empty<IGameEvent>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read event history when building match history snapshot. GameId={GameId}", gameId);
                return snapshot;
            }

            if (events.Count == 0)
            {
                return snapshot;
            }

            var artCache = new Dictionary<int, string>();
            var currentTurnNumber = 0;
            foreach (var gameEvent in events)
            {
                if (gameEvent is TurnStartEvent)
                {
                    currentTurnNumber = Math.Max(1, currentTurnNumber + 1);
                    continue;
                }

                if (gameEvent is PlayCardEvent playCardEvent)
                {
                    var usedEntry = BuildUsedCardEntry(playCardEvent, currentTurnNumber, artCache);
                    if (usedEntry != null)
                    {
                        snapshot.UsedCards.Add(usedEntry);
                    }
                    continue;
                }

                if (gameEvent is DestroyEvent destroyEvent)
                {
                    var destroyedEntry = BuildDestroyedCardEntry(destroyEvent, currentTurnNumber, artCache);
                    if (destroyedEntry != null)
                    {
                        snapshot.DestroyedCards.Add(destroyedEntry);
                    }
                }
            }

            TrimToTail(snapshot.UsedCards, MaxEntriesPerType);
            TrimToTail(snapshot.DestroyedCards, MaxEntriesPerType);

            return snapshot;
        }

        private MatchHistoryCardEntryDto? BuildUsedCardEntry(
            PlayCardEvent playCardEvent,
            int turnNumber,
            IDictionary<int, string> artCache)
        {
            var player = playCardEvent.PlayingPlayer;
            var card = playCardEvent.PlayedCard;
            if (player == null || card == null)
            {
                return null;
            }

            var cardId = card.BaseDefinitionId > 0 ? card.BaseDefinitionId : (int?)null;
            var cardName = string.IsNullOrWhiteSpace(card.Name)
                ? ResolveCardName(cardId)
                : card.Name.Trim();

            return new MatchHistoryCardEntryDto
            {
                OwnerId = player.PlayerId,
                CardUid = card.InstanceId,
                CardId = cardId,
                CardName = cardName,
                Cost = ResolveOriginalCost(card, cardId),
                ArtPreview = ResolveCardArtPreview(cardId, artCache),
                DestroyReason = string.Empty,
                TurnNumber = turnNumber,
                TimestampUtc = NormalizeTimestamp(playCardEvent.Timestamp)
            };
        }

        private MatchHistoryCardEntryDto? BuildDestroyedCardEntry(
            DestroyEvent destroyEvent,
            int turnNumber,
            IDictionary<int, string> artCache)
        {

            var targetSnapshot = destroyEvent.DestroyTargetSnapshot;
            if (targetSnapshot == null)
            {
                return null;
            }

            var cardId = targetSnapshot.BaseDefinitionId > 0 ? targetSnapshot.BaseDefinitionId : (int?)null;
            var cardName = string.IsNullOrWhiteSpace(targetSnapshot.Name)
                ? ResolveCardName(cardId)
                : targetSnapshot.Name.Trim();

            return new MatchHistoryCardEntryDto
            {
                OwnerId = targetSnapshot.OwnerPlayerId,
                CardUid = targetSnapshot.InstanceId,
                CardId = cardId,
                CardName = cardName,
                Cost = ResolveOriginalCost(targetSnapshot, cardId),
                ArtPreview = ResolveCardArtPreview(cardId, artCache),
                DestroyReason = destroyEvent.Reason.ToString(),
                TurnNumber = turnNumber,
                TimestampUtc = NormalizeTimestamp(destroyEvent.Timestamp)
            };
        }

        private int? ResolveOriginalCost(CardInstance? card, int? cardId)
        {
            if (card?.CurrentDefinition != null)
            {
                return Math.Max(0, card.CurrentDefinition.Cost);
            }

            return ResolveOriginalCostFromDefinition(cardId);
        }

        private int? ResolveOriginalCost(CardInstanceSnapshot? snapshot, int? cardId)
        {
            if (snapshot?.CurrentDefinition != null)
            {
                return Math.Max(0, snapshot.CurrentDefinition.Cost);
            }

            return ResolveOriginalCostFromDefinition(cardId);
        }

        private int? ResolveOriginalCostFromDefinition(int? cardId)
        {
            if (!cardId.HasValue || cardId.Value <= 0)
            {
                return null;
            }

            var definition = _gameDataProvider.GetCardDefinition(cardId.Value);
            if (definition == null)
            {
                return null;
            }

            return Math.Max(0, definition.Cost);
        }

        private string ResolveCardArtPreview(int? cardId, IDictionary<int, string> artCache)
        {
            if (!cardId.HasValue || cardId.Value <= 0)
            {
                return string.Empty;
            }

            if (artCache.TryGetValue(cardId.Value, out var cached))
            {
                return cached;
            }

            var preview = _gameDataProvider.GetCardDefinition(cardId.Value)?.ArtPreview ?? string.Empty;
            preview = preview.Trim();
            artCache[cardId.Value] = preview;
            return preview;
        }

        private string ResolveCardName(int? cardId)
        {
            if (!cardId.HasValue || cardId.Value <= 0)
            {
                return "未知卡牌";
            }

            var definition = _gameDataProvider.GetCardDefinition(cardId.Value);
            var name = definition?.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                return "未知卡牌";
            }

            return name.Trim();
        }

        private static DateTime NormalizeTimestamp(DateTime timestamp)
        {
            if (timestamp == default)
            {
                return DateTime.UtcNow;
            }

            return timestamp.Kind switch
            {
                DateTimeKind.Utc => timestamp,
                DateTimeKind.Local => timestamp.ToUniversalTime(),
                _ => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
            };
        }

        private static void TrimToTail<T>(List<T> items, int maxCount)
        {
            if (items.Count <= maxCount)
            {
                return;
            }

            var overflow = items.Count - maxCount;
            items.RemoveRange(0, overflow);
        }
    }
}
