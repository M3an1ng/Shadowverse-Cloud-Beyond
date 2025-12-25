using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services.Abstractions;

public interface IGuestDeckStore
{
    IReadOnlyList<GuestDeckRecord> List(Guid guestId);
    GuestDeckRecord? Get(Guid guestId, Guid deckId);
    GuestDeckRecord Create(Guid guestId, string name, CardClass deckClass, DeckEnvironment environment);
    bool Rename(Guid guestId, Guid deckId, string name);
    bool SaveCards(Guid guestId, Guid deckId, CardClass deckClass, DeckEnvironment environment, IReadOnlyList<DeckCardEntryDto> cards);
    bool Delete(Guid guestId, Guid deckId);
    void Clear(Guid guestId);
}

public record GuestDeckRecord
{
    public Guid DeckId { get; init; }
    public string Name { get; init; } = string.Empty;
    public CardClass Class { get; init; } = CardClass.Neutral;
    public DeckEnvironment Environment { get; init; } = DeckEnvironment.Open;
    public DateTime UpdatedAt { get; init; }
    public List<DeckCardEntryDto> Cards { get; init; } = new();
}
