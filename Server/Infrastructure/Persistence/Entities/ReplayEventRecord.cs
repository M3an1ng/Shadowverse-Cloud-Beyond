using System;

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Entities
{
    public class ReplayEventRecord
    {
        public long Id { get; set; }
        public string GameId { get; set; } = string.Empty;
        public long Seq { get; set; }

        public Guid BundleId { get; set; }
        public int EventIndex { get; set; }

        public Guid EventId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public DateTime TimestampUtc { get; set; }

        public int SchemaVersion { get; set; } = 1;
        public string? DtosJson { get; set; }
        public string? RawEventJson { get; set; }

        public string? Announcement { get; set; }
        public Guid? SourceId { get; set; }
        public string? TargetIdsJson { get; set; }
    }
}
