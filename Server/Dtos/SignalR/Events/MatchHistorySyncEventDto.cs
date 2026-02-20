using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    public class MatchHistoryCardEntryDto
    {
        public Guid OwnerId { get; set; } = Guid.Empty;
        public Guid CardUid { get; set; } = Guid.Empty;
        public int? CardId { get; set; }
        public string CardName { get; set; } = string.Empty;
        public int? Cost { get; set; }
        public string ArtPreview { get; set; } = string.Empty;
        public string DestroyReason { get; set; } = string.Empty;
        public int TurnNumber { get; set; }
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }

    public class MatchHistorySyncEventDto
    {
        public string GameId { get; set; } = string.Empty;
        public List<MatchHistoryCardEntryDto> UsedCards { get; set; } = new();
        public List<MatchHistoryCardEntryDto> DestroyedCards { get; set; } = new();
    }
}
