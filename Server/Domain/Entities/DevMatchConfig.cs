namespace ShadowverseBeyond.Server.Domain.Entities
{
    public class DevMatchConfig
    {
        public bool IgnoreDeckFormatConsistency { get; set; }
        public bool AllowIncompleteDecks { get; set; }
        public int? StartingPlayPoints { get; set; }
        public int? MaxPlayPoints { get; set; }
        public int? StartingHandSize { get; set; }
        public int? StartingTurn { get; set; }
        public int? StartingHealth { get; set; }
        public int? PpBoostUses { get; set; }
        public int? Seed { get; set; }
    }
}
