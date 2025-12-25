using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace ShadowverseBeyond.Server.Tests;

[Collection("DatabaseCollection")]
public class AbilityEditorHappyPathIntegrationTests
{
    private readonly DatabaseFixture _fixture;

    public AbilityEditorHappyPathIntegrationTests(DatabaseFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _fixture.OutputHelper = output;
    }

    [Fact]
    public async System.Threading.Tasks.Task DamageAbility_PlayCard_DealsDamageToEnemyFollower()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var sp = scope.ServiceProvider;

        var mapper = sp.GetRequiredService<AutoMapper.IMapper>();
        var accessor = sp.GetRequiredService<IGameContextAccessor>();
        GameServiceProvider.ContextAccessor = accessor;

        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var player1 = new PlayerState(p1Id);
        var player2 = new PlayerState(p2Id);

        var gameState = new GameState("ability-editor-happy-path-damage", player1, player2, firstPlayerId: p1Id)
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

        var sourceCard = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 1001, zone: CardZone.Hand);
        var enemyFollower = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 1002, zone: CardZone.Field);

        var config = new AbilityConfig
        {
            Name = "HappyPath Damage",
            Type = AbilityType.Triggered,
            Trigger = new TriggerConfig
            {
                EventTypes = new List<EventType> { EventType.PlayCard },
                Timing = TriggerTiming.On
            },
            Targets = new Dictionary<string, TargetConfig>
            {
                ["target"] = new TargetConfig
                {
                    RequireValidTarget = true,
                    OnFailure = FailureHandling.AbortAbility,
                    Query = new QueryConfig
                    {
                        SourcePoolExpression = "opponent.field",
                        Operations = new List<IQueryOperation>
                        {
                            new SelectionOperation(SelectionStrategy.First, new StaticValueConfig { Value = 1 })
                        }
                    }
                }
            },
            Effects = new List<EffectConfig>
            {
                new EffectConfig
                {
                    Type = EffectType.Damage,
                    Parameters = new Dictionary<string, ValueConfig>
                    {
                        ["DamageAmount"] = new StaticValueConfig { Value = 1 }
                    }
                }
            }
        };

        var runtimeAbility = abilityFactory.CreateAbility(config, sourceCard);
        gameState.AbilityManager.RegisterAbility(runtimeAbility);

        var playEvent = new PlayCardEvent(player1, sourceCard);
        using var context = new GameContext(sp, mapper, accessor, gameState, requestingPlayerId: p1Id, triggeringEvent: playEvent, sourceCard: sourceCard);

        var hpBefore = enemyFollower.CurrentHealth;

        var triggered = await gameState.AbilityManager.GetTriggeredAbilitiesAsync(playEvent, context, TriggerTiming.On);
        var ability = Assert.Single(triggered);

        var processResult = await gameState.AbilityManager.ProcessSingleAbilityAsync(ability, context, isCancellable: false);
        Assert.True(processResult.IsSuccess);
        Assert.Single(processResult.Effects);

        var effectExecutor = CreateEffectExecutor(sp);
        var effectResult = effectExecutor.ExecuteEffect(processResult.Effects.Single(), context);
        Assert.True(effectResult.Success);

        var damageEvent = Assert.Single(effectResult.GeneratedEvents.OfType<DamageEvent>());
        var damageCommit = damageEvent.Commit(context);
        Assert.True(damageCommit.Success);
        Assert.Contains(damageCommit.GeneratedEvents, e => e is HealthChangedEvent);
        CommitEventsRecursively(context, damageCommit.GeneratedEvents);

        Assert.Equal(1, enemyFollower.Damage);
        Assert.Equal(hpBefore - 1, enemyFollower.CurrentHealth);
    }

    [Fact]
    public async System.Threading.Tasks.Task DrawAbility_PlayCard_DrawsCardsForPlayer()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var sp = scope.ServiceProvider;

        var mapper = sp.GetRequiredService<AutoMapper.IMapper>();
        var accessor = sp.GetRequiredService<IGameContextAccessor>();
        GameServiceProvider.ContextAccessor = accessor;

        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var player1 = new PlayerState(p1Id);
        var player2 = new PlayerState(p2Id);

        var gameState = new GameState("ability-editor-happy-path-draw", player1, player2, firstPlayerId: p1Id)
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

        var deckCard = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 1002, zone: CardZone.Deck);
        var sourceCard = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 1001, zone: CardZone.Hand);

        var config = new AbilityConfig
        {
            Name = "HappyPath Draw",
            Type = AbilityType.Triggered,
            Trigger = new TriggerConfig
            {
                EventTypes = new List<EventType> { EventType.PlayCard },
                Timing = TriggerTiming.On
            },
            Targets = new Dictionary<string, TargetConfig>
            {
                ["player"] = new TargetConfig
                {
                    RequireValidTarget = true,
                    OnFailure = FailureHandling.AbortAbility,
                    Query = new QueryConfig
                    {
                        SourcePoolExpression = "player",
                        Operations = new List<IQueryOperation>()
                    }
                }
            },
            Effects = new List<EffectConfig>
            {
                new EffectConfig
                {
                    Type = EffectType.Draw,
                    Parameters = new Dictionary<string, ValueConfig>
                    {
                        ["DrawAmount"] = new StaticValueConfig { Value = 1 }
                    }
                }
            }
        };

        var runtimeAbility = abilityFactory.CreateAbility(config, sourceCard);
        gameState.AbilityManager.RegisterAbility(runtimeAbility);

        var playEvent = new PlayCardEvent(player1, sourceCard);
        using var context = new GameContext(sp, mapper, accessor, gameState, requestingPlayerId: p1Id, triggeringEvent: playEvent, sourceCard: sourceCard);

        Assert.Contains(deckCard, player1.Deck);
        var handBefore = player1.Hand.Count;
        var deckBefore = player1.Deck.Count;

        var triggered = await gameState.AbilityManager.GetTriggeredAbilitiesAsync(playEvent, context, TriggerTiming.On);
        var ability = Assert.Single(triggered);

        var processResult = await gameState.AbilityManager.ProcessSingleAbilityAsync(ability, context, isCancellable: false);
        Assert.True(processResult.IsSuccess);
        Assert.Single(processResult.Effects);

        var effectExecutor = CreateEffectExecutor(sp);
        var effectResult = effectExecutor.ExecuteEffect(processResult.Effects.Single(), context);
        Assert.True(effectResult.Success);

        CommitEventsRecursively(context, effectResult.GeneratedEvents);

        Assert.Equal(handBefore + 1, player1.Hand.Count);
        Assert.Equal(deckBefore - 1, player1.Deck.Count);
    }

    [Fact]
    public async System.Threading.Tasks.Task ConditionalAbility_DamageEvent_UsesEventIntFieldSelector()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var sp = scope.ServiceProvider;

        var mapper = sp.GetRequiredService<AutoMapper.IMapper>();
        var accessor = sp.GetRequiredService<IGameContextAccessor>();
        GameServiceProvider.ContextAccessor = accessor;

        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var player1 = new PlayerState(p1Id);
        var player2 = new PlayerState(p2Id);

        var gameState = new GameState("ability-editor-happy-path-conditional", player1, player2, firstPlayerId: p1Id)
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

        // Ensure draw has something to pull.
        CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 1002, zone: CardZone.Deck);

        var sourceCard = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 1001, zone: CardZone.Hand);
        var damageSource = CreateRegisteredCard(gameState, ownerPlayerId: p2Id, definitionId: 1002, zone: CardZone.Field);
        var damageTarget = CreateRegisteredCard(gameState, ownerPlayerId: p1Id, definitionId: 1002, zone: CardZone.Field);

        var config = new AbilityConfig
        {
            Name = "HappyPath Conditional",
            Type = AbilityType.Triggered,
            Trigger = new TriggerConfig
            {
                EventTypes = new List<EventType> { EventType.Damage },
                Timing = TriggerTiming.On
            },
            Conditions = new List<ConditionConfig>
            {
                ConditionConfig.FromExpression("eventInt(\"Damage\") >= 2")
            },
            Targets = new Dictionary<string, TargetConfig>
            {
                ["player"] = new TargetConfig
                {
                    RequireValidTarget = true,
                    OnFailure = FailureHandling.AbortAbility,
                    Query = new QueryConfig
                    {
                        SourcePoolExpression = "player",
                        Operations = new List<IQueryOperation>()
                    }
                }
            },
            Effects = new List<EffectConfig>
            {
                new EffectConfig
                {
                    Type = EffectType.Draw,
                    Parameters = new Dictionary<string, ValueConfig>
                    {
                        ["DrawAmount"] = new StaticValueConfig { Value = 1 }
                    }
                }
            }
        };

        var runtimeAbility = abilityFactory.CreateAbility(config, sourceCard);
        gameState.AbilityManager.RegisterAbility(runtimeAbility);

        // Case 1: Damage=1, condition fails -> no effects.
        var smallDamage = new DamageEvent(damageTarget, damage: 1, source: damageSource);
        using (var context = new GameContext(sp, mapper, accessor, gameState, requestingPlayerId: p1Id, triggeringEvent: smallDamage, sourceCard: sourceCard))
        {
            var triggered = await gameState.AbilityManager.GetTriggeredAbilitiesAsync(smallDamage, context, TriggerTiming.On);
            var ability = Assert.Single(triggered);

            var processResult = await gameState.AbilityManager.ProcessSingleAbilityAsync(ability, context, isCancellable: false);
            Assert.True(processResult.IsSuccess);
            Assert.Empty(processResult.Effects);
        }

        // Case 2: Damage=3, condition passes -> draw executes.
        var handBefore = player1.Hand.Count;
        var bigDamage = new DamageEvent(damageTarget, damage: 3, source: damageSource);
        using (var context = new GameContext(sp, mapper, accessor, gameState, requestingPlayerId: p1Id, triggeringEvent: bigDamage, sourceCard: sourceCard))
        {
            var triggered = await gameState.AbilityManager.GetTriggeredAbilitiesAsync(bigDamage, context, TriggerTiming.On);
            var ability = Assert.Single(triggered);

            var processResult = await gameState.AbilityManager.ProcessSingleAbilityAsync(ability, context, isCancellable: false);
            Assert.True(processResult.IsSuccess);
            Assert.Single(processResult.Effects);

            var effectExecutor = CreateEffectExecutor(sp);
            var effectResult = effectExecutor.ExecuteEffect(processResult.Effects.Single(), context);
            Assert.True(effectResult.Success);

            CommitEventsRecursively(context, effectResult.GeneratedEvents);
        }

        Assert.Equal(handBefore + 1, player1.Hand.Count);
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
        var queue = new Queue<IGameEvent>(events ?? Array.Empty<IGameEvent>());

        while (queue.Count > 0)
        {
            var next = queue.Dequeue();
            if (next is not ICommittableEvent committable)
            {
                continue;
            }

            var op = committable.Commit(context);
            if (!op.Success)
            {
                throw new InvalidOperationException(op.ErrorMessage ?? $"Commit failed for event {next.EventType}");
            }

            foreach (var generated in op.GeneratedEvents ?? new List<IGameEvent>())
            {
                queue.Enqueue(generated);
            }
        }
    }

    private static IEffectExecutor CreateEffectExecutor(IServiceProvider sp)
    {
        var valueSourceResolver = sp.GetRequiredService<IValueSourceResolver>();
        var registry = new EffectHandlerRegistry(NullLogger<EffectHandlerRegistry>.Instance);
        return new EffectExecutor(sp, valueSourceResolver, NullLogger<EffectExecutor>.Instance, registry);
    }
}
