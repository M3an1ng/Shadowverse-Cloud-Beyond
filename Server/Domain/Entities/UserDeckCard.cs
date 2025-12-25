using System.ComponentModel.DataAnnotations;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// Deck card entry entity - stores card counts for a deck.
/// </summary>
public class UserDeckCard
{
    public Guid Id { get; set; }

    public Guid DeckId { get; set; }

    public int CardDefinitionId { get; set; }

    [Range(1, 3)]
    public int Count { get; set; } = 1;

    public UserDeck Deck { get; set; } = null!;

    public CardDefinition? CardDefinition { get; set; }
}
