namespace ShadowverseBeyond.Server.Options
{
    public sealed class ReplayOptions
    {
        public bool EnableJournalWrites { get; set; } = false;

        public bool EnableSnapshots { get; set; } = true;
        public bool CompressSnapshots { get; set; } = true;

        public bool SnapshotOnGameStart { get; set; } = true;
        public bool SnapshotOnTurnStart { get; set; } = true;
        public bool SnapshotOnGameEnd { get; set; } = true;

        public int SnapshotEveryBundles { get; set; } = 0;

        public int RetentionDays { get; set; } = 7;
        public int CleanupIntervalMinutes { get; set; } = 30;

        public bool RequireJoinCodeForReplayReads { get; set; } = false;
    }
}
