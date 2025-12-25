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
using ShadowverseBeyond.Server.Dtos.Enums;
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
            using var context = CreateContext(baseCost: 2, currentPp: 7, out var player, out var card);
            var abilityConfigs = new List<AbilityConfig>
            {
                new AbilityConfig
                {
                    AbilityTag = ShadowverseBeyond.Server.Domain.Constants.EffectKeyword.Enhance,
                    EnhanceCost = 6
                }
            };

            var resolution = PlayModeResolver.Resolve(card, player, abilityConfigs);

            Assert.Equal(PlayMode.Enhance, resolution.SelectedMode);

            var enhanceMode = resolution.Modes.Single(m => m.Mode == PlayMode.Enhance);
            Assert.True(enhanceMode.IsAvailable);
            Assert.Equal(6, enhanceMode.EffectiveCost);
        }

        [Fact]
        public void Resolve_selects_highest_affordable_enhance_cost()
        {
            using var context = CreateContext(baseCost: 10, currentPp: 7, out var player, out var card);
            var abilityConfigs = new List<AbilityConfig>
            {
                new AbilityConfig
                {
                    AbilityTag = ShadowverseBeyond.Server.Domain.Constants.EffectKeyword.Enhance,
                    EnhanceCost = 4
                },
                new AbilityConfig
                {
                    AbilityTag = ShadowverseBeyond.Server.Domain.Constants.EffectKeyword.Enhance,
                    EnhanceCost = 6
                },
                new AbilityConfig
                {
                    AbilityTag = ShadowverseBeyond.Server.Domain.Constants.EffectKeyword.Enhance,
                    EnhanceCost = 8
                }
            };

            var resolution = PlayModeResolver.Resolve(card, player, abilityConfigs);

            Assert.Equal(PlayMode.Enhance, resolution.SelectedMode);

            var enhanceMode = resolution.Modes.Single(m => m.Mode == PlayMode.Enhance);
            Assert.True(enhanceMode.IsAvailable);
            Assert.Equal(6, enhanceMode.EffectiveCost);
        }

        [Fact]
        public void Resolve_marks_enhance_unavailable_when_costs_exceed_pp()
        {
            using var context = CreateContext(baseCost: 2, currentPp: 3, out var player, out var card);
            var abilityConfigs = new List<AbilityConfig>
            {
                new AbilityConfig
                {
                    AbilityTag = ShadowverseBeyond.Server.Domain.Constants.EffectKeyword.Enhance,
                    EnhanceCost = 4
                },
                new AbilityConfig
                {
                    AbilityTag = ShadowverseBeyond.Server.Domain.Constants.EffectKeyword.Enhance,
                    EnhanceCost = 6
                }
            };

            var resolution = PlayModeResolver.Resolve(card, player, abilityConfigs);

            Assert.Equal(PlayMode.Base, resolution.SelectedMode);

            var enhanceMode = resolution.Modes.Single(m => m.Mode == PlayMode.Enhance);
            Assert.False(enhanceMode.IsAvailable);
            Assert.Equal(4, enhanceMode.EffectiveCost);
            Assert.Contains("need 4", enhanceMode.Reason);
        }

        [Fact]
        public void Resolve_disables_enhance_when_flag_set()
        {
            using var context = CreateContext(baseCost: 2, currentPp: 7, out var player, out var card);
            card.AbilityState[PlayModeResolver.EnhanceDisabledKey] = true;

            var abilityConfigs = new List<AbilityConfig>
            {
                new AbilityConfig
                {
                    AbilityTag = ShadowverseBeyond.Server.Domain.Constants.EffectKeyword.Enhance,
                    EnhanceCost = 6
                }
            };

            var resolution = PlayModeResolver.Resolve(card, player, abilityConfigs);

            Assert.Equal(PlayMode.Base, resolution.SelectedMode);

            var enhanceMode = resolution.Modes.Single(m => m.Mode == PlayMode.Enhance);
            Assert.False(enhanceMode.IsAvailable);
            Assert.Equal("Enhance disabled.", enhanceMode.Reason);
        }

        private static GameContext CreateContext(int baseCost, int currentPp, out PlayerState player, out CardInstance card)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();

            var mockBuffService = new Mock<IBuffService>();
            mockBuffService
                .Setup(s => s.GetEffectiveCost(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(baseCost);
            services.AddSingleton(mockBuffService.Object);

            var serviceProvider = services.BuildServiceProvider();
            GameServiceProvider.ContextAccessor = serviceProvider.GetRequiredService<IGameContextAccessor>();

            var mapper = new Mock<IMapper>();

            player = new PlayerState(Guid.NewGuid())
            {
                CurrentPlayPoints = currentPp
            };
            var opponent = new PlayerState(Guid.NewGuid());

            var gameState = new GameState("test-game", player, opponent, player.PlayerId)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            var context = new GameContext(
                serviceProvider,
                mapper.Object,
                serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                player.PlayerId);

            card = new CardInstance(Guid.NewGuid(), 1, player.PlayerId);
            return context;
        }
    }
}
