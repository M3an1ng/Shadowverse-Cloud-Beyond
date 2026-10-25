using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private const DeckEnvironment DefaultEnvironment = DeckEnvironment.Rotation;
    private const DeckRule DefaultRule = DeckRule.Standard;
    private static readonly int[] DefaultDeckSeedCardIds = { 1001, 1002, 1005, 1006, 2001, 2002, 1003, 1004 };
    private static readonly Dictionary<int, int> DefaultDeckPriority = DefaultDeckSeedCardIds
        .Select((cardId, index) => new { cardId, index })
        .ToDictionary(entry => entry.cardId, entry => entry.index);

    private readonly ApplicationDbContext _db;
    private readonly IGuestDeckStore _guestStore;
    private readonly IDeckCodeService _deckCodeService;
    private readonly IDeckValidationService _deckValidation;
    private readonly ILogger<DeckService> _logger;

    public DeckService(
        ApplicationDbContext db,
        IGuestDeckStore guestStore,
        IDeckCodeService deckCodeService,
        IDeckValidationService deckValidation,
        ILogger<DeckService> logger)
    {
        _db = db;
        _guestStore = guestStore;
        _deckCodeService = deckCodeService;
        _deckValidation = deckValidation;
        _logger = logger;
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
            Rule = DefaultRule,
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
                SecondaryClass = deck.SecondaryClass,
                Environment = deck.Environment,
                Rule = deck.Rule,
                CardCount = deck.Cards.Sum(card => card.Count),
                UpdatedAt = deck.UpdatedAt,
                IsGuest = false
            })
            .ToListAsync(cancellationToken);

        if (decks.Count == 0)
        {
            return decks;
        }

        var deckIds = decks.Select(deck => deck.DeckId).ToList();
        var statsByDeckId = await _db.GameParticipantStatsFacts
            .AsNoTracking()
            .Where(fact =>
                fact.IsOfficial &&
                fact.UserDeckId.HasValue &&
                deckIds.Contains(fact.UserDeckId.Value))
            .GroupBy(fact => fact.UserDeckId!.Value)
            .Select(group => new
            {
                DeckId = group.Key,
                TotalGames = group.Count(),
                TotalWins = group.Count(fact => fact.IsWinner),
                GoingFirstGames = group.Count(fact => fact.IsFirstPlayer == true),
                GoingFirstWins = group.Count(fact => fact.IsFirstPlayer == true && fact.IsWinner),
                GoingSecondGames = group.Count(fact => fact.IsFirstPlayer == false),
                GoingSecondWins = group.Count(fact => fact.IsFirstPlayer == false && fact.IsWinner)
            })
            .ToDictionaryAsync(item => item.DeckId, cancellationToken);

        var enriched = decks.Select(deck =>
        {
            if (!statsByDeckId.TryGetValue(deck.DeckId, out var stats))
            {
                return deck;
            }

            return deck with
            {
                TotalGames = stats.TotalGames,
                TotalWins = stats.TotalWins,
                GoingFirstGames = stats.GoingFirstGames,
                GoingFirstWins = stats.GoingFirstWins,
                GoingSecondGames = stats.GoingSecondGames,
                GoingSecondWins = stats.GoingSecondWins
            };
        }).ToList();

        return enriched;

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

        var environment = request.Environment ?? DefaultEnvironment;
        var rule = request.Rule ?? DefaultRule;
        if (!DeckValidationService.TryResolveSecondaryClass(request.Class, rule, request.SecondaryClass, out var secondaryClass, out var formatError, requireComplete: false))
        {
            return DeckOperationResult<DeckDetailDto>.Fail(formatError ?? "Deck format is invalid.");
        }

        var ownerId = owner.IsGuest ? owner.GetGuestIdOrNull() : owner.GetUserIdOrNull();
        if (!ownerId.HasValue || ownerId.Value == Guid.Empty)
        {
            return DeckOperationResult<DeckDetailDto>.Fail(owner.IsGuest ? "GuestId is required." : "UserId is required.");
        }

        if (!owner.IsGuest && !await _db.Users.AnyAsync(user => user.Id == ownerId.Value, cancellationToken))
        {
            return DeckOperationResult<DeckDetailDto>.Fail("User does not exist.");
        }

        var cards = new List<DeckCardEntryDto>();
        if (request.Cards is { Count: > 0 })
        {
            var validation = await _deckValidation.ValidateAsync(request.Class, secondaryClass, request.Cards, environment, rule, requireComplete: false, cancellationToken);
            if (!validation.Success)
            {
                return DeckOperationResult<DeckDetailDto>.Fail(validation.Error ?? "Deck validation failed.");
            }

            cards = validation.Data!;
        }

        if (owner.IsGuest)
        {
            var deck = _guestStore.Create(ownerId.Value, request.Name.Trim(), request.Class, secondaryClass, environment, rule, cards);
            return DeckOperationResult<DeckDetailDto>.Ok(ToDetailDto(deck, isGuest: true));
        }

        var deckEntity = new UserDeck
        {
            Id = Guid.NewGuid(),
            UserId = ownerId.Value,
            Name = request.Name.Trim(),
            Class = request.Class,
            SecondaryClass = secondaryClass,
            Environment = environment,
            Rule = rule,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        deckEntity.Cards = cards.Select(entry => ToDeckCard(deckEntity.Id, entry)).ToList();

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

            var targetClass = request.Class ?? existing.Class;
            var guestEnvironment = request.Environment ?? existing.Environment;
            var guestRule = request.Rule ?? existing.Rule;
            if (!DeckValidationService.TryResolveSecondaryClass(targetClass, guestRule, request.SecondaryClass ?? existing.SecondaryClass, out var guestTargetSecondaryClass, out var guestFormatError, requireComplete: false))
            {
                return DeckOperationResult<DeckDetailDto>.Fail(guestFormatError ?? "Deck format is invalid.");
            }

            var cards = request.Cards ?? existing.Cards;
            if (request.Cards != null || ((request.Class.HasValue || request.SecondaryClass.HasValue || request.Rule.HasValue) && cards.Count > 0))
            {
                var validation = await _deckValidation.ValidateAsync(targetClass, guestTargetSecondaryClass, cards, guestEnvironment, guestRule, requireComplete: false, cancellationToken);
                if (!validation.Success)
                {
                    return DeckOperationResult<DeckDetailDto>.Fail(validation.Error ?? "Deck validation failed.");
                }

                cards = validation.Data!;
            }

            var updated = _guestStore.Update(guestId.Value, existing with
            {
                Name = string.IsNullOrWhiteSpace(request.Name) ? existing.Name : request.Name.Trim(),
                Class = targetClass,
                SecondaryClass = guestTargetSecondaryClass,
                Environment = guestEnvironment,
                Rule = guestRule,
                Cards = cards
            });
            return updated == null
                ? DeckOperationResult<DeckDetailDto>.Fail("Deck not found.")
                : DeckOperationResult<DeckDetailDto>.Ok(ToDetailDto(updated, isGuest: true));
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

        var previousDeckShapeHash = BuildDeckShapeHash(deckEntity.Class, deckEntity.SecondaryClass, deckEntity.Rule, deckEntity.Cards);
        var nextDeckShapeHash = previousDeckShapeHash;

        var ownerId = owner.IsGuest ? owner.GetGuestIdOrNull() : owner.GetUserIdOrNull();
        var incomingEntries = request.Cards?.Count ?? 0;
        var incomingTotal = request.Cards?.Sum(card => card.Count);
        _logger.LogInformation(
            "Deck update requested. DeckId={DeckId} OwnerType={OwnerType} OwnerId={OwnerId} ExistingEntries={ExistingEntries} IncomingEntries={IncomingEntries} IncomingTotal={IncomingTotal}",
            deckId,
            owner.IsGuest ? "Guest" : "User",
            ownerId,
            deckEntity.Cards.Count,
            incomingEntries,
            incomingTotal);

        var targetEnvironment = request.Environment ?? deckEntity.Environment;
        var targetDeckClass = request.Class ?? deckEntity.Class;
        var targetRule = request.Rule ?? deckEntity.Rule;
        if (!DeckValidationService.TryResolveSecondaryClass(targetDeckClass, targetRule, request.SecondaryClass ?? deckEntity.SecondaryClass, out var targetSecondaryClass, out var targetFormatError, requireComplete: false))
        {
            return DeckOperationResult<DeckDetailDto>.Fail(targetFormatError ?? "Deck format is invalid.");
        }
        if (request.Cards != null)
        {
            var validation = await _deckValidation.ValidateAsync(targetDeckClass, targetSecondaryClass, request.Cards, targetEnvironment, targetRule, requireComplete: false, cancellationToken);
            if (!validation.Success)
            {
                return DeckOperationResult<DeckDetailDto>.Fail(validation.Error ?? "Deck validation failed.");
            }

            nextDeckShapeHash = BuildDeckShapeHash(targetDeckClass, targetSecondaryClass, targetRule, validation.Data!);
            ReplaceCards(deckEntity, validation.Data!);
        }
        else if ((request.Class.HasValue || request.SecondaryClass.HasValue || request.Rule.HasValue) && deckEntity.Cards.Count > 0)
        {
            var currentCards = deckEntity.Cards.Select(card => new DeckCardEntryDto
            {
                CardDefinitionId = card.CardDefinitionId,
                Count = card.Count
            }).ToList();

            var validation = await _deckValidation.ValidateAsync(targetDeckClass, targetSecondaryClass, currentCards, targetEnvironment, targetRule, requireComplete: false, cancellationToken);
            if (!validation.Success)
            {
                return DeckOperationResult<DeckDetailDto>.Fail(validation.Error ?? "Deck validation failed.");
            }

            nextDeckShapeHash = BuildDeckShapeHash(targetDeckClass, targetSecondaryClass, targetRule, deckEntity.Cards);
        }
        else if ((request.Class.HasValue || request.SecondaryClass.HasValue || request.Rule.HasValue) && deckEntity.Cards.Count == 0)
        {
            nextDeckShapeHash = BuildDeckShapeHash(targetDeckClass, targetSecondaryClass, targetRule, Array.Empty<DeckCardEntryDto>());
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            deckEntity.Name = request.Name.Trim();
        }

        deckEntity.Class = targetDeckClass;
        deckEntity.SecondaryClass = targetSecondaryClass;
        deckEntity.Environment = targetEnvironment;
        deckEntity.Rule = targetRule;
        if (!string.Equals(previousDeckShapeHash, nextDeckShapeHash, StringComparison.Ordinal))
        {
            var linkedFacts = await _db.GameParticipantStatsFacts
                .Where(item => item.UserDeckId == deckEntity.Id)
                .ToListAsync(cancellationToken);

            if (linkedFacts.Count > 0)
            {
                var linkedHashes = linkedFacts
                    .Select(item => item.DeckShapeHash)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                // Guard against false positives: if historical linked hash already equals current shape,
                // do not reset this deck's winrate history.
                var shouldResetLinkedFacts = linkedHashes.Count == 0 ||
                    linkedHashes.Any(item => !string.Equals(item, nextDeckShapeHash, StringComparison.Ordinal));
                if (shouldResetLinkedFacts)
                {
                    foreach (var fact in linkedFacts)
                    {
                        fact.UserDeckId = null;
                    }

                    _logger.LogInformation(
                        "Deck shape changed and detached historical facts from UserDeckId. DeckId={DeckId} UserId={UserId} DetachedFacts={DetachedFacts}",
                        deckEntity.Id,
                        userId.Value,
                        linkedFacts.Count);
                }
            }
        }

        deckEntity.Touch();
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var deckStillExists = await _db.UserDecks
                .AsNoTracking()
                .AnyAsync(deck => deck.Id == deckId && deck.UserId == userId.Value, cancellationToken);
            var dbCardCount = await _db.UserDeckCards
                .AsNoTracking()
                .CountAsync(card => card.DeckId == deckId, cancellationToken);
            var trackedStates = _db.ChangeTracker.Entries()
                .Select(entry => $"{entry.Entity.GetType().Name}:{entry.State}")
                .ToArray();

            _logger.LogError(
                ex,
                "Deck update concurrency conflict. DeckId={DeckId} OwnerType={OwnerType} OwnerId={OwnerId} ExistingEntries={ExistingEntries} IncomingEntries={IncomingEntries} IncomingTotal={IncomingTotal} DeckExists={DeckExists} DbCardCount={DbCardCount} TrackedStates={TrackedStates}",
                deckId,
                owner.IsGuest ? "Guest" : "User",
                ownerId,
                deckEntity.Cards.Count,
                incomingEntries,
                incomingTotal,
                deckStillExists,
                dbCardCount,
                string.Join(", ", trackedStates));
            return DeckOperationResult<DeckDetailDto>.Fail("Deck update conflict. Please reload and try again.");
        }

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

            var validation = await _deckValidation.ValidateAsync(deck.Class, deck.SecondaryClass, deck.Cards, deck.Environment, deck.Rule, requireComplete: true, cancellationToken);
            if (!validation.Success)
            {
                return DeckOperationResult<string>.Fail(validation.Error ?? "Deck validation failed.");
            }

            var code = _deckCodeService.Encode(deck.Class, deck.SecondaryClass, deck.Environment, deck.Rule, validation.Data!);
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

        var validationResult = await _deckValidation.ValidateAsync(deckEntity.Class, deckEntity.SecondaryClass, entries, deckEntity.Environment, deckEntity.Rule, requireComplete: true, cancellationToken);
        if (!validationResult.Success)
        {
            return DeckOperationResult<string>.Fail(validationResult.Error ?? "Deck validation failed.");
        }

        var deckCode = _deckCodeService.Encode(deckEntity.Class, deckEntity.SecondaryClass, deckEntity.Environment, deckEntity.Rule, validationResult.Data!);
        return DeckOperationResult<string>.Ok(deckCode);
    }

    public async Task<DeckOperationResult<DeckDetailDto>> ImportCodeAsync(DeckOwner owner, ImportDeckCodeRequest request, CancellationToken cancellationToken)
    {
        if (!_deckCodeService.TryDecode(request.Code, out var deckClass, out var decodedSecondaryClass, out var decodedEnvironment, out var decodedRule, out var cards, out var error))
        {
            return DeckOperationResult<DeckDetailDto>.Fail(error ?? "Deck code is invalid.");
        }

        var ownerId = owner.IsGuest ? owner.GetGuestIdOrNull() : owner.GetUserIdOrNull();
        if (!ownerId.HasValue || ownerId.Value == Guid.Empty)
        {
            return DeckOperationResult<DeckDetailDto>.Fail(owner.IsGuest ? "GuestId is required." : "UserId is required.");
        }

        if (!owner.IsGuest && !await _db.Users.AnyAsync(user => user.Id == ownerId.Value, cancellationToken))
        {
            return DeckOperationResult<DeckDetailDto>.Fail("User does not exist.");
        }

        GuestDeckRecord? guestDeck = null;
        UserDeck? userDeck = null;
        if (request.TargetDeckId.HasValue)
        {
            if (owner.IsGuest)
            {
                guestDeck = _guestStore.Get(ownerId.Value, request.TargetDeckId.Value);
            }
            else
            {
                userDeck = await _db.UserDecks.Include(deck => deck.Cards)
                    .FirstOrDefaultAsync(deck => deck.Id == request.TargetDeckId.Value && deck.UserId == ownerId.Value, cancellationToken);
            }

            if (guestDeck == null && userDeck == null)
            {
                return DeckOperationResult<DeckDetailDto>.Fail("Target deck not found.");
            }
        }

        var environment = request.Environment ?? decodedEnvironment ?? guestDeck?.Environment ?? userDeck?.Environment ?? DefaultEnvironment;
        var rule = request.Rule ?? decodedRule ?? guestDeck?.Rule ?? userDeck?.Rule ?? DefaultRule;
        var requestedSecondaryClass = request.SecondaryClass ?? decodedSecondaryClass ?? guestDeck?.SecondaryClass ?? userDeck?.SecondaryClass;
        if (!DeckValidationService.TryResolveSecondaryClass(deckClass, rule, requestedSecondaryClass, out var secondaryClass, out var formatError))
        {
            return DeckOperationResult<DeckDetailDto>.Fail(formatError ?? "Deck format is invalid.");
        }

        var validation = await _deckValidation.ValidateAsync(deckClass, secondaryClass, cards, environment, rule, requireComplete: true, cancellationToken);
        if (!validation.Success)
        {
            return DeckOperationResult<DeckDetailDto>.Fail(validation.Error ?? "Deck validation failed.");
        }

        var name = string.IsNullOrWhiteSpace(request.Name)
            ? guestDeck?.Name ?? userDeck?.Name ?? "Imported Deck"
            : request.Name.Trim();
        if (owner.IsGuest)
        {
            if (guestDeck == null)
            {
                var created = _guestStore.Create(ownerId.Value, name, deckClass, secondaryClass, environment, rule, validation.Data!);
                return DeckOperationResult<DeckDetailDto>.Ok(ToDetailDto(created, isGuest: true));
            }

            var updated = _guestStore.Update(ownerId.Value, guestDeck with
            {
                Name = name,
                Class = deckClass,
                SecondaryClass = secondaryClass,
                Environment = environment,
                Rule = rule,
                Cards = validation.Data!
            });
            return updated == null
                ? DeckOperationResult<DeckDetailDto>.Fail("Target deck not found.")
                : DeckOperationResult<DeckDetailDto>.Ok(ToDetailDto(updated, isGuest: true));
        }

        if (userDeck == null)
        {
            userDeck = new UserDeck { Id = Guid.NewGuid(), UserId = ownerId.Value, CreatedAt = DateTime.UtcNow };
            userDeck.Cards = validation.Data!.Select(entry => ToDeckCard(userDeck.Id, entry)).ToList();
            _db.UserDecks.Add(userDeck);
        }
        else
        {
            ReplaceCards(userDeck, validation.Data!);
        }

        userDeck.Name = name;
        userDeck.Class = deckClass;
        userDeck.SecondaryClass = secondaryClass;
        userDeck.Environment = environment;
        userDeck.Rule = rule;
        userDeck.Touch();
        await _db.SaveChangesAsync(cancellationToken);
        return DeckOperationResult<DeckDetailDto>.Ok(ToDetailDto(userDeck, isGuest: false));
    }

    private static DeckSummaryDto ToSummaryDto(GuestDeckRecord deck, bool isGuest)
    {
        var cardCount = deck.Cards.Sum(card => card.Count);
        return new DeckSummaryDto
        {
            DeckId = deck.DeckId,
            Name = deck.Name,
            Class = deck.Class,
            SecondaryClass = deck.SecondaryClass,
            Environment = deck.Environment,
            Rule = deck.Rule,
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
            SecondaryClass = deck.SecondaryClass,
            Environment = deck.Environment,
            Rule = deck.Rule,
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
            SecondaryClass = deck.SecondaryClass,
            Environment = deck.Environment,
            Rule = deck.Rule,
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

        if (cards.Count == 0)
        {
            return;
        }

        var newCards = cards.Select(entry => ToDeckCard(deck.Id, entry)).ToList();

        _db.UserDeckCards.AddRange(newCards);
        deck.Cards.AddRange(newCards);
    }

    private static UserDeckCard ToDeckCard(Guid deckId, DeckCardEntryDto entry) => new()
    {
        Id = Guid.NewGuid(),
        DeckId = deckId,
        CardDefinitionId = entry.CardDefinitionId,
        Count = entry.Count
    };

    private async Task<DefaultDeckPlan?> BuildDefaultDeckPlanAsync(CancellationToken cancellationToken)
    {
        var cards = await _db.CardDefinitions
            .AsNoTracking()
            .Where(card => card.Id > 0 && card.Type != CardType.Leader && card.IsCollectible)
            .Select(card => new { card.Id, card.Class })
            .ToListAsync(cancellationToken);

        if (cards.Count == 0)
        {
            return null;
        }

        var defaultCopyLimitByCardId = await _db.DeckCardCopyLimitPolicies
            .AsNoTracking()
            .Where(item => item.Environment == DefaultEnvironment && item.Rule == DefaultRule)
            .ToDictionaryAsync(item => item.CardDefinitionId, item => item.MaxCopies, cancellationToken);

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

            var legalCandidateIds = candidateIds
                .Where(cardId => ResolveDeckCardCopyLimit(defaultCopyLimitByCardId, cardId) > 0)
                .ToList();
            if (legalCandidateIds.Count == 0)
            {
                continue;
            }

            var maxPossibleCount = legalCandidateIds
                .Sum(cardId => ResolveDeckCardCopyLimit(defaultCopyLimitByCardId, cardId));
            if (maxPossibleCount < GameConstants.Cards.StandardDeckSize)
            {
                continue;
            }

            var ordered = OrderByDefaultPriority(legalCandidateIds);
            var targetSize = GameConstants.Cards.StandardDeckSize;
            var entries = BuildDeckEntries(ordered, targetSize, defaultCopyLimitByCardId);
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

    private static List<DeckCardEntryDto>? BuildDeckEntries(
        IReadOnlyList<int> orderedCardIds,
        int targetSize,
        IReadOnlyDictionary<int, int> copyLimitByCardId)
    {
        var entries = new List<DeckCardEntryDto>();
        var remaining = targetSize;

        foreach (var cardId in orderedCardIds)
        {
            if (remaining <= 0)
            {
                break;
            }

            var maxCopies = ResolveDeckCardCopyLimit(copyLimitByCardId, cardId);
            if (maxCopies <= 0)
            {
                continue;
            }

            var count = Math.Min(maxCopies, remaining);
            entries.Add(new DeckCardEntryDto
            {
                CardDefinitionId = cardId,
                Count = count
            });
            remaining -= count;
        }

        return remaining == 0 ? entries : null;
    }

    private static int ResolveDeckCardCopyLimit(IReadOnlyDictionary<int, int> copyLimitByCardId, int cardId)
    {
        if (!copyLimitByCardId.TryGetValue(cardId, out var configured))
        {
            return GameConstants.Cards.MaxCopiesPerCard;
        }

        return Math.Clamp(configured, 0, GameConstants.Cards.MaxCopiesPerCard);
    }

    private static string BuildDeckShapeHash(CardClass deckClass, CardClass? secondaryClass, DeckRule rule, IEnumerable<UserDeckCard> cards) =>
        BuildDeckShapeHash(deckClass, secondaryClass, rule, cards.Select(card => new DeckCardEntryDto
        {
            CardDefinitionId = card.CardDefinitionId,
            Count = card.Count
        }));

    private static string BuildDeckShapeHash(CardClass deckClass, CardClass? secondaryClass, DeckRule rule, IEnumerable<DeckCardEntryDto> cards)
    {
        var payload = string.Join(",",
            cards
                .Where(card => card.Count > 0)
                .GroupBy(card => card.CardDefinitionId)
                .OrderBy(group => group.Key)
                .Select(group => $"{group.Key}:{group.Sum(item => item.Count)}"));

        var raw = $"{deckClass}|{secondaryClass?.ToString() ?? string.Empty}|{rule}|{payload}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    private sealed record DefaultDeckPlan(CardClass DeckClass, List<DeckCardEntryDto> Cards);

}



