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
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Infrastructure.Services;

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
    }
}
