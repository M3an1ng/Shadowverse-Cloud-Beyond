using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.GameFlow;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Dtos.Enums;

using Microsoft.Extensions.Logging.Abstractions;

namespace ShadowverseBeyond.Server.Tests
{
    public class AbilityManagerTests
    {
        private readonly Mock<ILogger<AbilityManager>> _mockLogger;
        private readonly Mock<IAbilityFactory> _mockAbilityFactory;
        private readonly AbilityManager _abilityManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly Mock<IMapper> _mockMapper;
        private readonly TargetSelectionService _targetSelectionService;
        private readonly Mock<IQueryExecutor> _mockQueryExecutor;
        private readonly Mock<IPendingActionService> _mockPendingActionService;

        public AbilityManagerTests()
        {
            _mockLogger = new Mock<ILogger<AbilityManager>>();
            _mockAbilityFactory = new Mock<IAbilityFactory>();
            _mockMapper = new Mock<IMapper>();
            _mockQueryExecutor = new Mock<IQueryExecutor>();
            _mockPendingActionService = new Mock<IPendingActionService>();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(new Mock<IBuffService>().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            _serviceProvider = services.BuildServiceProvider();

            GameServiceProvider.ContextAccessor = _serviceProvider.GetRequiredService<IGameContextAccessor>();

            _targetSelectionService = new TargetSelectionService(
                new NullLogger<TargetSelectionService>(),
                _mockQueryExecutor.Object,
                _mockPendingActionService.Object
            );

            // System abilities can be an empty list for most tests
            _abilityManager = new AbilityManager(_mockLogger.Object, _mockAbilityFactory.Object, new List<IAbility>());
        }

        private GameState CreateTestGameState(PlayerState player1, PlayerState player2)
        {
            return new GameState("test-game", player1, player2, player1.PlayerId);
        }

        private Ability CreateOnEventAbility(
            CardInstance sourceCard,
            EventType eventType,
            long definitionId,
            TriggerTiming timing = TriggerTiming.On)
        {
            var conditionFactory = new Mock<IConditionFactory>();
            var targetFactory = new Mock<ITargetFactory>();

            var config = new AbilityConfig
            {
                Id = definitionId,
                Name = $"Ability-{definitionId}",
                Trigger = new TriggerConfig
                {
                    EventTypes = new List<EventType> { eventType },
                    Timing = timing,
                    IsEnabled = true
                }
            };

            return new Ability(config, conditionFactory.Object, targetFactory.Object, _targetSelectionService, sourceCard);
        }

        [Fact]
        public void CreateAbilitiesForCard_ShouldInitializePlayerCounters_ByScope()
        {
            // Arrange
            var owner = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = CreateTestGameState(owner, opponent);
            var card = new CardInstance(Guid.NewGuid(), 1, owner.PlayerId);

            var abilityConfigs = new List<AbilityConfig>
            {
                new AbilityConfig
                {
                    PlayerCounterRegistrations = new List<PlayerCounterRegistrationConfig>
                    {
                        new() { Name = "owner_only", InitialValue = 2, PlayerScope = PlayerScope.Self },
                        new() { Name = "opponent_only", InitialValue = 3, PlayerScope = PlayerScope.Opponent },
                        new() { Name = "both_sides", InitialValue = 5, PlayerScope = PlayerScope.Both }
                    }
                }
            };

            _mockAbilityFactory
                .Setup(factory => factory.CreateAbilities(card, It.IsAny<IEnumerable<AbilityConfig>>(), null))
                .Returns(new List<Ability>());

            // Act
            _abilityManager.CreateAbilitiesForCard(card, abilityConfigs, null, gameState);

            // Assert
            Assert.Equal(2, owner.GetCounter("owner_only"));
            Assert.Equal(0, opponent.GetCounter("owner_only"));

            Assert.Equal(0, owner.GetCounter("opponent_only"));
            Assert.Equal(3, opponent.GetCounter("opponent_only"));

            Assert.Equal(5, owner.GetCounter("both_sides"));
            Assert.Equal(5, opponent.GetCounter("both_sides"));
        }

        [Fact]
        public void CreateAbilitiesForCard_ShouldKeepCardCounterInitialization()
        {
            // Arrange
            var owner = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = CreateTestGameState(owner, opponent);
            var card = new CardInstance(Guid.NewGuid(), 1, owner.PlayerId);

            var abilityConfigs = new List<AbilityConfig>
            {
                new AbilityConfig
                {
                    CounterRegistrations = new List<CounterRegistrationConfig>
                    {
                        new() { Name = "card_counter", InitialValue = 7 }
                    }
                }
            };

            _mockAbilityFactory
                .Setup(factory => factory.CreateAbilities(card, It.IsAny<IEnumerable<AbilityConfig>>(), null))
                .Returns(new List<Ability>());

            // Act
            _abilityManager.CreateAbilitiesForCard(card, abilityConfigs, null, gameState);

            // Assert
            Assert.Equal(7, card.GetCounter("card_counter"));
        }

        [Fact]
        public void UnregisterAbilitiesFromBuff_ShouldRemoveOnlyBuffRelatedAbilities()
        {
            // Arrange
            var buffId = Guid.NewGuid();
            var playerId = Guid.NewGuid();
            var cardInstance = new CardInstance(Guid.NewGuid(), 1, playerId);

            var player1 = new PlayerState(playerId);
            player1.Hand.Add(cardInstance);
            var player2 = new PlayerState(Guid.NewGuid());

            // Mocking IConditionFactory and ITargetFactory for Ability constructor
            var mockConditionFactory = new Mock<IConditionFactory>();
            var mockTargetFactory = new Mock<ITargetFactory>();

            var abilityConfig1 = new AbilityConfig { Id = 1, Name = "BuffAbility" };
            var abilityConfig2 = new AbilityConfig { Id = 2, Name = "NormalAbility" };

            var buffAbility = new Ability(abilityConfig1, mockConditionFactory.Object, mockTargetFactory.Object, _targetSelectionService, cardInstance, buffId);
            var normalAbility = new Ability(abilityConfig2, mockConditionFactory.Object, mockTargetFactory.Object, _targetSelectionService, cardInstance);

            var gameState = CreateTestGameState(player1, player2);
            gameState.RegisterCardInstance(cardInstance);

            using (var context = new GameContext(_serviceProvider, _mockMapper.Object, _serviceProvider.GetRequiredService<IGameContextAccessor>(), gameState, player1.PlayerId))
            {
                // Manually set the context for the duration of this test.
                // In a real scenario, this would be handled by the GameContextFilter.
                GameServiceProvider.ContextAccessor!.GameContext = context;

                _abilityManager.RegisterAbility(buffAbility);
                _abilityManager.RegisterAbility(normalAbility);

                // Act
                _abilityManager.UnregisterAbilitiesFromBuff(buffId);

                // Assert
                var abilitiesOnCard = _abilityManager.GetAbilitiesForCard(cardInstance.InstanceId);
                Assert.DoesNotContain(buffAbility, abilitiesOnCard);
                Assert.Contains(normalAbility, abilitiesOnCard);
                Assert.Single(abilitiesOnCard);
            }
        }

        [Fact]
        public async Task GetTriggeredAbilitiesAsync_ShouldOrderSameZoneByZoneIndex()
        {
            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = CreateTestGameState(player1, player2);
            gameState.AbilityManager = _abilityManager;
            gameState.BuffManager = new BuffManager(NullLogger<BuffManager>.Instance, _abilityManager);

            var cardA = new CardInstance(Guid.NewGuid(), 1, player1.PlayerId) { CurrentZone = CardZone.Field, ZoneIndex = 2 };
            var cardB = new CardInstance(Guid.NewGuid(), 1, player1.PlayerId) { CurrentZone = CardZone.Field, ZoneIndex = 0 };
            var cardC = new CardInstance(Guid.NewGuid(), 1, player1.PlayerId) { CurrentZone = CardZone.Field, ZoneIndex = 1 };

            player1.Field.AddRange(new[] { cardA, cardB, cardC });
            gameState.RegisterCardInstance(cardA);
            gameState.RegisterCardInstance(cardB);
            gameState.RegisterCardInstance(cardC);

            var abilityA = CreateOnEventAbility(cardA, EventType.TurnStart, 1001);
            var abilityB = CreateOnEventAbility(cardB, EventType.TurnStart, 1002);
            var abilityC = CreateOnEventAbility(cardC, EventType.TurnStart, 1003);

            // Register in reverse of expected board order to ensure sort does not depend on registration order.
            _abilityManager.RegisterAbility(abilityA);
            _abilityManager.RegisterAbility(abilityC);
            _abilityManager.RegisterAbility(abilityB);

            using var context = new GameContext(
                _serviceProvider,
                _mockMapper.Object,
                _serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                player1.PlayerId);

            var triggered = await _abilityManager.GetTriggeredAbilitiesAsync(new TurnStartEvent(player1), context, TriggerTiming.On);
            var orderedCardIds = triggered.Select(a => a.SourceCard!.InstanceId).ToList();
            var expected = new List<Guid> { cardB.InstanceId, cardC.InstanceId, cardA.InstanceId };

            Assert.Equal(expected, orderedCardIds);
        }

        [Fact]
        public async Task GetTriggeredAbilitiesAsync_ShouldUseRegistrationOrderAsTieBreakerWhenZoneIndexEqual()
        {
            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = CreateTestGameState(player1, player2);
            gameState.AbilityManager = _abilityManager;
            gameState.BuffManager = new BuffManager(NullLogger<BuffManager>.Instance, _abilityManager);

            var cardFirst = new CardInstance(Guid.NewGuid(), 1, player1.PlayerId)
            {
                CurrentZone = CardZone.Field,
                ZoneIndex = 0
            };
            var cardSecond = new CardInstance(Guid.NewGuid(), 1, player1.PlayerId)
            {
                CurrentZone = CardZone.Field,
                ZoneIndex = 0
            };

            player1.Field.AddRange(new[] { cardFirst, cardSecond });
            gameState.RegisterCardInstance(cardFirst);
            gameState.RegisterCardInstance(cardSecond);

            var abilityFirst = CreateOnEventAbility(cardFirst, EventType.TurnStart, 2001);
            var abilitySecond = CreateOnEventAbility(cardSecond, EventType.TurnStart, 2002);

            // Reverse registration to verify tie-break follows registration order.
            _abilityManager.RegisterAbility(abilitySecond);
            _abilityManager.RegisterAbility(abilityFirst);

            using var context = new GameContext(
                _serviceProvider,
                _mockMapper.Object,
                _serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                player1.PlayerId);

            var triggered = await _abilityManager.GetTriggeredAbilitiesAsync(new TurnStartEvent(player1), context, TriggerTiming.On);
            var orderedCardIds = triggered.Select(a => a.SourceCard!.InstanceId).ToList();
            var expected = new List<Guid> { cardSecond.InstanceId, cardFirst.InstanceId };

            Assert.Equal(expected, orderedCardIds);
        }

        [Fact]
        public async Task GetTriggeredAbilitiesAsync_ShouldPrioritizeSourceCardBeforeZonePosition()
        {
            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = CreateTestGameState(player1, player2);
            gameState.AbilityManager = _abilityManager;
            gameState.BuffManager = new BuffManager(NullLogger<BuffManager>.Instance, _abilityManager);
            gameState.CurrentPlayerId = player1.PlayerId;

            var attacker = new CardInstance(Guid.NewGuid(), 1, player1.PlayerId)
            {
                CurrentZone = CardZone.Field,
                ZoneIndex = 4
            };
            var defender = new CardInstance(Guid.NewGuid(), 1, player2.PlayerId)
            {
                CurrentZone = CardZone.Field,
                ZoneIndex = 0
            };

            player1.Field.Add(attacker);
            player2.Field.Add(defender);
            gameState.RegisterCardInstance(attacker);
            gameState.RegisterCardInstance(defender);

            var attackerAbility = CreateOnEventAbility(attacker, EventType.Clash, 3001);
            var defenderAbility = CreateOnEventAbility(defender, EventType.Clash, 3002);

            _abilityManager.RegisterAbility(defenderAbility);
            _abilityManager.RegisterAbility(attackerAbility);

            using var context = new GameContext(
                _serviceProvider,
                _mockMapper.Object,
                _serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                player1.PlayerId);

            var triggered = await _abilityManager.GetTriggeredAbilitiesAsync(new ClashEvent(attacker, defender), context, TriggerTiming.On);
            var orderedCardIds = triggered.Select(a => a.SourceCard!.InstanceId).ToList();
            var expected = new List<Guid> { attacker.InstanceId, defender.InstanceId };

            Assert.Equal(expected, orderedCardIds);
        }

        [Fact]
        public async Task GetTriggeredAbilitiesAsync_ShouldPrioritizeEventTargetBeforeOtherCards()
        {
            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = CreateTestGameState(player1, player2);
            gameState.AbilityManager = _abilityManager;
            gameState.BuffManager = new BuffManager(NullLogger<BuffManager>.Instance, _abilityManager);
            gameState.CurrentPlayerId = player1.PlayerId;

            var attacker = new CardInstance(Guid.NewGuid(), 1, player1.PlayerId)
            {
                CurrentZone = CardZone.Field,
                ZoneIndex = 3
            };
            var defender = new CardInstance(Guid.NewGuid(), 1, player2.PlayerId)
            {
                CurrentZone = CardZone.Field,
                ZoneIndex = 0
            };
            var nonTarget = new CardInstance(Guid.NewGuid(), 1, player1.PlayerId)
            {
                CurrentZone = CardZone.Hand,
                ZoneIndex = 0
            };

            player1.Field.Add(attacker);
            player2.Field.Add(defender);
            player1.Hand.Add(nonTarget);
            gameState.RegisterCardInstance(attacker);
            gameState.RegisterCardInstance(defender);
            gameState.RegisterCardInstance(nonTarget);

            var attackerAbility = CreateOnEventAbility(attacker, EventType.Attack, 3401);
            var defenderAbility = CreateOnEventAbility(defender, EventType.Attack, 3402);
            var nonTargetAbility = CreateOnEventAbility(nonTarget, EventType.Attack, 3403);

            _abilityManager.RegisterAbility(nonTargetAbility);
            _abilityManager.RegisterAbility(defenderAbility);
            _abilityManager.RegisterAbility(attackerAbility);

            using var context = new GameContext(
                _serviceProvider,
                _mockMapper.Object,
                _serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                player1.PlayerId);

            var attackEvent = new AttackEvent(attacker, defender);
            var triggered = await _abilityManager.GetTriggeredAbilitiesAsync(attackEvent, context, TriggerTiming.On);
            var orderedCardIds = triggered.Select(a => a.SourceCard!.InstanceId).ToList();
            var expected = new List<Guid> { attacker.InstanceId, defender.InstanceId, nonTarget.InstanceId };

            Assert.Equal(expected, orderedCardIds);
        }

        [Fact]
        public async Task GetTriggeredAbilitiesAsync_ShouldPrioritizeZoneBeforeZonePosition()
        {
            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = CreateTestGameState(player1, player2);
            gameState.AbilityManager = _abilityManager;
            gameState.BuffManager = new BuffManager(NullLogger<BuffManager>.Instance, _abilityManager);
            gameState.CurrentPlayerId = player1.PlayerId;

            var handCard = new CardInstance(Guid.NewGuid(), 1, player1.PlayerId)
            {
                CurrentZone = CardZone.Hand,
                ZoneIndex = 5
            };
            var fieldCard = new CardInstance(Guid.NewGuid(), 1, player1.PlayerId)
            {
                CurrentZone = CardZone.Field,
                ZoneIndex = 0
            };

            player1.Hand.Add(handCard);
            player1.Field.Add(fieldCard);
            gameState.RegisterCardInstance(handCard);
            gameState.RegisterCardInstance(fieldCard);

            var handAbility = CreateOnEventAbility(handCard, EventType.TurnStart, 3501);
            var fieldAbility = CreateOnEventAbility(fieldCard, EventType.TurnStart, 3502);

            // Register in reverse to ensure ordering isn't dependent on registration.
            _abilityManager.RegisterAbility(fieldAbility);
            _abilityManager.RegisterAbility(handAbility);

            using var context = new GameContext(
                _serviceProvider,
                _mockMapper.Object,
                _serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                player1.PlayerId);

            var triggered = await _abilityManager.GetTriggeredAbilitiesAsync(new TurnStartEvent(player1), context, TriggerTiming.On);
            var orderedCardIds = triggered.Select(a => a.SourceCard!.InstanceId).ToList();
            var expected = new List<Guid> { handCard.InstanceId, fieldCard.InstanceId };

            Assert.Equal(expected, orderedCardIds);
        }

        [Fact]
        public async Task GetTriggeredAbilitiesAsync_ShouldUseSameOrderingPolicyForBeforeAndAfterTimings()
        {
            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = CreateTestGameState(player1, player2);
            gameState.AbilityManager = _abilityManager;
            gameState.BuffManager = new BuffManager(NullLogger<BuffManager>.Instance, _abilityManager);

            var left = new CardInstance(Guid.NewGuid(), 1, player1.PlayerId)
            {
                CurrentZone = CardZone.Field,
                ZoneIndex = 1
            };
            var right = new CardInstance(Guid.NewGuid(), 1, player1.PlayerId)
            {
                CurrentZone = CardZone.Field,
                ZoneIndex = 0
            };

            player1.Field.AddRange(new[] { left, right });
            gameState.RegisterCardInstance(left);
            gameState.RegisterCardInstance(right);

            var leftBefore = CreateOnEventAbility(left, EventType.TurnStart, 4001, TriggerTiming.Before);
            var rightBefore = CreateOnEventAbility(right, EventType.TurnStart, 4002, TriggerTiming.Before);
            var leftAfter = CreateOnEventAbility(left, EventType.TurnStart, 4003, TriggerTiming.After);
            var rightAfter = CreateOnEventAbility(right, EventType.TurnStart, 4004, TriggerTiming.After);

            _abilityManager.RegisterAbility(leftBefore);
            _abilityManager.RegisterAbility(leftAfter);
            _abilityManager.RegisterAbility(rightAfter);
            _abilityManager.RegisterAbility(rightBefore);

            using var context = new GameContext(
                _serviceProvider,
                _mockMapper.Object,
                _serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                player1.PlayerId);

            var before = await _abilityManager.GetTriggeredAbilitiesAsync(new TurnStartEvent(player1), context, TriggerTiming.Before);
            var after = await _abilityManager.GetTriggeredAbilitiesAsync(new TurnStartEvent(player1), context, TriggerTiming.After);

            var expected = new List<Guid> { right.InstanceId, left.InstanceId };
            Assert.Equal(expected, before.Select(a => a.SourceCard!.InstanceId).ToList());
            Assert.Equal(expected, after.Select(a => a.SourceCard!.InstanceId).ToList());
        }

        [Fact]
        public async Task GetTriggeredAbilitiesAsync_ShouldFallbackToRegistrationOrderWhenPriorKeysEqual()
        {
            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = CreateTestGameState(player1, player2);
            gameState.AbilityManager = _abilityManager;
            gameState.BuffManager = new BuffManager(NullLogger<BuffManager>.Instance, _abilityManager);

            var card = new CardInstance(Guid.NewGuid(), 1, player1.PlayerId)
            {
                CurrentZone = CardZone.Field,
                ZoneIndex = 0
            };

            player1.Field.Add(card);
            gameState.RegisterCardInstance(card);

            var firstRegistered = CreateOnEventAbility(card, EventType.TurnStart, 5001);
            var secondRegistered = CreateOnEventAbility(card, EventType.TurnStart, 5002);

            _abilityManager.RegisterAbility(firstRegistered);
            _abilityManager.RegisterAbility(secondRegistered);

            // Clear in-card order so policy reaches registration-order fallback.
            card.SetAbilities(Array.Empty<Ability>());

            using var context = new GameContext(
                _serviceProvider,
                _mockMapper.Object,
                _serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                player1.PlayerId);

            var triggered = await _abilityManager.GetTriggeredAbilitiesAsync(new TurnStartEvent(player1), context, TriggerTiming.On);
            var orderedRuntimeIds = triggered.Select(a => a.RuntimeId).ToList();
            var expected = new List<Guid> { firstRegistered.RuntimeId, secondRegistered.RuntimeId };

            Assert.Equal(expected, orderedRuntimeIds);
        }

        [Fact]
        public void CardMovedEvent_LeavingZone_ShouldKeepZoneIndexContiguous()
        {
            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = CreateTestGameState(player1, player2);
            gameState.AbilityManager = _abilityManager;
            gameState.BuffManager = new BuffManager(NullLogger<BuffManager>.Instance, _abilityManager);

            var leftCard = new CardInstance(Guid.NewGuid(), 1, player1.PlayerId)
            {
                CurrentZone = CardZone.Field,
                ZoneIndex = 0
            };
            var rightCard = new CardInstance(Guid.NewGuid(), 1, player1.PlayerId)
            {
                CurrentZone = CardZone.Field,
                ZoneIndex = 1
            };

            player1.Field.Add(leftCard);
            player1.Field.Add(rightCard);
            gameState.RegisterCardInstance(leftCard);
            gameState.RegisterCardInstance(rightCard);

            using var context = new GameContext(
                _serviceProvider,
                _mockMapper.Object,
                _serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                player1.PlayerId);

            var moveEvent = new CardMovedEvent(leftCard, CardZone.Field, CardZone.Hand, CardMoveReason.ReturnedToHand);
            var commitResult = moveEvent.Commit(context);

            Assert.True(commitResult.Success);
            Assert.Single(player1.Field);
            Assert.Equal(rightCard.InstanceId, player1.Field[0].InstanceId);
            Assert.Equal(0, player1.Field[0].ZoneIndex);
            Assert.Equal(CardZone.Hand, leftCard.CurrentZone);
            Assert.Equal(0, leftCard.ZoneIndex);
        }
    }
}
