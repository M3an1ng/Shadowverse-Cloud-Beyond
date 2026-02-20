using System;
using System.Collections.Generic;
using System.Linq;
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
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class CardInstanceDestroyRulesTests
    {
        [Fact]
        public void CanBeDestroyed_AbilityReasonWithDestroyResist_ReturnsFalse()
        {
            var blockedCardId = Guid.Empty;
            using var fixture = CreateFixture(card =>
                card.InstanceId == blockedCardId
                    ? new List<CardKeyword> { CardKeyword.DestroyResist }
                    : new List<CardKeyword>());
            blockedCardId = fixture.Target.InstanceId;

            var canBeDestroyed = fixture.Target.CanBeDestroyed(fixture.Context, EventReasonSource.Ability);
            Assert.False(canBeDestroyed);
        }

        [Fact]
        public void CanBeDestroyed_SystemReasonWithDestroyResist_ReturnsTrue()
        {
            using var fixture = CreateFixture(_ => new List<CardKeyword> { CardKeyword.DestroyResist });
            var canBeDestroyed = fixture.Target.CanBeDestroyed(fixture.Context, EventReasonSource.System);
            Assert.True(canBeDestroyed);
        }

        [Fact]
        public void CanBeDestroyed_AbilityReasonOnCurrentTurnSuperEvolvedFollower_ReturnsFalse()
        {
            using var fixture = CreateFixture(_ => new List<CardKeyword>());
            fixture.Source.ApplyEvolutionChange(fixture.Context, isSuperEvolution: true);

            var canBeDestroyed = fixture.Source.CanBeDestroyed(fixture.Context, EventReasonSource.Ability);
            Assert.False(canBeDestroyed);
        }

        [Fact]
        public void DestroyEffectHandler_WhenTargetCannotBeDestroyed_GeneratesLeaveFieldAndRoutesToNoOp()
        {
            using var fixture = CreateFixture(_ => new List<CardKeyword> { CardKeyword.DestroyResist });
            var handler = new DestroyEffectHandler(
                NullLogger<DestroyEffectHandler>.Instance,
                Mock.Of<IMapper>(),
                new TargetProcessingService(NullLogger<TargetProcessingService>.Instance));

            var effectConfig = new EffectConfig { Type = EffectType.Destroy };
            var effect = new Effect(
                effectConfig,
                fixture.Player.PlayerId,
                new List<TargetInfo> { TargetInfo.ForCard(fixture.Target) },
                null,
                Guid.NewGuid(),
                fixture.Source.InstanceId);

            var result = handler.Apply(effect, fixture.Context, new DestroyEffectParams());
            Assert.True(result.Success);
            var leaveField = Assert.Single(result.GeneratedEvents.OfType<LeaveFieldEvent>());
            var leaveCommit = leaveField.Commit(fixture.Context);
            Assert.True(leaveCommit.Success);
            Assert.Empty(leaveCommit.GeneratedEvents);
            Assert.Equal(LeaveFieldKind.NoOp, leaveField.RoutedKind);
        }

        [Fact]
        public void CombatDestroyResolutionEvent_WhenBaneTargetCannotBeDestroyed_LeaveFieldRoutesToNoOp()
        {
            var blockedCardId = Guid.Empty;
            using var fixture = CreateFixture(card =>
                card.InstanceId == blockedCardId
                    ? new List<CardKeyword> { CardKeyword.DestroyResist }
                    : new List<CardKeyword>());
            blockedCardId = fixture.Target.InstanceId;

            var combatEvent = new CombatDestroyResolutionEvent(
                fixture.Source,
                fixture.Target,
                attackerHadBane: true,
                defenderHadBane: false,
                hasCounterAttack: false);

            var commitResult = combatEvent.Commit(fixture.Context);
            Assert.True(commitResult.Success);
            var leaveField = Assert.Single(commitResult.GeneratedEvents.OfType<LeaveFieldEvent>());
            var leaveCommit = leaveField.Commit(fixture.Context);
            Assert.True(leaveCommit.Success);
            Assert.Empty(leaveCommit.GeneratedEvents);
            Assert.Equal(LeaveFieldKind.NoOp, leaveField.RoutedKind);
        }

        private static TestFixture CreateFixture(Func<CardInstance, List<CardKeyword>> keywordProvider)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IBuffService>(CreateBuffService(keywordProvider).Object);
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var abilityManager = new Mock<IAbilityManager>();
            abilityManager
                .Setup(manager => manager.GetEffectiveAbilitiesForCard(It.IsAny<Guid>(), It.IsAny<GameContext>()))
                .Returns(new List<ShadowverseBeyond.Server.Domain.Abilities.Common.IAbility>());
            var gameState = new GameState("destroy-rules", player, opponent, player.PlayerId)
            {
                CurrentPlayerId = player.PlayerId,
                AbilityManager = abilityManager.Object,
                BuffManager = Mock.Of<IBuffManager>()
            };

            var source = new CardInstance(Guid.NewGuid(), 1, player.PlayerId)
            {
                CurrentZone = CardZone.Field
            };
            var target = new CardInstance(Guid.NewGuid(), 2, opponent.PlayerId)
            {
                CurrentZone = CardZone.Field
            };

            player.Field.Add(source);
            opponent.Field.Add(target);
            gameState.RegisterCardInstance(source);
            gameState.RegisterCardInstance(target);

            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var context = new GameContext(
                serviceProvider,
                Mock.Of<IMapper>(),
                accessor,
                gameState,
                player.PlayerId,
                default,
                null,
                source);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            return new TestFixture(context, player, source, target);
        }

        private static Mock<IBuffService> CreateBuffService(Func<CardInstance, List<CardKeyword>> keywordProvider)
        {
            var buffService = new Mock<IBuffService>();
            buffService.Setup(service => service.GetEffectiveMaxHealth(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(10);
            buffService.Setup(service => service.GetEffectiveAttack(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(2);
            buffService.Setup(service => service.GetEffectiveCost(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(0);
            buffService.Setup(service => service.GetEffectiveKeywords(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => keywordProvider(card));
            return buffService;
        }

        private sealed class TestFixture : IDisposable
        {
            public TestFixture(GameContext context, PlayerState player, CardInstance source, CardInstance target)
            {
                Context = context;
                Player = player;
                Source = source;
                Target = target;
            }

            public GameContext Context { get; }
            public PlayerState Player { get; }
            public CardInstance Source { get; }
            public CardInstance Target { get; }

            public void Dispose()
            {
                Context.Dispose();
            }
        }
    }
}
