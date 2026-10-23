using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public sealed class DualClassDeckValidationTests
{
    [Fact]
    public async Task ValidateAsync_DualClass_MissingSecondaryClass_ReturnsValidationError()
    {
        await using var db = CreateDbContext();
        SeedCard(db, 1001, CardClass.Forestcraft);
        await db.SaveChangesAsync();

        var service = new DeckValidationService(db);
        var result = await service.ValidateAsync(
            CardClass.Forestcraft,
            secondaryClass: null,
            new List<DeckCardEntryDto> { new() { CardDefinitionId = 1001, Count = 1 } },
            DeckEnvironment.Open,
            DeckRule.DualClass,
            requireComplete: false,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Secondary class is required", result.Error ?? string.Empty);
    }

    [Fact]
    public async Task ValidateAsync_DualClass_OffClassCard_ReturnsValidationError()
    {
        await using var db = CreateDbContext();
        SeedCard(db, 1001, CardClass.Forestcraft);
        SeedCard(db, 1002, CardClass.Swordcraft);
        SeedCard(db, 1003, CardClass.Dragoncraft);
        await db.SaveChangesAsync();

        var service = new DeckValidationService(db);
        var result = await service.ValidateAsync(
            CardClass.Forestcraft,
            secondaryClass: CardClass.Swordcraft,
            new List<DeckCardEntryDto>
            {
                new() { CardDefinitionId = 1001, Count = 1 },
                new() { CardDefinitionId = 1002, Count = 1 },
                new() { CardDefinitionId = 1003, Count = 1 }
            },
            DeckEnvironment.Open,
            DeckRule.DualClass,
            requireComplete: false,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("outside primary, secondary, or neutral", result.Error ?? string.Empty);
    }

    [Fact]
    public async Task ValidateAsync_DualClass_PrimaryOverflow_ReturnsValidationError()
    {
        await using var db = CreateDbContext();
        var cards = new List<DeckCardEntryDto>();
        cards.AddRange(AddCards(db, 1100, CardClass.Forestcraft, totalCopies: 25));
        cards.AddRange(AddCards(db, 2100, CardClass.Swordcraft, totalCopies: 15));
        await db.SaveChangesAsync();

        var service = new DeckValidationService(db);
        var result = await service.ValidateAsync(
            CardClass.Forestcraft,
            secondaryClass: CardClass.Swordcraft,
            cards,
            DeckEnvironment.Open,
            DeckRule.DualClass,
            requireComplete: true,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("primary class limit", result.Error ?? string.Empty);
    }

    [Fact]
    public async Task ValidateAsync_DualClass_SecondaryOverflow_ReturnsValidationError()
    {
        await using var db = CreateDbContext();
        var cards = new List<DeckCardEntryDto>();
        cards.AddRange(AddCards(db, 1200, CardClass.Forestcraft, totalCopies: 23));
        cards.AddRange(AddCards(db, 2200, CardClass.Swordcraft, totalCopies: 17));
        await db.SaveChangesAsync();

        var service = new DeckValidationService(db);
        var result = await service.ValidateAsync(
            CardClass.Forestcraft,
            secondaryClass: CardClass.Swordcraft,
            cards,
            DeckEnvironment.Open,
            DeckRule.DualClass,
            requireComplete: true,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("secondary class limit", result.Error ?? string.Empty);
    }

    [Fact]
    public async Task ValidateAsync_DualClass_NeutralDoesNotConsumeClassQuota_ReturnsSuccess()
    {
        await using var db = CreateDbContext();
        var cards = new List<DeckCardEntryDto>();
        cards.AddRange(AddCards(db, 1300, CardClass.Forestcraft, totalCopies: 24));
        cards.AddRange(AddCards(db, 2300, CardClass.Swordcraft, totalCopies: 15));
        cards.AddRange(AddCards(db, 3300, CardClass.Neutral, totalCopies: 1));
        await db.SaveChangesAsync();

        var service = new DeckValidationService(db);
        var result = await service.ValidateAsync(
            CardClass.Forestcraft,
            secondaryClass: CardClass.Swordcraft,
            cards,
            DeckEnvironment.Open,
            DeckRule.DualClass,
            requireComplete: true,
            CancellationToken.None);

        Assert.True(result.Success, result.Error);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"dual-class-validation-{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static List<DeckCardEntryDto> AddCards(ApplicationDbContext db, int startId, CardClass cardClass, int totalCopies)
    {
        var entries = new List<DeckCardEntryDto>();
        var remaining = totalCopies;
        var cardId = startId;
        while (remaining > 0)
        {
            var copies = Math.Min(3, remaining);
            SeedCard(db, cardId, cardClass);
            entries.Add(new DeckCardEntryDto
            {
                CardDefinitionId = cardId,
                Count = copies
            });
            remaining -= copies;
            cardId++;
        }

        return entries;
    }

    private static void SeedCard(ApplicationDbContext db, int id, CardClass cardClass)
    {
        db.CardDefinitions.Add(new CardDefinition
        {
            Id = id,
            Name = $"Card-{id}",
            Type = CardType.Follower,
            Cost = 1,
            Attack = 1,
            Health = 1,
            Class = cardClass
        });
    }
}
