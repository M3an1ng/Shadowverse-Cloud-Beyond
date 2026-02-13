using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Entities;

public class MatchDeckSelection
{
    public Guid? UserId { get; set; }
    public Guid? GuestId { get; set; }
    public Guid? DeckId { get; set; }
    public string? DeckCode { get; set; }
    public CardClass? DeckClass { get; set; }

    public bool HasSelection => DeckId.HasValue || !string.IsNullOrWhiteSpace(DeckCode);
}
