using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

public sealed class GuestDeckMigrationService : IGuestDeckMigrationService
{
    private readonly IGuestDeckStore _guestDeckStore;
    private readonly IDeckService _deckService;
    private readonly ILogger<GuestDeckMigrationService> _logger;

    public GuestDeckMigrationService(
        IGuestDeckStore guestDeckStore,
        IDeckService deckService,
        ILogger<GuestDeckMigrationService> logger)
    {
        _guestDeckStore = guestDeckStore;
        _deckService = deckService;
        _logger = logger;
    }

    public async Task<GuestDeckMigrationResult> MigrateAsync(Guid guestId, Guid userId, CancellationToken cancellationToken)
    {
        if (guestId == Guid.Empty || userId == Guid.Empty)
        {
            return new GuestDeckMigrationResult(0, 0);
        }

        var decks = _guestDeckStore.List(guestId);
        if (decks.Count == 0)
        {
            return new GuestDeckMigrationResult(0, 0);
        }

        var migrated = 0;
        var failed = 0;
        var owner = new DeckOwner(userId, null);

        foreach (var deck in decks)
        {
            var request = new CreateDeckRequest
            {
                Name = string.IsNullOrWhiteSpace(deck.Name) ? "Migrated Deck" : deck.Name,
                Class = deck.Class,
                Environment = deck.Environment,
                Cards = deck.Cards?.Select(card => new DeckCardEntryDto
                {
                    CardDefinitionId = card.CardDefinitionId,
                    Count = card.Count
                }).ToList()
            };

            var result = await _deckService.CreateAsync(owner, request, cancellationToken);
            if (result.Success)
            {
                migrated++;
                _guestDeckStore.Delete(guestId, deck.DeckId);
            }
            else
            {
                failed++;
                _logger.LogWarning(
                    "Failed to migrate guest deck {DeckId} for guest {GuestId}: {Error}",
                    deck.DeckId,
                    guestId,
                    result.Error ?? "Unknown error");
            }
        }

        return new GuestDeckMigrationResult(migrated, failed);
    }
}
