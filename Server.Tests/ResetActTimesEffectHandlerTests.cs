using System;
using System.Collections.Generic;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class ResetActTimesEffectHandlerTests
    {
        [Fact]
        public void EffectHandlerRegistry_Registers_ResetActTimes()
        {
            var registry = new EffectHandlerRegistry(NullLogger<EffectHandlerRegistry>.Instance);

            Assert.True(registry.TryGetHandler(EffectType.ResetActTimes, out var registration));
            Assert.NotNull(registration);
            Assert.Equal(typeof(ResetActTimesEffectHandler), registration?.handlerType);
        }

        [Fact]
        public void ActTimesResetEvent_ResetsActsThisTurn_AndEmitsActStateUpdatedEvent()
        {
            var serviceProvider = BuildServiceProvider();
            var player1Id = Guid.NewGuid();
            var gameState = BuildGameState(player1Id);
            using var context = BuildContext(serviceProvider, gameState, player1Id);

            var card = new CardInstance(Guid.NewGuid(), 1001, player1Id)
            {
                CurrentZone = CardZone.Field,
                ActsThisTurn = 1
            };
            gameState.Player1.Field.Add(card);
            gameState.RegisterCardInstance(card);

            var result = new ActTimesResetEvent(card).Commit(context);

            Assert.True(result.Success);
            Assert.Equal(0, card.ActsThisTurn);
            var stateUpdated = Assert.Single(result.GeneratedEvents.OfType<ActStateUpdatedEvent>());
            Assert.Equal(0, stateUpdated.ActsThisTurn);
            Assert.Equal(1, stateUpdated.MaxActsPerTurn);
        }

        [Fact]
        public void ResetActTimesEffectHandler_AllowsFieldAmulet_AndGeneratesResetEvent()
        {
            const int amuletDefinitionId = 9001;
            SetTestGameDataProvider(amuletDefinitionId, CardType.Amulet);

            var serviceProvider = BuildServiceProvider();
            var player1Id = Guid.NewGuid();
            var gameState = BuildGameState(player1Id);
            using var context = BuildContext(serviceProvider, gameState, player1Id);

            var card = new CardInstance(Guid.NewGuid(), amuletDefinitionId, player1Id)
            {
                CurrentZone = CardZone.Field,
                ActsThisTurn = 1
            };
            gameState.Player1.Field.Add(card);
            gameState.RegisterCardInstance(card);

            var targetProcessingService = new Mock<ITargetProcessingService>();
            targetProcessingService.Setup(service => service.ProcessTargets(
                    It.IsAny<GameContext>(),
                    It.IsAny<IReadOnlyList<TargetInfo>>(),
                    It.IsAny<Func<TargetInfo, GameContext, DomainOperationResult>>(),
                    It.IsAny<string>()))
                .Returns((GameContext ctx,
                    IReadOnlyList<TargetInfo> targets,
                    Func<TargetInfo, GameContext, DomainOperationResult> action,
                    string _) =>
                {
                    var domainResult = action(Assert.Single(targets), ctx);
                    return new GameStateActionResult
                    {
                        Success = domainResult.Success,
                        ErrorMessage = domainResult.ErrorMessage,
                        GeneratedEvents = domainResult.GeneratedEvents,
                        Results = new List<DomainOperationResult> { domainResult }
                    };
                });

            var handler = new ResetActTimesEffectHandler(
                NullLogger<ResetActTimesEffectHandler>.Instance,
                targetProcessingService.Object);

            var effect = new Effect(player1Id)
            {
                SelectedTargets = new List<TargetInfo> { TargetInfo.ForCard(card) }
            };

            var applyResult = handler.Apply(effect, context, new ResetActTimesEffectParams());
            Assert.True(applyResult.Success);

            var resetEvent = Assert.Single(applyResult.GeneratedEvents.OfType<ActTimesResetEvent>());
            var commitResult = resetEvent.Commit(context);
            Assert.True(commitResult.Success);
            Assert.Equal(0, card.ActsThisTurn);
            Assert.Single(commitResult.GeneratedEvents.OfType<ActStateUpdatedEvent>());
        }

        private static void SetTestGameDataProvider(int definitionId, CardType type)
        {
            var mockProvider = new Mock<IGameDataProvider>();
            mockProvider.SetupGet(provider => provider.IsInitialized).Returns(true);
            mockProvider.Setup(provider => provider.GetCardDefinition(definitionId)).Returns(new CardDefinition
            {
                Id = definitionId,
                Name = "TestCard",
                Type = type,
                Attack = 1,
                Health = 1
            });
            mockProvider.Setup(provider => provider.GetAbilityConfigs(definitionId)).Returns(new List<AbilityConfig>());
            GameDataProvider.SetInstanceForTesting(mockProvider.Object);
        }

        private static ServiceProvider BuildServiceProvider()
        {
            var buffService = new Mock<IBuffService>();
            buffService.Setup(service => service.GetEffectiveKeywords(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => card.BaseKeywords);
            buffService.Setup(service => service.GetEffectiveActionUsageLimit(It.IsAny<CardInstance>(), It.IsAny<CardActionUsageType>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, CardActionUsageType actionType, GameContext _) => card.GetBaseActionUsageLimit(actionType));

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            return services.BuildServiceProvider();
        }

        private static GameState BuildGameState(Guid player1Id)
        {
            var player1 = new PlayerState(player1Id);
            var player2 = new PlayerState(Guid.NewGuid());

            return new GameState("reset-act-times-test", player1, player2, player1Id)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };
        }

        private static GameContext BuildContext(IServiceProvider serviceProvider, GameState gameState, Guid requestingPlayerId)
        {
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            GameServiceProvider.ContextAccessor = accessor;
            return new GameContext(serviceProvider, Mock.Of<IMapper>(), accessor, gameState, requestingPlayerId);
        }
    }
}
