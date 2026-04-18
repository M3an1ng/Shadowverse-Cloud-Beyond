using System.Collections.Concurrent;
using System.Linq;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

public class GuestDeckStore : IGuestDeckStore
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, GuestDeckRecord>> _store = new();

    public IReadOnlyList<GuestDeckRecord> List(Guid guestId)
    {
        return _store.TryGetValue(guestId, out var decks)
            ? decks.Values.OrderByDescending(deck => deck.UpdatedAt).ToList()
            : new List<GuestDeckRecord>();
    }

    public GuestDeckRecord? Get(Guid guestId, Guid deckId)
    {
        if (_store.TryGetValue(guestId, out var decks) && decks.TryGetValue(deckId, out var deck))
        {
            return deck;
        }

        return null;
    }

    public GuestDeckRecord Create(Guid guestId, string name, CardClass deckClass, CardClass? secondaryClass, DeckEnvironment environment, DeckRule rule)
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
            Cards = new List<DeckCardEntryDto>()
        };

        var decks = _store.GetOrAdd(guestId, _ => new ConcurrentDictionary<Guid, GuestDeckRecord>());
        decks[deck.DeckId] = deck;
        return deck;
    }

    public bool Rename(Guid guestId, Guid deckId, string name)
    {
        if (_store.TryGetValue(guestId, out var decks) && decks.TryGetValue(deckId, out var deck))
        {
            decks[deckId] = deck with { Name = name, UpdatedAt = DateTime.UtcNow };
            return true;
        }

        return false;
    }

    public bool SaveCards(Guid guestId, Guid deckId, CardClass deckClass, CardClass? secondaryClass, DeckEnvironment environment, DeckRule rule, IReadOnlyList<DeckCardEntryDto> cards)
    {
        if (_store.TryGetValue(guestId, out var decks) && decks.TryGetValue(deckId, out var deck))
        {
            var clonedCards = cards.Select(card => new DeckCardEntryDto
            {
                CardDefinitionId = card.CardDefinitionId,
                Count = card.Count
            }).ToList();

            decks[deckId] = deck with
            {
                Class = deckClass,
                SecondaryClass = secondaryClass,
                Environment = environment,
                Rule = rule,
                Cards = clonedCards,
                UpdatedAt = DateTime.UtcNow
            };
            return true;
        }

        return false;
    }

    public bool Delete(Guid guestId, Guid deckId)
    {
        if (_store.TryGetValue(guestId, out var decks))
        {
            return decks.TryRemove(deckId, out _);
        }

        return false;
    }

    public void Clear(Guid guestId)
    {
        _store.TryRemove(guestId, out _);
    }
}



