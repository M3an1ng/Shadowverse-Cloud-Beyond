using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

public class GuestDeckStore : IGuestDeckStore
{
    private readonly Dictionary<Guid, Dictionary<Guid, GuestDeckRecord>> _store = new();

    public IReadOnlyList<GuestDeckRecord> List(Guid guestId)
    {
        lock (_store)
        {
            return _store.TryGetValue(guestId, out var decks)
                ? decks.Values.OrderByDescending(deck => deck.UpdatedAt).Select(Snapshot).ToList()
                : new List<GuestDeckRecord>();
        }
    }

    public GuestDeckRecord? Get(Guid guestId, Guid deckId)
    {
        lock (_store)
        {
            return _store.TryGetValue(guestId, out var decks) && decks.TryGetValue(deckId, out var deck)
                ? Snapshot(deck)
                : null;
        }
    }

    public GuestDeckRecord Create(Guid guestId, string name, CardClass deckClass, CardClass? secondaryClass, DeckEnvironment environment, DeckRule rule, IReadOnlyList<DeckCardEntryDto>? cards = null)
    {
        var deck = new GuestDeckRecord
        {
            DeckId = Guid.NewGuid(),
            Name = name,
            Class = deckClass,
            SecondaryClass = secondaryClass,
            Environment = environment,
            Rule = rule,
            UpdatedAt = DateTime.UtcNow,
            Cards = cards?.ToList() ?? new List<DeckCardEntryDto>()
        };

        lock (_store)
        {
            if (!_store.TryGetValue(guestId, out var decks))
            {
                decks = new Dictionary<Guid, GuestDeckRecord>();
                _store.Add(guestId, decks);
            }

            decks.Add(deck.DeckId, deck);
            return Snapshot(deck);
        }
    }

    public GuestDeckRecord? Update(Guid guestId, GuestDeckRecord deck)
    {
        lock (_store)
        {
            if (!_store.TryGetValue(guestId, out var decks) || !decks.ContainsKey(deck.DeckId))
            {
                return null;
            }

            var updated = Snapshot(deck with { UpdatedAt = DateTime.UtcNow });
            decks[deck.DeckId] = updated;
            return Snapshot(updated);
        }
    }

    public bool Delete(Guid guestId, Guid deckId)
    {
        lock (_store)
        {
            return _store.TryGetValue(guestId, out var decks) && decks.Remove(deckId);
        }
    }

    public void Clear(Guid guestId)
    {
        lock (_store)
        {
            _store.Remove(guestId);
        }
    }

    // DTO entries have init-only scalar fields; copying the mutable list isolates each snapshot.
    private static GuestDeckRecord Snapshot(GuestDeckRecord deck) => deck with { Cards = deck.Cards.ToList() };
}
