using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Recent room chat history sent to newly connected clients.
    /// </summary>
    public class ChatHistoryEventDto
    {
        public string GameId { get; set; } = string.Empty;
        public List<ChatMessageEventDto> Messages { get; set; } = new();
    }
}
