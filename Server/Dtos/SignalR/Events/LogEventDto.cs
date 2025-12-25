using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Lightweight DTO for broadcasting textual event announcements to the client log.
    /// Used when a domain event has no dedicated UI DTO but still needs to be visible to players.
    /// </summary>
    public class LogEventDto : GameEventDto
    {
        public Guid EventId { get; }
        public string EventType { get; }

        public LogEventDto(Guid eventId, string eventType, string? announcement, Guid sourcePlayerId = default)
            : base(sourcePlayerId, "LogEvent")
        {
            EventId = eventId;
            EventType = eventType;
            Announcement = announcement;
        }
    }
}
