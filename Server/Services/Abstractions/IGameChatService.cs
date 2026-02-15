using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;

namespace ShadowverseBeyond.Server.Services.Abstractions
{
    public interface IGameChatService
    {
        bool TryNormalizeContent(string? rawContent, out string normalizedContent, out string errorMessage);
        bool TryConsumeRateLimit(string participantKey, DateTime utcNow, out TimeSpan retryAfter);
        void AppendMessage(ChatMessageEventDto message);
        IReadOnlyList<ChatMessageEventDto> GetRecentMessages(string gameId, int limit);
        void ClearGame(string gameId);
    }
}
