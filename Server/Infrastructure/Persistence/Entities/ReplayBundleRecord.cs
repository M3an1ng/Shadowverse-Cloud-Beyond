using System;

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Entities
{
    public class ReplayBundleRecord
    {
        public long Id { get; set; }
        public string GameId { get; set; } = string.Empty;

        public long BundleSeq { get; set; }
        public Guid BundleId { get; set; }

        public long StartSeq { get; set; }
        public long EndSeq { get; set; }
        public int EventCount { get; set; }

        public DateTime CommittedAtUtc { get; set; }
    }
}

