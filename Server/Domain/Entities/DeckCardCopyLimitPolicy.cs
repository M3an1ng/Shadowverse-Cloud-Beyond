using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// Format-scoped copy limit policy for a specific card.
/// </summary>
public class DeckCardCopyLimitPolicy
{
    public Guid Id { get; set; }

    public DeckEnvironment Environment { get; set; } = DeckEnvironment.Rotation;
    public DeckRule Rule { get; set; } = DeckRule.Standard;

    public int CardDefinitionId { get; set; }
    public int MaxCopies { get; set; } = 3;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public CardDefinition CardDefinition { get; set; } = null!;
}

