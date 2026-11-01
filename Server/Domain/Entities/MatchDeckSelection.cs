using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Entities;

public class MatchDeckSelection
{
    [System.Text.Json.Serialization.JsonIgnore]
    public int[]? GeneratedDraftCardIds { get; set; }
    public Guid? UserId { get; set; }
    public Guid? GuestId { get; set; }
    public Guid? DeckId { get; set; }
    public string? DeckCode { get; set; }
    public CardClass? DeckClass { get; set; }
    public CardClass? SecondaryClass { get; set; }
    public DeckEnvironment? Environment { get; set; }
    public DeckRule? Rule { get; set; }

    public bool HasSelection => GeneratedDraftCardIds?.Length == 30 || DeckId.HasValue || !string.IsNullOrWhiteSpace(DeckCode);
}

