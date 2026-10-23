using System;
using System.Collections.Generic;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class FreeEvolveEvolutionTests
    {
        private const int FreeEvolveFollowerDefinitionId = 910001;

        [Fact]
        public void EvolveRequestedEvent_FreeEvolveFollowerWithZeroEp_SucceedsWithoutEpConsumption()
        {
            SetTestGameDataProvider();

            var buffServiceMock = new Mock<IBuffService>();
            buffServiceMock
                .Setup(service => service.GetEffectiveKeywords(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => card.BaseKeywords);
            buffServiceMock
                .Setup(service => service.GetEffectiveAttack(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => card.BaseAttack);
            buffServiceMock
                .Setup(service => service.GetEffectiveMaxHealth(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => card.BaseHealth);
            buffServiceMock
                .Setup(service => service.GetEffectiveCost(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => card.BaseCost);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton(buffServiceMock.Object);
            var serviceProvider = services.BuildServiceProvider();

            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var player1 = new PlayerState(player1Id)
            {
                IsEvolutionUnlocked = true,
                CanEvolve = true,
                MaxEvolutionPoints = 2,
                CurrentEvolutionPoints = 0
            };
            var player2 = new PlayerState(player2Id);

            var gameState = new GameState("free-evolve-ep0", player1, player2, player1Id)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            var freeEvolveFollower = new CardInstance(Guid.NewGuid(), FreeEvolveFollowerDefinitionId, player1Id)
            {
                CurrentZone = CardZone.Field
            };
            player1.Field.Add(freeEvolveFollower);
            gameState.RegisterCardInstance(freeEvolveFollower);

            var actionDto = new EvolveActionDto { UnitUid = freeEvolveFollower.InstanceId.ToString() };
            var requestEvent = new EvolveRequestedEvent(player1Id, actionDto);

            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            GameServiceProvider.ContextAccessor = accessor;
            using var context = new GameContext(
                serviceProvider,
                new Mock<IMapper>().Object,
                accessor,
                gameState,
                player1Id);

            var result = requestEvent.Commit(context);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Contains(result.GeneratedEvents, gameEvent => gameEvent is EvolveEvent);
            Assert.DoesNotContain(result.GeneratedEvents, gameEvent => gameEvent is EpUpdateEvent);
        }

        [Fact]
        public void ApplyTurnStart_EvolutionUnlockedAndZeroEp_StillKeepsEvolutionRight()
        {
            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();

            var player1 = new PlayerState(player1Id)
            {
                IsEvolutionUnlocked = true,
                MaxEvolutionPoints = 2,
                CurrentEvolutionPoints = 0
            };
            var player2 = new PlayerState(player2Id);
            var gameState = new GameState("turn-start-evolve-right", player1, player2, player1Id);

            player1.ApplyTurnStart(gameState);

            Assert.True(player1.CanEvolve);
        }

        private static void SetTestGameDataProvider()
        {
            var mockProvider = new Mock<IGameDataProvider>();
            mockProvider.SetupGet(provider => provider.IsInitialized).Returns(true);
            mockProvider.Setup(provider => provider.GetCardDefinition(FreeEvolveFollowerDefinitionId)).Returns(new CardDefinition
            {
                Id = FreeEvolveFollowerDefinitionId,
                Name = "FreeEvolveFollower",
                Type = CardType.Follower,
                Attack = 2,
                Health = 2,
                Cost = 2,
                Keywords = new List<string> { CardKeyword.FreeEvolve.ToString() }
            });
            mockProvider.Setup(provider => provider.GetAbilityConfigs(FreeEvolveFollowerDefinitionId)).Returns(new List<AbilityConfig>());
            GameDataProvider.SetInstanceForTesting(mockProvider.Object);
        }
    }
}
