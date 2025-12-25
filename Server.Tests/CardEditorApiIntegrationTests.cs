using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Controllers;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

[Collection("DatabaseCollection")]
public class CardEditorApiIntegrationTests
{
    private readonly DatabaseFixture _fixture;
    private readonly IServiceProvider _serviceProvider;

    public CardEditorApiIntegrationTests(DatabaseFixture fixture, Xunit.Abstractions.ITestOutputHelper output)
    {
        _fixture = fixture;
        _fixture.OutputHelper = output;
        _serviceProvider = _fixture.ServiceProvider;
    }

    [Fact]
    public async Task CreateAbility_PersistsToDatabase_AndLinksToCard()
    {
        var db = _serviceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = _serviceProvider.GetRequiredService<ILogger<AbilitiesController>>();

        var card = new CardDefinition
        {
            Id = 90001,
            Name = "Test Card",
            Type = CardType.Follower,
            Rarity = CardRarity.Bronze,
            CardSet = "Test",
            Cost = 1,
            Attack = 1,
            Health = 1,
            Description = "Test",
            Class = CardClass.Neutral,
            AbilityIds = new List<long>()
        };
        db.CardDefinitions.Add(card);
        await db.SaveChangesAsync();

        var controller = new AbilitiesController(db, logger);

        using var doc = JsonDocument.Parse("""
        {
          "cardId": 90001,
          "name": "Test Ability",
          "description": "Hello",
          "abilityTag": "Fanfare",
          "abilityType": 1,
          "priority": 5,
          "isActive": true,
          "jsonConfig": "{\"name\":\"Test Ability\",\"type\":\"Triggered\"}"
        }
        """);

        var result = await controller.CreateAbility(doc.RootElement);
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.NotNull(created.Value);

        var abilityCount = db.CardAbilities.Count(a => a.CardId == 90001 && a.Name == "Test Ability");
        Assert.Equal(1, abilityCount);

        var updatedCard = await db.CardDefinitions.FindAsync(90001);
        Assert.NotNull(updatedCard);
        Assert.Contains(db.CardAbilities.Single(a => a.CardId == 90001 && a.Name == "Test Ability").Id, updatedCard!.AbilityIds);
    }

    [Fact]
    public async Task LinkAndUnlinkAbility_UpdatesCardAbilityIds_AndAbilityCardId()
    {
        var db = _serviceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = _serviceProvider.GetRequiredService<ILogger<CardsController>>();
        var repo = _serviceProvider.GetRequiredService<ICardRepository>();
        var mapper = _serviceProvider.GetRequiredService<AutoMapper.IMapper>();

        var oldCard = new CardDefinition
        {
            Id = 90010,
            Name = "Old Card",
            Type = CardType.Follower,
            Rarity = CardRarity.Bronze,
            CardSet = "Test",
            Cost = 1,
            Attack = 1,
            Health = 1,
            Description = "Test",
            Class = CardClass.Neutral,
            AbilityIds = new List<long>()
        };
        var newCard = new CardDefinition
        {
            Id = 90011,
            Name = "New Card",
            Type = CardType.Follower,
            Rarity = CardRarity.Bronze,
            CardSet = "Test",
            Cost = 1,
            Attack = 1,
            Health = 1,
            Description = "Test",
            Class = CardClass.Neutral,
            AbilityIds = new List<long>()
        };
        db.CardDefinitions.AddRange(oldCard, newCard);

        var ability = new AbilityDefinition
        {
            CardId = 90010,
            Name = "Move Me",
            Description = "Move",
            AbilityTag = "Fanfare",
            AbilityType = 1,
            Priority = 0,
            IsActive = true,
            JsonConfig = "{\"name\":\"Move Me\"}"
        };
        db.CardAbilities.Add(ability);
        await db.SaveChangesAsync();

        oldCard.AbilityIds.Add(ability.Id);
        await db.SaveChangesAsync();

        var controller = new CardsController(repo, logger, mapper, db);

        var linkResult = await controller.LinkAbilityToCard(90011, ability.Id);
        Assert.IsType<OkObjectResult>(linkResult.Result);

        var reloadedAbility = await db.CardAbilities.FindAsync(ability.Id);
        Assert.NotNull(reloadedAbility);
        Assert.Equal(90011, reloadedAbility!.CardId);

        var reloadedOld = await db.CardDefinitions.FindAsync(90010);
        var reloadedNew = await db.CardDefinitions.FindAsync(90011);
        Assert.NotNull(reloadedOld);
        Assert.NotNull(reloadedNew);
        Assert.DoesNotContain(ability.Id, reloadedOld!.AbilityIds);
        Assert.Contains(ability.Id, reloadedNew!.AbilityIds);

        var unlinkResult = await controller.UnlinkAbilityFromCard(90011, ability.Id);
        Assert.IsType<OkObjectResult>(unlinkResult);

        var afterUnlinkNew = await db.CardDefinitions.FindAsync(90011);
        Assert.NotNull(afterUnlinkNew);
        Assert.DoesNotContain(ability.Id, afterUnlinkNew!.AbilityIds);
    }

    [Fact]
    public async Task GetCardAbilities_ReturnsPersistedAbilities()
    {
        var db = _serviceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = _serviceProvider.GetRequiredService<ICardRepository>();
        var logger = _serviceProvider.GetRequiredService<ILogger<CardsController>>();
        var mapper = _serviceProvider.GetRequiredService<AutoMapper.IMapper>();

        var card = new CardDefinition
        {
            Id = 90020,
            Name = "Ability Card",
            Type = CardType.Follower,
            Rarity = CardRarity.Bronze,
            CardSet = "Test",
            Cost = 1,
            Attack = 1,
            Health = 1,
            Description = "Test",
            Class = CardClass.Neutral,
            AbilityIds = new List<long>()
        };
        db.CardDefinitions.Add(card);

        var ability = new AbilityDefinition
        {
            CardId = 90020,
            Name = "Listed Ability",
            Description = "Listed",
            AbilityTag = "Fanfare",
            AbilityType = 1,
            Priority = 0,
            IsActive = true,
            JsonConfig = "{\"name\":\"Listed Ability\"}"
        };
        db.CardAbilities.Add(ability);
        await db.SaveChangesAsync();

        card.AbilityIds.Add(ability.Id);
        await db.SaveChangesAsync();

        var controller = new CardsController(repo, logger, mapper, db);
        var result = await controller.GetCardAbilities(90020);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<object>>(ok.Value);
        Assert.Contains(list, item => item.ToString()!.Contains("Listed Ability"));
    }
}
