using System.Diagnostics.Metrics;

namespace ShadowverseBeyond.Server.Infrastructure.Services
{
    public static class ReplayMetrics
    {
        private static readonly Meter Meter = new("ShadowverseBeyond.Replay", "1.0.0");

        public static readonly Histogram<double> JournalAppendMs =
            Meter.CreateHistogram<double>("replay.journal.append.ms", unit: "ms");

        public static readonly Counter<long> JournalAppendedEvents =
            Meter.CreateCounter<long>("replay.journal.append.events", unit: "events");

        public static readonly Histogram<double> SnapshotSaveMs =
            Meter.CreateHistogram<double>("replay.snapshot.save.ms", unit: "ms");

        public static readonly Histogram<long> SnapshotBytes =
            Meter.CreateHistogram<long>("replay.snapshot.bytes", unit: "bytes");

        public static readonly Counter<long> CleanupDeletedGames =
            Meter.CreateCounter<long>("replay.cleanup.deleted.games");
    }
}

