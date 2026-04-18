using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public sealed class DeckCopyLimitPolicyIntegrationTests
{
    [Fact]
    public async Task ValidateAsync_WithoutPolicy_FallsBackToThreeCopies()
    {
        await using var harness = await DeckHarness.CreateAsync();
        await harness.SeedCardAsync(1001, CardClass.Forestcraft);

        var result = await harness.ValidationService.ValidateAsync(
            CardClass.Forestcraft,
            secondaryClass: null,
            new List<DeckCardEntryDto>
            {
                new() { CardDefinitionId = 1001, Count = 3 }
            },
            DeckEnvironment.Open,
            DeckRule.Standard,
            requireComplete: false,
            CancellationToken.None);

        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public async Task ValidateAsync_WithLimitOne_RejectsOverflowWithCardName()
    {
        await using var harness = await DeckHarness.CreateAsync();
        await harness.SeedCardAsync(1001, CardClass.Forestcraft);
        await harness.UpsertPolicyAsync(DeckEnvironment.Open, DeckRule.Standard, 1001, 1);

        var result = await harness.ValidationService.ValidateAsync(
            CardClass.Forestcraft,
            secondaryClass: null,
            new List<DeckCardEntryDto>
            {
                new() { CardDefinitionId = 1001, Count = 2 }
            },
            DeckEnvironment.Open,
            DeckRule.Standard,
            requireComplete: false,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Card-1001", result.Error ?? string.Empty);
        Assert.Contains("copy limit 1", result.Error ?? string.Empty);
    }

    [Fact]
    public async Task ValidateAsync_WithLimitZero_RejectsBannedCard()
    {
        await using var harness = await DeckHarness.CreateAsync();
        await harness.SeedCardAsync(1001, CardClass.Forestcraft);
        await harness.UpsertPolicyAsync(DeckEnvironment.Open, DeckRule.Standard, 1001, 0);
        Assert.True(await harness.Db.DeckCardCopyLimitPolicies
            .AsNoTracking()
            .AnyAsync(item =>
                item.Environment == DeckEnvironment.Open &&
                item.Rule == DeckRule.Standard &&
                item.CardDefinitionId == 1001 &&
                item.MaxCopies == 0));

        var result = await harness.ValidationService.ValidateAsync(
            CardClass.Forestcraft,
            secondaryClass: null,
            new List<DeckCardEntryDto>
            {
                new() { CardDefinitionId = 1001, Count = 1 }
            },
            DeckEnvironment.Open,
            DeckRule.Standard,
            requireComplete: false,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("banned", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_WithLimitTwo_RejectsOverflow()
    {
        await using var harness = await DeckHarness.CreateAsync();
        await harness.SeedCardAsync(1001, CardClass.Forestcraft);
        await harness.UpsertPolicyAsync(DeckEnvironment.Open, DeckRule.Standard, 1001, 2);

        var result = await harness.ValidationService.ValidateAsync(
            CardClass.Forestcraft,
            secondaryClass: null,
            new List<DeckCardEntryDto>
            {
                new() { CardDefinitionId = 1001, Count = 3 }
            },
            DeckEnvironment.Open,
            DeckRule.Standard,
            requireComplete: false,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("copy limit 2", result.Error ?? string.Empty);
    }

    [Fact]
    public async Task ValidateAsync_WithLimitThree_AllowsThreeAndRejectsFour()
    {
        await using var harness = await DeckHarness.CreateAsync();
        await harness.SeedCardAsync(1001, CardClass.Forestcraft);
        await harness.UpsertPolicyAsync(DeckEnvironment.Open, DeckRule.Standard, 1001, 3);

        var legal = await harness.ValidationService.ValidateAsync(
            CardClass.Forestcraft,
            secondaryClass: null,
            new List<DeckCardEntryDto>
            {
                new() { CardDefinitionId = 1001, Count = 3 }
            },
            DeckEnvironment.Open,
            DeckRule.Standard,
            requireComplete: false,
            CancellationToken.None);

        Assert.True(legal.Success, legal.Error);

        var overflow = await harness.ValidationService.ValidateAsync(
            CardClass.Forestcraft,
            secondaryClass: null,
            new List<DeckCardEntryDto>
            {
                new() { CardDefinitionId = 1001, Count = 4 }
            },
            DeckEnvironment.Open,
            DeckRule.Standard,
            requireComplete: false,
            CancellationToken.None);

        Assert.False(overflow.Success);
        Assert.Contains("copy limit 3", overflow.Error ?? string.Empty);
    }

    [Fact]
    public async Task DeckService_CreateUpdateImport_UseSameCopyLimitValidation()
    {
        await using var harness = await DeckHarness.CreateAsync();
        var userId = await harness.SeedUserAsync();

        var limitedCardId = 2001;
        await harness.SeedCardAsync(limitedCardId, CardClass.Forestcraft);
        await harness.UpsertPolicyAsync(DeckEnvironment.Open, DeckRule.Standard, limitedCardId, 1);

        var legalEntries = await harness.SeedDeckEntriesAsync(
            CardClass.Forestcraft,
            totalCount: 39,
            startCardId: 2100);
        legalEntries.Insert(0, new DeckCardEntryDto { CardDefinitionId = limitedCardId, Count = 1 });

        var created = await harness.DeckService.CreateAsync(
            new DeckOwner(userId, null),
            new CreateDeckRequest
            {
                Name = "Policy baseline",
                Class = CardClass.Forestcraft,
                Environment = DeckEnvironment.Open,
                Rule = DeckRule.Standard,
                Cards = legalEntries
            },
            CancellationToken.None);
        Assert.True(created.Success, created.Error);

        var overflowEntries = await harness.SeedDeckEntriesAsync(
            CardClass.Forestcraft,
            totalCount: 38,
            startCardId: 2300);
        overflowEntries.Insert(0, new DeckCardEntryDto { CardDefinitionId = limitedCardId, Count = 2 });

        var createOverflow = await harness.DeckService.CreateAsync(
            new DeckOwner(userId, null),
            new CreateDeckRequest
            {
                Name = "Policy overflow",
                Class = CardClass.Forestcraft,
                Environment = DeckEnvironment.Open,
                Rule = DeckRule.Standard,
                Cards = overflowEntries
            },
            CancellationToken.None);
        Assert.False(createOverflow.Success);
        Assert.Contains("copy limit 1", createOverflow.Error ?? string.Empty);

        var updateOverflow = await harness.DeckService.UpdateAsync(
            new DeckOwner(userId, null),
            created.Data!.DeckId,
            new UpdateDeckRequest
            {
                Cards = new List<DeckCardEntryDto>
                {
                    new() { CardDefinitionId = limitedCardId, Count = 2 }
                }
            },
            CancellationToken.None);
        Assert.False(updateOverflow.Success);
        Assert.Contains("copy limit 1", updateOverflow.Error ?? string.Empty);

        var importCode = harness.DeckCodeService.Encode(
            CardClass.Forestcraft,
            secondaryClass: null,
            DeckEnvironment.Open,
            DeckRule.Standard,
            overflowEntries);
        var importOverflow = await harness.DeckService.ImportCodeAsync(
            new DeckOwner(userId, null),
            new ImportDeckCodeRequest
            {
                Name = "Policy import overflow",
                Code = importCode
            },
            CancellationToken.None);
        Assert.False(importOverflow.Success);
        Assert.Contains("copy limit 1", importOverflow.Error ?? string.Empty);

        var exportOverflow = await harness.DeckService.ExportCodeAsync(
            new DeckOwner(userId, null),
            created.Data!.DeckId,
            CancellationToken.None);
        Assert.True(exportOverflow.Success, exportOverflow.Error);

        await harness.UpsertPolicyAsync(DeckEnvironment.Open, DeckRule.Standard, limitedCardId, 0);
        var exportAfterBan = await harness.DeckService.ExportCodeAsync(
            new DeckOwner(userId, null),
            created.Data!.DeckId,
            CancellationToken.None);
        Assert.False(exportAfterBan.Success);
        Assert.Contains("banned", exportAfterBan.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnsureDefaultDeckAsync_WithRestrictiveLimits_FailsWithoutCreatingDeck()
    {
        await using var harness = await DeckHarness.CreateAsync();
        var userId = await harness.SeedUserAsync();

        var cardIds = new List<int>();
        for (var i = 0; i < 20; i++)
        {
            var cardId = 3000 + i;
            cardIds.Add(cardId);
            await harness.SeedCardAsync(cardId, CardClass.Neutral);
            await harness.UpsertPolicyAsync(DeckEnvironment.Open, DeckRule.Standard, cardId, 1);
        }

        var ensured = await harness.DeckService.EnsureDefaultDeckAsync(
            new DeckOwner(userId, null),
            CancellationToken.None);

        Assert.False(ensured.Success);
        Assert.Contains("No available cards", ensured.Error ?? string.Empty);
        Assert.Equal(0, await harness.Db.UserDecks.CountAsync());
    }

    [Fact]
    public async Task EnsureDefaultDeckAsync_WithoutLimits_KeepsLegacyThreeCopyBehavior()
    {
        await using var harness = await DeckHarness.CreateAsync();
        var userId = await harness.SeedUserAsync();

        for (var i = 0; i < 20; i++)
        {
            await harness.SeedCardAsync(4000 + i, CardClass.Neutral);
        }

        var ensured = await harness.DeckService.EnsureDefaultDeckAsync(
            new DeckOwner(userId, null),
            CancellationToken.None);

        Assert.True(ensured.Success, ensured.Error);
        Assert.True(ensured.Data);

        var deck = await harness.Db.UserDecks
            .Include(item => item.Cards)
            .SingleAsync(item => item.UserId == userId);
        Assert.Equal(40, deck.Cards.Sum(item => item.Count));
        Assert.All(deck.Cards, item => Assert.InRange(item.Count, 1, 3));
    }

    private sealed class DeckHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private DeckHarness(
            SqliteConnection connection,
            ApplicationDbContext db,
            DeckService deckService,
            DeckValidationService validationService,
            DeckCodeService deckCodeService)
        {
            _connection = connection;
            Db = db;
            DeckService = deckService;
            ValidationService = validationService;
            DeckCodeService = deckCodeService;
        }

        public ApplicationDbContext Db { get; }
        public DeckService DeckService { get; }
        public DeckValidationService ValidationService { get; }
        public DeckCodeService DeckCodeService { get; }

        public static async Task<DeckHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new ApplicationDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var deckCodeService = new DeckCodeService();
            var validationService = new DeckValidationService(db);
            var deckService = new DeckService(
                db,
                new Mock<IGuestDeckStore>().Object,
                deckCodeService,
                validationService,
                NullLogger<DeckService>.Instance);

            return new DeckHarness(connection, db, deckService, validationService, deckCodeService);
        }

        public async Task<Guid> SeedUserAsync()
        {
            var userId = Guid.NewGuid();
            Db.Users.Add(new User
            {
                Id = userId,
                Username = $"u-{userId:N}",
                Email = $"u-{userId:N}@example.com",
                PasswordHash = "hash"
            });
            await Db.SaveChangesAsync();
            return userId;
        }

        public async Task SeedCardAsync(int cardId, CardClass cardClass)
        {
            var exists = await Db.CardDefinitions.AsNoTracking().AnyAsync(item => item.Id == cardId);
            if (exists)
            {
                return;
            }

            Db.CardDefinitions.Add(new CardDefinition
            {
                Id = cardId,
                Name = $"Card-{cardId}",
                Type = CardType.Follower,
                Cost = 1,
                Attack = 1,
                Health = 1,
                Class = cardClass,
                IsCollectible = true
            });
            await Db.SaveChangesAsync();
        }

        public async Task UpsertPolicyAsync(DeckEnvironment environment, DeckRule rule, int cardDefinitionId, int maxCopies)
        {
            var row = await Db.DeckCardCopyLimitPolicies.FirstOrDefaultAsync(
                item => item.Environment == environment &&
                        item.Rule == rule &&
                        item.CardDefinitionId == cardDefinitionId);
            if (row == null)
            {
                Db.DeckCardCopyLimitPolicies.Add(new DeckCardCopyLimitPolicy
                {
                    Id = Guid.NewGuid(),
                    Environment = environment,
                    Rule = rule,
                    CardDefinitionId = cardDefinitionId,
                    MaxCopies = maxCopies,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            }
            else
            {
                row.MaxCopies = maxCopies;
                row.UpdatedAtUtc = DateTime.UtcNow;
            }

            await Db.SaveChangesAsync();
        }

        public async Task<List<DeckCardEntryDto>> SeedDeckEntriesAsync(CardClass cardClass, int totalCount, int startCardId)
        {
            var entries = new List<DeckCardEntryDto>();
            var remaining = totalCount;
            var currentCardId = startCardId;
            while (remaining > 0)
            {
                var copies = Math.Min(3, remaining);
                await SeedCardAsync(currentCardId, cardClass);
                entries.Add(new DeckCardEntryDto
                {
                    CardDefinitionId = currentCardId,
                    Count = copies
                });
                remaining -= copies;
                currentCardId++;
            }

            return entries;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
