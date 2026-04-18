using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public sealed class DeckCollectibleValidationTests
{
    [Fact]
    public async Task ValidateAsync_CompleteDeckWithNonCollectibleCard_ReturnsValidationError()
    {
        await using var db = CreateDbContext();
        for (var cardId = 1; cardId <= 39; cardId++)
        {
            SeedCard(db, cardId, isCollectible: true);
        }
        SeedCard(db, 40, isCollectible: false);
        await db.SaveChangesAsync();

        var service = new DeckValidationService(db);
        var result = await service.ValidateAsync(
            CardClass.Forestcraft,
            secondaryClass: null,
            Enumerable.Range(1, 40)
                .Select(cardId => new DeckCardEntryDto { CardDefinitionId = cardId, Count = 1 })
                .ToList(),
            DeckEnvironment.Open,
            DeckRule.Standard,
            requireComplete: true,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("not collectible", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Card-40", result.Error ?? string.Empty);
    }

    [Fact]
    public async Task ValidateAsync_DraftSaveWithNonCollectibleCard_AllowsTemporarySave()
    {
        await using var db = CreateDbContext();
        SeedCard(db, 1, isCollectible: false);
        await db.SaveChangesAsync();

        var service = new DeckValidationService(db);
        var result = await service.ValidateAsync(
            CardClass.Forestcraft,
            secondaryClass: null,
            new List<DeckCardEntryDto>
            {
                new() { CardDefinitionId = 1, Count = 1 }
            },
            DeckEnvironment.Open,
            DeckRule.Standard,
            requireComplete: false,
            CancellationToken.None);

        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public async Task ValidateAsync_CompleteDeckWithNoCards_ReturnsValidationError()
    {
        await using var db = CreateDbContext();

        var service = new DeckValidationService(db);
        var result = await service.ValidateAsync(
            CardClass.Forestcraft,
            secondaryClass: null,
            new List<DeckCardEntryDto>(),
            DeckEnvironment.Open,
            DeckRule.Standard,
            requireComplete: true,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("exactly 40 cards", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_DraftSaveWithNoCards_AllowsTemporarySave()
    {
        await using var db = CreateDbContext();

        var service = new DeckValidationService(db);
        var result = await service.ValidateAsync(
            CardClass.Forestcraft,
            secondaryClass: null,
            new List<DeckCardEntryDto>(),
            DeckEnvironment.Open,
            DeckRule.Standard,
            requireComplete: false,
            CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Empty(result.Data ?? new List<DeckCardEntryDto>());
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"deck-collectible-validation-{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static void SeedCard(ApplicationDbContext db, int id, bool isCollectible)
    {
        db.CardDefinitions.Add(new CardDefinition
        {
            Id = id,
            Name = $"Card-{id}",
            Type = CardType.Follower,
            Cost = 1,
            Attack = 1,
            Health = 1,
            Class = CardClass.Forestcraft,
            IsCollectible = isCollectible
        });
    }
}
