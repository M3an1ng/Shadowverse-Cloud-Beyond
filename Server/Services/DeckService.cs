using System.Linq;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

public class DeckService : IDeckService
{
    private const string DefaultDeckName = "Default Deck";
    private const DeckEnvironment DefaultEnvironment = DeckEnvironment.Open;
    private static readonly int[] DefaultDeckSeedCardIds = { 1001, 1002, 1005, 1006, 2001, 2002, 1003, 1004 };
    private static readonly Dictionary<int, int> DefaultDeckPriority = DefaultDeckSeedCardIds
        .Select((cardId, index) => new { cardId, index })
        .ToDictionary(entry => entry.cardId, entry => entry.index);

    private readonly ApplicationDbContext _db;
    private readonly IGuestDeckStore _guestStore;
    private readonly IDeckCodeService _deckCodeService;
    private readonly IDeckValidationService _deckValidation;

    public DeckService(
        ApplicationDbContext db,
        IGuestDeckStore guestStore,
        IDeckCodeService deckCodeService,
        IDeckValidationService deckValidation)
    {
        _db = db;
        _guestStore = guestStore;
        _deckCodeService = deckCodeService;
        _deckValidation = deckValidation;
    }

    public async Task<bool> HasAnyDeckAsync(DeckOwner owner, CancellationToken cancellationToken)
    {
        if (owner.IsGuest)
        {
            var guestId = owner.GetGuestIdOrNull();
            if (!guestId.HasValue || guestId.Value == Guid.Empty)
            {
                return false;
            }

            return _guestStore.List(guestId.Value).Count > 0;
        }

        var userId = owner.GetUserIdOrNull();
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            return false;
        }

        return await _db.UserDecks.AnyAsync(deck => deck.UserId == userId.Value, cancellationToken);
    }

    public async Task<DeckOperationResult<bool>> EnsureDefaultDeckAsync(DeckOwner owner, CancellationToken cancellationToken)
    {
        if (owner.IsGuest)
        {
            var guestId = owner.GetGuestIdOrNull();
            if (!guestId.HasValue || guestId.Value == Guid.Empty)
            {
                return DeckOperationResult<bool>.Fail("GuestId is required.");
            }
        }
        else
        {
            var userId = owner.GetUserIdOrNull();
            if (!userId.HasValue || userId.Value == Guid.Empty)
            {
                return DeckOperationResult<bool>.Fail("UserId is required.");
            }
        }

        if (await HasAnyDeckAsync(owner, cancellationToken))
        {
            return DeckOperationResult<bool>.Ok(false);
        }

        var plan = await BuildDefaultDeckPlanAsync(cancellationToken);
        if (plan == null)
        {
            return DeckOperationResult<bool>.Fail("No available cards to build a default deck.");
        }

        var createRequest = new CreateDeckRequest
        {
            Name = DefaultDeckName,
            Class = plan.DeckClass,
            Environment = DefaultEnvironment,
            Cards = plan.Cards
        };

        var created = await CreateAsync(owner, createRequest, cancellationToken);
        if (!created.Success)
        {
            return DeckOperationResult<bool>.Fail(created.Error ?? "Failed to create default deck.");
        }

        return DeckOperationResult<bool>.Ok(true);
    }

    public async Task<IReadOnlyList<DeckSummaryDto>> ListAsync(DeckOwner owner, CancellationToken cancellationToken)
    {
        if (owner.IsGuest)
        {
            var guestId = owner.GetGuestIdOrNull();
            if (!guestId.HasValue || guestId.Value == Guid.Empty)
            {
                return new List<DeckSummaryDto>();
            }

            return _guestStore.List(guestId.Value)
                .Select(deck => ToSummaryDto(deck, isGuest: true))
                .ToList();
        }

        var userId = owner.GetUserIdOrNull();
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            return new List<DeckSummaryDto>();
        }

        var decks = await _db.UserDecks
            .AsNoTracking()
            .Where(deck => deck.UserId == userId.Value)
            .OrderByDescending(deck => deck.UpdatedAt)
            .Select(deck => new DeckSummaryDto
            {
                DeckId = deck.Id,
                Name = deck.Name,
                Class = deck.Class,
                Environment = deck.Environment,
                CardCount = deck.Cards.Sum(card => card.Count),
                UpdatedAt = deck.UpdatedAt,
                IsGuest = false
            })
            .ToListAsync(cancellationToken);

        return decks;
    }

    public async Task<DeckDetailDto?> GetAsync(DeckOwner owner, Guid deckId, CancellationToken cancellationToken)
    {
        if (owner.IsGuest)
        {
            var guestId = owner.GetGuestIdOrNull();
            if (!guestId.HasValue || guestId.Value == Guid.Empty)
            {
                return null;
            }

            var deck = _guestStore.Get(guestId.Value, deckId);
            return deck == null ? null : ToDetailDto(deck, isGuest: true);
        }

        var userId = owner.GetUserIdOrNull();
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            return null;
        }

        var deckEntity = await _db.UserDecks
            .Include(deck => deck.Cards)
            .AsNoTracking()
            .FirstOrDefaultAsync(deck => deck.Id == deckId && deck.UserId == userId.Value, cancellationToken);

        return deckEntity == null ? null : ToDetailDto(deckEntity, isGuest: false);
    }

    public async Task<DeckOperationResult<DeckDetailDto>> CreateAsync(DeckOwner owner, CreateDeckRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return DeckOperationResult<DeckDetailDto>.Fail("Deck name is required.");
        }

        if (!request.Environment.HasValue)
        {
            return DeckOperationResult<DeckDetailDto>.Fail("Deck environment is required.");
        }

        var environment = request.Environment.Value;

        if (owner.IsGuest)
        {
            var guestId = owner.GetGuestIdOrNull();
            if (!guestId.HasValue || guestId.Value == Guid.Empty)
            {
                return DeckOperationResult<DeckDetailDto>.Fail("GuestId is required.");
            }

            var deck = _guestStore.Create(guestId.Value, request.Name.Trim(), request.Class, environment);
            if (request.Cards != null && request.Cards.Count > 0)
            {
                var validation = await _deckValidation.ValidateAsync(request.Class, request.Cards, requireComplete: true, cancellationToken);
                if (!validation.Success)
                {
                    return DeckOperationResult<DeckDetailDto>.Fail(validation.Error ?? "Deck validation failed.");
                }

                _guestStore.SaveCards(guestId.Value, deck.DeckId, request.Class, environment, validation.Data!);
                deck = _guestStore.Get(guestId.Value, deck.DeckId) ?? deck;
            }

            return DeckOperationResult<DeckDetailDto>.Ok(ToDetailDto(deck, isGuest: true));
        }

        var userId = owner.GetUserIdOrNull();
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            return DeckOperationResult<DeckDetailDto>.Fail("UserId is required.");
        }

        var userExists = await _db.Users.AnyAsync(user => user.Id == userId.Value, cancellationToken);
        if (!userExists)
        {
            return DeckOperationResult<DeckDetailDto>.Fail("User does not exist.");
        }

        var deckEntity = new UserDeck
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            Name = request.Name.Trim(),
            Class = request.Class,
            Environment = environment,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (request.Cards != null && request.Cards.Count > 0)
        {
            var validation = await _deckValidation.ValidateAsync(request.Class, request.Cards, requireComplete: true, cancellationToken);
            if (!validation.Success)
            {
                return DeckOperationResult<DeckDetailDto>.Fail(validation.Error ?? "Deck validation failed.");
            }

            deckEntity.Cards = validation.Data!
                .Select(entry => new UserDeckCard
                {
                    Id = Guid.NewGuid(),
                    DeckId = deckEntity.Id,
                    CardDefinitionId = entry.CardDefinitionId,
                    Count = entry.Count
                })
                .ToList();
        }

        _db.UserDecks.Add(deckEntity);
        await _db.SaveChangesAsync(cancellationToken);

        return DeckOperationResult<DeckDetailDto>.Ok(ToDetailDto(deckEntity, isGuest: false));
    }

    public async Task<DeckOperationResult<DeckDetailDto>> UpdateAsync(DeckOwner owner, Guid deckId, UpdateDeckRequest request, CancellationToken cancellationToken)
    {
        if (owner.IsGuest)
        {
            var guestId = owner.GetGuestIdOrNull();
            if (!guestId.HasValue || guestId.Value == Guid.Empty)
            {
                return DeckOperationResult<DeckDetailDto>.Fail("GuestId is required.");
            }

            var existing = _guestStore.Get(guestId.Value, deckId);
            if (existing == null)
            {
                return DeckOperationResult<DeckDetailDto>.Fail("Deck not found.");
            }

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                _guestStore.Rename(guestId.Value, deckId, request.Name.Trim());
            }

            var targetClass = request.Class ?? existing.Class;
            var guestEnvironment = request.Environment ?? existing.Environment;
            if (request.Cards != null)
            {
                var validation = await _deckValidation.ValidateAsync(targetClass, request.Cards, requireComplete: true, cancellationToken);
                if (!validation.Success)
                {
                    return DeckOperationResult<DeckDetailDto>.Fail(validation.Error ?? "Deck validation failed.");
                }

                _guestStore.SaveCards(guestId.Value, deckId, targetClass, guestEnvironment, validation.Data!);
            }
            else if (request.Class.HasValue && request.Class.Value != existing.Class && existing.Cards.Count > 0)
            {
                var validation = await _deckValidation.ValidateAsync(targetClass, existing.Cards, requireComplete: false, cancellationToken);
                if (!validation.Success)
                {
                    return DeckOperationResult<DeckDetailDto>.Fail(validation.Error ?? "Deck validation failed.");
                }

                _guestStore.SaveCards(guestId.Value, deckId, targetClass, guestEnvironment, validation.Data!);
            }
            else if (request.Environment.HasValue && request.Environment.Value != existing.Environment)
            {
                _guestStore.SaveCards(guestId.Value, deckId, targetClass, guestEnvironment, existing.Cards);
            }

            var updated = _guestStore.Get(guestId.Value, deckId) ?? existing;
            return DeckOperationResult<DeckDetailDto>.Ok(ToDetailDto(updated, isGuest: true));
        }

        var userId = owner.GetUserIdOrNull();
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            return DeckOperationResult<DeckDetailDto>.Fail("UserId is required.");
        }

        var deckEntity = await _db.UserDecks
            .Include(deck => deck.Cards)
            .FirstOrDefaultAsync(deck => deck.Id == deckId && deck.UserId == userId.Value, cancellationToken);

        if (deckEntity == null)
        {
            return DeckOperationResult<DeckDetailDto>.Fail("Deck not found.");
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            deckEntity.Name = request.Name.Trim();
        }

        var targetEnvironment = request.Environment ?? deckEntity.Environment;
        var targetDeckClass = request.Class ?? deckEntity.Class;
        if (request.Cards != null)
        {
            var validation = await _deckValidation.ValidateAsync(targetDeckClass, request.Cards, requireComplete: true, cancellationToken);
            if (!validation.Success)
            {
                return DeckOperationResult<DeckDetailDto>.Fail(validation.Error ?? "Deck validation failed.");
            }

            ReplaceCards(deckEntity, validation.Data!);
        }
        else if (request.Class.HasValue && request.Class.Value != deckEntity.Class && deckEntity.Cards.Count > 0)
        {
            var currentCards = deckEntity.Cards.Select(card => new DeckCardEntryDto
            {
                CardDefinitionId = card.CardDefinitionId,
                Count = card.Count
            }).ToList();

            var validation = await _deckValidation.ValidateAsync(targetDeckClass, currentCards, requireComplete: false, cancellationToken);
            if (!validation.Success)
            {
                return DeckOperationResult<DeckDetailDto>.Fail(validation.Error ?? "Deck validation failed.");
            }
        }

        deckEntity.Class = targetDeckClass;
        deckEntity.Environment = targetEnvironment;
        deckEntity.Touch();
        await _db.SaveChangesAsync(cancellationToken);

        return DeckOperationResult<DeckDetailDto>.Ok(ToDetailDto(deckEntity, isGuest: false));
    }

    public async Task<DeckOperationResult<bool>> DeleteAsync(DeckOwner owner, Guid deckId, CancellationToken cancellationToken)
    {
        if (owner.IsGuest)
        {
            var guestId = owner.GetGuestIdOrNull();
            if (!guestId.HasValue || guestId.Value == Guid.Empty)
            {
                return DeckOperationResult<bool>.Fail("GuestId is required.");
            }

            var deleted = _guestStore.Delete(guestId.Value, deckId);
            return deleted
                ? DeckOperationResult<bool>.Ok(true)
                : DeckOperationResult<bool>.Fail("Deck not found.");
        }

        var userId = owner.GetUserIdOrNull();
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            return DeckOperationResult<bool>.Fail("UserId is required.");
        }

        var deckEntity = await _db.UserDecks
            .FirstOrDefaultAsync(deck => deck.Id == deckId && deck.UserId == userId.Value, cancellationToken);

        if (deckEntity == null)
        {
            return DeckOperationResult<bool>.Fail("Deck not found.");
        }

        _db.UserDecks.Remove(deckEntity);
        await _db.SaveChangesAsync(cancellationToken);

        return DeckOperationResult<bool>.Ok(true);
    }

    public async Task<DeckOperationResult<string>> ExportCodeAsync(DeckOwner owner, Guid deckId, CancellationToken cancellationToken)
    {
        if (owner.IsGuest)
        {
            var guestId = owner.GetGuestIdOrNull();
            if (!guestId.HasValue || guestId.Value == Guid.Empty)
            {
                return DeckOperationResult<string>.Fail("GuestId is required.");
            }

            var deck = _guestStore.Get(guestId.Value, deckId);
            if (deck == null)
            {
                return DeckOperationResult<string>.Fail("Deck not found.");
            }

            var validation = await _deckValidation.ValidateAsync(deck.Class, deck.Cards, requireComplete: true, cancellationToken);
            if (!validation.Success)
            {
                return DeckOperationResult<string>.Fail(validation.Error ?? "Deck validation failed.");
            }

            var code = _deckCodeService.Encode(deck.Class, validation.Data!);
            return DeckOperationResult<string>.Ok(code);
        }

        var userId = owner.GetUserIdOrNull();
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            return DeckOperationResult<string>.Fail("UserId is required.");
        }

        var deckEntity = await _db.UserDecks
            .Include(deck => deck.Cards)
            .AsNoTracking()
            .FirstOrDefaultAsync(deck => deck.Id == deckId && deck.UserId == userId.Value, cancellationToken);

        if (deckEntity == null)
        {
            return DeckOperationResult<string>.Fail("Deck not found.");
        }

        var entries = deckEntity.Cards.Select(card => new DeckCardEntryDto
        {
            CardDefinitionId = card.CardDefinitionId,
            Count = card.Count
        }).ToList();

        var validationResult = await _deckValidation.ValidateAsync(deckEntity.Class, entries, requireComplete: true, cancellationToken);
        if (!validationResult.Success)
        {
            return DeckOperationResult<string>.Fail(validationResult.Error ?? "Deck validation failed.");
        }

        var deckCode = _deckCodeService.Encode(deckEntity.Class, validationResult.Data!);
        return DeckOperationResult<string>.Ok(deckCode);
    }

    public async Task<DeckOperationResult<DeckDetailDto>> ImportCodeAsync(DeckOwner owner, ImportDeckCodeRequest request, CancellationToken cancellationToken)
    {
        if (!_deckCodeService.TryDecode(request.Code, out var deckClass, out var cards, out var error))
        {
            return DeckOperationResult<DeckDetailDto>.Fail(error ?? "Deck code is invalid.");
        }

        var validation = await _deckValidation.ValidateAsync(deckClass, cards, requireComplete: true, cancellationToken);
        if (!validation.Success)
        {
            return DeckOperationResult<DeckDetailDto>.Fail(validation.Error ?? "Deck validation failed.");
        }

        var targetEnvironment = request.Environment ?? DefaultEnvironment;

        if (owner.IsGuest)
        {
            var guestId = owner.GetGuestIdOrNull();
            if (!guestId.HasValue || guestId.Value == Guid.Empty)
            {
                return DeckOperationResult<DeckDetailDto>.Fail("GuestId is required.");
            }

            if (request.TargetDeckId.HasValue)
            {
                var existing = _guestStore.Get(guestId.Value, request.TargetDeckId.Value);
                if (existing == null)
                {
                    return DeckOperationResult<DeckDetailDto>.Fail("Target deck not found.");
                }

                if (!string.IsNullOrWhiteSpace(request.Name))
                {
                    _guestStore.Rename(guestId.Value, existing.DeckId, request.Name.Trim());
                }

                var environment = request.Environment ?? existing.Environment;
                _guestStore.SaveCards(guestId.Value, existing.DeckId, deckClass, environment, validation.Data!);
                var updated = _guestStore.Get(guestId.Value, existing.DeckId) ?? existing;
                return DeckOperationResult<DeckDetailDto>.Ok(ToDetailDto(updated, isGuest: true));
            }

            var name = string.IsNullOrWhiteSpace(request.Name) ? "Imported Deck" : request.Name.Trim();
            var created = _guestStore.Create(guestId.Value, name, deckClass, targetEnvironment);
            _guestStore.SaveCards(guestId.Value, created.DeckId, deckClass, targetEnvironment, validation.Data!);
            var finalDeck = _guestStore.Get(guestId.Value, created.DeckId) ?? created;
            return DeckOperationResult<DeckDetailDto>.Ok(ToDetailDto(finalDeck, isGuest: true));
        }

        var userId = owner.GetUserIdOrNull();
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            return DeckOperationResult<DeckDetailDto>.Fail("UserId is required.");
        }

        var userExists = await _db.Users.AnyAsync(user => user.Id == userId.Value, cancellationToken);
        if (!userExists)
        {
            return DeckOperationResult<DeckDetailDto>.Fail("User does not exist.");
        }

        if (request.TargetDeckId.HasValue)
        {
            var existing = await _db.UserDecks
                .Include(deck => deck.Cards)
                .FirstOrDefaultAsync(deck => deck.Id == request.TargetDeckId.Value && deck.UserId == userId.Value, cancellationToken);

            if (existing == null)
            {
                return DeckOperationResult<DeckDetailDto>.Fail("Target deck not found.");
            }

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                existing.Name = request.Name.Trim();
            }

            existing.Class = deckClass;
            existing.Environment = request.Environment ?? existing.Environment;
            ReplaceCards(existing, validation.Data!);
            existing.Touch();
            await _db.SaveChangesAsync(cancellationToken);
            return DeckOperationResult<DeckDetailDto>.Ok(ToDetailDto(existing, isGuest: false));
        }

        var newDeck = new UserDeck
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            Name = string.IsNullOrWhiteSpace(request.Name) ? "Imported Deck" : request.Name.Trim(),
            Class = deckClass,
            Environment = targetEnvironment,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Cards = validation.Data!
                .Select(entry => new UserDeckCard
                {
                    Id = Guid.NewGuid(),
                    DeckId = Guid.Empty,
                    CardDefinitionId = entry.CardDefinitionId,
                    Count = entry.Count
                })
                .ToList()
        };

        foreach (var card in newDeck.Cards)
        {
            card.DeckId = newDeck.Id;
        }

        _db.UserDecks.Add(newDeck);
        await _db.SaveChangesAsync(cancellationToken);

        return DeckOperationResult<DeckDetailDto>.Ok(ToDetailDto(newDeck, isGuest: false));
    }

    private static DeckSummaryDto ToSummaryDto(GuestDeckRecord deck, bool isGuest)
    {
        var cardCount = deck.Cards.Sum(card => card.Count);
        return new DeckSummaryDto
        {
            DeckId = deck.DeckId,
            Name = deck.Name,
            Class = deck.Class,
            Environment = deck.Environment,
            CardCount = cardCount,
            UpdatedAt = deck.UpdatedAt,
            IsGuest = isGuest
        };
    }

    private static DeckDetailDto ToDetailDto(GuestDeckRecord deck, bool isGuest)
    {
        return new DeckDetailDto
        {
            DeckId = deck.DeckId,
            Name = deck.Name,
            Class = deck.Class,
            Environment = deck.Environment,
            CardCount = deck.Cards.Sum(card => card.Count),
            UpdatedAt = deck.UpdatedAt,
            IsGuest = isGuest,
            Cards = deck.Cards
                .Select(card => new DeckCardEntryDto
                {
                    CardDefinitionId = card.CardDefinitionId,
                    Count = card.Count
                })
                .ToList()
        };
    }

    private static DeckDetailDto ToDetailDto(UserDeck deck, bool isGuest)
    {
        var cards = deck.Cards
            .OrderBy(card => card.CardDefinitionId)
            .Select(card => new DeckCardEntryDto
            {
                CardDefinitionId = card.CardDefinitionId,
                Count = card.Count
            })
            .ToList();

        return new DeckDetailDto
        {
            DeckId = deck.Id,
            Name = deck.Name,
            Class = deck.Class,
            Environment = deck.Environment,
            CardCount = cards.Sum(card => card.Count),
            UpdatedAt = deck.UpdatedAt,
            IsGuest = isGuest,
            Cards = cards
        };
    }

    private void ReplaceCards(UserDeck deck, List<DeckCardEntryDto> cards)
    {
        if (deck.Cards.Count > 0)
        {
            _db.UserDeckCards.RemoveRange(deck.Cards);
            deck.Cards.Clear();
        }

        foreach (var entry in cards)
        {
            deck.Cards.Add(new UserDeckCard
            {
                Id = Guid.NewGuid(),
                DeckId = deck.Id,
                CardDefinitionId = entry.CardDefinitionId,
                Count = entry.Count
            });
        }
    }

    private async Task<DefaultDeckPlan?> BuildDefaultDeckPlanAsync(CancellationToken cancellationToken)
    {
        var cards = await _db.CardDefinitions
            .AsNoTracking()
            .Where(card => card.Id > 0 && card.Type != CardType.Leader)
            .Select(card => new { card.Id, card.Class })
            .ToListAsync(cancellationToken);

        if (cards.Count == 0)
        {
            return null;
        }

        DefaultDeckPlan? fallback = null;
        var fallbackCount = 0;

        foreach (var deckClass in Enum.GetValues<CardClass>())
        {
            var candidateIds = cards
                .Where(card => card.Class == CardClass.Neutral || card.Class == deckClass)
                .Select(card => card.Id)
                .Distinct()
                .ToList();

            if (candidateIds.Count == 0)
            {
                continue;
            }

            var maxPossibleCount = candidateIds.Count * GameConstants.Cards.MaxCopiesPerCard;
            if (maxPossibleCount <= 0)
            {
                continue;
            }

            var ordered = OrderByDefaultPriority(candidateIds);
            var targetSize = Math.Min(GameConstants.Cards.StandardDeckSize, maxPossibleCount);
            var entries = BuildDeckEntries(ordered, targetSize);
            if (entries == null)
            {
                continue;
            }

            if (deckClass == CardClass.Neutral)
            {
                return new DefaultDeckPlan(deckClass, entries);
            }

            if (fallback == null || candidateIds.Count > fallbackCount)
            {
                fallback = new DefaultDeckPlan(deckClass, entries);
                fallbackCount = candidateIds.Count;
            }
        }

        return fallback;
    }

    private static List<int> OrderByDefaultPriority(IReadOnlyCollection<int> cardIds)
    {
        return cardIds
            .Distinct()
            .OrderBy(cardId => DefaultDeckPriority.TryGetValue(cardId, out var order) ? order : int.MaxValue)
            .ThenBy(cardId => cardId)
            .ToList();
    }

    private static List<DeckCardEntryDto>? BuildDeckEntries(IReadOnlyList<int> orderedCardIds, int targetSize)
    {
        var entries = new List<DeckCardEntryDto>();
        var remaining = targetSize;

        foreach (var cardId in orderedCardIds)
        {
            if (remaining <= 0)
            {
                break;
            }

            var count = Math.Min(GameConstants.Cards.MaxCopiesPerCard, remaining);
            entries.Add(new DeckCardEntryDto
            {
                CardDefinitionId = cardId,
                Count = count
            });
            remaining -= count;
        }

        return remaining == 0 ? entries : null;
    }

    private sealed record DefaultDeckPlan(CardClass DeckClass, List<DeckCardEntryDto> Cards);

}
