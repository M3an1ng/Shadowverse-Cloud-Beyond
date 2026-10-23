using System;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Services;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class InMemoryGameChatServiceTests
    {
        [Fact]
        public void TryNormalizeContent_ShouldRejectEmptyMessage()
        {
            var service = new InMemoryGameChatService(NullLogger<InMemoryGameChatService>.Instance);

            var ok = service.TryNormalizeContent("   ", out var normalized, out var error);

            Assert.False(ok);
            Assert.Equal(string.Empty, normalized);
            Assert.Contains("不能为空", error);
        }

        [Fact]
        public void TryNormalizeContent_ShouldCollapseWhitespaceAndControlChars()
        {
            var service = new InMemoryGameChatService(NullLogger<InMemoryGameChatService>.Instance);

            var ok = service.TryNormalizeContent("  hello\t\u0001  world \n ", out var normalized, out var error);

            Assert.True(ok);
            Assert.Equal("hello world", normalized);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void TryNormalizeContent_ShouldRejectOverLimitMessage()
        {
            var service = new InMemoryGameChatService(NullLogger<InMemoryGameChatService>.Instance);
            var tooLong = new string('a', 201);

            var ok = service.TryNormalizeContent(tooLong, out _, out var error);

            Assert.False(ok);
            Assert.Contains("200", error);
        }

        [Fact]
        public void TryConsumeRateLimit_ShouldRejectFourthMessageWithinWindow()
        {
            var service = new InMemoryGameChatService(NullLogger<InMemoryGameChatService>.Instance);
            var now = new DateTime(2026, 2, 14, 12, 0, 0, DateTimeKind.Utc);
            const string participant = "g1:Player:p1";

            Assert.True(service.TryConsumeRateLimit(participant, now, out _));
            Assert.True(service.TryConsumeRateLimit(participant, now.AddSeconds(1), out _));
            Assert.True(service.TryConsumeRateLimit(participant, now.AddSeconds(2), out _));
            Assert.False(service.TryConsumeRateLimit(participant, now.AddSeconds(3), out var retryAfter));
            Assert.True(retryAfter > TimeSpan.Zero);

            Assert.True(service.TryConsumeRateLimit(participant, now.AddSeconds(6), out _));
        }

        [Fact]
        public void AppendMessage_ShouldKeepRoomIsolationAndBoundedHistory()
        {
            var service = new InMemoryGameChatService(NullLogger<InMemoryGameChatService>.Instance);

            for (var i = 0; i < 220; i++)
            {
                service.AppendMessage(new ChatMessageEventDto
                {
                    MessageId = Guid.NewGuid(),
                    GameId = "game-a",
                    SenderType = ChatSenderType.Player,
                    SenderId = "p1",
                    SenderName = "玩家1",
                    Content = $"msg-{i}",
                    SentAtUtc = DateTime.UtcNow
                });
            }

            service.AppendMessage(new ChatMessageEventDto
            {
                MessageId = Guid.NewGuid(),
                GameId = "game-b",
                SenderType = ChatSenderType.Spectator,
                SenderId = "s1",
                SenderName = "观战者",
                Content = "hello-b",
                SentAtUtc = DateTime.UtcNow
            });

            var gameAMessages = service.GetRecentMessages("game-a", 500);
            var gameBMessages = service.GetRecentMessages("game-b", 500);

            Assert.Equal(200, gameAMessages.Count);
            Assert.Single(gameBMessages);
            Assert.Equal("hello-b", gameBMessages[0].Content);
            Assert.Equal("msg-20", gameAMessages.First().Content);
        }

        [Fact]
        public void AppendMessage_ShouldPreserveEmoteFieldsInHistory()
        {
            var service = new InMemoryGameChatService(NullLogger<InMemoryGameChatService>.Instance);

            service.AppendMessage(new ChatMessageEventDto
            {
                MessageId = Guid.NewGuid(),
                GameId = "game-a",
                SenderType = ChatSenderType.Player,
                SenderId = "p1",
                SenderName = "玩家1",
                MessageType = "emote",
                Content = "smile",
                EmoteId = "default/smile.png",
                EmoteName = "smile",
                EmoteUrl = "/assets/emotes/default/smile.png",
                SentAtUtc = DateTime.UtcNow
            });

            var message = Assert.Single(service.GetRecentMessages("game-a", 10));

            Assert.Equal("emote", message.MessageType);
            Assert.Equal("default/smile.png", message.EmoteId);
            Assert.Equal("smile", message.EmoteName);
            Assert.Equal("/assets/emotes/default/smile.png", message.EmoteUrl);
        }
    }
}
