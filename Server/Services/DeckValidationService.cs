using System.Linq;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

public class DeckValidationService : IDeckValidationService
{
    private readonly ApplicationDbContext _db;

    public DeckValidationService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DeckOperationResult<List<DeckCardEntryDto>>> ValidateAsync(
        CardClass deckClass,
        IReadOnlyList<DeckCardEntryDto> cards,
        bool requireComplete,
        CancellationToken cancellationToken)
    {
        if (cards == null || cards.Count == 0)
        {
            return requireComplete
                ? DeckOperationResult<List<DeckCardEntryDto>>.Fail("Deck must contain cards.")
                : DeckOperationResult<List<DeckCardEntryDto>>.Ok(new List<DeckCardEntryDto>());
        }

        var normalized = cards
            .GroupBy(card => card.CardDefinitionId)
            .Select(group => new DeckCardEntryDto
            {
                CardDefinitionId = group.Key,
                Count = group.Sum(entry => entry.Count)
            })
            .ToList();

        foreach (var entry in normalized)
        {
            if (entry.CardDefinitionId <= 0)
            {
                return DeckOperationResult<List<DeckCardEntryDto>>.Fail("CardDefinitionId is invalid.");
            }

            if (entry.Count <= 0)
            {
                return DeckOperationResult<List<DeckCardEntryDto>>.Fail("Card count must be positive.");
            }

            if (entry.Count > GameConstants.Cards.MaxCopiesPerCard)
            {
                return DeckOperationResult<List<DeckCardEntryDto>>.Fail("Deck contains too many copies of a card.");
            }
        }

        var totalCount = normalized.Sum(entry => entry.Count);
        // TODO: Re-enable exact 40-card enforcement when moving to production.
        if (requireComplete && totalCount > GameConstants.Cards.MaxDeckSize)
        {
            return DeckOperationResult<List<DeckCardEntryDto>>.Fail("Deck cannot exceed 40 cards.");
        }

        var cardIds = normalized.Select(entry => entry.CardDefinitionId).ToList();
        var cardDefinitions = await _db.CardDefinitions
            .Where(card => cardIds.Contains(card.Id))
            .Select(card => new { card.Id, card.Class })
            .ToListAsync(cancellationToken);

        if (cardDefinitions.Count != cardIds.Count)
        {
            return DeckOperationResult<List<DeckCardEntryDto>>.Fail("Deck contains unknown cards.");
        }

        foreach (var card in cardDefinitions)
        {
            if (card.Class != CardClass.Neutral && card.Class != deckClass)
            {
                return DeckOperationResult<List<DeckCardEntryDto>>.Fail("Deck contains cards outside its class.");
            }
        }

        return DeckOperationResult<List<DeckCardEntryDto>>.Ok(normalized);
    }
}
