using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Controllers;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;
using Xunit;

namespace ShadowverseBeyond.Server.Tests.CardSets;

public sealed class CardSetEnvironmentMembershipControllerTests
{
    private static ApplicationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static CardSetsController CreateController(ApplicationDbContext db)
    {
        return new CardSetsController(db, NullLogger<CardSetsController>.Instance);
    }

    [Fact]
    public async Task GetEnvironmentMembership_DefaultEnvironmentIsOpen()
    {
        var dbName = $"cardset-membership-get-{Guid.NewGuid()}";
        await using var db = CreateDbContext(dbName);
        db.CardSetDefinitions.AddRange(
            new CardSetDefinition
            {
                Id = 1001,
                Name = "Set-A",
                Abbreviation = "A",
                Region = 0,
                Sequence = 1,
                Environments = new List<DeckEnvironment> { DeckEnvironment.Open }
            },
            new CardSetDefinition
            {
                Id = 1002,
                Name = "Set-B",
                Abbreviation = "B",
                Region = 0,
                Sequence = 2,
                Environments = new List<DeckEnvironment>()
            });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.GetEnvironmentMembership(null, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CardSetsController.CardSetEnvironmentMembershipResponse>(ok.Value);

        Assert.Equal(DeckEnvironment.Open, payload.Environment);
        Assert.Equal(new[] { 1001 }, payload.IncludedCardSetDefinitionIds.OrderBy(id => id));
        Assert.Equal(2, payload.Items.Count);
        Assert.Contains(payload.Items, item => item.CardSetDefinitionId == 1001 && item.Included);
        Assert.Contains(payload.Items, item => item.CardSetDefinitionId == 1002 && !item.Included);
    }

    [Fact]
    public async Task UpdateEnvironmentMembership_AdminCanReplaceMembership()
    {
        var dbName = $"cardset-membership-update-{Guid.NewGuid()}";
        await using var db = CreateDbContext(dbName);
        var adminId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = adminId,
            Username = "admin",
            Email = "admin@local.invalid",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            IsActive = true
        });
        db.CardSetDefinitions.AddRange(
            new CardSetDefinition
            {
                Id = 2001,
                Name = "Set-C",
                Abbreviation = "C",
                Region = 0,
                Sequence = 1,
                Environments = new List<DeckEnvironment> { DeckEnvironment.Open }
            },
            new CardSetDefinition
            {
                Id = 2002,
                Name = "Set-D",
                Abbreviation = "D",
                Region = 0,
                Sequence = 2,
                Environments = new List<DeckEnvironment>()
            });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var request = new CardSetsController.UpdateCardSetEnvironmentMembershipRequest
        {
            ActorUserId = adminId,
            Environment = DeckEnvironment.Open,
            IncludedCardSetDefinitionIds = new List<int> { 2002 }
        };

        var result = await controller.UpdateEnvironmentMembership(request, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CardSetsController.CardSetEnvironmentMembershipResponse>(ok.Value);

        Assert.Equal(new[] { 2002 }, payload.IncludedCardSetDefinitionIds.OrderBy(id => id));
        var setC = await db.CardSetDefinitions.AsNoTracking().SingleAsync(item => item.Id == 2001);
        var setD = await db.CardSetDefinitions.AsNoTracking().SingleAsync(item => item.Id == 2002);
        Assert.DoesNotContain(DeckEnvironment.Open, setC.Environments ?? new List<DeckEnvironment>());
        Assert.Contains(DeckEnvironment.Open, setD.Environments ?? new List<DeckEnvironment>());
    }

    [Fact]
    public async Task UpdateEnvironmentMembership_PlayerIsForbidden()
    {
        var dbName = $"cardset-membership-forbidden-{Guid.NewGuid()}";
        await using var db = CreateDbContext(dbName);
        var playerId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = playerId,
            Username = "player",
            Email = "player@local.invalid",
            PasswordHash = "hash",
            Role = UserRole.Player,
            IsActive = true
        });
        db.CardSetDefinitions.Add(new CardSetDefinition
        {
            Id = 3001,
            Name = "Set-E",
            Abbreviation = "E",
            Region = 0,
            Sequence = 1,
            Environments = new List<DeckEnvironment> { DeckEnvironment.Open }
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var request = new CardSetsController.UpdateCardSetEnvironmentMembershipRequest
        {
            ActorUserId = playerId,
            Environment = DeckEnvironment.Open,
            IncludedCardSetDefinitionIds = new List<int> { 3001 }
        };

        var result = await controller.UpdateEnvironmentMembership(request, CancellationToken.None);
        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }
}
