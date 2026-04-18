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
        CardClass primaryClass,
        CardClass? secondaryClass,
        IReadOnlyList<DeckCardEntryDto> cards,
        DeckEnvironment environment,
        DeckRule rule,
        bool requireComplete,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(rule))
        {
            return DeckOperationResult<List<DeckCardEntryDto>>.Fail("Deck rule is invalid.");
        }

        if (rule != DeckRule.Standard && rule != DeckRule.DualClass)
        {
            return DeckOperationResult<List<DeckCardEntryDto>>.Fail("Deck rule is not supported.");
        }

        if (requireComplete && rule == DeckRule.DualClass)
        {
            if (!secondaryClass.HasValue)
            {
                return DeckOperationResult<List<DeckCardEntryDto>>.Fail("Secondary class is required for dual-class rule.");
            }

            if (secondaryClass.Value == primaryClass)
            {
                return DeckOperationResult<List<DeckCardEntryDto>>.Fail("Primary class and secondary class must be different in dual-class rule.");
            }
        }

        if (cards == null || cards.Count == 0)
        {
            if (requireComplete)
            {
                return DeckOperationResult<List<DeckCardEntryDto>>.Fail("Deck must contain exactly 40 cards.");
            }

            return DeckOperationResult<List<DeckCardEntryDto>>.Ok(new List<DeckCardEntryDto>());
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
        }

        var totalCount = normalized.Sum(entry => entry.Count);
        if (requireComplete && totalCount != GameConstants.Cards.StandardDeckSize)
        {
            return DeckOperationResult<List<DeckCardEntryDto>>.Fail("Deck must contain exactly 40 cards.");
        }

        var cardIds = normalized.Select(entry => entry.CardDefinitionId).ToList();
        var cardDefinitions = await _db.CardDefinitions
            .Include(card => card.CardSetDefinition)
            .Where(card => cardIds.Contains(card.Id))
            .Select(card => new
            {
                card.Id,
                card.Name,
                card.Class,
                card.IsCollectible,
                HasCardSetDefinition = card.CardSetDefinitionId.HasValue,
                Environments = card.CardSetDefinition != null ? card.CardSetDefinition.Environments : new List<DeckEnvironment>()
            })
            .ToListAsync(cancellationToken);

        if (cardDefinitions.Count != cardIds.Count)
        {
            return DeckOperationResult<List<DeckCardEntryDto>>.Fail("Deck contains unknown cards.");
        }

        if (!requireComplete)
        {
            // Draft-save path: only keep normalized entries and basic data integrity checks.
            // All deck legality rules are enforced in complete validation (battle/export).
            return DeckOperationResult<List<DeckCardEntryDto>>.Ok(normalized);
        }

        var cardDefinitionsById = cardDefinitions.ToDictionary(card => card.Id, card => card);
        var copyLimitByCardId = await _db.DeckCardCopyLimitPolicies
            .AsNoTracking()
            .Where(item =>
                item.Environment == environment &&
                item.Rule == rule &&
                cardIds.Contains(item.CardDefinitionId))
            .ToDictionaryAsync(item => item.CardDefinitionId, item => item.MaxCopies, cancellationToken);

        foreach (var entry in normalized)
        {
            var configuredLimit = copyLimitByCardId.TryGetValue(entry.CardDefinitionId, out var maxCopies)
                ? maxCopies
                : GameConstants.Cards.MaxCopiesPerCard;
            var effectiveLimit = Math.Clamp(configuredLimit, 0, GameConstants.Cards.MaxCopiesPerCard);
            if (entry.Count <= effectiveLimit)
            {
                continue;
            }

            var cardName = cardDefinitionsById.TryGetValue(entry.CardDefinitionId, out var definition)
                ? definition.Name
                : $"#{entry.CardDefinitionId}";
            if (effectiveLimit == 0)
            {
                return DeckOperationResult<List<DeckCardEntryDto>>.Fail(
                    $"Card '{cardName}' is banned for {environment}/{rule}.");
            }

            return DeckOperationResult<List<DeckCardEntryDto>>.Fail(
                $"Card '{cardName}' exceeds copy limit {effectiveLimit} for {environment}/{rule} (current {entry.Count}).");
        }

        foreach (var card in cardDefinitions)
        {
            if (!card.IsCollectible)
            {
                return DeckOperationResult<List<DeckCardEntryDto>>.Fail(
                    $"Card '{card.Name}' is not collectible and cannot be used in decks.");
            }

            if (!card.HasCardSetDefinition)
            {
                continue;
            }

            if (card.Environments == null || !card.Environments.Contains(environment))
            {
                return DeckOperationResult<List<DeckCardEntryDto>>.Fail("Deck contains cards outside the selected environment.");
            }
        }

        if (rule == DeckRule.Standard)
        {
            foreach (var card in cardDefinitions)
            {
                if (card.Class != CardClass.Neutral && card.Class != primaryClass)
                {
                    return DeckOperationResult<List<DeckCardEntryDto>>.Fail("Deck contains cards outside its class.");
                }
            }

            return DeckOperationResult<List<DeckCardEntryDto>>.Ok(normalized);
        }

        var primaryCount = 0;
        var secondaryCountValue = 0;
        var effectiveSecondaryClass = secondaryClass!.Value;

        foreach (var entry in normalized)
        {
            var definition = cardDefinitionsById[entry.CardDefinitionId];
            if (definition.Class == CardClass.Neutral)
            {
                continue;
            }

            if (definition.Class == primaryClass)
            {
                primaryCount += entry.Count;
                continue;
            }

            if (definition.Class == effectiveSecondaryClass)
            {
                secondaryCountValue += entry.Count;
                continue;
            }

            return DeckOperationResult<List<DeckCardEntryDto>>.Fail("Dual-class deck contains cards outside primary, secondary, or neutral classes.");
        }

        if (primaryCount < 24)
        {
            return DeckOperationResult<List<DeckCardEntryDto>>.Fail("Dual-class deck must contain at least 24 primary-class cards.");
        }

        if (secondaryCountValue < 9)
        {
            return DeckOperationResult<List<DeckCardEntryDto>>.Fail("Dual-class deck must contain at least 9 secondary-class cards.");
        }

        return DeckOperationResult<List<DeckCardEntryDto>>.Ok(normalized);
    }
}
