using System.ComponentModel.DataAnnotations;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// User deck entity - represents a saved deck owned by a user.
/// </summary>
public class UserDeck
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public CardClass Class { get; set; } = CardClass.Neutral;

    public DeckEnvironment Environment { get; set; } = DeckEnvironment.Open;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;

    public List<UserDeckCard> Cards { get; set; } = new();

    public void Touch()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}
