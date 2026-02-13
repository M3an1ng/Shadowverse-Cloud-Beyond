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
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Infrastructure.Services;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class CardSourceModeInstanceTests
    {
        private static ServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(new Mock<IBuffService>().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            return services.BuildServiceProvider();
        }

        private static GameState BuildGameState(Guid player1Id, Guid player2Id)
        {
            var player1 = new PlayerState(player1Id);
            var player2 = new PlayerState(player2Id);
            var gameState = new GameState("test-game", player1, player2, player1Id)
            {
                AbilityManager = new Mock<IAbilityManager>().Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            return gameState;
        }

        private static GameContext BuildContext(GameState gameState, Guid requestingPlayerId, IServiceProvider serviceProvider)
        {
            var mapper = new Mock<IMapper>().Object;
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            return new GameContext(serviceProvider, mapper, accessor, gameState, requestingPlayerId);
        }

        [Fact]
        public void Summon_InstanceMode_UsesCardOwner()
        {
            var serviceProvider = BuildServiceProvider();
            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var gameState = BuildGameState(player1Id, player2Id);
            var card = new CardInstance(Guid.NewGuid(), 1, player2Id);

            var effectConfig = new EffectConfig { Type = EffectType.Summon };
            var effect = new Effect(effectConfig, player1Id, new List<TargetInfo> { TargetInfo.ForCard(card) }, null, Guid.NewGuid(), null);
            var parameters = new SummonEffectParams { CardSourceMode = CardSourceMode.Instance };
            var handler = new SummonEffectHandler(
                NullLogger<SummonEffectHandler>.Instance,
                new Mock<ITargetProcessingService>().Object,
                new Mock<ICardFactory>().Object);

            using var context = BuildContext(gameState, player1Id, serviceProvider);
            var result = handler.Apply(effect, context, parameters);

            Assert.True(result.Success);
            var summonEvent = Assert.IsType<SummonEvent>(result.GeneratedEvents.Single());
            Assert.Equal(player2Id, summonEvent.SummoningPlayer?.PlayerId);
        }

        [Fact]
        public void GetToken_InstanceMode_GroupsByOwner()
        {
            var serviceProvider = BuildServiceProvider();
            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var gameState = BuildGameState(player1Id, player2Id);
            var card1 = new CardInstance(Guid.NewGuid(), 1, player1Id);
            var card2 = new CardInstance(Guid.NewGuid(), 1, player2Id);

            var effectConfig = new EffectConfig { Type = EffectType.GetToken };
            var effect = new Effect(effectConfig, player1Id, new List<TargetInfo>
            {
                TargetInfo.ForCard(card1),
                TargetInfo.ForCard(card2)
            }, null, Guid.NewGuid(), null);
            var parameters = new GetTokenEffectParams { CardSourceMode = CardSourceMode.Instance };
            var handler = new GetTokenEffectHandler(
                NullLogger<GetTokenEffectHandler>.Instance,
                new Mock<ITargetProcessingService>().Object,
                new Mock<ICardFactory>().Object);

            using var context = BuildContext(gameState, player1Id, serviceProvider);
            var result = handler.Apply(effect, context, parameters);

            Assert.True(result.Success);
            var events = result.GeneratedEvents.OfType<AddCardToHandEvent>().ToList();
            Assert.Equal(2, events.Count);
            Assert.All(events, evt =>
            {
                Assert.NotNull(evt.TargetPlayer);
                Assert.All(evt.CardsToAdd, card => Assert.Equal(evt.TargetPlayer!.PlayerId, card.OwnerPlayerId));
            });
        }

        [Fact]
        public void AddDeck_InstanceMode_GroupsByOwner()
        {
            var serviceProvider = BuildServiceProvider();
            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var gameState = BuildGameState(player1Id, player2Id);
            var card1 = new CardInstance(Guid.NewGuid(), 1, player1Id);
            var card2 = new CardInstance(Guid.NewGuid(), 1, player2Id);

            var effectConfig = new EffectConfig { Type = EffectType.AddDeck };
            var effect = new Effect(effectConfig, player1Id, new List<TargetInfo>
            {
                TargetInfo.ForCard(card1),
                TargetInfo.ForCard(card2)
            }, null, Guid.NewGuid(), null);
            var parameters = new AddDeckEffectParams { CardSourceMode = CardSourceMode.Instance };
            var handler = new AddDeckEffectHandler(
                NullLogger<AddDeckEffectHandler>.Instance,
                new Mock<ITargetProcessingService>().Object,
                new Mock<ICardFactory>().Object);

            using var context = BuildContext(gameState, player1Id, serviceProvider);
            var result = handler.Apply(effect, context, parameters);

            Assert.True(result.Success);
            var events = result.GeneratedEvents.OfType<AddCardToDeckEvent>().ToList();
            Assert.Equal(2, events.Count);
            Assert.All(events, evt =>
            {
                Assert.NotNull(evt.TargetPlayer);
                Assert.All(evt.CardsToAdd, card => Assert.Equal(evt.TargetPlayer!.PlayerId, card.OwnerPlayerId));
                Assert.Equal(DeckInsertMode.Random, evt.InsertMode);
            });
        }

        [Fact]
        public void AddDeck_InstanceMode_UsesConfiguredInsertMode()
        {
            var serviceProvider = BuildServiceProvider();
            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var gameState = BuildGameState(player1Id, player2Id);
            var card = new CardInstance(Guid.NewGuid(), 1, player1Id);

            var effectConfig = new EffectConfig { Type = EffectType.AddDeck };
            var effect = new Effect(effectConfig, player1Id, new List<TargetInfo> { TargetInfo.ForCard(card) }, null, Guid.NewGuid(), null);
            var parameters = new AddDeckEffectParams
            {
                CardSourceMode = CardSourceMode.Instance,
                InsertMode = DeckInsertMode.Bottom
            };
            var handler = new AddDeckEffectHandler(
                NullLogger<AddDeckEffectHandler>.Instance,
                new Mock<ITargetProcessingService>().Object,
                new Mock<ICardFactory>().Object);

            using var context = BuildContext(gameState, player1Id, serviceProvider);
            var result = handler.Apply(effect, context, parameters);

            Assert.True(result.Success);
            var addDeckEvent = Assert.Single(result.GeneratedEvents.OfType<AddCardToDeckEvent>());
            Assert.Equal(DeckInsertMode.Bottom, addDeckEvent.InsertMode);
        }
    }
}
