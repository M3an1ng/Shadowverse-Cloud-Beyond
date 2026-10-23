namespace ShadowverseBeyond.Server.Dtos.SignalR.Actions
{
    /// <summary>
    /// DTO for sending a chat message from client to hub.
    /// </summary>
    public class SendChatMessageActionDto
    {
        /// <summary>
        /// Optional client-generated id used for deduplication/ack correlation.
        /// </summary>
        public string? ClientMessageId { get; set; }

        /// <summary>
        /// Chat message content.
        /// </summary>
        public string? Content { get; set; }

        /// <summary>
        /// Chat message type. Defaults to text for backward compatibility.
        /// </summary>
        public string? MessageType { get; set; }

        /// <summary>
        /// Server-owned emote catalog id for emote messages.
        /// </summary>
        public string? EmoteId { get; set; }
    }
}
