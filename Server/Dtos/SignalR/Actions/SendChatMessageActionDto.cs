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
    }
}
