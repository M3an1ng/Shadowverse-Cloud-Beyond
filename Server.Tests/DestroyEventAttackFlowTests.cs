using System;
using System.Collections.Generic;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class DestroyEventAttackFlowTests
    {
        private const int FollowerDefinitionId = 1001;
        private const int LeaderDefinitionId = 9001;

        [Fact]
        public void Commit_WhenAttackFlowMatchesSuperEvolvedAttacker_DealsLeaderPing()
        {
            SetTestGameDataProvider();
            var services = CreateServiceProvider();
            var gameState = CreateGameStateWithLeaders(out var attacker, out var defender);
            attacker.ApplyEvolutionChange(null!, isSuperEvolution: true);

            var attackFlow = new AttackFlowContext(Guid.NewGuid(), attacker.InstanceId, defender.InstanceId);
            using var context = CreateContext(services, gameState, gameState.Player1.PlayerId, attackFlow);

            var destroyEvent = new DestroyEvent(defender, attacker, EventReasonSource.System);
            var result = destroyEvent.Commit(context);

            Assert.True(result.Success);
            var ping = Assert.Single(result.GeneratedEvents.OfType<DamageEvent>());
            Assert.Equal(1, ping.Damage);
            Assert.Equal(DamageKind.Ability, ping.Kind);
            Assert.Equal(gameState.Player2.Leader.InstanceId, ping.DamageTarget?.InstanceId);
        }

        [Fact]
        public void Commit_WhenNoAttackFlow_DoesNotDealLeaderPing()
        {
            SetTestGameDataProvider();
            var services = CreateServiceProvider();
            var gameState = CreateGameStateWithLeaders(out var attacker, out var defender);
            attacker.ApplyEvolutionChange(null!, isSuperEvolution: true);

            using var context = CreateContext(services, gameState, gameState.Player1.PlayerId, attackFlow: null);

            var destroyEvent = new DestroyEvent(defender, attacker, EventReasonSource.System);
            var result = destroyEvent.Commit(context);

            Assert.True(result.Success);
            Assert.Empty(result.GeneratedEvents.OfType<DamageEvent>());
        }

        [Fact]
        public void Commit_WhenSameTargetDestroyedTwiceInSameFlow_DealsLeaderPingOnce()
        {
            SetTestGameDataProvider();
            var services = CreateServiceProvider();
            var gameState = CreateGameStateWithLeaders(out var attacker, out var defender);
            attacker.ApplyEvolutionChange(null!, isSuperEvolution: true);

            var attackFlow = new AttackFlowContext(Guid.NewGuid(), attacker.InstanceId, defender.InstanceId);
            using var context = CreateContext(services, gameState, gameState.Player1.PlayerId, attackFlow);

            var first = new DestroyEvent(defender, attacker, EventReasonSource.System).Commit(context);
            var second = new DestroyEvent(defender, attacker, EventReasonSource.System).Commit(context);

            Assert.True(first.Success);
            Assert.True(second.Success);
            Assert.Single(first.GeneratedEvents.OfType<DamageEvent>());
            Assert.Empty(second.GeneratedEvents.OfType<DamageEvent>());
        }

        private static ServiceProvider CreateServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            var buffServiceMock = new Mock<IBuffService>();
            buffServiceMock
                .Setup(service => service.GetEffectiveKeywords(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(new List<CardKeyword>());
            buffServiceMock
                .Setup(service => service.GetEffectiveAttack(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns<CardInstance, GameContext>((card, _) => card.BaseAttack);
            buffServiceMock
                .Setup(service => service.GetEffectiveMaxHealth(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns<CardInstance, GameContext>((card, _) => card.BaseHealth);
            buffServiceMock
                .Setup(service => service.GetEffectiveCost(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns<CardInstance, GameContext>((card, _) => card.BaseCost);
            services.AddSingleton<IBuffService>(buffServiceMock.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            return services.BuildServiceProvider();
        }

        private static GameState CreateGameStateWithLeaders(out CardInstance attacker, out CardInstance defender)
        {
            var p1Id = Guid.NewGuid();
            var p2Id = Guid.NewGuid();
            var player1 = new PlayerState(p1Id);
            var player2 = new PlayerState(p2Id);
            var abilityManagerMock = new Mock<IAbilityManager>();
            abilityManagerMock
                .Setup(manager => manager.GetEffectiveAbilitiesForCard(It.IsAny<Guid>(), It.IsAny<GameContext>()))
                .Returns(new List<IAbility>());
            var gameState = new GameState("destroy-attack-flow", player1, player2, p1Id)
            {
                AbilityManager = abilityManagerMock.Object,
                BuffManager = Mock.Of<IBuffManager>()
            };

            var leader1 = new CardInstance(Guid.NewGuid(), LeaderDefinitionId, p1Id) { CurrentZone = CardZone.Field };
            var leader2 = new CardInstance(Guid.NewGuid(), LeaderDefinitionId, p2Id) { CurrentZone = CardZone.Field };
            player1.Leader = leader1;
            player2.Leader = leader2;
            gameState.RegisterCardInstance(leader1);
            gameState.RegisterCardInstance(leader2);

            attacker = new CardInstance(Guid.NewGuid(), FollowerDefinitionId, p1Id) { CurrentZone = CardZone.Field };
            defender = new CardInstance(Guid.NewGuid(), FollowerDefinitionId, p2Id) { CurrentZone = CardZone.Field };
            player1.Field.Add(attacker);
            player2.Field.Add(defender);
            gameState.RegisterCardInstance(attacker);
            gameState.RegisterCardInstance(defender);
            return gameState;
        }

        private static GameContext CreateContext(ServiceProvider services, GameState gameState, Guid requestingPlayerId, AttackFlowContext? attackFlow)
        {
            var accessor = services.GetRequiredService<IGameContextAccessor>();
            GameServiceProvider.ContextAccessor = accessor;
            return new GameContext(
                services,
                new Mock<IMapper>().Object,
                accessor,
                gameState,
                requestingPlayerId,
                attackFlow: attackFlow);
        }

        private static void SetTestGameDataProvider()
        {
            var mockProvider = new Mock<IGameDataProvider>();
            mockProvider.SetupGet(provider => provider.IsInitialized).Returns(true);
            mockProvider.Setup(provider => provider.GetCardDefinition(FollowerDefinitionId)).Returns(new CardDefinition
            {
                Id = FollowerDefinitionId,
                Name = "DestroyFlowFollower",
                Type = CardType.Follower,
                Attack = 2,
                Health = 2
            });
            mockProvider.Setup(provider => provider.GetCardDefinition(LeaderDefinitionId)).Returns(new CardDefinition
            {
                Id = LeaderDefinitionId,
                Name = "DestroyFlowLeader",
                Type = CardType.Leader,
                Attack = 0,
                Health = 20
            });
            mockProvider.Setup(provider => provider.GetAbilityConfigs(It.IsAny<int>())).Returns(new List<AbilityConfig>());
            GameDataProvider.SetInstanceForTesting(mockProvider.Object);
        }
    }
}
