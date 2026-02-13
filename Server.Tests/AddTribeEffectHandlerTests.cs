using System;
using System.Collections.Generic;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class AddTribeEffectHandlerTests
    {
        [Fact]
        public void Apply_WithCardTarget_DelegatesToBuffService()
        {
            var buffService = new Mock<IBuffService>();
            buffService
                .Setup(service => service.ApplyTribeBuff(
                    It.IsAny<GameContext>(),
                    It.IsAny<CardInstance>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<DurationType>(),
                    It.IsAny<int?>(),
                    It.IsAny<string?>(),
                    It.IsAny<bool>()))
                .Returns(new DomainOperationResult { Success = true });

            var (context, card) = CreateContext(buffService.Object);
            var handler = new AddTribeEffectHandler(
                NullLogger<AddTribeEffectHandler>.Instance,
                new TargetProcessingService(NullLogger<TargetProcessingService>.Instance),
                buffService.Object);

            var effect = new Effect(
                new EffectConfig { Type = EffectType.AddTribe },
                context.RequestingPlayerId,
                selectedTargets: new List<TargetInfo> { TargetInfo.ForCard(card) },
                parentAbilityId: null,
                triggeredEventId: Guid.Empty,
                sourceCardId: card.InstanceId);
            effect.sourceAbilityId = Guid.NewGuid();
            effect.EffectIndex = 2;

            var parameters = new AddTribeEffectParams
            {
                Tribe = "Reanimate, Zombie",
                DurationType = DurationType.EndOfTurn,
                DurationParameter = 2,
                CanStack = false
            };

            var result = handler.Apply(effect, context, parameters);

            Assert.True(result.Success);
            buffService.Verify(service => service.ApplyTribeBuff(
                It.IsAny<GameContext>(),
                card,
                It.Is<List<string>>(tribes =>
                    tribes.Count == 2 &&
                    tribes.Contains("Reanimate") &&
                    tribes.Contains("Zombie")),
                DurationType.EndOfTurn,
                2,
                It.Is<string?>(key => !string.IsNullOrWhiteSpace(key) && key.EndsWith(":2")),
                false), Times.Once);
        }

        [Fact]
        public void Apply_WithEmptyTribe_SkipsBuffService()
        {
            var buffService = new Mock<IBuffService>();
            var (context, card) = CreateContext(buffService.Object);
            var handler = new AddTribeEffectHandler(
                NullLogger<AddTribeEffectHandler>.Instance,
                new TargetProcessingService(NullLogger<TargetProcessingService>.Instance),
                buffService.Object);

            var effect = new Effect(
                new EffectConfig { Type = EffectType.AddTribe },
                context.RequestingPlayerId,
                selectedTargets: new List<TargetInfo> { TargetInfo.ForCard(card) },
                parentAbilityId: null,
                triggeredEventId: Guid.Empty,
                sourceCardId: card.InstanceId);

            var result = handler.Apply(effect, context, new AddTribeEffectParams { Tribe = "   " });

            Assert.True(result.Success);
            buffService.Verify(service => service.ApplyTribeBuff(
                It.IsAny<GameContext>(),
                It.IsAny<CardInstance>(),
                It.IsAny<List<string>>(),
                It.IsAny<DurationType>(),
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>()), Times.Never);
        }

        private static (GameContext Context, CardInstance Card) CreateContext(IBuffService buffService)
        {
            var mapper = new Mock<IMapper>();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton(buffService);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var provider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("add-tribe-handler-tests", player, opponent, player.PlayerId);

            var card = new CardInstance(Guid.NewGuid(), 1001, player.PlayerId);
            card.CurrentZone = CardZone.Field;
            player.Field.Add(card);
            gameState.RegisterCardInstance(card);

            var context = new GameContext(
                provider,
                mapper.Object,
                provider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                player.PlayerId);

            return (context, card);
        }
    }
}
