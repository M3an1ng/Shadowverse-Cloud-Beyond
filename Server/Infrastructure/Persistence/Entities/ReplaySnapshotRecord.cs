using System;

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Entities
{
    public class ReplaySnapshotRecord
    {
        public long Id { get; set; }
        public string GameId { get; set; } = string.Empty;

        public long AtSeq { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public int SchemaVersion { get; set; } = 1;
        public string Compression { get; set; } = "none";

        public string PayloadHash { get; set; } = string.Empty;
        public byte[] Payload { get; set; } = Array.Empty<byte>();
    }
}
