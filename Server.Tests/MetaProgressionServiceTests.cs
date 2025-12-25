using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

[Collection("DatabaseCollection")]
public class MetaProgressionServiceTests
{
    private readonly DatabaseFixture _fixture;

    public MetaProgressionServiceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ApplyPostCommitAsync_GameEnd_IsIdempotentPerUser()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<ApplicationDbContext>();
        var meta = sp.GetRequiredService<IMetaProgressionService>();
        var mapper = sp.GetRequiredService<AutoMapper.IMapper>();
        var accessor = sp.GetRequiredService<IGameContextAccessor>();

        var gameId = $"test-{Guid.NewGuid()}";
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        db.Users.Add(new User { Id = p1, Username = "p1", Email = "p1@test.local", PasswordHash = "x" });
        db.Users.Add(new User { Id = p2, Username = "p2", Email = "p2@test.local", PasswordHash = "x" });
        await db.SaveChangesAsync();

        var player1 = new PlayerState(p1) { IsFirst = true, IsAI = false };
        var player2 = new PlayerState(p2) { IsFirst = false, IsAI = false };
        var gameState = new GameState(gameId, player1, player2, firstPlayerId: p1);

        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        var abilityFactory = sp.GetRequiredService<IAbilityFactory>();
        var systemAbilities = sp.GetServices<IAbility>();
        var abilityManager = new AbilityManager(loggerFactory.CreateLogger<AbilityManager>(), abilityFactory, systemAbilities);
        var buffManager = new BuffManager(loggerFactory.CreateLogger<BuffManager>(), abilityManager);
        gameState.AbilityManager = abilityManager;
        gameState.BuffManager = buffManager;

        var events = new List<IGameEvent> { new GameEndEvent(winnerId: p1, loserId: p2, reason: "test") };

        using var gameContext = new GameContext(sp, mapper, accessor, gameState, requestingPlayerId: p1, cancellationToken: CancellationToken.None);

        await meta.ApplyPostCommitAsync(gameContext, events, CancellationToken.None);
        await meta.ApplyPostCommitAsync(gameContext, events, CancellationToken.None);

        var settlements = await db.GameSettlements.Where(s => s.GameId == gameId).ToListAsync();
        Assert.Equal(2, settlements.Count);

        var sessions = await db.GameSessions.Where(s => s.GameStateId == gameId).ToListAsync();
        Assert.Equal(2, sessions.Count);

        var winTx = await db.CoinTransactions.Where(t => t.UserId == p1 && t.Type == CoinTransactionType.GameWin && t.RelatedEntityId == gameId).ToListAsync();
        var lossTx = await db.CoinTransactions.Where(t => t.UserId == p2 && t.Type == CoinTransactionType.GameLoss && t.RelatedEntityId == gameId).ToListAsync();
        Assert.Single(winTx);
        Assert.Single(lossTx);
    }
}
