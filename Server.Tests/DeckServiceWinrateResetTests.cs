using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Cryptography;
using System.Text;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public sealed class DeckServiceWinrateResetTests
{
    [Fact]
    public async Task UpdateAsync_DeckShapeChanged_DetachesHistoricalFactsFromUserDeckId()
    {
        await using var harness = await Harness.CreateAsync();
        var userId = Guid.NewGuid();
        var deckId = Guid.NewGuid();

        await harness.SeedUserDeckWithFactsAsync(
            userId,
            deckId,
            deckClass: CardClass.Forestcraft,
            cardEntries: new[] { new DeckCardEntryDto { CardDefinitionId = 1001, Count = 3 } });

        var result = await harness.Service.UpdateAsync(
            new DeckOwner(userId, null),
            deckId,
            new UpdateDeckRequest
            {
                Cards = new List<DeckCardEntryDto>
                {
                    new() { CardDefinitionId = 1002, Count = 3 }
                }
            },
            CancellationToken.None);

        Assert.True(result.Success, result.Error);

        var linkedFacts = await harness.Db.GameParticipantStatsFacts
            .AsNoTracking()
            .Where(item => item.UserDeckId == deckId)
            .ToListAsync();
        Assert.Empty(linkedFacts);

        var detachedFacts = await harness.Db.GameParticipantStatsFacts
            .AsNoTracking()
            .Where(item => item.UserDeckId == null)
            .ToListAsync();
        Assert.Equal(2, detachedFacts.Count);
    }

    [Fact]
    public async Task UpdateAsync_DeckShapeUnchanged_KeepsHistoricalFactsLinked()
    {
        await using var harness = await Harness.CreateAsync();
        var userId = Guid.NewGuid();
        var deckId = Guid.NewGuid();

        await harness.SeedUserDeckWithFactsAsync(
            userId,
            deckId,
            deckClass: CardClass.Forestcraft,
            cardEntries: new[] { new DeckCardEntryDto { CardDefinitionId = 1001, Count = 3 } });

        var result = await harness.Service.UpdateAsync(
            new DeckOwner(userId, null),
            deckId,
            new UpdateDeckRequest
            {
                Name = "rename only"
            },
            CancellationToken.None);

        Assert.True(result.Success, result.Error);

        var linkedFactCount = await harness.Db.GameParticipantStatsFacts
            .AsNoTracking()
            .CountAsync(item => item.UserDeckId == deckId);
        Assert.Equal(2, linkedFactCount);
    }

    [Fact]
    public async Task UpdateAsync_CardPayloadSubmittedButShapeUnchanged_KeepsHistoricalFactsLinked()
    {
        await using var harness = await Harness.CreateAsync();
        var userId = Guid.NewGuid();
        var deckId = Guid.NewGuid();

        await harness.SeedUserDeckWithFactsAsync(
            userId,
            deckId,
            deckClass: CardClass.Forestcraft,
            cardEntries: new[]
            {
                new DeckCardEntryDto { CardDefinitionId = 1001, Count = 2 },
                new DeckCardEntryDto { CardDefinitionId = 1003, Count = 1 }
            });

        var result = await harness.Service.UpdateAsync(
            new DeckOwner(userId, null),
            deckId,
            new UpdateDeckRequest
            {
                Cards = new List<DeckCardEntryDto>
                {
                    new() { CardDefinitionId = 1003, Count = 1 },
                    new() { CardDefinitionId = 1001, Count = 2 }
                }
            },
            CancellationToken.None);

        Assert.True(result.Success, result.Error);

        var linkedFactCount = await harness.Db.GameParticipantStatsFacts
            .AsNoTracking()
            .CountAsync(item => item.UserDeckId == deckId);
        Assert.Equal(2, linkedFactCount);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private Harness(SqliteConnection connection, ApplicationDbContext db, DeckService service)
        {
            _connection = connection;
            Db = db;
            Service = service;
        }

        public ApplicationDbContext Db { get; }
        public DeckService Service { get; }

        public static async Task<Harness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new ApplicationDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var guestStore = new Mock<IGuestDeckStore>();
            var deckCode = new Mock<IDeckCodeService>();
            var validation = new Mock<IDeckValidationService>();
            validation
                .Setup(item => item.ValidateAsync(
                    It.IsAny<CardClass>(),
                    It.IsAny<IReadOnlyList<DeckCardEntryDto>>(),
                    It.IsAny<DeckEnvironment>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((
                    CardClass cardClass,
                    IReadOnlyList<DeckCardEntryDto> cards,
                    DeckEnvironment environment,
                    bool requireComplete,
                    CancellationToken cancellationToken) => DeckOperationResult<List<DeckCardEntryDto>>.Ok(cards.ToList()));

            var service = new DeckService(
                db,
                guestStore.Object,
                deckCode.Object,
                validation.Object,
                NullLogger<DeckService>.Instance);

            return new Harness(connection, db, service);
        }

        public async Task SeedUserDeckWithFactsAsync(
            Guid userId,
            Guid deckId,
            CardClass deckClass,
            IReadOnlyList<DeckCardEntryDto> cardEntries)
        {
            Db.Users.Add(new User
            {
                Id = userId,
                Username = $"u-{userId:N}",
                Email = $"u-{userId:N}@example.com",
                PasswordHash = "hash"
            });

            foreach (var cardId in cardEntries.Select(item => item.CardDefinitionId).Distinct())
            {
                Db.CardDefinitions.Add(new CardDefinition
                {
                    Id = cardId,
                    Name = $"Card-{cardId}",
                    Type = CardType.Follower,
                    Cost = 1,
                    Class = deckClass
                });
            }

            var userDeck = new UserDeck
            {
                Id = deckId,
                UserId = userId,
                Name = "test-deck",
                Class = deckClass,
                Environment = DeckEnvironment.Open,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Cards = cardEntries.Select(item => new UserDeckCard
                {
                    Id = Guid.NewGuid(),
                    DeckId = deckId,
                    CardDefinitionId = item.CardDefinitionId,
                    Count = item.Count
                }).ToList()
            };
            Db.UserDecks.Add(userDeck);

            var deckShapeHash = BuildDeckShapeHash(deckClass, cardEntries);

            Db.GameParticipantStatsFacts.AddRange(
                new GameParticipantStatsFact
                {
                    Id = Guid.NewGuid(),
                    GameId = $"g-{Guid.NewGuid():N}",
                    PlayerId = userId,
                    IsWinner = true,
                    IsFirstPlayer = true,
                    EndedAtUtc = DateTime.UtcNow,
                    DeckClass = deckClass,
                    Environment = DeckEnvironment.Open,
                    DeckShapeHash = deckShapeHash,
                    UserDeckId = deckId,
                    InitialDeckCardCountsJson = "{}",
                    DrawnCardIdsJson = "[]",
                    PlayedCardIdsJson = "[]",
                    IsOfficial = true
                },
                new GameParticipantStatsFact
                {
                    Id = Guid.NewGuid(),
                    GameId = $"g-{Guid.NewGuid():N}",
                    PlayerId = Guid.NewGuid(),
                    IsWinner = false,
                    IsFirstPlayer = false,
                    EndedAtUtc = DateTime.UtcNow,
                    DeckClass = deckClass,
                    Environment = DeckEnvironment.Open,
                    DeckShapeHash = deckShapeHash,
                    UserDeckId = deckId,
                    InitialDeckCardCountsJson = "{}",
                    DrawnCardIdsJson = "[]",
                    PlayedCardIdsJson = "[]",
                    IsOfficial = true
                });

            await Db.SaveChangesAsync();

            var changedCardId = 1002;
            if (cardEntries.All(item => item.CardDefinitionId != changedCardId))
            {
                Db.CardDefinitions.Add(new CardDefinition
                {
                    Id = changedCardId,
                    Name = $"Card-{changedCardId}",
                    Type = CardType.Follower,
                    Cost = 1,
                    Class = deckClass
                });
                await Db.SaveChangesAsync();
            }
        }

        private static string BuildDeckShapeHash(CardClass deckClass, IReadOnlyList<DeckCardEntryDto> cardEntries)
        {
            var payload = string.Join(",",
                cardEntries
                    .Where(item => item.Count > 0)
                    .GroupBy(item => item.CardDefinitionId)
                    .OrderBy(group => group.Key)
                    .Select(group => $"{group.Key}:{group.Sum(item => item.Count)}"));

            var raw = $"{deckClass}|{payload}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
