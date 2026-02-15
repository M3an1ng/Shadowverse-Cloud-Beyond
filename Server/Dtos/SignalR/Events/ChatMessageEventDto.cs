using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Room chat message payload broadcast to all participants.
    /// </summary>
    public class ChatMessageEventDto
    {
        public Guid MessageId { get; set; } = Guid.Empty;
        public string GameId { get; set; } = string.Empty;
        public ChatSenderType SenderType { get; set; } = ChatSenderType.Player;
        public string SenderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string SenderAvatarUrl { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
        public string? ClientMessageId { get; set; }
    }
}
