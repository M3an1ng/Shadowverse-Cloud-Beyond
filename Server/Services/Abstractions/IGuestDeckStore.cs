using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services.Abstractions;

public interface IGuestDeckStore
{
    IReadOnlyList<GuestDeckRecord> List(Guid guestId);
    GuestDeckRecord? Get(Guid guestId, Guid deckId);
    GuestDeckRecord Create(Guid guestId, string name, CardClass deckClass, CardClass? secondaryClass, DeckEnvironment environment, DeckRule rule, IReadOnlyList<DeckCardEntryDto>? cards = null);
    GuestDeckRecord? Update(Guid guestId, GuestDeckRecord deck);
    bool Delete(Guid guestId, Guid deckId);
    void Clear(Guid guestId);
}

public record GuestDeckRecord
{
    public Guid DeckId { get; init; }
    public string Name { get; init; } = string.Empty;
    public CardClass Class { get; init; } = CardClass.Neutral;
    public CardClass? SecondaryClass { get; init; }
    public DeckEnvironment Environment { get; init; } = DeckEnvironment.Rotation;
    public DeckRule Rule { get; init; } = DeckRule.Standard;
    public DateTime UpdatedAt { get; init; }
    public List<DeckCardEntryDto> Cards { get; init; } = new();
}



