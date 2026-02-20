using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using System;
using System.Collections.Generic;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class StatChangedEventSystemBuffTests
    {
        [Fact]
        public void StatChanged_without_ability_context_creates_no_source_buff()
        {
            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var player1 = new PlayerState(player1Id);
            var player2 = new PlayerState(player2Id);

            var gameState = new GameState("stat-buff-test", player1, player2, player1Id)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            var target = new CardInstance(Guid.NewGuid(), 1001, player1Id) { CurrentZone = CardZone.Field };
            player1.Field.Add(target);
            gameState.RegisterCardInstance(target);

            var buffManager = new Mock<IBuffManager>();
            buffManager.Setup(m => m.GetBuffsForCard(It.IsAny<Guid>())).Returns(new List<Buff>());
            gameState.BuffManager = buffManager.Object;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffFactory>(new BuffFactory(NullLogger<BuffFactory>.Instance));
            services.AddSingleton(Mock.Of<IConditionEvaluator>());
            services.AddSingleton<IBuffEvaluator>(sp =>
                new BuffEvaluator(sp.GetRequiredService<IConditionEvaluator>(), NullLogger<BuffEvaluator>.Instance));
            services.AddSingleton(new BuffStackingEngine(NullLogger<BuffStackingEngine>.Instance));
            services.AddSingleton<IBuffService>(sp =>
                new BuffService(
                    buffManager.Object,
                    sp.GetRequiredService<IBuffEvaluator>(),
                    sp.GetRequiredService<IBuffFactory>(),
                    NullLogger<BuffService>.Instance,
                    sp.GetRequiredService<BuffStackingEngine>()));
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();

            var sp = services.BuildServiceProvider();
            GameServiceProvider.ContextAccessor = sp.GetRequiredService<IGameContextAccessor>();
            GameServiceProvider.Logger = NullLogger.Instance;

            using var ctx = new GameContext(
                sp,
                Mock.Of<IMapper>(),
                sp.GetRequiredService<IGameContextAccessor>(),
                gameState,
                requestingPlayerId: player1Id,
                triggeringEvent: null,
                sourceCard: null,
                currentEffect: null);

            var ev = new StatChangedEvent(target, attackModifier: 1, healthModifier: 0, costModifier: 0);
            var result = ev.Commit(ctx);

            Assert.True(result.Success);
            var applied = Assert.Single(result.GeneratedEvents, e => e is BuffAppliedEvent) as BuffAppliedEvent;
            Assert.NotNull(applied);
            Assert.Equal(Guid.Empty, applied!.AppliedBuff.SourceAbilityId);
            Assert.Null(applied.BuffSource);
        }

        [Fact]
        public void StatChangedEvent_PassesDurationParameterToBuff()
        {
            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var player1 = new PlayerState(player1Id);
            var player2 = new PlayerState(player2Id);

            var gameState = new GameState("stat-buff-duration-test", player1, player2, player1Id)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            var target = new CardInstance(Guid.NewGuid(), 1001, player1Id) { CurrentZone = CardZone.Field };
            player1.Field.Add(target);
            gameState.RegisterCardInstance(target);

            var buffManager = new Mock<IBuffManager>();
            buffManager.Setup(m => m.GetBuffsForCard(It.IsAny<Guid>())).Returns(new List<Buff>());
            gameState.BuffManager = buffManager.Object;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffFactory>(new BuffFactory(NullLogger<BuffFactory>.Instance));
            services.AddSingleton(Mock.Of<IConditionEvaluator>());
            services.AddSingleton<IBuffEvaluator>(sp =>
                new BuffEvaluator(sp.GetRequiredService<IConditionEvaluator>(), NullLogger<BuffEvaluator>.Instance));
            services.AddSingleton(new BuffStackingEngine(NullLogger<BuffStackingEngine>.Instance));
            services.AddSingleton<IBuffService>(sp =>
                new BuffService(
                    buffManager.Object,
                    sp.GetRequiredService<IBuffEvaluator>(),
                    sp.GetRequiredService<IBuffFactory>(),
                    NullLogger<BuffService>.Instance,
                    sp.GetRequiredService<BuffStackingEngine>()));
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();

            var sp = services.BuildServiceProvider();

            using var ctx = new GameContext(
                sp,
                Mock.Of<IMapper>(),
                sp.GetRequiredService<IGameContextAccessor>(),
                gameState,
                requestingPlayerId: player1Id,
                triggeringEvent: null,
                sourceCard: null,
                currentEffect: null);

            var ev = new StatChangedEvent(
                target,
                attackModifier: 1,
                healthModifier: 0,
                costModifier: 0,
                durationType: DurationType.EndOfTurn,
                durationParameter: 2);
            var result = ev.Commit(ctx);

            Assert.True(result.Success);
            var applied = Assert.Single(result.GeneratedEvents, e => e is BuffAppliedEvent) as BuffAppliedEvent;
            Assert.NotNull(applied);
            Assert.Equal(DurationType.EndOfTurn, applied!.AppliedBuff.DurationType);
            Assert.Equal(2, applied.AppliedBuff.DurationParameter);
        }

        [Fact]
        public void StatChangedEvent_Announcement_UsesLayerSpecificText()
        {
            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var player1 = new PlayerState(player1Id);
            var player2 = new PlayerState(player2Id);
            var gameState = new GameState("stat-announcement-test", player1, player2, player1Id)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            var target = new CardInstance(Guid.NewGuid(), 1001, player1Id) { CurrentZone = CardZone.Field };
            player1.Field.Add(target);
            gameState.RegisterCardInstance(target);

            var additive = new StatChangedEvent(target, attackModifier: 2, healthModifier: null, costModifier: null, modifierLayer: ModifierLayer.Additive);
            var multiplicative = new StatChangedEvent(target, attackModifier: null, healthModifier: null, costModifier: 3, modifierLayer: ModifierLayer.Multiplicative);
            var set = new StatChangedEvent(target, attackModifier: null, healthModifier: 7, costModifier: null, modifierLayer: ModifierLayer.Set);
            var baseLayer = new StatChangedEvent(target, attackModifier: 1, healthModifier: null, costModifier: null, modifierLayer: ModifierLayer.Base);

            Assert.Contains("攻击力增加[[num:2]]", additive.GetAnnouncement(gameState));
            Assert.Contains("费用调整为[[num:3]]倍", multiplicative.GetAnnouncement(gameState));
            Assert.Contains("生命值设为[[num:7]]", set.GetAnnouncement(gameState));
            Assert.Contains("攻击力基础值设为[[num:1]]", baseLayer.GetAnnouncement(gameState));
        }

        [Fact]
        public void StatChanged_LethalFollowerInAbilityContext_UsesSystemDestroyReason()
        {
            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var player1 = new PlayerState(player1Id);
            var player2 = new PlayerState(player2Id);
            var abilityManager = new Mock<IAbilityManager>();
            abilityManager
                .Setup(manager => manager.GetEffectiveAbilitiesForCard(It.IsAny<Guid>(), It.IsAny<GameContext>()))
                .Returns(new List<IAbility>());

            var gameState = new GameState("stat-lethal-system-reason-test", player1, player2, player1Id)
            {
                AbilityManager = abilityManager.Object,
                BuffManager = Mock.Of<IBuffManager>()
            };

            var source = new CardInstance(Guid.NewGuid(), 1002, player1Id) { CurrentZone = CardZone.Field };
            var target = new CardInstance(Guid.NewGuid(), 1001, player2Id) { CurrentZone = CardZone.Field };
            player1.Field.Add(source);
            player2.Field.Add(target);
            gameState.RegisterCardInstance(source);
            gameState.RegisterCardInstance(target);

            var buffManager = new Mock<IBuffManager>();
            buffManager.Setup(m => m.GetBuffsForCard(It.IsAny<Guid>())).Returns(new List<Buff>());
            gameState.BuffManager = buffManager.Object;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffFactory>(new BuffFactory(NullLogger<BuffFactory>.Instance));
            services.AddSingleton(Mock.Of<IConditionEvaluator>());
            services.AddSingleton<IBuffEvaluator>(sp =>
                new BuffEvaluator(sp.GetRequiredService<IConditionEvaluator>(), NullLogger<BuffEvaluator>.Instance));
            services.AddSingleton(new BuffStackingEngine(NullLogger<BuffStackingEngine>.Instance));
            services.AddSingleton<IBuffService>(sp =>
                new BuffService(
                    buffManager.Object,
                    sp.GetRequiredService<IBuffEvaluator>(),
                    sp.GetRequiredService<IBuffFactory>(),
                    NullLogger<BuffService>.Instance,
                    sp.GetRequiredService<BuffStackingEngine>()));
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();

            var sp = services.BuildServiceProvider();

            using var ctx = new GameContext(
                sp,
                Mock.Of<IMapper>(),
                sp.GetRequiredService<IGameContextAccessor>(),
                gameState,
                requestingPlayerId: player1Id,
                triggeringEvent: null,
                sourceCard: source,
                currentEffect: new Effect(player1Id));

            var ev = new StatChangedEvent(
                targetCard: target,
                attackModifier: null,
                healthModifier: 0,
                costModifier: null,
                modifierLayer: ModifierLayer.Set);

            var result = ev.Commit(ctx);
            Assert.True(result.Success);

            var destroy = Assert.Single(result.GeneratedEvents, e => e is DestroyEvent) as DestroyEvent;
            Assert.NotNull(destroy);
            Assert.Equal(EventReasonSource.System, destroy!.Reason);
        }

        [Fact]
        public void StatChanged_LethalFollowerWithMultiTargetEffect_DelaysDestroyByEffectId()
        {
            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var player1 = new PlayerState(player1Id);
            var player2 = new PlayerState(player2Id);
            var abilityManager = new Mock<IAbilityManager>();
            abilityManager
                .Setup(manager => manager.GetEffectiveAbilitiesForCard(It.IsAny<Guid>(), It.IsAny<GameContext>()))
                .Returns(new List<IAbility>());

            var gameState = new GameState("stat-delay-by-effect-test", player1, player2, player1Id)
            {
                AbilityManager = abilityManager.Object,
                BuffManager = Mock.Of<IBuffManager>()
            };

            var source = new CardInstance(Guid.NewGuid(), 1002, player1Id) { CurrentZone = CardZone.Field };
            var target = new CardInstance(Guid.NewGuid(), 1001, player2Id) { CurrentZone = CardZone.Field };
            var extraTarget = new CardInstance(Guid.NewGuid(), 1001, player2Id) { CurrentZone = CardZone.Field };
            player1.Field.Add(source);
            player2.Field.Add(target);
            player2.Field.Add(extraTarget);
            gameState.RegisterCardInstance(source);
            gameState.RegisterCardInstance(target);
            gameState.RegisterCardInstance(extraTarget);

            var buffManager = new Mock<IBuffManager>();
            buffManager.Setup(m => m.GetBuffsForCard(It.IsAny<Guid>())).Returns(new List<Buff>());
            gameState.BuffManager = buffManager.Object;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffFactory>(new BuffFactory(NullLogger<BuffFactory>.Instance));
            services.AddSingleton(Mock.Of<IConditionEvaluator>());
            services.AddSingleton<IBuffEvaluator>(sp =>
                new BuffEvaluator(sp.GetRequiredService<IConditionEvaluator>(), NullLogger<BuffEvaluator>.Instance));
            services.AddSingleton(new BuffStackingEngine(NullLogger<BuffStackingEngine>.Instance));
            services.AddSingleton<IBuffService>(sp =>
                new BuffService(
                    buffManager.Object,
                    sp.GetRequiredService<IBuffEvaluator>(),
                    sp.GetRequiredService<IBuffFactory>(),
                    NullLogger<BuffService>.Instance,
                    sp.GetRequiredService<BuffStackingEngine>()));
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();

            var sp = services.BuildServiceProvider();
            GameServiceProvider.ContextAccessor = sp.GetRequiredService<IGameContextAccessor>();
            GameServiceProvider.Logger = NullLogger.Instance;

            var effect = new Effect(player1Id);
            effect.SelectedTargets.Add(TargetInfo.ForCard(target));
            effect.SelectedTargets.Add(TargetInfo.ForCard(extraTarget));

            using var ctx = new GameContext(
                sp,
                Mock.Of<IMapper>(),
                sp.GetRequiredService<IGameContextAccessor>(),
                gameState,
                requestingPlayerId: player1Id,
                triggeringEvent: null,
                sourceCard: source,
                currentEffect: effect);

            var ev = new StatChangedEvent(
                targetCard: target,
                attackModifier: null,
                healthModifier: 0,
                costModifier: null,
                modifierLayer: ModifierLayer.Set);

            var result = ev.Commit(ctx);
            Assert.True(result.Success);

            var destroy = Assert.Single(result.GeneratedEvents, e => e is DestroyEvent) as DestroyEvent;
            Assert.NotNull(destroy);
            Assert.True(destroy!.DelayConfig.Enabled);
            Assert.Equal(DelayMode.ParentEffectsComplete, destroy.DelayConfig.Mode);
            Assert.Equal(effect.Id, destroy.DelayConfig.ParentEventId);
        }
    }
}
