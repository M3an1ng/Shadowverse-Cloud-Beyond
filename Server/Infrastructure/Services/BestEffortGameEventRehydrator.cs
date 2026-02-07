using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Dtos.Replay;
using ShadowverseBeyond.Server.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Infrastructure.Services
{
    public class BestEffortGameEventRehydrator : IGameEventRehydrator
    {
        private readonly IGameService _gameService;

        public BestEffortGameEventRehydrator(IGameService gameService)
        {
            _gameService = gameService;
        }

        public Task<IGameEvent?> RehydrateAsync(
            string gameId,
            RecordedCommittedEventDto recorded,
            CancellationToken cancellationToken = default)
        {
            var gameState = _gameService.GetGameState(gameId);
            return Task.FromResult<IGameEvent?>(new RehydratedGameEvent(recorded, gameState));
        }

        private sealed class RehydratedGameEvent : IGameEvent, IRawEventBackedGameEvent
        {
            public EventType EventType { get; }
            public Guid EventId { get; }
            public DateTime Timestamp { get; }
            public TargetInfo? Source { get; }
            public List<TargetInfo> Target { get; }
            public JsonObject OutputPayload { get; }

            public RehydratedGameEvent(RecordedCommittedEventDto recorded, GameState? gameState)
            {
                EventId = recorded.EventId;
                Timestamp = recorded.TimestampUtc;
                EventType = Enum.TryParse<EventType>(recorded.EventType, out var et) ? et : EventType.Unknown;

                if (TryParseEnvelopeTargets(recorded.RawEventJson, out var sourceToken, out var targetTokens))
                {
                    Source = !string.IsNullOrWhiteSpace(sourceToken)
                        ? ResolveTargetInfo(gameState, sourceToken!)
                        : null;

                    Target = targetTokens
                        .Select(t => ResolveTargetInfo(gameState, t))
                        .Where(t => t != null)
                        .Cast<TargetInfo>()
                        .ToList();
                }
                else
                {
                    Source = recorded.SourceId.HasValue
                        ? ResolveTargetInfo(gameState, recorded.SourceId.Value)
                        : null;

                    Target = recorded.TargetIds
                        .Select(id => ResolveTargetInfo(gameState, id))
                        .Where(t => t != null)
                        .Cast<TargetInfo>()
                        .ToList();
                }

                OutputPayload = ParseOutputPayload(recorded.RawEventJson, recorded.EventType, recorded.Announcement);
            }

            private static TargetInfo? ResolveTargetInfo(GameState? gameState, Guid instanceId)
            {
                if (gameState == null) return null;

                if (instanceId == gameState.Player1.PlayerId) return TargetInfo.ForPlayer(gameState.Player1);
                if (instanceId == gameState.Player2.PlayerId) return TargetInfo.ForPlayer(gameState.Player2);

                var card = gameState.FindCardInstance(instanceId);
                return card == null ? null : TargetInfo.ForCard(card);
            }

            private static TargetInfo? ResolveTargetInfo(GameState? gameState, string token)
            {
                if (gameState == null) return null;
                if (string.IsNullOrWhiteSpace(token)) return null;

                var trimmed = token.Trim();
                if (Guid.TryParse(trimmed, out var guid) && guid != Guid.Empty)
                {
                    return ResolveTargetInfo(gameState, guid);
                }

                if (!InGameId.TryParse(trimmed, out var inGameId)) return null;

                if (!gameState.InGameIds.TryResolveGuid(inGameId.Kind, trimmed, out var resolved) || resolved == Guid.Empty)
                    return null;

                if (inGameId.Kind == InGameIdKind.Player)
                {
                    var player = gameState.GetPlayerState(resolved);
                    return player == null ? null : TargetInfo.ForPlayer(player);
                }

                if (inGameId.Kind == InGameIdKind.Card)
                {
                    var card = gameState.FindCardInstance(resolved);
                    return card == null ? null : TargetInfo.ForCard(card);
                }

                return null;
            }

            private static bool TryParseEnvelopeTargets(string? rawEventJson, out string? sourceId, out IReadOnlyList<string> targetIds)
            {
                sourceId = null;
                targetIds = Array.Empty<string>();

                if (string.IsNullOrWhiteSpace(rawEventJson)) return false;
                try
                {
                    var env = JsonSerializer.Deserialize<RawEventEnvelopeDto>(rawEventJson);
                    if (env?.TargetIds == null) return false;
                    sourceId = env.SourceId;
                    targetIds = env.TargetIds;
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            private static JsonObject ParseOutputPayload(string? rawEventJson, string eventType, string? announcement)
            {
                if (string.IsNullOrWhiteSpace(rawEventJson))
                {
                    return ParseFromAnnouncement(eventType, announcement);
                }

                try
                {
                    var env = JsonSerializer.Deserialize<RawEventEnvelopeDto>(rawEventJson);
                    return env?.Payload ?? new JsonObject();
                }
                catch
                {
                    // Fallback to best-effort extraction from announcement when raw JSON is not available.
                    return ParseFromAnnouncement(eventType, announcement);
                }
            }

            private static JsonObject ParseFromAnnouncement(string eventType, string? announcement)
            {
                if (string.IsNullOrWhiteSpace(announcement)) return new JsonObject();

                var normalized = StripTurnPrefix(announcement);
                var tokens = ExtractTokens(normalized);
                if (TryExtractNumber(tokens, out var number))
                {
                    var payload = new JsonObject();
                    if (string.Equals(eventType, "Damage", StringComparison.OrdinalIgnoreCase))
                    {
                        payload["Damage"] = number;
                        return payload;
                    }

                    if (string.Equals(eventType, "Heal", StringComparison.OrdinalIgnoreCase))
                    {
                        payload["HealAmount"] = number;
                        return payload;
                    }

                    if (string.Equals(eventType, "PpUpdated", StringComparison.OrdinalIgnoreCase))
                    {
                        payload["CurrentPp"] = number;
                        return payload;
                    }

                    if (string.Equals(eventType, "EpUpdated", StringComparison.OrdinalIgnoreCase))
                    {
                        payload["CurrentEp"] = number;
                        return payload;
                    }
                }

                var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length == 0) return new JsonObject();

                var kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var part in parts.Skip(1))
                {
                    var idx = part.IndexOf('=');
                    if (idx <= 0 || idx >= part.Length - 1) continue;
                    var key = part[..idx];
                    var value = part[(idx + 1)..];
                    kv[key] = value;
                }

                var legacyPayload = new JsonObject();
                if (string.Equals(eventType, "Damage", StringComparison.OrdinalIgnoreCase) ||
                    parts[0].Equals("damage", StringComparison.OrdinalIgnoreCase))
                {
                    if (kv.TryGetValue("amount", out var amountStr) && int.TryParse(amountStr, out var amount))
                        legacyPayload["Damage"] = amount;
                    return legacyPayload;
                }

                if (string.Equals(eventType, "Heal", StringComparison.OrdinalIgnoreCase) ||
                    parts[0].Equals("heal", StringComparison.OrdinalIgnoreCase))
                {
                    if (kv.TryGetValue("amount", out var amountStr) && int.TryParse(amountStr, out var amount))
                        legacyPayload["HealAmount"] = amount;
                    return legacyPayload;
                }

                if (string.Equals(eventType, "PpUpdated", StringComparison.OrdinalIgnoreCase) ||
                    parts[0].Equals("pp", StringComparison.OrdinalIgnoreCase))
                {
                    if (kv.TryGetValue("current", out var currentStr) && int.TryParse(currentStr, out var current))
                        legacyPayload["CurrentPp"] = current;
                    return legacyPayload;
                }

                if (string.Equals(eventType, "EpUpdated", StringComparison.OrdinalIgnoreCase) ||
                    parts[0].Equals("ep", StringComparison.OrdinalIgnoreCase))
                {
                    if (kv.TryGetValue("current", out var currentStr) && int.TryParse(currentStr, out var current))
                        legacyPayload["CurrentEp"] = current;
                    return legacyPayload;
                }

                return legacyPayload;
            }

            private static string StripTurnPrefix(string announcement)
            {
                return Regex.Replace(announcement, @"^\[Turn\s+\d+\]\s*", string.Empty);
            }

            private static IReadOnlyList<string> ExtractTokens(string announcement)
            {
                var tokens = new List<string>();
                foreach (Match match in Regex.Matches(announcement, @"\[\[(.+?)\]\]"))
                {
                    if (match.Success && match.Groups.Count > 1)
                    {
                        tokens.Add(match.Groups[1].Value);
                    }
                }
                return tokens;
            }

            private static bool TryExtractNumber(IReadOnlyList<string> tokens, out int value)
            {
                foreach (var token in tokens)
                {
                    if (token.StartsWith("num:", StringComparison.OrdinalIgnoreCase) &&
                        int.TryParse(token[4..], out value))
                    {
                        return true;
                    }
                }

                value = 0;
                return false;
            }
        }
    }
}
