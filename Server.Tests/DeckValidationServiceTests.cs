using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

[Collection("DatabaseCollection")]
public class DeckValidationServiceTests
{
    private readonly DatabaseFixture _fixture;

    public DeckValidationServiceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ValidateAsync_Allows_ValidDeck()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var validator = scope.ServiceProvider.GetRequiredService<IDeckValidationService>();

        var deckClass = CardClass.Neutral;
        var candidates = db.CardDefinitions
            .Where(card => card.Class == deckClass)
            .Select(card => card.Id)
            .Take(14)
            .ToList();

        if (candidates.Count < 14)
        {
            deckClass = db.CardDefinitions.Select(card => card.Class).FirstOrDefault();
            candidates = db.CardDefinitions
                .Where(card => card.Class == deckClass || card.Class == CardClass.Neutral)
                .Select(card => card.Id)
                .Take(14)
                .ToList();
        }

        var cards = BuildDeckEntries(candidates);
        if (cards.Sum(entry => entry.Count) != 40)
        {
            return;
        }

        var result = await validator.ValidateAsync(deckClass, cards, requireComplete: true, cancellationToken: default);

        Assert.True(result.Success, result.Error);
        Assert.Equal(40, result.Data?.Sum(entry => entry.Count));
    }

    [Fact]
    public async Task ValidateAsync_Rejects_TooManyCopies()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<IDeckValidationService>();

        var cards = new List<DeckCardEntryDto>
        {
            new DeckCardEntryDto { CardDefinitionId = 1001, Count = 4 }
        };

        var result = await validator.ValidateAsync(CardClass.Neutral, cards, requireComplete: false, cancellationToken: default);

        Assert.False(result.Success);
        Assert.Contains("copies", result.Error ?? string.Empty);
    }

    [Fact]
    public async Task ValidateAsync_Rejects_ClassMismatch()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var validator = scope.ServiceProvider.GetRequiredService<IDeckValidationService>();

        var deckClass = db.CardDefinitions.Select(card => card.Class).FirstOrDefault();
        var offClass = db.CardDefinitions.FirstOrDefault(card => card.Class != deckClass && card.Class != CardClass.Neutral);
        if (offClass == null)
        {
            return;
        }

        var cards = new List<DeckCardEntryDto>
        {
            new DeckCardEntryDto { CardDefinitionId = offClass.Id, Count = 1 }
        };

        var result = await validator.ValidateAsync(deckClass, cards, requireComplete: false, cancellationToken: default);

        Assert.False(result.Success);
        Assert.Contains("class", result.Error ?? string.Empty);
    }

    private static List<DeckCardEntryDto> BuildDeckEntries(List<int> cardIds)
    {
        var entries = new List<DeckCardEntryDto>();
        var remaining = 40;

        foreach (var cardId in cardIds)
        {
            if (remaining <= 0) break;
            var count = Math.Min(3, remaining);
            entries.Add(new DeckCardEntryDto { CardDefinitionId = cardId, Count = count });
            remaining -= count;
        }

        return entries;
    }
}
