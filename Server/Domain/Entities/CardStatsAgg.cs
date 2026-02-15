using System.ComponentModel.DataAnnotations;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// Aggregated per-card winrate counters by environment.
/// </summary>
public class CardStatsAgg
{
    public Guid Id { get; set; }

    public DeckEnvironment Environment { get; set; } = DeckEnvironment.Open;
    public int CardDefinitionId { get; set; }

    public int DeckGames { get; set; }
    public int DeckWins { get; set; }

    public int DrawnGames { get; set; }
    public int DrawnWins { get; set; }

    public int PlayedGames { get; set; }
    public int PlayedWins { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
