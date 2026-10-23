using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services
{
    public class InMemoryGameChatService : IGameChatService
    {
        private const int MaxMessageLength = 200;
        private const int HistoryCapacityPerGame = 200;
        private const int RateLimitCount = 3;
        private static readonly TimeSpan RateLimitWindow = TimeSpan.FromSeconds(5);
        private static readonly Regex MultiWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

        private readonly ConcurrentDictionary<string, RoomChatBuffer> _roomBuffers = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, ParticipantRateState> _rateStates = new(StringComparer.Ordinal);
        private readonly ILogger<InMemoryGameChatService> _logger;

        public InMemoryGameChatService(ILogger<InMemoryGameChatService> logger)
        {
            _logger = logger;
        }

        public bool TryNormalizeContent(string? rawContent, out string normalizedContent, out string errorMessage)
        {
            normalizedContent = string.Empty;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(rawContent))
            {
                errorMessage = "消息不能为空。";
                return false;
            }

            var builder = new StringBuilder(rawContent.Length);
            foreach (var ch in rawContent)
            {
                if (char.IsControl(ch) && ch != '\r' && ch != '\n' && ch != '\t')
                {
                    builder.Append(' ');
                }
                else
                {
                    builder.Append(ch);
                }
            }

            var sanitized = builder.ToString();
            var collapsed = MultiWhitespaceRegex.Replace(sanitized, " ").Trim();
            if (string.IsNullOrWhiteSpace(collapsed))
            {
                errorMessage = "消息不能为空。";
                return false;
            }

            if (collapsed.Length > MaxMessageLength)
            {
                errorMessage = $"消息长度不能超过 {MaxMessageLength} 字符。";
                return false;
            }

            normalizedContent = collapsed;
            return true;
        }

        public bool TryConsumeRateLimit(string participantKey, DateTime utcNow, out TimeSpan retryAfter)
        {
            retryAfter = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(participantKey))
            {
                return false;
            }

            var state = _rateStates.GetOrAdd(participantKey, _ => new ParticipantRateState());
            lock (state.SyncRoot)
            {
                while (state.SentAtUtc.Count > 0 && (utcNow - state.SentAtUtc.Peek()) >= RateLimitWindow)
                {
                    state.SentAtUtc.Dequeue();
                }

                if (state.SentAtUtc.Count >= RateLimitCount)
                {
                    var oldest = state.SentAtUtc.Peek();
                    retryAfter = RateLimitWindow - (utcNow - oldest);
                    if (retryAfter < TimeSpan.Zero)
                    {
                        retryAfter = TimeSpan.Zero;
                    }
                    return false;
                }

                state.SentAtUtc.Enqueue(utcNow);
                state.LastTouchedUtc = utcNow;
                return true;
            }
        }

        public void AppendMessage(ChatMessageEventDto message)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.GameId))
            {
                return;
            }

            var buffer = _roomBuffers.GetOrAdd(message.GameId, _ => new RoomChatBuffer());
            var cloned = CloneMessage(message);
            lock (buffer.SyncRoot)
            {
                buffer.Messages.Enqueue(cloned);
                while (buffer.Messages.Count > HistoryCapacityPerGame)
                {
                    buffer.Messages.Dequeue();
                }
            }
        }

        public IReadOnlyList<ChatMessageEventDto> GetRecentMessages(string gameId, int limit)
        {
            if (string.IsNullOrWhiteSpace(gameId) || !_roomBuffers.TryGetValue(gameId, out var buffer))
            {
                return Array.Empty<ChatMessageEventDto>();
            }

            List<ChatMessageEventDto> snapshot;
            lock (buffer.SyncRoot)
            {
                snapshot = buffer.Messages.Select(CloneMessage).ToList();
            }

            if (limit > 0 && snapshot.Count > limit)
            {
                snapshot = snapshot.Skip(snapshot.Count - limit).ToList();
            }

            return snapshot;
        }

        public void ClearGame(string gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                return;
            }

            _roomBuffers.TryRemove(gameId, out _);
            _logger.LogDebug("Cleared in-memory chat history for GameId={GameId}", gameId);
        }

        private static ChatMessageEventDto CloneMessage(ChatMessageEventDto message)
        {
            return new ChatMessageEventDto
            {
                MessageId = message.MessageId,
                GameId = message.GameId,
                SenderType = message.SenderType,
                SenderId = message.SenderId,
                SenderName = message.SenderName,
                SenderAvatarUrl = message.SenderAvatarUrl,
                MessageType = message.MessageType,
                Content = message.Content,
                EmoteId = message.EmoteId,
                EmoteName = message.EmoteName,
                EmoteUrl = message.EmoteUrl,
                SentAtUtc = message.SentAtUtc,
                ClientMessageId = message.ClientMessageId
            };
        }

        private sealed class RoomChatBuffer
        {
            public object SyncRoot { get; } = new();
            public Queue<ChatMessageEventDto> Messages { get; } = new();
        }

        private sealed class ParticipantRateState
        {
            public object SyncRoot { get; } = new();
            public Queue<DateTime> SentAtUtc { get; } = new();
            public DateTime LastTouchedUtc { get; set; } = DateTime.UtcNow;
        }
    }
}
