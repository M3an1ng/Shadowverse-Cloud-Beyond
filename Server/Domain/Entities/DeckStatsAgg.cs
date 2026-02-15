using System.ComponentModel.DataAnnotations;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// Aggregated per-deck-shape winrate counters by environment.
/// </summary>
public class DeckStatsAgg
{
    public Guid Id { get; set; }

    public DeckEnvironment Environment { get; set; } = DeckEnvironment.Open;

    [StringLength(128)]
    public string DeckShapeHash { get; set; } = string.Empty;

    public CardClass DeckClass { get; set; } = CardClass.Neutral;

    public int TotalGames { get; set; }
    public int TotalWins { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
