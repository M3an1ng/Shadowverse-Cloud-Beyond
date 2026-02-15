using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class PlayModeResolverTests
    {
        [Fact]
        public void Resolve_selects_enhance_when_available()
        {
            var enhanceAbility = CreateEnhanceAbility(6);
            using var context = CreateContext(
                baseCost: 2,
                currentPp: 7,
                effectiveAbilities: new[] { enhanceAbility },
                out var player,
                out var card);

            var resolution = PlayModeResolver.Resolve(card, player, context);

            Assert.Equal(PlayMode.Enhance, resolution.SelectedMode);
            Assert.Equal(enhanceAbility.RuntimeId, resolution.SelectedAbilityRuntimeId);

            var enhanceMode = resolution.Modes.Single(m => m.Mode == PlayMode.Enhance);
            Assert.True(enhanceMode.IsAvailable);
            Assert.Equal(6, enhanceMode.EffectiveCost);
        }

        [Fact]
        public void Resolve_selects_highest_affordable_enhance_cost()
        {
            var enhance4 = CreateEnhanceAbility(4);
            var enhance6 = CreateEnhanceAbility(6);
            var enhance8 = CreateEnhanceAbility(8);
            using var context = CreateContext(
                baseCost: 10,
                currentPp: 7,
                effectiveAbilities: new[] { enhance4, enhance6, enhance8 },
                out var player,
                out var card);

            var resolution = PlayModeResolver.Resolve(card, player, context);

            Assert.Equal(PlayMode.Enhance, resolution.SelectedMode);
            Assert.Equal(enhance6.RuntimeId, resolution.SelectedAbilityRuntimeId);

            var enhanceMode = resolution.Modes.Single(m => m.Mode == PlayMode.Enhance);
            Assert.True(enhanceMode.IsAvailable);
            Assert.Equal(6, enhanceMode.EffectiveCost);
        }

        [Fact]
        public void Resolve_marks_enhance_unavailable_when_costs_exceed_pp()
        {
            using var context = CreateContext(
                baseCost: 2,
                currentPp: 3,
                effectiveAbilities: new[] { CreateEnhanceAbility(4), CreateEnhanceAbility(6) },
                out var player,
                out var card);

            var resolution = PlayModeResolver.Resolve(card, player, context);

            Assert.Equal(PlayMode.Base, resolution.SelectedMode);
            Assert.Null(resolution.SelectedAbilityRuntimeId);

            var enhanceMode = resolution.Modes.Single(m => m.Mode == PlayMode.Enhance);
            Assert.False(enhanceMode.IsAvailable);
            Assert.Equal(4, enhanceMode.EffectiveCost);
            Assert.Contains("need 4", enhanceMode.Reason);
        }

        [Fact]
        public void Resolve_disables_enhance_when_flag_set()
        {
            using var context = CreateContext(
                baseCost: 2,
                currentPp: 7,
                effectiveAbilities: new[] { CreateEnhanceAbility(6) },
                out var player,
                out var card);
            card.AbilityState[PlayModeResolver.EnhanceDisabledKey] = true;

            var resolution = PlayModeResolver.Resolve(card, player, context);

            Assert.Equal(PlayMode.Base, resolution.SelectedMode);
            Assert.Null(resolution.SelectedAbilityRuntimeId);

            var enhanceMode = resolution.Modes.Single(m => m.Mode == PlayMode.Enhance);
            Assert.False(enhanceMode.IsAvailable);
            Assert.Equal("Enhance disabled.", enhanceMode.Reason);
        }

        [Fact]
        public void Resolve_does_not_add_enhance_mode_when_no_effective_enhance_abilities()
        {
            using var context = CreateContext(
                baseCost: 2,
                currentPp: 7,
                effectiveAbilities: Array.Empty<IAbility>(),
                out var player,
                out var card);

            var resolution = PlayModeResolver.Resolve(card, player, context);

            Assert.Equal(PlayMode.Base, resolution.SelectedMode);
            Assert.DoesNotContain(resolution.Modes, mode => mode.Mode == PlayMode.Enhance);
            Assert.Null(resolution.SelectedAbilityRuntimeId);
        }

        private static IAbility CreateEnhanceAbility(int enhanceCost)
        {
            var mock = new Mock<IAbility>();
            mock.SetupGet(a => a.RuntimeId).Returns(Guid.NewGuid());
            mock.SetupGet(a => a.Name).Returns($"Enhance {enhanceCost}");
            mock.SetupGet(a => a.AbilityKeywords).Returns(new List<AbilityKeywordConfig>
            {
                new() { Keyword = AbilityKeyword.Enhance, Value = enhanceCost }
            });
            mock.Setup(a => a.HasKeyword(It.IsAny<AbilityKeyword>()))
                .Returns<AbilityKeyword>(keyword => keyword == AbilityKeyword.Enhance);
            mock.Setup(a => a.GetKeywordValue(It.IsAny<AbilityKeyword>()))
                .Returns<AbilityKeyword>(keyword => keyword == AbilityKeyword.Enhance ? enhanceCost : null);
            return mock.Object;
        }

        private static GameContext CreateContext(
            int baseCost,
            int currentPp,
            IReadOnlyCollection<IAbility> effectiveAbilities,
            out PlayerState player,
            out CardInstance card)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();

            var mockBuffService = new Mock<IBuffService>();
            mockBuffService
                .Setup(s => s.GetEffectiveCost(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(baseCost);
            mockBuffService
                .Setup(s => s.GetEffectiveAbilityKeywordValue(
                    It.IsAny<CardInstance>(),
                    It.IsAny<AbilityKeyword>(),
                    It.IsAny<int>(),
                    It.IsAny<GameContext>()))
                .Returns<CardInstance, AbilityKeyword, int, GameContext>((_, _, value, _) => value);
            services.AddSingleton(mockBuffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();

            var serviceProvider = services.BuildServiceProvider();
            GameServiceProvider.ContextAccessor = serviceProvider.GetRequiredService<IGameContextAccessor>();

            var mapper = new Mock<IMapper>();

            player = new PlayerState(Guid.NewGuid())
            {
                CurrentPlayPoints = currentPp
            };
            var opponent = new PlayerState(Guid.NewGuid());
            card = new CardInstance(Guid.NewGuid(), 1, player.PlayerId);
            var cardInstanceId = card.InstanceId;

            var mockAbilityManager = new Mock<IAbilityManager>();
            mockAbilityManager
                .Setup(manager => manager.GetEffectiveAbilitiesForCard(cardInstanceId, It.IsAny<GameContext>()))
                .Returns(effectiveAbilities.ToList());

            var gameState = new GameState("test-game", player, opponent, player.PlayerId)
            {
                AbilityManager = mockAbilityManager.Object,
                BuffManager = Mock.Of<IBuffManager>()
            };

            var context = new GameContext(
                serviceProvider,
                mapper.Object,
                serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                player.PlayerId);

            return context;
        }
    }
}
