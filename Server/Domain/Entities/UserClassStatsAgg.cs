using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// Aggregated per-user per-class match counters.
/// </summary>
public class UserClassStatsAgg
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public CardClass DeckClass { get; set; } = CardClass.Neutral;

    public int TotalGames { get; set; }
    public int TotalWins { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
