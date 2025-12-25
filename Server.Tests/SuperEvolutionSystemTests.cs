using System;
using System.Collections.Generic;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace ShadowverseBeyond.Server.Tests;

[Collection("DatabaseCollection")]
public class SuperEvolutionSystemTests
{
    private const int EvolvableFollowerDefinitionId = 1001;
    private const int GenericFollowerDefinitionId = 1002;
    private readonly DatabaseFixture _fixture;

    public SuperEvolutionSystemTests(DatabaseFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _fixture.OutputHelper = output;
    }

    [Fact]
    public void EvolutionPointsUnlockAtExpectedTurns()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        EnsureGameDataProvider(sp);

        var mapper = sp.GetRequiredService<IMapper>();
        var accessor = sp.GetRequiredService<IGameContextAccessor>();
        GameServiceProvider.ContextAccessor = accessor;

        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var gameState = CreateGameState(sp, p1Id, p2Id, out var player1, out var player2);

        using var context = new GameContext(sp, mapper, accessor, gameState, requestingPlayerId: p1Id);

        player1.TurnNumber = GameConstants.Evolution.FirstPlayerEvolutionStartTurn - 2;
        var preUnlockFirst = player1.StartTurn(gameState);
        CommitEventsRecursively(context, preUnlockFirst.GeneratedEvents);
        Assert.Equal(0, player1.MaxEvolutionPoints);
        Assert.Equal(0, player1.CurrentEvolutionPoints);
        Assert.False(player1.CanEvolve);

        var unlockFirst = player1.StartTurn(gameState);
        CommitEventsRecursively(context, unlockFirst.GeneratedEvents);
        Assert.Equal(GameConstants.Evolution.FirstPlayerEvolutionPoints, player1.MaxEvolutionPoints);
        Assert.Equal(GameConstants.Evolution.FirstPlayerEvolutionPoints, player1.CurrentEvolutionPoints);
        Assert.True(player1.CanEvolve);

        player2.TurnNumber = GameConstants.Evolution.SecondPlayerEvolutionStartTurn - 2;
        var preUnlockSecond = player2.StartTurn(gameState);
        CommitEventsRecursively(context, preUnlockSecond.GeneratedEvents);
        Assert.Equal(0, player2.MaxEvolutionPoints);
        Assert.Equal(0, player2.CurrentEvolutionPoints);
        Assert.False(player2.CanEvolve);

        var unlockSecond = player2.StartTurn(gameState);
        CommitEventsRecursively(context, unlockSecond.GeneratedEvents);
        Assert.Equal(GameConstants.Evolution.SecondPlayerEvolutionPoints, player2.MaxEvolutionPoints);
        Assert.Equal(GameConstants.Evolution.SecondPlayerEvolutionPoints, player2.CurrentEvolutionPoints);
        Assert.True(player2.CanEvolve);
    }

    [Fact]
    public void SuperEvolutionPointsUnlockAtExpectedTurns()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        EnsureGameDataProvider(sp);

        var mapper = sp.GetRequiredService<IMapper>();
        var accessor = sp.GetRequiredService<IGameContextAccessor>();
        GameServiceProvider.ContextAccessor = accessor;

        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var gameState = CreateGameState(sp, p1Id, p2Id, out var player1, out var player2);

        using var context = new GameContext(sp, mapper, accessor, gameState, requestingPlayerId: p1Id);

        player1.TurnNumber = GameConstants.SuperEvolution.FirstPlayerSuperEvolutionStartTurn - 2;
        var preUnlockFirst = player1.StartTurn(gameState);
        CommitEventsRecursively(context, preUnlockFirst.GeneratedEvents);
        Assert.Equal(0, player1.MaxSuperEvolutionPoints);
        Assert.Equal(0, player1.CurrentSuperEvolutionPoints);
        Assert.False(player1.CanSuperEvolve);

        var unlockFirst = player1.StartTurn(gameState);
        CommitEventsRecursively(context, unlockFirst.GeneratedEvents);
        Assert.Equal(GameConstants.SuperEvolution.FirstPlayerSuperEvolutionPoints, player1.MaxSuperEvolutionPoints);
        Assert.Equal(GameConstants.SuperEvolution.FirstPlayerSuperEvolutionPoints, player1.CurrentSuperEvolutionPoints);
        Assert.True(player1.CanSuperEvolve);

        player2.TurnNumber = GameConstants.SuperEvolution.SecondPlayerSuperEvolutionStartTurn - 2;
        var preUnlockSecond = player2.StartTurn(gameState);
        CommitEventsRecursively(context, preUnlockSecond.GeneratedEvents);
        Assert.Equal(0, player2.MaxSuperEvolutionPoints);
        Assert.Equal(0, player2.CurrentSuperEvolutionPoints);
        Assert.False(player2.CanSuperEvolve);

        var unlockSecond = player2.StartTurn(gameState);
        CommitEventsRecursively(context, unlockSecond.GeneratedEvents);
        Assert.Equal(GameConstants.SuperEvolution.SecondPlayerSuperEvolutionPoints, player2.MaxSuperEvolutionPoints);
        Assert.Equal(GameConstants.SuperEvolution.SecondPlayerSuperEvolutionPoints, player2.CurrentSuperEvolutionPoints);
        Assert.True(player2.CanSuperEvolve);
    }

    [Fact]
    public void SuperEvolveConsumesSepAndTriggersEvolveEffects()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        EnsureGameDataProvider(sp);

        var mapper = sp.GetRequiredService<IMapper>();
        var accessor = sp.GetRequiredService<IGameContextAccessor>();
        GameServiceProvider.ContextAccessor = accessor;

        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var gameState = CreateGameState(sp, p1Id, p2Id, out var player1, out _);
        gameState.CurrentPlayerId = p1Id;

        player1.TurnNumber = GameConstants.SuperEvolution.FirstPlayerSuperEvolutionStartTurn;
        player1.MaxSuperEvolutionPoints = GameConstants.SuperEvolution.FirstPlayerSuperEvolutionPoints;
        player1.CurrentSuperEvolutionPoints = GameConstants.SuperEvolution.FirstPlayerSuperEvolutionPoints;
        player1.CanSuperEvolve = true;
        player1.MaxEvolutionPoints = GameConstants.Evolution.FirstPlayerEvolutionPoints;
        player1.CurrentEvolutionPoints = GameConstants.Evolution.FirstPlayerEvolutionPoints;
        player1.CanEvolve = true;

        var follower = CreateRegisteredCard(gameState, p1Id, EvolvableFollowerDefinitionId, CardZone.Field);

        using var context = new GameContext(sp, mapper, accessor, gameState, requestingPlayerId: p1Id);

        var request = new SuperEvolveRequestedEvent(p1Id, new SuperEvolveActionDto
        {
            UnitUid = follower.InstanceId.ToString()
        });

        var commitResult = request.Commit(context);
        Assert.True(commitResult.Success, commitResult.ErrorMessage);
        CommitEventsRecursively(context, commitResult.GeneratedEvents);

        Assert.False(player1.CanSuperEvolve);
        Assert.False(player1.CanEvolve);
        Assert.Equal(GameConstants.SuperEvolution.FirstPlayerSuperEvolutionPoints - 1, player1.CurrentSuperEvolutionPoints);
        Assert.Equal(GameConstants.Evolution.FirstPlayerEvolutionPoints, player1.CurrentEvolutionPoints);

        Assert.True(follower.IsEvolved);
        Assert.True(follower.IsSuperEvolved);
        Assert.Equal(follower.BaseAttack + 3, follower.CurrentAttack);
        Assert.Equal(follower.BaseHealth + 3, follower.MaxHealth);
        Assert.True(follower.CanAttack);
    }

    [Fact]
    public void SuperEvolvedFollowerIsImmuneOnOwnersTurn()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        EnsureGameDataProvider(sp);

        var mapper = sp.GetRequiredService<IMapper>();
        var accessor = sp.GetRequiredService<IGameContextAccessor>();
        GameServiceProvider.ContextAccessor = accessor;

        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var gameState = CreateGameState(sp, p1Id, p2Id, out _, out _);
        gameState.CurrentPlayerId = p1Id;

        var target = CreateRegisteredCard(gameState, p1Id, EvolvableFollowerDefinitionId, CardZone.Field);
        var source = CreateRegisteredCard(gameState, p2Id, GenericFollowerDefinitionId, CardZone.Field);

        using var context = new GameContext(sp, mapper, accessor, gameState, requestingPlayerId: p1Id);

        var evolveResult = target.ApplySuperEvolution();
        CommitEventsRecursively(context, evolveResult.GeneratedEvents);

        var healthBefore = target.CurrentHealth;
        var damageCommit = new DamageEvent(target, 5, source, DamageKind.Ability).Commit(context);
        Assert.True(damageCommit.Success, damageCommit.ErrorMessage);
        CommitEventsRecursively(context, damageCommit.GeneratedEvents);
        Assert.Equal(healthBefore, target.CurrentHealth);

        var destroyCommit = new DestroyEvent(target, source, "Effect").Commit(context);
        Assert.True(destroyCommit.Success, destroyCommit.ErrorMessage);
        CommitEventsRecursively(context, destroyCommit.GeneratedEvents);
        Assert.Equal(CardZone.Field, target.CurrentZone);
    }

    [Fact]
    public void SuperEvolvedFollowerDealsLeaderDamageOnKill()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        EnsureGameDataProvider(sp);

        var mapper = sp.GetRequiredService<IMapper>();
        var accessor = sp.GetRequiredService<IGameContextAccessor>();
        GameServiceProvider.ContextAccessor = accessor;

        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var gameState = CreateGameState(sp, p1Id, p2Id, out _, out var player2);
        gameState.CurrentPlayerId = p1Id;

        var attacker = CreateRegisteredCard(gameState, p1Id, EvolvableFollowerDefinitionId, CardZone.Field);
        var defender = CreateRegisteredCard(gameState, p2Id, GenericFollowerDefinitionId, CardZone.Field);

        using var context = new GameContext(sp, mapper, accessor, gameState, requestingPlayerId: p1Id);

        var evolveResult = attacker.ApplySuperEvolution();
        CommitEventsRecursively(context, evolveResult.GeneratedEvents);

        var leader = player2.Leader;
        var leaderHealthBefore = leader.CurrentHealth;

        var damageCommit = new DamageEvent(defender, attacker.CurrentAttack, attacker, DamageKind.Combat).Commit(context);
        Assert.True(damageCommit.Success, damageCommit.ErrorMessage);
        CommitEventsRecursively(context, damageCommit.GeneratedEvents);

        Assert.Equal(leaderHealthBefore - 1, leader.CurrentHealth);
        Assert.Equal(CardZone.Graveyard, defender.CurrentZone);
    }

    private static GameState CreateGameState(IServiceProvider serviceProvider, Guid p1Id, Guid p2Id, out PlayerState player1, out PlayerState player2)
    {
        player1 = new PlayerState(p1Id) { IsFirst = true };
        player2 = new PlayerState(p2Id) { IsFirst = false };

        var gameState = new GameState("super-evolve-system-test", player1, player2, firstPlayerId: p1Id)
        {
            CurrentPlayerId = p1Id,
            Phase = GamePhase.Main,
            BuffManager = serviceProvider.GetRequiredService<IBuffManager>()
        };

        var abilityFactory = serviceProvider.GetRequiredService<IAbilityFactory>();
        gameState.AbilityManager = new AbilityManager(NullLogger<AbilityManager>.Instance, abilityFactory, new List<IAbility>());

        var leader1 = CreateRegisteredCard(gameState, p1Id, GenericFollowerDefinitionId, CardZone.Emblem);
        var leader2 = CreateRegisteredCard(gameState, p2Id, GenericFollowerDefinitionId, CardZone.Emblem);
        player1.Leader = leader1;
        player2.Leader = leader2;

        return gameState;
    }

    private static CardInstance CreateRegisteredCard(GameState gameState, Guid ownerPlayerId, int definitionId, CardZone zone)
    {
        var card = new CardInstance(Guid.NewGuid(), definitionId, ownerPlayerId)
        {
            CurrentZone = zone
        };

        gameState.RegisterCardInstance(card);

        var owner = gameState.GetPlayerState(ownerPlayerId);
        if (owner != null)
        {
            switch (zone)
            {
                case CardZone.Deck:
                    owner.Deck.Add(card);
                    break;
                case CardZone.Hand:
                    owner.Hand.Add(card);
                    break;
                case CardZone.Field:
                    owner.Field.Add(card);
                    break;
                case CardZone.Graveyard:
                    owner.Graveyard.Add(card);
                    break;
                case CardZone.Extra:
                    owner.Extra.Add(card);
                    break;
                case CardZone.Emblem:
                    owner.Emblem.Add(card);
                    break;
            }
        }

        return card;
    }

    private static void CommitEventsRecursively(GameContext context, IEnumerable<IGameEvent> events)
    {
        var queue = new Queue<IGameEvent>(events);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current is not ICommittableEvent committable)
            {
                continue;
            }

            var commitResult = committable.Commit(context);
            Assert.True(commitResult.Success, commitResult.ErrorMessage);

            foreach (var generated in commitResult.GeneratedEvents)
            {
                queue.Enqueue(generated);
            }
        }
    }

    private static void EnsureGameDataProvider(IServiceProvider serviceProvider)
    {
        var gdp = serviceProvider.GetRequiredService<IGameDataProvider>();
        GameDataProvider.SetInstanceForTesting(gdp);
        gdp.RefreshCacheAsync().GetAwaiter().GetResult();
    }
}
