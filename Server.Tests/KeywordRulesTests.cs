using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace ShadowverseBeyond.Server.Tests;

[Collection("DatabaseCollection")]
public class KeywordRulesTests
{
    private readonly DatabaseFixture _fixture;

    public KeywordRulesTests(DatabaseFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _fixture.OutputHelper = output;
    }

    [Fact]
    public void Shackle_PreventsAttackDeclaration()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        EnsureGameDataProvider(sp);

        var mapper = sp.GetRequiredService<AutoMapper.IMapper>();
        var accessor = sp.GetRequiredService<IGameContextAccessor>();
        GameServiceProvider.ContextAccessor = accessor;

        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var player1 = new PlayerState(p1Id);
        var player2 = new PlayerState(p2Id);

        var gameState = new GameState("keyword-shackle", player1, player2, firstPlayerId: p1Id)
        {
            CurrentPlayerId = p1Id,
            BuffManager = sp.GetRequiredService<IBuffManager>()
        };

        var abilityFactory = sp.GetRequiredService<IAbilityFactory>();
        gameState.AbilityManager = new AbilityManager(NullLogger<AbilityManager>.Instance, abilityFactory, new List<IAbility>());

        var leader1 = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 0, zone: CardZone.Emblem);
        var leader2 = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 0, zone: CardZone.Emblem);
        player1.Leader = leader1;
        player2.Leader = leader2;

        var attacker = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 1002, zone: CardZone.Field);
        attacker.FieldTurn = 1;
        var defender = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 1002, zone: CardZone.Field);

        gameState.BuffManager.RegisterBuffInstance(
            new Buff(
                sourceAbilityId: Guid.Empty,
                targetCardInstanceId: attacker.InstanceId,
                currentTurn: 0,
                effects: new List<IBuffEffect> { new KeywordBuffEffect(new List<CardKeyword> { CardKeyword.Shackle }) }),
            gameState);

        using var context = new GameContext(sp, mapper, accessor, gameState, requestingPlayerId: p1Id);
        Assert.False(attacker.CanAttack);
        Assert.False(attacker.CanPerformAttack(gameState, player1, defender));
    }

    [Fact]
    public void Pressure_CannotBeAttacked_AndWardCanYieldNoTargets()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        EnsureGameDataProvider(sp);

        var mapper = sp.GetRequiredService<AutoMapper.IMapper>();
        var accessor = sp.GetRequiredService<IGameContextAccessor>();
        GameServiceProvider.ContextAccessor = accessor;

        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var player1 = new PlayerState(p1Id);
        var player2 = new PlayerState(p2Id);

        var gameState = new GameState("keyword-pressure", player1, player2, firstPlayerId: p1Id)
        {
            CurrentPlayerId = p1Id,
            BuffManager = sp.GetRequiredService<IBuffManager>()
        };

        var abilityFactory = sp.GetRequiredService<IAbilityFactory>();
        gameState.AbilityManager = new AbilityManager(NullLogger<AbilityManager>.Instance, abilityFactory, new List<IAbility>());

        var leader1 = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 0, zone: CardZone.Emblem);
        var leader2 = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 0, zone: CardZone.Emblem);
        player1.Leader = leader1;
        player2.Leader = leader2;

        var attacker = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 1002, zone: CardZone.Field);
        attacker.FieldTurn = 1;

        var wardPressure = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 1002, zone: CardZone.Field);
        var otherFollower = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 1002, zone: CardZone.Field);

        gameState.BuffManager.RegisterBuffInstance(
            new Buff(
                sourceAbilityId: Guid.Empty,
                targetCardInstanceId: wardPressure.InstanceId,
                currentTurn: 0,
                effects: new List<IBuffEffect>
                {
                    new KeywordBuffEffect(new List<CardKeyword> { CardKeyword.Ward, CardKeyword.Pressure })
                }),
            gameState);

        using var context = new GameContext(sp, mapper, accessor, gameState, requestingPlayerId: p1Id);
        Assert.False(attacker.CanPerformAttack(gameState, player1, wardPressure));
        Assert.False(attacker.CanPerformAttack(gameState, player1, otherFollower)); // Ward forces targeting ward, but ward is pressure => no valid targets.
    }

    [Fact]
    public async System.Threading.Tasks.Task Aura_MakesTargetedAbilityUnavailable()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        await EnsureGameDataProviderAsync(sp);

        var mapper = sp.GetRequiredService<AutoMapper.IMapper>();
        var accessor = sp.GetRequiredService<IGameContextAccessor>();
        GameServiceProvider.ContextAccessor = accessor;

        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var player1 = new PlayerState(p1Id);
        var player2 = new PlayerState(p2Id);

        var gameState = new GameState("keyword-aura-targeting", player1, player2, firstPlayerId: p1Id)
        {
            CurrentPlayerId = p1Id,
            BuffManager = sp.GetRequiredService<IBuffManager>()
        };

        var abilityFactory = sp.GetRequiredService<IAbilityFactory>();
        gameState.AbilityManager = new AbilityManager(NullLogger<AbilityManager>.Instance, abilityFactory, new List<IAbility>());

        var leader1 = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 0, zone: CardZone.Emblem);
        var leader2 = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 0, zone: CardZone.Emblem);
        player1.Leader = leader1;
        player2.Leader = leader2;

        var sourceCard = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 1001, zone: CardZone.Field);
        sourceCard.FieldTurn = 1;
        var onlyEnemy = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 1002, zone: CardZone.Field);

        gameState.BuffManager.RegisterBuffInstance(
            new Buff(
                sourceAbilityId: Guid.Empty,
                targetCardInstanceId: onlyEnemy.InstanceId,
                currentTurn: 0,
                effects: new List<IBuffEffect> { new KeywordBuffEffect(new List<CardKeyword> { CardKeyword.Aura }) }),
            gameState);

        var config = new AbilityConfig
        {
            Name = "Targeted Ability",
            Type = AbilityType.Triggered,
            AbilityTag = EffectKeyword.Fanfare,
            Targets = new Dictionary<string, TargetConfig>
            {
                ["target"] = new TargetConfig
                {
                    RequireValidTarget = true,
                    Query = new QueryConfig { SourcePoolExpression = "opponent.field" },
                    PlayerChoiceOptions = new PlayerChoiceConfig { IsRequired = true, MinCount = 1, MaxCount = 1 }
                }
            }
        };

        var runtimeAbility = abilityFactory.CreateAbility(config, sourceCard);
        gameState.AbilityManager.RegisterAbility(runtimeAbility);

        using var context = new GameContext(sp, mapper, accessor, gameState, requestingPlayerId: p1Id);
        var availability = await runtimeAbility.CheckTargetAvailabilityAsync(context);
        Assert.False(availability.IsAvailable);
    }

    [Fact]
    public async System.Threading.Tasks.Task Ambush_MakesTargetedAbilityUnavailable()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        await EnsureGameDataProviderAsync(sp);

        var mapper = sp.GetRequiredService<AutoMapper.IMapper>();
        var accessor = sp.GetRequiredService<IGameContextAccessor>();
        GameServiceProvider.ContextAccessor = accessor;

        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var player1 = new PlayerState(p1Id);
        var player2 = new PlayerState(p2Id);

        var gameState = new GameState("keyword-ambush-targeting", player1, player2, firstPlayerId: p1Id)
        {
            CurrentPlayerId = p1Id,
            BuffManager = sp.GetRequiredService<IBuffManager>()
        };

        var abilityFactory = sp.GetRequiredService<IAbilityFactory>();
        gameState.AbilityManager = new AbilityManager(NullLogger<AbilityManager>.Instance, abilityFactory, new List<IAbility>());

        var leader1 = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 0, zone: CardZone.Emblem);
        var leader2 = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 0, zone: CardZone.Emblem);
        player1.Leader = leader1;
        player2.Leader = leader2;

        var sourceCard = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 1001, zone: CardZone.Field);
        sourceCard.FieldTurn = 1;
        var onlyEnemy = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 1002, zone: CardZone.Field);

        gameState.BuffManager.RegisterBuffInstance(
            new Buff(
                sourceAbilityId: Guid.Empty,
                targetCardInstanceId: onlyEnemy.InstanceId,
                currentTurn: 0,
                effects: new List<IBuffEffect> { new KeywordBuffEffect(new List<CardKeyword> { CardKeyword.Ambush }) }),
            gameState);

        var config = new AbilityConfig
        {
            Name = "Targeted Ability",
            Type = AbilityType.Triggered,
            AbilityTag = EffectKeyword.Fanfare,
            Targets = new Dictionary<string, TargetConfig>
            {
                ["target"] = new TargetConfig
                {
                    RequireValidTarget = true,
                    Query = new QueryConfig { SourcePoolExpression = "opponent.field" },
                    PlayerChoiceOptions = new PlayerChoiceConfig { IsRequired = true, MinCount = 1, MaxCount = 1 }
                }
            }
        };

        var runtimeAbility = abilityFactory.CreateAbility(config, sourceCard);
        gameState.AbilityManager.RegisterAbility(runtimeAbility);

        using var context = new GameContext(sp, mapper, accessor, gameState, requestingPlayerId: p1Id);
        var availability = await runtimeAbility.CheckTargetAvailabilityAsync(context);
        Assert.False(availability.IsAvailable);
    }

    [Fact]
    public void Shield_NegatesNextDamageAndIsConsumed()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        EnsureGameDataProvider(sp);

        var mapper = sp.GetRequiredService<AutoMapper.IMapper>();
        var accessor = sp.GetRequiredService<IGameContextAccessor>();
        GameServiceProvider.ContextAccessor = accessor;

        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var player1 = new PlayerState(p1Id);
        var player2 = new PlayerState(p2Id);

        var gameState = new GameState("keyword-shield", player1, player2, firstPlayerId: p1Id)
        {
            CurrentPlayerId = p1Id,
            BuffManager = sp.GetRequiredService<IBuffManager>()
        };

        var abilityFactory = sp.GetRequiredService<IAbilityFactory>();
        gameState.AbilityManager = new AbilityManager(NullLogger<AbilityManager>.Instance, abilityFactory, new List<IAbility>());

        var leader1 = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 0, zone: CardZone.Emblem);
        var leader2 = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 0, zone: CardZone.Emblem);
        player1.Leader = leader1;
        player2.Leader = leader2;

        var source = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 1002, zone: CardZone.Field);
        var target = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 1002, zone: CardZone.Field);

        gameState.BuffManager.RegisterBuffInstance(
            new Buff(
                sourceAbilityId: Guid.Empty,
                targetCardInstanceId: target.InstanceId,
                currentTurn: 0,
                effects: new List<IBuffEffect> { new KeywordBuffEffect(new List<CardKeyword> { CardKeyword.Shield }) }),
            gameState);

        using var context = new GameContext(sp, mapper, accessor, gameState, requestingPlayerId: p2Id);
        var healthBefore = target.CurrentHealth;

        CommitEventsRecursively(context, new[] { new DamageEvent(target, 2, source, DamageKind.Ability) });

        Assert.Equal(healthBefore, target.CurrentHealth);
        Assert.False(target.HasKeyword(CardKeyword.Shield));
    }

    [Fact]
    public void Shield_IsConsumedByZeroDamage()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        EnsureGameDataProvider(sp);

        var mapper = sp.GetRequiredService<AutoMapper.IMapper>();
        var accessor = sp.GetRequiredService<IGameContextAccessor>();
        GameServiceProvider.ContextAccessor = accessor;

        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var player1 = new PlayerState(p1Id);
        var player2 = new PlayerState(p2Id);

        var gameState = new GameState("keyword-shield-zero", player1, player2, firstPlayerId: p1Id)
        {
            CurrentPlayerId = p1Id,
            BuffManager = sp.GetRequiredService<IBuffManager>()
        };

        var abilityFactory = sp.GetRequiredService<IAbilityFactory>();
        gameState.AbilityManager = new AbilityManager(NullLogger<AbilityManager>.Instance, abilityFactory, new List<IAbility>());

        var leader1 = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 0, zone: CardZone.Emblem);
        var leader2 = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 0, zone: CardZone.Emblem);
        player1.Leader = leader1;
        player2.Leader = leader2;

        var source = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 1002, zone: CardZone.Field);
        var target = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 1002, zone: CardZone.Field);

        gameState.BuffManager.RegisterBuffInstance(
            new Buff(
                sourceAbilityId: Guid.Empty,
                targetCardInstanceId: target.InstanceId,
                currentTurn: 0,
                effects: new List<IBuffEffect> { new KeywordBuffEffect(new List<CardKeyword> { CardKeyword.Shield }) }),
            gameState);

        using var context = new GameContext(sp, mapper, accessor, gameState, requestingPlayerId: p2Id);
        var healthBefore = target.CurrentHealth;

        CommitEventsRecursively(context, new[] { new DamageEvent(target, 0, source, DamageKind.Ability) });

        Assert.Equal(healthBefore, target.CurrentHealth);
        Assert.False(target.HasKeyword(CardKeyword.Shield));
    }

    [Fact]
    public void Ambush_BlocksAttack_AndIsRemovedAfterAttacking()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        EnsureGameDataProvider(sp);

        var mapper = sp.GetRequiredService<AutoMapper.IMapper>();
        var accessor = sp.GetRequiredService<IGameContextAccessor>();
        GameServiceProvider.ContextAccessor = accessor;

        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var player1 = new PlayerState(p1Id);
        var player2 = new PlayerState(p2Id);

        var gameState = new GameState("keyword-ambush", player1, player2, firstPlayerId: p1Id)
        {
            CurrentPlayerId = p1Id,
            BuffManager = sp.GetRequiredService<IBuffManager>()
        };

        var abilityFactory = sp.GetRequiredService<IAbilityFactory>();
        gameState.AbilityManager = new AbilityManager(NullLogger<AbilityManager>.Instance, abilityFactory, new List<IAbility>());

        var leader1 = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 0, zone: CardZone.Emblem);
        var leader2 = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 0, zone: CardZone.Emblem);
        player1.Leader = leader1;
        player2.Leader = leader2;

        var attacker = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 1002, zone: CardZone.Field);
        attacker.FieldTurn = 1;

        var ambushTarget = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 1002, zone: CardZone.Field);
        var normalTarget = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 1002, zone: CardZone.Field);

        gameState.BuffManager.RegisterBuffInstance(
            new Buff(
                sourceAbilityId: Guid.Empty,
                targetCardInstanceId: ambushTarget.InstanceId,
                currentTurn: 0,
                effects: new List<IBuffEffect> { new KeywordBuffEffect(new List<CardKeyword> { CardKeyword.Ambush }) }),
            gameState);

        using var context = new GameContext(sp, mapper, accessor, gameState, requestingPlayerId: p1Id);
        Assert.False(attacker.CanPerformAttack(gameState, player1, ambushTarget));

        gameState.BuffManager.RegisterBuffInstance(
            new Buff(
                sourceAbilityId: Guid.Empty,
                targetCardInstanceId: attacker.InstanceId,
                currentTurn: 0,
                effects: new List<IBuffEffect> { new KeywordBuffEffect(new List<CardKeyword> { CardKeyword.Ambush }) }),
            gameState);

        var attackResult = new AttackEvent(attacker, normalTarget).Commit(context);
        Assert.True(attackResult.Success, attackResult.ErrorMessage);
        CommitEventsRecursively(context, attackResult.GeneratedEvents);
        Assert.False(attacker.HasKeyword(CardKeyword.Ambush));
    }

    [Fact]
    public void DestroyResist_BlocksEffectDestroy()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        EnsureGameDataProvider(sp);

        var mapper = sp.GetRequiredService<AutoMapper.IMapper>();
        var accessor = sp.GetRequiredService<IGameContextAccessor>();
        GameServiceProvider.ContextAccessor = accessor;

        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var player1 = new PlayerState(p1Id);
        var player2 = new PlayerState(p2Id);

        var gameState = new GameState("keyword-destroy-resist", player1, player2, firstPlayerId: p1Id)
        {
            CurrentPlayerId = p2Id,
            BuffManager = sp.GetRequiredService<IBuffManager>()
        };

        var abilityFactory = sp.GetRequiredService<IAbilityFactory>();
        gameState.AbilityManager = new AbilityManager(NullLogger<AbilityManager>.Instance, abilityFactory, new List<IAbility>());

        var leader1 = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 0, zone: CardZone.Emblem);
        var leader2 = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 0, zone: CardZone.Emblem);
        player1.Leader = leader1;
        player2.Leader = leader2;

        var source = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 1002, zone: CardZone.Field);
        var target = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 1002, zone: CardZone.Field);

        gameState.BuffManager.RegisterBuffInstance(
            new Buff(
                sourceAbilityId: Guid.Empty,
                targetCardInstanceId: target.InstanceId,
                currentTurn: 0,
                effects: new List<IBuffEffect> { new KeywordBuffEffect(new List<CardKeyword> { CardKeyword.DestroyResist }) }),
            gameState);

        using var context = new GameContext(sp, mapper, accessor, gameState, requestingPlayerId: p2Id);
        var destroyResult = new DestroyEvent(target, source, "Effect").Commit(context);
        Assert.True(destroyResult.Success, destroyResult.ErrorMessage);
        Assert.Empty(destroyResult.GeneratedEvents);
        Assert.Equal(CardZone.Field, target.CurrentZone);
    }

    [Fact]
    public void VanishResist_BlocksVanishEffect()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        EnsureGameDataProvider(sp);

        var mapper = sp.GetRequiredService<AutoMapper.IMapper>();
        var accessor = sp.GetRequiredService<IGameContextAccessor>();
        GameServiceProvider.ContextAccessor = accessor;

        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var player1 = new PlayerState(p1Id);
        var player2 = new PlayerState(p2Id);

        var gameState = new GameState("keyword-vanish-resist", player1, player2, firstPlayerId: p1Id)
        {
            CurrentPlayerId = p2Id,
            BuffManager = sp.GetRequiredService<IBuffManager>()
        };

        var abilityFactory = sp.GetRequiredService<IAbilityFactory>();
        gameState.AbilityManager = new AbilityManager(NullLogger<AbilityManager>.Instance, abilityFactory, new List<IAbility>());

        var leader1 = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 0, zone: CardZone.Emblem);
        var leader2 = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 0, zone: CardZone.Emblem);
        player1.Leader = leader1;
        player2.Leader = leader2;

        var source = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 1002, zone: CardZone.Field);
        var target = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 1002, zone: CardZone.Field);

        gameState.BuffManager.RegisterBuffInstance(
            new Buff(
                sourceAbilityId: Guid.Empty,
                targetCardInstanceId: target.InstanceId,
                currentTurn: 0,
                effects: new List<IBuffEffect> { new KeywordBuffEffect(new List<CardKeyword> { CardKeyword.VanishResist }) }),
            gameState);

        using var context = new GameContext(sp, mapper, accessor, gameState, requestingPlayerId: p2Id);

        var handler = new VanishEffectHandler(
            NullLogger<VanishEffectHandler>.Instance,
            new TargetProcessingService(NullLogger<TargetProcessingService>.Instance));

        var effectConfig = new EffectConfig { Type = EffectType.Vanish };
        var effect = new Effect(
            effectConfig,
            requestingPlayerId: p2Id,
            selectedTargets: new List<TargetInfo> { TargetInfo.ForCard(target) },
            parentAbilityId: null,
            triggeredEventId: Guid.Empty,
            sourceCardId: source.InstanceId);

        var applyResult = handler.Apply(effect, context, new VanishEffectParams { IgnoreVanishResist = false, VanishReason = "Effect" });
        Assert.True(applyResult.Success, applyResult.ErrorMessage);
        Assert.DoesNotContain(applyResult.GeneratedEvents, e => e is CardVanishedEvent);
        Assert.Equal(CardZone.Field, target.CurrentZone);
    }

    [Fact]
    public void FreeEvolve_DoesNotConsumeEvolutionPoints()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        EnsureGameDataProvider(sp);

        var mapper = sp.GetRequiredService<AutoMapper.IMapper>();
        var accessor = sp.GetRequiredService<IGameContextAccessor>();
        GameServiceProvider.ContextAccessor = accessor;

        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var player1 = new PlayerState(p1Id) { IsFirst = true };
        var player2 = new PlayerState(p2Id);

        player1.MaxEvolutionPoints = 1;
        player1.CurrentEvolutionPoints = 0;
        player1.CanEvolve = true;
        player1.TurnNumber = GameConstants.Evolution.FirstPlayerEvolutionStartTurn;

        var gameState = new GameState("keyword-free-evolve", player1, player2, firstPlayerId: p1Id)
        {
            CurrentPlayerId = p1Id,
            BuffManager = sp.GetRequiredService<IBuffManager>(),
            Phase = GamePhase.Main
        };

        var abilityFactory = sp.GetRequiredService<IAbilityFactory>();
        gameState.AbilityManager = new AbilityManager(NullLogger<AbilityManager>.Instance, abilityFactory, new List<IAbility>());

        var leader1 = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 0, zone: CardZone.Emblem);
        var leader2 = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 0, zone: CardZone.Emblem);
        player1.Leader = leader1;
        player2.Leader = leader2;

        var target = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 1002, zone: CardZone.Field);

        gameState.BuffManager.RegisterBuffInstance(
            new Buff(
                sourceAbilityId: Guid.Empty,
                targetCardInstanceId: target.InstanceId,
                currentTurn: 0,
                effects: new List<IBuffEffect> { new KeywordBuffEffect(new List<CardKeyword> { CardKeyword.FreeEvolve }) }),
            gameState);

        using var context = new GameContext(sp, mapper, accessor, gameState, requestingPlayerId: p1Id);
        var originalEp = player1.CurrentEvolutionPoints;

        var evolveAction = new EvolveActionDto { UnitUid = target.InstanceId.ToString() };
        var evolveResult = new EvolveRequestedEvent(p1Id, evolveAction).Commit(context);

        Assert.True(evolveResult.Success, evolveResult.ErrorMessage);
        Assert.DoesNotContain(evolveResult.GeneratedEvents, e => e is EpUpdateEvent);
        CommitEventsRecursively(context, evolveResult.GeneratedEvents);
        Assert.Equal(originalEp, player1.CurrentEvolutionPoints);
    }

    [Fact]
    public void BanEvolve_BlocksEvolveAndSuperEvolve()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        EnsureGameDataProvider(sp);

        var mapper = sp.GetRequiredService<AutoMapper.IMapper>();
        var accessor = sp.GetRequiredService<IGameContextAccessor>();
        GameServiceProvider.ContextAccessor = accessor;

        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var player1 = new PlayerState(p1Id) { IsFirst = true };
        var player2 = new PlayerState(p2Id);

        player1.MaxEvolutionPoints = 1;
        player1.CurrentEvolutionPoints = 1;
        player1.CanEvolve = true;
        player1.MaxSuperEvolutionPoints = 1;
        player1.CurrentSuperEvolutionPoints = 1;
        player1.CanSuperEvolve = true;
        player1.TurnNumber = Math.Max(GameConstants.Evolution.FirstPlayerEvolutionStartTurn, GameConstants.SuperEvolution.FirstPlayerSuperEvolutionStartTurn);

        var gameState = new GameState("keyword-ban-evolve", player1, player2, firstPlayerId: p1Id)
        {
            CurrentPlayerId = p1Id,
            BuffManager = sp.GetRequiredService<IBuffManager>(),
            Phase = GamePhase.Main
        };

        var abilityFactory = sp.GetRequiredService<IAbilityFactory>();
        gameState.AbilityManager = new AbilityManager(NullLogger<AbilityManager>.Instance, abilityFactory, new List<IAbility>());

        var leader1 = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 0, zone: CardZone.Emblem);
        var leader2 = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 0, zone: CardZone.Emblem);
        player1.Leader = leader1;
        player2.Leader = leader2;

        var target = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 1002, zone: CardZone.Field);

        gameState.BuffManager.RegisterBuffInstance(
            new Buff(
                sourceAbilityId: Guid.Empty,
                targetCardInstanceId: target.InstanceId,
                currentTurn: 0,
                effects: new List<IBuffEffect> { new KeywordBuffEffect(new List<CardKeyword> { CardKeyword.BanEvolve }) }),
            gameState);

        using var context = new GameContext(sp, mapper, accessor, gameState, requestingPlayerId: p1Id);

        var evolveAction = new EvolveActionDto { UnitUid = target.InstanceId.ToString() };
        var evolveResult = new EvolveRequestedEvent(p1Id, evolveAction).Commit(context);
        Assert.False(evolveResult.Success);

        var superAction = new SuperEvolveActionDto { UnitUid = target.InstanceId.ToString() };
        var superResult = new SuperEvolveRequestedEvent(p1Id, superAction).Commit(context);
        Assert.False(superResult.Success);
    }

    [Fact]
    public void CountdownTick_ReachesZeroAndDestroys()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        EnsureGameDataProvider(sp);

        var mapper = sp.GetRequiredService<AutoMapper.IMapper>();
        var accessor = sp.GetRequiredService<IGameContextAccessor>();
        GameServiceProvider.ContextAccessor = accessor;

        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var player1 = new PlayerState(p1Id);
        var player2 = new PlayerState(p2Id);

        var gameState = new GameState("keyword-countdown", player1, player2, firstPlayerId: p1Id)
        {
            CurrentPlayerId = p1Id,
            BuffManager = sp.GetRequiredService<IBuffManager>(),
            Phase = GamePhase.Main
        };

        var abilityFactory = sp.GetRequiredService<IAbilityFactory>();
        gameState.AbilityManager = new AbilityManager(NullLogger<AbilityManager>.Instance, abilityFactory, new List<IAbility>());

        var leader1 = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 0, zone: CardZone.Emblem);
        var leader2 = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 0, zone: CardZone.Emblem);
        player1.Leader = leader1;
        player2.Leader = leader2;

        var amulet = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 1002, zone: CardZone.Field);
        var countdownProperty = typeof(CardInstance).GetProperty("CurrentCountdown", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        countdownProperty?.SetValue(amulet, 1);

        using var context = new GameContext(sp, mapper, accessor, gameState, requestingPlayerId: p1Id);
        var tickResult = new CountdownTickEvent(amulet, -1).Commit(context);
        Assert.True(tickResult.Success, tickResult.ErrorMessage);
        CommitEventsRecursively(context, tickResult.GeneratedEvents);
        Assert.Equal(CardZone.Graveyard, amulet.CurrentZone);
    }

    [Fact]
    public void Drain_HealsOnlyOnCombatDamage_AndBane_KillsOnlyOnCombatDamage()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var sp = scope.ServiceProvider;
        EnsureGameDataProvider(sp);

        var mapper = sp.GetRequiredService<AutoMapper.IMapper>();
        var accessor = sp.GetRequiredService<IGameContextAccessor>();
        GameServiceProvider.ContextAccessor = accessor;

        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var player1 = new PlayerState(p1Id);
        var player2 = new PlayerState(p2Id);

        var gameState = new GameState("keyword-drain-bane", player1, player2, firstPlayerId: p1Id)
        {
            CurrentPlayerId = p1Id,
            BuffManager = sp.GetRequiredService<IBuffManager>()
        };

        var abilityFactory = sp.GetRequiredService<IAbilityFactory>();
        gameState.AbilityManager = new AbilityManager(NullLogger<AbilityManager>.Instance, abilityFactory, new List<IAbility>());

        var leader1 = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 1002, zone: CardZone.Emblem);
        var leader2 = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 1002, zone: CardZone.Emblem);
        player1.Leader = leader1;
        player2.Leader = leader2;
        leader1.Damage = 1;

        var attacker = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 1002, zone: CardZone.Field);
        attacker.FieldTurn = 1;
        var defender = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 1002, zone: CardZone.Field);
        defender.Damage = -10;

        gameState.BuffManager.RegisterBuffInstance(
            new Buff(
                sourceAbilityId: Guid.Empty,
                targetCardInstanceId: attacker.InstanceId,
                currentTurn: 0,
                effects: new List<IBuffEffect> { new KeywordBuffEffect(new List<CardKeyword> { CardKeyword.Drain, CardKeyword.Bane }) }),
            gameState);

        using var context = new GameContext(sp, mapper, accessor, gameState, requestingPlayerId: p1Id);
        Assert.True(attacker.HasKeyword(CardKeyword.Drain));
        Assert.True(attacker.HasKeyword(CardKeyword.Bane));
        Assert.True(defender.CurrentHealth > 0);

        // Combat: Drain/Bane should both be triggered (healing + destroy follower).
        var combatDamage = new DamageEvent(defender, 1, attacker, DamageKind.Combat);
        var combatCommit = combatDamage.Commit(context);
        Assert.True(combatCommit.Success, combatCommit.ErrorMessage);
        Assert.Contains(combatCommit.GeneratedEvents, e => e is HealEvent);
        Assert.Contains(combatCommit.GeneratedEvents, e => e is DestroyEvent);
        CommitEventsRecursively(context, combatCommit.GeneratedEvents);
        Assert.Equal(CardZone.Graveyard, defender.CurrentZone);

        // Reset: revive defender as a new card for ability damage checks.
        var defender2 = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 1002, zone: CardZone.Field);
        defender2.Damage = -10;

        var abilityDamage = new DamageEvent(defender2, 1, attacker, DamageKind.Ability);
        var abilityCommit = abilityDamage.Commit(context);
        Assert.True(abilityCommit.Success, abilityCommit.ErrorMessage);
        Assert.DoesNotContain(abilityCommit.GeneratedEvents, e => e is HealEvent);
        Assert.DoesNotContain(abilityCommit.GeneratedEvents, e => e is DestroyEvent && ((DestroyEvent)e).Reason == "DestroyedByBane");
        CommitEventsRecursively(context, abilityCommit.GeneratedEvents);
        Assert.NotEqual(CardZone.Graveyard, defender2.CurrentZone);
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

    private static async System.Threading.Tasks.Task EnsureGameDataProviderAsync(IServiceProvider serviceProvider)
    {
        var gdp = serviceProvider.GetRequiredService<IGameDataProvider>();
        GameDataProvider.SetInstanceForTesting(gdp);
        await gdp.RefreshCacheAsync();
    }
}
