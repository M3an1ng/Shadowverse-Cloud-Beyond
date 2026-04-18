using System;
using System.Collections.Generic;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Buffs;
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
    public class PlayerRallyCounterTests
    {
        private static void SetTestGameDataProvider(params (int DefinitionId, CardType CardType)[] definitions)
        {
            var mockProvider = new Mock<IGameDataProvider>();
            mockProvider.SetupGet(provider => provider.IsInitialized).Returns(true);
            mockProvider.Setup(provider => provider.GetCardDefinition(It.IsAny<int>())).Returns<int>(definitionId =>
            {
                var definition = Array.Find(definitions, entry => entry.DefinitionId == definitionId);
                return definition == default
                    ? null
                    : new CardDefinition
                    {
                        Id = definitionId,
                        Name = "TestCard",
                        Type = definition.CardType,
                        Attack = 1,
                        Health = 1
                    };
            });
            mockProvider.Setup(provider => provider.GetAbilityConfigs(It.IsAny<int>())).Returns(new List<AbilityConfig>());
            GameDataProvider.SetInstanceForTesting(mockProvider.Object);
        }

        private static IServiceProvider BuildServiceProvider()
        {
            var buffService = new Mock<IBuffService>();
            buffService.Setup(service => service.GetEffectiveAttack(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => card.BaseAttack);
            buffService.Setup(service => service.GetEffectiveMaxHealth(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => card.BaseHealth);
            buffService.Setup(service => service.GetEffectiveCost(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => card.BaseCost);
            buffService.Setup(service => service.GetEffectiveKeywords(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => card.BaseKeywords);
            buffService.Setup(service => service.GetEffectiveActionUsageLimit(It.IsAny<CardInstance>(), It.IsAny<CardActionUsageType>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, CardActionUsageType actionType, GameContext _) => card.GetBaseActionUsageLimit(actionType));
            buffService.Setup(service => service.RemoveAllBuffs(
                    It.IsAny<Guid>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Func<Buff, bool>>()))
                .Returns(new DomainOperationResult { Success = true });

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            return services.BuildServiceProvider();
        }

        private static GameContext BuildContext(GameState gameState, Guid requestingPlayerId, IServiceProvider serviceProvider)
        {
            var mapper = new Mock<IMapper>().Object;
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;
            var context = new GameContext(serviceProvider, mapper, accessor, gameState, requestingPlayerId);
            accessor.GameContext = context;
            return context;
        }

        private static GameState BuildGameState(Guid player1Id, Guid player2Id)
        {
            var abilityManager = new Mock<IAbilityManager>();
            abilityManager.Setup(manager => manager.GetAbilitiesForCard(It.IsAny<Guid>()))
                .Returns(new List<ShadowverseBeyond.Server.Domain.Abilities.Common.IAbility>());
            abilityManager.Setup(manager => manager.GetEffectiveAbilitiesForCard(It.IsAny<Guid>(), It.IsAny<GameContext>()))
                .Returns(new List<ShadowverseBeyond.Server.Domain.Abilities.Common.IAbility>());
            abilityManager.Setup(manager => manager.CreateAbilitiesForCard(
                    It.IsAny<CardInstance>(),
                    It.IsAny<List<AbilityConfig>>(),
                    It.IsAny<Guid?>()))
                .Returns(new List<Ability>());
            abilityManager.Setup(manager => manager.CreateAbilitiesForCard(
                    It.IsAny<CardInstance>(),
                    It.IsAny<List<AbilityConfig>>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<GameState>()))
                .Returns(new List<Ability>());

            var player1 = new PlayerState(player1Id);
            var player2 = new PlayerState(player2Id);
            return new GameState("rally-test", player1, player2, player1Id)
            {
                AbilityManager = abilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };
        }

        [Fact]
        public void CardMovedEvent_Commit_IncrementsRally_WhenFollowerIsSuccessfullySummoned()
        {
            const int definitionId = 1;
            SetTestGameDataProvider((definitionId, CardType.Follower));

            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var gameState = BuildGameState(player1Id, player2Id);
            var serviceProvider = BuildServiceProvider();
            using var context = BuildContext(gameState, player1Id, serviceProvider);

            var follower = new CardInstance(Guid.NewGuid(), definitionId, player1Id)
            {
                CurrentZone = CardZone.Hand
            };
            gameState.Player1.Hand.Add(follower);

            var evt = new CardMovedEvent(follower, CardZone.Hand, CardZone.Field, CardMoveReason.Summoned);
            var result = evt.Commit(context);

            Assert.True(result.Success);
            Assert.Equal(1, gameState.Player1.Rally);
            var rallyEvent = Assert.Single(result.GeneratedEvents.OfType<CounterChangedEvent>());
            Assert.Equal(PlayerState.RallyCounterKey, rallyEvent.CounterName);
            Assert.Equal(0, rallyEvent.OldValue);
            Assert.Equal(1, rallyEvent.NewValue);
        }

        [Fact]
        public void CardMovedEvent_Commit_DoesNotIncrementRally_WhenMoveIsNotFollowerSummon()
        {
            const int followerDefinitionId = 2;
            const int amuletDefinitionId = 3;
            SetTestGameDataProvider(
                (followerDefinitionId, CardType.Follower),
                (amuletDefinitionId, CardType.Amulet));

            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var gameState = BuildGameState(player1Id, player2Id);
            var serviceProvider = BuildServiceProvider();
            using var context = BuildContext(gameState, player1Id, serviceProvider);

            var transformedFollower = new CardInstance(Guid.NewGuid(), followerDefinitionId, player1Id)
            {
                CurrentZone = CardZone.Hand
            };
            var summonedAmulet = new CardInstance(Guid.NewGuid(), amuletDefinitionId, player1Id)
            {
                CurrentZone = CardZone.Hand
            };
            gameState.Player1.Hand.Add(transformedFollower);
            gameState.Player1.Hand.Add(summonedAmulet);

            var transformMove = new CardMovedEvent(transformedFollower, CardZone.Hand, CardZone.Field, CardMoveReason.Transform);
            var amuletSummon = new CardMovedEvent(summonedAmulet, CardZone.Hand, CardZone.Field, CardMoveReason.Summoned);

            var transformResult = transformMove.Commit(context);
            var amuletResult = amuletSummon.Commit(context);

            Assert.True(transformResult.Success);
            Assert.True(amuletResult.Success);
            Assert.Equal(0, gameState.Player1.Rally);
            Assert.DoesNotContain(transformResult.GeneratedEvents, e => e is CounterChangedEvent counter && counter.CounterName == PlayerState.RallyCounterKey);
            Assert.DoesNotContain(amuletResult.GeneratedEvents, e => e is CounterChangedEvent counter && counter.CounterName == PlayerState.RallyCounterKey);
        }
    }
}
