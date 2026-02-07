using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// Defines a card set used for legality and environment checks.
/// </summary>
public class CardSetDefinition
{
    /// <summary>
    /// Unique identifier for the card set.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Display name of the set.
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Short abbreviation used for lookup or display.
    /// </summary>
    [StringLength(20)]
    public string Abbreviation { get; set; } = string.Empty;

    /// <summary>
    /// Region segment of the three-digit set code (0-9).
    /// </summary>
    [Range(0, 9)]
    public int Region { get; set; }

    /// <summary>
    /// Sequence segment of the three-digit set code (0-99).
    /// </summary>
    [Range(0, 99)]
    public int Sequence { get; set; }

    /// <summary>
    /// Environments where the set is legal.
    /// </summary>
    public List<DeckEnvironment> Environments { get; set; } = new() { DeckEnvironment.Open };

    /// <summary>
    /// Derived three-digit set code.
    /// </summary>
    [NotMapped]
    public string SetCode => $"{Region}{Sequence:D2}";

    /// <summary>
    /// Created timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Cards that belong to this set.
    /// </summary>
    public List<CardDefinition> CardDefinitions { get; set; } = new();

    /// <summary>
    /// Purchasable pack associated with this set (if any).
    /// </summary>
    public CardPackDefinition? CardPackDefinition { get; set; }
}
