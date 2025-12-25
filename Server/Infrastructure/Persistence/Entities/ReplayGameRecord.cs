using System;

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Entities
{
    public class ReplayGameRecord
    {
        public string GameId { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; }
        public DateTime? EndedAtUtc { get; set; }

        public string? HostPlayerId { get; set; }
        public string? GuestPlayerId { get; set; }
        public string? WinnerPlayerId { get; set; }

        public string? ServerBuild { get; set; }
        public int SchemaVersion { get; set; } = 1;
    }
}

