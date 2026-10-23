using System;
using System.Linq;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class EvolutionEffectManualEvolutionTests
    {
        [Fact]
        public void Effect_metadata_exposes_manual_evolution_checkbox_for_evolve()
        {
            var registry = new EffectHandlerRegistry(NullLogger<EffectHandlerRegistry>.Instance);
            var svc = new EffectUIMetadataService(registry);

            var evolve = svc.GetAllEffectUIMetadata().First(m => m.EffectTypeName == EffectType.Evolve.ToString());
            var manualEvolution = evolve.Parameters.Single(p => p.PropertyName == nameof(EvolveEffectParams.IsManualEvolution));

            Assert.Equal(UiControlType.Checkbox, manualEvolution.ControlType);
            Assert.False((bool?)manualEvolution.DefaultValue ?? true);
        }

        [Fact]
        public void Effect_metadata_exposes_manual_evolution_checkbox_for_super_evolve()
        {
            var registry = new EffectHandlerRegistry(NullLogger<EffectHandlerRegistry>.Instance);
            var svc = new EffectUIMetadataService(registry);

            var superEvolve = svc.GetAllEffectUIMetadata().First(m => m.EffectTypeName == EffectType.SuperEvolve.ToString());
            var manualEvolution = superEvolve.Parameters.Single(p => p.PropertyName == nameof(SuperEvolveEffectParams.IsManualEvolution));

            Assert.Equal(UiControlType.Checkbox, manualEvolution.ControlType);
            Assert.False((bool?)manualEvolution.DefaultValue ?? true);
        }

        [Fact]
        public void EvolveEffect_manual_flag_true_is_forwarded_to_evolve_event()
        {
            SetTestGameDataProvider();

            var serviceProvider = BuildServiceProvider();
            var player1Id = Guid.NewGuid();
            var gameState = BuildGameState(player1Id, Guid.NewGuid());
            var player = gameState.Player1;

            var card = new CardInstance(Guid.NewGuid(), 1001, player1Id)
            {
                CurrentZone = CardZone.Field
            };
            player.Field.Add(card);
            gameState.RegisterCardInstance(card);

            var effectConfig = new EffectConfig { Type = EffectType.Evolve };
            var effect = new Effect(effectConfig, player1Id, new[] { TargetInfo.ForCard(card) }.ToList(), null, Guid.NewGuid(), null);
            var parameters = new EvolveEffectParams
            {
                IsManualEvolution = true
            };

            var handler = new EvolveEffectHandler(
                NullLogger<EvolveEffectHandler>.Instance,
                new TargetProcessingService(NullLogger<TargetProcessingService>.Instance));

            using var context = BuildContext(gameState, player1Id, serviceProvider);
            var result = handler.Apply(effect, context, parameters);

            Assert.True(result.Success, result.ErrorMessage);
            var evolveEvent = result.GeneratedEvents.OfType<EvolveEvent>().Single();
            Assert.True(evolveEvent.IsManualEvolution);
        }

        [Fact]
        public void SuperEvolveEffect_manual_flag_true_is_forwarded_to_evolve_events()
        {
            SetTestGameDataProvider();

            var serviceProvider = BuildServiceProvider();
            var player1Id = Guid.NewGuid();
            var gameState = BuildGameState(player1Id, Guid.NewGuid());
            var player = gameState.Player1;
            player.MaxSuperEvolutionPoints = 1;
            player.CurrentSuperEvolutionPoints = 1;

            var card = new CardInstance(Guid.NewGuid(), 1002, player1Id)
            {
                CurrentZone = CardZone.Field
            };
            player.Field.Add(card);
            gameState.RegisterCardInstance(card);

            var effectConfig = new EffectConfig { Type = EffectType.SuperEvolve };
            var effect = new Effect(effectConfig, player1Id, new[] { TargetInfo.ForCard(card) }.ToList(), null, Guid.NewGuid(), null);
            var parameters = new SuperEvolveEffectParams
            {
                IsManualEvolution = true
            };

            var handler = new SuperEvolveEffectHandler(
                NullLogger<SuperEvolveEffectHandler>.Instance,
                new TargetProcessingService(NullLogger<TargetProcessingService>.Instance));

            using var context = BuildContext(gameState, player1Id, serviceProvider);
            var result = handler.Apply(effect, context, parameters);

            Assert.True(result.Success, result.ErrorMessage);
            var evolveEvent = result.GeneratedEvents.OfType<EvolveEvent>().Single();
            var superEvolveEvent = result.GeneratedEvents.OfType<SuperEvolveEvent>().Single();
            Assert.True(evolveEvent.IsManualEvolution);
            Assert.True(superEvolveEvent.IsManualEvolution);
        }

        private static ServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            var buffService = new Mock<IBuffService>();
            buffService.Setup(s => s.GetEffectiveKeywords(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(new List<CardKeyword>());
            buffService.Setup(s => s.GetEffectiveAttack(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns<CardInstance, GameContext>((card, _) => card.BaseAttack);
            buffService.Setup(s => s.GetEffectiveMaxHealth(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns<CardInstance, GameContext>((card, _) => card.BaseHealth);
            buffService.Setup(s => s.GetEffectiveCost(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns<CardInstance, GameContext>((card, _) => card.BaseCost);
            services.AddSingleton<IBuffService>(buffService.Object);
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
            GameServiceProvider.ContextAccessor = accessor;
            return new GameContext(serviceProvider, mapper, accessor, gameState, requestingPlayerId);
        }

        private static void SetTestGameDataProvider()
        {
            var mockProvider = new Mock<IGameDataProvider>();
            mockProvider.SetupGet(provider => provider.IsInitialized).Returns(true);
            mockProvider.Setup(provider => provider.GetCardDefinition(It.IsAny<int>())).Returns((int id) => new CardDefinition
            {
                Id = id,
                Name = $"Card-{id}",
                Type = CardType.Follower,
                Attack = 2,
                Health = 2,
                Cost = 2
            });
            mockProvider.Setup(provider => provider.GetAbilityConfigs(It.IsAny<int>())).Returns(new List<AbilityConfig>());
            GameDataProvider.SetInstanceForTesting(mockProvider.Object);
        }
    }
}
