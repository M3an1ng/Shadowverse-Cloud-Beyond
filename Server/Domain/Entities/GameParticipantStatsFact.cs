using System.ComponentModel.DataAnnotations;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// Per-game per-participant immutable stats fact row.
/// This table is the idempotency anchor for winrate settlement.
/// </summary>
public class GameParticipantStatsFact
{
    public Guid Id { get; set; }

    [Required]
    [StringLength(100)]
    public string GameId { get; set; } = string.Empty;

    public Guid PlayerId { get; set; }
    public bool IsWinner { get; set; }
    public bool? IsFirstPlayer { get; set; }
    public int? EndTurnNumber { get; set; }
    public DateTime EndedAtUtc { get; set; } = DateTime.UtcNow;

    public CardClass DeckClass { get; set; } = CardClass.Neutral;
    public DeckEnvironment Environment { get; set; } = DeckEnvironment.Open;

    [StringLength(128)]
    public string DeckShapeHash { get; set; } = string.Empty;

    public Guid? UserDeckId { get; set; }

    /// <summary>
    /// Serialized Dictionary<int, int> of initial deck card counts by CardDefinitionId.
    /// </summary>
    [Required]
    public string InitialDeckCardCountsJson { get; set; } = "{}";

    /// <summary>
    /// Serialized int[] of CardDefinitionIds seen in draw.
    /// </summary>
    [Required]
    public string DrawnCardIdsJson { get; set; } = "[]";

    /// <summary>
    /// Serialized int[] of CardDefinitionIds played from initial deck.
    /// </summary>
    [Required]
    public string PlayedCardIdsJson { get; set; } = "[]";

    /// <summary>
    /// Officiality flag aligned with coin reward eligibility.
    /// </summary>
    public bool IsOfficial { get; set; }
}
