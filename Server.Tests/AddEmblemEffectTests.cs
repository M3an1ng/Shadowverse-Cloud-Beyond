using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class AddEmblemEffectTests
    {
        private static ServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(Mock.Of<IBuffService>());
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            return services.BuildServiceProvider();
        }

        private static GameState BuildGameState(Guid player1Id, Guid player2Id)
        {
            var player1 = new PlayerState(player1Id);
            var player2 = new PlayerState(player2Id);
            return new GameState("test-game", player1, player2, player1Id)
            {
                AbilityManager = new Mock<IAbilityManager>().Object,
                BuffManager = new Mock<IBuffManager>().Object
            };
        }

        private static GameContext BuildContext(GameState gameState, Guid requestingPlayerId, IServiceProvider serviceProvider)
        {
            var mapper = new Mock<IMapper>().Object;
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            return new GameContext(serviceProvider, mapper, accessor, gameState, requestingPlayerId);
        }

        private static void SetCardDefinition(int cardDefinitionId, CardType type)
        {
            SetCardDefinitions((cardDefinitionId, type));
        }

        private static void SetCardDefinitions(params (int Id, CardType Type)[] definitions)
        {
            var mockProvider = new Mock<IGameDataProvider>();
            foreach (var (id, type) in definitions)
            {
                mockProvider.Setup(p => p.GetCardDefinition(id))
                    .Returns(new CardDefinition { Id = id, Type = type, Name = $"Test-{id}" });
            }
            GameDataProvider.SetInstanceForTesting(mockProvider.Object);
        }

        private static ICardFactory BuildCardFactory()
        {
            var factory = new Mock<ICardFactory>();
            factory.Setup(f => f.CreateAndRegisterNewCard(
                    It.IsAny<GameState>(),
                    It.IsAny<int>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CardZone>()))
                .Returns((GameState gameState, int definitionId, Guid ownerId, CardZone zone) =>
                {
                    var card = new CardInstance(Guid.NewGuid(), definitionId, ownerId)
                    {
                        CurrentZone = zone
                    };
                    return card;
                });
            return factory.Object;
        }

        [Fact]
        public void AddEmblem_DefinitionMode_AddsCrestCards()
        {
            var serviceProvider = BuildServiceProvider();
            var player1Id = Guid.NewGuid();
            var gameState = BuildGameState(player1Id, Guid.NewGuid());
            SetCardDefinition(10, CardType.Crest);

            var effectConfig = new EffectConfig { Type = EffectType.AddEmblem };
            var effect = new Effect(effectConfig, player1Id, new List<TargetInfo>(), null, Guid.NewGuid(), null);
            var parameters = new AddEmblemEffectParams
            {
                CardDefinitionId = new List<int> { 10 },
                Amount = 2,
                CardSourceMode = CardSourceMode.Definition,
                PlayerScope = PlayerScope.Self
            };

            var handler = new AddEmblemEffectHandler(
                NullLogger<AddEmblemEffectHandler>.Instance,
                new TargetProcessingService(NullLogger<TargetProcessingService>.Instance),
                BuildCardFactory());

            using var context = BuildContext(gameState, player1Id, serviceProvider);
            var result = handler.Apply(effect, context, parameters);

            Assert.True(result.Success);
            var evt = Assert.Single(result.GeneratedEvents.OfType<AddEmblemEvent>());
            Assert.Equal(player1Id, evt.TargetPlayer?.PlayerId);
            Assert.Single(evt.CardsToAdd);
        }

        [Fact]
        public void AddEmblem_DefinitionMode_AddsFaithCards()
        {
            var serviceProvider = BuildServiceProvider();
            var player1Id = Guid.NewGuid();
            var gameState = BuildGameState(player1Id, Guid.NewGuid());
            SetCardDefinition(410, CardType.Faith);

            var effectConfig = new EffectConfig { Type = EffectType.AddEmblem };
            var effect = new Effect(effectConfig, player1Id, new List<TargetInfo>(), null, Guid.NewGuid(), null);
            var parameters = new AddEmblemEffectParams
            {
                CardDefinitionId = new List<int> { 410 },
                Amount = 1,
                CardSourceMode = CardSourceMode.Definition,
                PlayerScope = PlayerScope.Self
            };

            var handler = new AddEmblemEffectHandler(
                NullLogger<AddEmblemEffectHandler>.Instance,
                new TargetProcessingService(NullLogger<TargetProcessingService>.Instance),
                BuildCardFactory());

            using var context = BuildContext(gameState, player1Id, serviceProvider);
            var result = handler.Apply(effect, context, parameters);

            Assert.True(result.Success);
            var evt = Assert.Single(result.GeneratedEvents.OfType<AddEmblemEvent>());
            Assert.Equal(player1Id, evt.TargetPlayer?.PlayerId);
            Assert.Single(evt.CardsToAdd);
        }

        [Fact]
        public void AddEmblem_DefinitionMode_RejectsNonCrest()
        {
            var serviceProvider = BuildServiceProvider();
            var player1Id = Guid.NewGuid();
            var gameState = BuildGameState(player1Id, Guid.NewGuid());
            SetCardDefinition(11, CardType.Follower);

            var effectConfig = new EffectConfig { Type = EffectType.AddEmblem };
            var effect = new Effect(effectConfig, player1Id, new List<TargetInfo>(), null, Guid.NewGuid(), null);
            var parameters = new AddEmblemEffectParams
            {
                CardDefinitionId = new List<int> { 11 },
                Amount = 1,
                CardSourceMode = CardSourceMode.Definition
            };

            var handler = new AddEmblemEffectHandler(
                NullLogger<AddEmblemEffectHandler>.Instance,
                new TargetProcessingService(NullLogger<TargetProcessingService>.Instance),
                BuildCardFactory());

            using var context = BuildContext(gameState, player1Id, serviceProvider);
            var result = handler.Apply(effect, context, parameters);

            Assert.False(result.Success);
            Assert.Empty(result.GeneratedEvents);
        }

        [Fact]
        public void AddEmblem_InstanceMode_RejectsNonCrest()
        {
            var serviceProvider = BuildServiceProvider();
            var player1Id = Guid.NewGuid();
            var gameState = BuildGameState(player1Id, Guid.NewGuid());
            SetCardDefinition(12, CardType.Spell);

            var card = new CardInstance(Guid.NewGuid(), 12, player1Id);
            var effectConfig = new EffectConfig { Type = EffectType.AddEmblem };
            var effect = new Effect(effectConfig, player1Id, new List<TargetInfo> { TargetInfo.ForCard(card) }, null, Guid.NewGuid(), null);
            var parameters = new AddEmblemEffectParams { CardSourceMode = CardSourceMode.Instance };

            var handler = new AddEmblemEffectHandler(
                NullLogger<AddEmblemEffectHandler>.Instance,
                new TargetProcessingService(NullLogger<TargetProcessingService>.Instance),
                BuildCardFactory());

            using var context = BuildContext(gameState, player1Id, serviceProvider);
            var result = handler.Apply(effect, context, parameters);

            Assert.False(result.Success);
            Assert.Empty(result.GeneratedEvents);
        }

        [Fact]
        public void AddEmblem_InstanceMode_PreventDuplicate_SkipsExisting()
        {
            var serviceProvider = BuildServiceProvider();
            var player1Id = Guid.NewGuid();
            var gameState = BuildGameState(player1Id, Guid.NewGuid());
            SetCardDefinition(13, CardType.Crest);

            var existing = new CardInstance(Guid.NewGuid(), 13, player1Id)
            {
                CurrentZone = CardZone.Emblem
            };
            gameState.Player1.Emblem.Add(existing);

            var card = new CardInstance(Guid.NewGuid(), 13, player1Id);
            var effectConfig = new EffectConfig { Type = EffectType.AddEmblem };
            var effect = new Effect(effectConfig, player1Id, new List<TargetInfo> { TargetInfo.ForCard(card) }, null, Guid.NewGuid(), null);
            var parameters = new AddEmblemEffectParams
            {
                CardSourceMode = CardSourceMode.Instance,
                PreventDuplicate = true
            };

            var handler = new AddEmblemEffectHandler(
                NullLogger<AddEmblemEffectHandler>.Instance,
                new TargetProcessingService(NullLogger<TargetProcessingService>.Instance),
                BuildCardFactory());

            using var context = BuildContext(gameState, player1Id, serviceProvider);
            var result = handler.Apply(effect, context, parameters);

            Assert.True(result.Success);
            Assert.Empty(result.GeneratedEvents);
        }

        [Fact]
        public void AddEmblem_InstanceMode_CapsAtEmblemLimit()
        {
            var serviceProvider = BuildServiceProvider();
            var player1Id = Guid.NewGuid();
            var gameState = BuildGameState(player1Id, Guid.NewGuid());
            SetCardDefinitions(
                (20, CardType.Crest),
                (21, CardType.Crest),
                (22, CardType.Crest),
                (23, CardType.Crest),
                (24, CardType.Crest),
                (25, CardType.Crest));

            var cards = new List<CardInstance>
            {
                new CardInstance(Guid.NewGuid(), 20, player1Id),
                new CardInstance(Guid.NewGuid(), 21, player1Id),
                new CardInstance(Guid.NewGuid(), 22, player1Id),
                new CardInstance(Guid.NewGuid(), 23, player1Id),
                new CardInstance(Guid.NewGuid(), 24, player1Id),
                new CardInstance(Guid.NewGuid(), 25, player1Id)
            };

            var effectConfig = new EffectConfig { Type = EffectType.AddEmblem };
            var effect = new Effect(effectConfig, player1Id, cards.Select(TargetInfo.ForCard).ToList(), null, Guid.NewGuid(), null);
            var parameters = new AddEmblemEffectParams
            {
                CardSourceMode = CardSourceMode.Instance,
                PreventDuplicate = true
            };

            var handler = new AddEmblemEffectHandler(
                NullLogger<AddEmblemEffectHandler>.Instance,
                new TargetProcessingService(NullLogger<TargetProcessingService>.Instance),
                BuildCardFactory());

            using var context = BuildContext(gameState, player1Id, serviceProvider);
            var result = handler.Apply(effect, context, parameters);

            Assert.True(result.Success);
            var evt = Assert.Single(result.GeneratedEvents.OfType<AddEmblemEvent>());
            Assert.Equal(GameConstants.Emblem.MaxEmblemSize, evt.CardsToAdd.Count);
        }

        [Fact]
        public void AddEmblem_DefinitionMode_SkipsWhenEmblemFull()
        {
            var serviceProvider = BuildServiceProvider();
            var player1Id = Guid.NewGuid();
            var gameState = BuildGameState(player1Id, Guid.NewGuid());
            SetCardDefinitions((30, CardType.Crest), (31, CardType.Crest));

            for (int i = 0; i < GameConstants.Emblem.MaxEmblemSize; i++)
            {
                gameState.Player1.Emblem.Add(new CardInstance(Guid.NewGuid(), 31, player1Id)
                {
                    CurrentZone = CardZone.Emblem
                });
            }

            var effectConfig = new EffectConfig { Type = EffectType.AddEmblem };
            var effect = new Effect(effectConfig, player1Id, new List<TargetInfo>(), null, Guid.NewGuid(), null);
            var parameters = new AddEmblemEffectParams
            {
                CardDefinitionId = new List<int> { 30 },
                Amount = 1,
                CardSourceMode = CardSourceMode.Definition
            };

            var handler = new AddEmblemEffectHandler(
                NullLogger<AddEmblemEffectHandler>.Instance,
                new TargetProcessingService(NullLogger<TargetProcessingService>.Instance),
                BuildCardFactory());

            using var context = BuildContext(gameState, player1Id, serviceProvider);
            var result = handler.Apply(effect, context, parameters);

            Assert.True(result.Success);
            Assert.Empty(result.GeneratedEvents);
        }

        [Fact]
        public void AddEmblem_CopyMode_PreventDuplicate_SkipsDuplicateDefinitions()
        {
            var serviceProvider = BuildServiceProvider();
            var player1Id = Guid.NewGuid();
            var gameState = BuildGameState(player1Id, Guid.NewGuid());
            SetCardDefinition(40, CardType.Crest);

            var cardA = new CardInstance(Guid.NewGuid(), 40, player1Id);
            var cardB = new CardInstance(Guid.NewGuid(), 40, player1Id);

            var effectConfig = new EffectConfig { Type = EffectType.AddEmblem };
            var effect = new Effect(effectConfig, player1Id, new List<TargetInfo>
            {
                TargetInfo.ForCard(cardA),
                TargetInfo.ForCard(cardB)
            }, null, Guid.NewGuid(), null);
            var parameters = new AddEmblemEffectParams
            {
                CardSourceMode = CardSourceMode.Copy,
                PreventDuplicate = true
            };

            var handler = new AddEmblemEffectHandler(
                NullLogger<AddEmblemEffectHandler>.Instance,
                new TargetProcessingService(NullLogger<TargetProcessingService>.Instance),
                BuildCardFactory());

            using var context = BuildContext(gameState, player1Id, serviceProvider);
            var result = handler.Apply(effect, context, parameters);

            Assert.True(result.Success);
            var emblemEvent = Assert.Single(result.GeneratedEvents.OfType<AddEmblemEvent>());
            Assert.Single(emblemEvent.CardsToAdd);
        }
    }
}
