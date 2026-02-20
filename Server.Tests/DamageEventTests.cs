using System;
using System.Collections.Generic;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Effects;
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
    public class DamageEventTests
    {
        [Fact]
        public void DamageEvent_MultiTargetCurrentEffect_DelaysGeneratedDestroyEventByTriggeredEventId()
        {
            const int definitionId = 1001;
            SetTestGameDataProvider(definitionId);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton(CreateBuffServiceForDamageTests().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var p1Id = Guid.NewGuid();
            var p2Id = Guid.NewGuid();
            var abilityManager = new Mock<IAbilityManager>();
            abilityManager
                .Setup(manager => manager.GetEffectiveAbilitiesForCard(It.IsAny<Guid>(), It.IsAny<GameContext>()))
                .Returns(new List<IAbility>());
            var gameState = new GameState("damage-delay-multi", new PlayerState(p1Id), new PlayerState(p2Id), p1Id)
            {
                AbilityManager = abilityManager.Object,
                BuffManager = Mock.Of<IBuffManager>()
            };

            var source = new CardInstance(Guid.NewGuid(), definitionId, p1Id) { CurrentZone = CardZone.Field };
            var target = new CardInstance(Guid.NewGuid(), definitionId, p2Id) { CurrentZone = CardZone.Field };
            var extraTarget = new CardInstance(Guid.NewGuid(), definitionId, p2Id) { CurrentZone = CardZone.Field };
            gameState.Player1.Field.Add(source);
            gameState.Player2.Field.Add(target);
            gameState.Player2.Field.Add(extraTarget);
            gameState.RegisterCardInstance(source);
            gameState.RegisterCardInstance(target);
            gameState.RegisterCardInstance(extraTarget);

            var effect = new Effect(
                new EffectConfig { Type = EffectType.Damage },
                p1Id,
                new List<TargetInfo> { TargetInfo.ForCard(target), TargetInfo.ForCard(extraTarget) },
                null,
                Guid.NewGuid(),
                source.InstanceId);

            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            using var context = new GameContext(
                serviceProvider,
                new Mock<IMapper>().Object,
                accessor,
                gameState,
                p1Id,
                default,
                null,
                source,
                effect);

            var evt = new DamageEvent(target, 2, source, DamageKind.Ability);
            var result = evt.Commit(context);

            Assert.True(result.Success);
            var leaveField = Assert.Single(result.GeneratedEvents.OfType<LeaveFieldEvent>());
            Assert.True(leaveField.DelayConfig.Enabled);
            Assert.Equal(DelayMode.ParentEffectsComplete, leaveField.DelayConfig.Mode);
            Assert.Equal(effect.TriggeredEventId, leaveField.DelayConfig.ParentEventId);
        }

        [Fact]
        public void DamageEvent_SingleTargetCurrentEffect_DelaysGeneratedDestroyEventByTriggeredEventId()
        {
            const int definitionId = 1001;
            SetTestGameDataProvider(definitionId);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton(CreateBuffServiceForDamageTests().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var p1Id = Guid.NewGuid();
            var p2Id = Guid.NewGuid();
            var abilityManager = new Mock<IAbilityManager>();
            abilityManager
                .Setup(manager => manager.GetEffectiveAbilitiesForCard(It.IsAny<Guid>(), It.IsAny<GameContext>()))
                .Returns(new List<IAbility>());
            var gameState = new GameState("damage-delay-single", new PlayerState(p1Id), new PlayerState(p2Id), p1Id)
            {
                AbilityManager = abilityManager.Object,
                BuffManager = Mock.Of<IBuffManager>()
            };

            var source = new CardInstance(Guid.NewGuid(), definitionId, p1Id) { CurrentZone = CardZone.Field };
            var target = new CardInstance(Guid.NewGuid(), definitionId, p2Id) { CurrentZone = CardZone.Field };
            gameState.Player1.Field.Add(source);
            gameState.Player2.Field.Add(target);
            gameState.RegisterCardInstance(source);
            gameState.RegisterCardInstance(target);

            var effect = new Effect(
                new EffectConfig { Type = EffectType.Damage },
                p1Id,
                new List<TargetInfo> { TargetInfo.ForCard(target) },
                null,
                Guid.NewGuid(),
                source.InstanceId);

            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            using var context = new GameContext(
                serviceProvider,
                new Mock<IMapper>().Object,
                accessor,
                gameState,
                p1Id,
                default,
                null,
                source,
                effect);

            var evt = new DamageEvent(target, 2, source, DamageKind.Ability);
            var result = evt.Commit(context);

            Assert.True(result.Success);
            var leaveField = Assert.Single(result.GeneratedEvents.OfType<LeaveFieldEvent>());
            Assert.True(leaveField.DelayConfig.Enabled);
            Assert.Equal(DelayMode.ParentEffectsComplete, leaveField.DelayConfig.Mode);
            Assert.Equal(effect.TriggeredEventId, leaveField.DelayConfig.ParentEventId);
        }

        [Theory]
        [InlineData(CardZone.Graveyard)]
        [InlineData(CardZone.Removed)]
        public void DamageEvent_TargetAlreadyOutOfPlay_SkipsResolution(CardZone targetZone)
        {
            const int definitionId = 1001;
            SetTestGameDataProvider(definitionId);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(Mock.Of<IBuffService>());
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var p1Id = Guid.NewGuid();
            var p2Id = Guid.NewGuid();
            var gameState = new GameState("damage-zone-guard", new PlayerState(p1Id), new PlayerState(p2Id), p1Id)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            var source = new CardInstance(Guid.NewGuid(), definitionId, p1Id) { CurrentZone = CardZone.Field };
            gameState.Player1.Field.Add(source);
            gameState.RegisterCardInstance(source);

            var target = new CardInstance(Guid.NewGuid(), definitionId, p2Id) { CurrentZone = targetZone };
            if (targetZone == CardZone.Graveyard)
            {
                gameState.Player2.Graveyard.Add(target);
            }
            gameState.RegisterCardInstance(target);

            using var context = new GameContext(
                serviceProvider,
                new Mock<IMapper>().Object,
                serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                p1Id);

            var evt = new DamageEvent(target, 3, source, DamageKind.Ability);
            var result = evt.Commit(context);

            Assert.False(result.Success);
            Assert.Empty(result.GeneratedEvents);
            Assert.Equal(0, evt.ActualDamage);
            Assert.Contains("no longer valid", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static Mock<IBuffService> CreateBuffServiceForDamageTests()
        {
            var buffService = new Mock<IBuffService>();
            buffService.Setup(service => service.GetEffectiveMaxHealth(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(2);
            buffService.Setup(service => service.GetEffectiveAttack(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(2);
            buffService.Setup(service => service.GetEffectiveCost(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(0);
            buffService.Setup(service => service.GetEffectiveKeywords(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(new List<CardKeyword>());
            return buffService;
        }

        private static void SetTestGameDataProvider(int definitionId)
        {
            var mockProvider = new Mock<IGameDataProvider>();
            mockProvider.SetupGet(provider => provider.IsInitialized).Returns(true);
            mockProvider.Setup(provider => provider.GetCardDefinition(definitionId)).Returns(new CardDefinition
            {
                Id = definitionId,
                Name = "DamageTestCard",
                Type = CardType.Follower,
                Attack = 2,
                Health = 2
            });
            mockProvider.Setup(provider => provider.GetAbilityConfigs(definitionId)).Returns(new System.Collections.Generic.List<AbilityConfig>());
            GameDataProvider.SetInstanceForTesting(mockProvider.Object);
        }
    }
}
