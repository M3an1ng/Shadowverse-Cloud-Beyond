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
    public class LeaveFieldEventRoutingTests
    {
        [Fact]
        public void LeaveField_AbilityDestroyWithDestroyResist_RoutesToNoOp()
        {
            using var fixture = CreateFixture(_ => new List<CardKeyword> { CardKeyword.DestroyResist });
            var evt = new LeaveFieldEvent(fixture.Target, LeaveFieldKind.Destroy, EventReasonSource.Ability, fixture.Source);

            var result = evt.Commit(fixture.Context);

            Assert.True(result.Success);
            Assert.Empty(result.GeneratedEvents);
            Assert.Equal(LeaveFieldKind.NoOp, evt.RoutedKind);
        }

        [Fact]
        public void LeaveField_SystemDestroyWithDestroyResist_RoutesToDestroy()
        {
            using var fixture = CreateFixture(_ => new List<CardKeyword> { CardKeyword.DestroyResist });
            var evt = new LeaveFieldEvent(fixture.Target, LeaveFieldKind.Destroy, EventReasonSource.System, fixture.Source);

            var result = evt.Commit(fixture.Context);

            Assert.True(result.Success);
            var destroy = Assert.Single(result.GeneratedEvents.OfType<DestroyEvent>());
            Assert.Equal(EventReasonSource.System, destroy.Reason);
            Assert.Equal(LeaveFieldKind.Destroy, evt.RoutedKind);
        }

        [Fact]
        public void LeaveField_AbilityVanishWithVanishResist_RoutesToDestroyWithoutVanishEvent()
        {
            using var fixture = CreateFixture(_ => new List<CardKeyword> { CardKeyword.VanishResist });
            var evt = new LeaveFieldEvent(fixture.Target, LeaveFieldKind.Vanish, EventReasonSource.Ability, fixture.Source);

            var result = evt.Commit(fixture.Context);

            Assert.True(result.Success);
            Assert.Equal(LeaveFieldKind.Destroy, evt.RoutedKind);
            Assert.Single(result.GeneratedEvents.OfType<DestroyEvent>());
            Assert.Empty(result.GeneratedEvents.OfType<CardVanishedEvent>());
        }

        [Fact]
        public void LeaveField_SystemVanishWithVanishResist_StillRoutesToVanish()
        {
            using var fixture = CreateFixture(_ => new List<CardKeyword> { CardKeyword.VanishResist });
            var evt = new LeaveFieldEvent(fixture.Target, LeaveFieldKind.Vanish, EventReasonSource.System, fixture.Source);

            var result = evt.Commit(fixture.Context);

            Assert.True(result.Success);
            Assert.Equal(LeaveFieldKind.Vanish, evt.RoutedKind);
            Assert.Single(result.GeneratedEvents.OfType<CardVanishedEvent>());
        }

        [Fact]
        public void LeaveField_DestroyRoute_LegacyCommitStillEmitsCardMoved()
        {
            using var fixture = CreateFixture(_ => []);
            var evt = new LeaveFieldEvent(fixture.Target, LeaveFieldKind.Destroy, EventReasonSource.System, fixture.Source);

            var routeResult = evt.Commit(fixture.Context);
            var destroyEvent = Assert.Single(routeResult.GeneratedEvents.OfType<DestroyEvent>());
            var destroyCommit = destroyEvent.Commit(fixture.Context);

            Assert.True(destroyCommit.Success);
            Assert.Contains(destroyCommit.GeneratedEvents, e => e is CardMovedEvent moved && moved.ToZone == CardZone.Graveyard);
        }

        [Fact]
        public void LeaveField_DestroyRoute_CapturesSnapshotAtCommitTime()
        {
            var effectiveAttack = 2;
            using var fixture = CreateFixture(_ => [], getEffectiveAttack: _ => effectiveAttack);
            var evt = new LeaveFieldEvent(fixture.Target, LeaveFieldKind.Destroy, EventReasonSource.System, fixture.Source);

            effectiveAttack = 0;
            var routeResult = evt.Commit(fixture.Context);

            var destroyEvent = Assert.Single(routeResult.GeneratedEvents.OfType<DestroyEvent>());
            Assert.Equal(0, evt.LeaveTargetSnapshot?.CurrentAttack);
            Assert.Equal(0, destroyEvent.DestroyTargetSnapshot?.CurrentAttack);
        }

        [Fact]
        public void LeaveField_VanishRoute_LegacyCommitStillEmitsCardMovedToRemoved()
        {
            using var fixture = CreateFixture(_ => []);
            var evt = new LeaveFieldEvent(fixture.Target, LeaveFieldKind.Vanish, EventReasonSource.System, fixture.Source);

            var routeResult = evt.Commit(fixture.Context);
            var vanishEvent = Assert.Single(routeResult.GeneratedEvents.OfType<CardVanishedEvent>());
            var vanishCommit = vanishEvent.Commit(fixture.Context);

            Assert.True(vanishCommit.Success);
            Assert.Contains(vanishCommit.GeneratedEvents, e => e is CardMovedEvent moved && moved.ToZone == CardZone.Removed);
        }

        [Fact]
        public void RoutedLeaveEvents_CaptureFallbackSnapshotsAtCommitTime()
        {
            var effectiveAttack = 2;
            using var fixture = CreateFixture(_ => [], getEffectiveAttack: _ => effectiveAttack);
            var vanish = new CardVanishedEvent(fixture.Target, CardZone.Field, EventReasonSource.System, fixture.Source);
            var returnEvent = new ReturnEvent(fixture.Target, EventReasonSource.Ability);
            var tuck = new TuckEvent(fixture.Target, EventReasonSource.Ability);

            effectiveAttack = 0;

            Assert.True(vanish.Commit(fixture.Context).Success);
            Assert.True(returnEvent.Commit(fixture.Context).Success);
            Assert.True(tuck.Commit(fixture.Context).Success);
            Assert.Equal(0, vanish.VanishedCardSnapshot?.CurrentAttack);
            Assert.Equal(0, returnEvent.ReturnedCardSnapshot?.CurrentAttack);
            Assert.Equal(0, tuck.TuckedCardSnapshot?.CurrentAttack);
        }

        [Fact]
        public void LeaveField_ReturnAndTuckRoutes_LegacyCommitSemanticsRemain()
        {
            using var returnFixture = CreateFixture(_ => []);
            var returnEvt = new LeaveFieldEvent(returnFixture.Target, LeaveFieldKind.Return, EventReasonSource.Ability, returnFixture.Source);
            var returnRoute = returnEvt.Commit(returnFixture.Context);
            var returnEvent = Assert.Single(returnRoute.GeneratedEvents.OfType<ReturnEvent>());
            var returnCommit = returnEvent.Commit(returnFixture.Context);
            Assert.True(returnCommit.Success);
            Assert.Contains(returnCommit.GeneratedEvents, e => e is AddCardToHandEvent);

            using var tuckFixture = CreateFixture(_ => []);
            var tuckEvt = new LeaveFieldEvent(tuckFixture.Target, LeaveFieldKind.Tuck, EventReasonSource.Ability, tuckFixture.Source);
            var tuckRoute = tuckEvt.Commit(tuckFixture.Context);
            var tuckEvent = Assert.Single(tuckRoute.GeneratedEvents.OfType<TuckEvent>());
            var tuckCommit = tuckEvent.Commit(tuckFixture.Context);
            Assert.True(tuckCommit.Success);
            Assert.Contains(tuckCommit.GeneratedEvents, e => e is AddCardToDeckEvent);
        }

        [Fact]
        public void LeaveField_TuckRoute_UsesConfiguredDeckInsertMode()
        {
            using var fixture = CreateFixture(_ => []);
            var evt = new LeaveFieldEvent(
                fixture.Target,
                LeaveFieldKind.Tuck,
                EventReasonSource.Ability,
                fixture.Source,
                deckInsertMode: DeckInsertMode.Bottom);

            var routeResult = evt.Commit(fixture.Context);
            var tuckEvent = Assert.Single(routeResult.GeneratedEvents.OfType<TuckEvent>());
            var tuckCommit = tuckEvent.Commit(fixture.Context);

            Assert.True(tuckCommit.Success);
            var addDeckEvent = Assert.Single(tuckCommit.GeneratedEvents.OfType<AddCardToDeckEvent>());
            Assert.Equal(DeckInsertMode.Bottom, addDeckEvent.InsertMode);
        }

        [Fact]
        public void TuckEffect_UsesConfiguredDeckInsertMode()
        {
            using var fixture = CreateFixture(_ => []);
            var processor = new PassthroughTargetProcessingService();
            var handler = new TuckEffectHandler(NullLogger<TuckEffectHandler>.Instance, processor);
            var effectConfig = new EffectConfig { Type = EffectType.Tuck };
            var effect = new Effect(
                effectConfig,
                fixture.Source.OwnerPlayerId,
                new List<TargetInfo> { TargetInfo.ForCard(fixture.Target) },
                null,
                Guid.NewGuid(),
                null);
            var parameters = new TuckEffectParams { InsertMode = DeckInsertMode.Top };

            var result = handler.Apply(effect, fixture.Context, parameters);

            Assert.True(result.Success);
            var leaveFieldEvent = Assert.Single(result.GeneratedEvents.OfType<LeaveFieldEvent>());
            var routeResult = leaveFieldEvent.Commit(fixture.Context);
            var tuckEvent = Assert.Single(routeResult.GeneratedEvents.OfType<TuckEvent>());
            var tuckCommit = tuckEvent.Commit(fixture.Context);
            var addDeckEvent = Assert.Single(tuckCommit.GeneratedEvents.OfType<AddCardToDeckEvent>());
            Assert.Equal(DeckInsertMode.Top, addDeckEvent.InsertMode);
        }

        private static TestFixture CreateFixture(
            Func<CardInstance, List<CardKeyword>> keywordProvider,
            Func<CardInstance, int>? getEffectiveAttack = null)
        {
            SetTestGameDataProvider();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IBuffService>(CreateBuffService(keywordProvider, getEffectiveAttack).Object);
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var abilityManager = new Mock<IAbilityManager>();
            abilityManager
                .Setup(manager => manager.GetEffectiveAbilitiesForCard(It.IsAny<Guid>(), It.IsAny<GameContext>()))
                .Returns(new List<ShadowverseBeyond.Server.Domain.Abilities.Common.IAbility>());
            var gameState = new GameState("leave-field-routing", player, opponent, player.PlayerId)
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
            GameServiceProvider.Logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

            return new TestFixture(context, source, target);
        }

        private static void SetTestGameDataProvider()
        {
            var mockProvider = new Mock<IGameDataProvider>();
            mockProvider.SetupGet(provider => provider.IsInitialized).Returns(true);
            mockProvider.Setup(provider => provider.GetCardDefinition(It.IsAny<int>()))
                .Returns((int definitionId) => new CardDefinition
                {
                    Id = definitionId,
                    Name = $"LeaveFieldTestCard{definitionId}",
                    Type = CardType.Follower,
                    Attack = 2,
                    Health = 2
                });
            mockProvider.Setup(provider => provider.GetAbilityConfigs(It.IsAny<int>()))
                .Returns(new List<AbilityConfig>());
            GameDataProvider.SetInstanceForTesting(mockProvider.Object);
        }

        private static Mock<IBuffService> CreateBuffService(
            Func<CardInstance, List<CardKeyword>> keywordProvider,
            Func<CardInstance, int>? getEffectiveAttack)
        {
            var buffService = new Mock<IBuffService>();
            buffService.Setup(service => service.GetEffectiveMaxHealth(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(10);
            buffService.Setup(service => service.GetEffectiveAttack(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => getEffectiveAttack?.Invoke(card) ?? 2);
            buffService.Setup(service => service.GetEffectiveCost(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(0);
            buffService.Setup(service => service.GetEffectiveKeywords(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => keywordProvider(card));
            return buffService;
        }

        private sealed class TestFixture : IDisposable
        {
            public TestFixture(GameContext context, CardInstance source, CardInstance target)
            {
                Context = context;
                Source = source;
                Target = target;
            }

            public GameContext Context { get; }
            public CardInstance Source { get; }
            public CardInstance Target { get; }

            public void Dispose()
            {
                Context.Dispose();
            }
        }

        private sealed class PassthroughTargetProcessingService : ITargetProcessingService
        {
            public GameStateActionResult ProcessTargets(
                GameContext context,
                IReadOnlyList<TargetInfo> targets,
                Func<TargetInfo, GameContext, DomainOperationResult> action,
                string effectName)
            {
                var result = new GameStateActionResult { Success = true };
                foreach (var target in targets)
                {
                    var actionResult = action(target, context);
                    if (!actionResult.Success)
                    {
                        result.Success = false;
                        result.ErrorMessage = actionResult.ErrorMessage;
                        return result;
                    }

                    result.GeneratedEvents.AddRange(actionResult.GeneratedEvents);
                }

                return result;
            }
        }
    }
}
