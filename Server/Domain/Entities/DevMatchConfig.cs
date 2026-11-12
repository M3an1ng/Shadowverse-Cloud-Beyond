namespace ShadowverseBeyond.Server.Domain.Entities
{
    public class DevMatchConfig
    {
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsLocalAiTraining { get; set; }
        internal bool? BenchmarkHostGoesFirst { get; set; }
        internal int? BenchmarkShuffleSeed { get; set; }
        public bool IgnoreDeckFormatConsistency { get; set; }
        public bool AllowIncompleteDecks { get; set; }
        public int? StartingPlayPoints { get; set; }
        public int? MaxPlayPoints { get; set; }
        public int? StartingHandSize { get; set; }
        public int? StartingTurn { get; set; }
        public int? StartingHealth { get; set; }
        public int? PpBoostUses { get; set; }
        public int? Seed { get; set; }
        public int? TurnTimeoutSeconds { get; set; }
        public string? FirstPlayerMode { get; set; }
    }
}
