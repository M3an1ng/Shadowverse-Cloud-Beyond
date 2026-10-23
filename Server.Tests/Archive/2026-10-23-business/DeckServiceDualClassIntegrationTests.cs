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

public sealed class DeckServiceDualClassIntegrationTests
{
    [Fact]
    public async Task CreateAsync_DualClass_PersistsModeAndSecondaryClass()
    {
        await using var harness = await DeckHarness.CreateAsync();
        var userId = await harness.SeedUserAsync();
        var cards = await harness.SeedDeckCardsAsync(
            (CardClass.Forestcraft, 24),
            (CardClass.Swordcraft, 16));

        var result = await harness.Service.CreateAsync(
            new DeckOwner(userId, null),
            new CreateDeckRequest
            {
                Name = "Dual Deck",
                Class = CardClass.Forestcraft,
                SecondaryClass = CardClass.Swordcraft,
                Environment = DeckEnvironment.Open,
                Rule = DeckRule.DualClass,
                Cards = cards
            },
            CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Equal(DeckRule.DualClass, result.Data!.Rule);
        Assert.Equal(CardClass.Swordcraft, result.Data.SecondaryClass);
        Assert.Equal(40, result.Data.CardCount);

        var persistedDeck = await harness.Db.UserDecks
            .AsNoTracking()
            .SingleAsync(item => item.Id == result.Data.DeckId);
        Assert.Equal(DeckRule.DualClass, persistedDeck.Rule);
        Assert.Equal(CardClass.Swordcraft, persistedDeck.SecondaryClass);
    }

    [Fact]
    public async Task UpdateAsync_StandardMode_IgnoresSecondaryClassAssignment()
    {
        await using var harness = await DeckHarness.CreateAsync();
        var userId = await harness.SeedUserAsync();
        var cards = await harness.SeedDeckCardsAsync((CardClass.Forestcraft, 40));
        var create = await harness.Service.CreateAsync(
            new DeckOwner(userId, null),
            new CreateDeckRequest
            {
                Name = "Standard Deck",
                Class = CardClass.Forestcraft,
                Environment = DeckEnvironment.Open,
                Rule = DeckRule.Standard,
                Cards = cards
            },
            CancellationToken.None);
        Assert.True(create.Success, create.Error);

        var update = await harness.Service.UpdateAsync(
            new DeckOwner(userId, null),
            create.Data!.DeckId,
            new UpdateDeckRequest
            {
                Rule = DeckRule.Standard,
                SecondaryClass = CardClass.Swordcraft
            },
            CancellationToken.None);

        Assert.True(update.Success, update.Error);
        Assert.Equal(DeckRule.Standard, update.Data!.Rule);
        Assert.Null(update.Data.SecondaryClass);
    }

    [Fact]
    public async Task ImportCodeAsync_DualClass_RestoresModeAndSecondaryClass()
    {
        await using var harness = await DeckHarness.CreateAsync();
        var userId = await harness.SeedUserAsync();
        var cards = await harness.SeedDeckCardsAsync(
            (CardClass.Forestcraft, 24),
            (CardClass.Swordcraft, 16));
        var code = harness.DeckCodeService.Encode(
            CardClass.Forestcraft,
            CardClass.Swordcraft,
            DeckEnvironment.Open,
            DeckRule.DualClass,
            cards);

        var imported = await harness.Service.ImportCodeAsync(
            new DeckOwner(userId, null),
            new ImportDeckCodeRequest
            {
                Code = code,
                Name = "Imported Dual"
            },
            CancellationToken.None);

        Assert.True(imported.Success, imported.Error);
        Assert.Equal(DeckRule.DualClass, imported.Data!.Rule);
        Assert.Equal(CardClass.Swordcraft, imported.Data.SecondaryClass);
        Assert.Equal(40, imported.Data.CardCount);
    }

    [Fact]
    public async Task CreateAsync_MissingEnvironmentAndMode_UsesDefaultOpenAndStandard()
    {
        await using var harness = await DeckHarness.CreateAsync();
        var userId = await harness.SeedUserAsync();

        var created = await harness.Service.CreateAsync(
            new DeckOwner(userId, null),
            new CreateDeckRequest
            {
                Name = "Legacy Compatible Deck",
                Class = CardClass.Forestcraft
            },
            CancellationToken.None);

        Assert.True(created.Success, created.Error);
        Assert.Equal(DeckEnvironment.Open, created.Data!.Environment);
        Assert.Equal(DeckRule.Standard, created.Data.Rule);
        Assert.Null(created.Data.SecondaryClass);
    }

    private sealed class DeckHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private int _nextCardId = 1000;

        private DeckHarness(SqliteConnection connection, ApplicationDbContext db, DeckService service, DeckCodeService deckCodeService)
        {
            _connection = connection;
            Db = db;
            Service = service;
            DeckCodeService = deckCodeService;
        }

        public ApplicationDbContext Db { get; }
        public DeckService Service { get; }
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
            var deckValidationService = new DeckValidationService(db);
            var service = new DeckService(
                db,
                new Mock<IGuestDeckStore>().Object,
                deckCodeService,
                deckValidationService,
                NullLogger<DeckService>.Instance);

            return new DeckHarness(connection, db, service, deckCodeService);
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

        public async Task<List<DeckCardEntryDto>> SeedDeckCardsAsync(params (CardClass cardClass, int copies)[] distributions)
        {
            var entries = new List<DeckCardEntryDto>();
            foreach (var (cardClass, copies) in distributions)
            {
                var remaining = copies;
                while (remaining > 0)
                {
                    var count = Math.Min(3, remaining);
                    var cardId = _nextCardId++;
                    Db.CardDefinitions.Add(new CardDefinition
                    {
                        Id = cardId,
                        Name = $"Card-{cardId}",
                        Type = CardType.Follower,
                        Cost = 1,
                        Attack = 1,
                        Health = 1,
                        Class = cardClass
                    });

                    entries.Add(new DeckCardEntryDto
                    {
                        CardDefinitionId = cardId,
                        Count = count
                    });

                    remaining -= count;
                }
            }

            await Db.SaveChangesAsync();
            return entries;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
