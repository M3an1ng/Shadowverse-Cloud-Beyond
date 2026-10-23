using System;
using System.Linq;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class PpBoostPaymentModelTests
    {
        [Fact]
        public void PpBoostRequest_toggles_armed_state_without_changing_pp_or_uses()
        {
            using var ctx = CreateContext(out var player, out _);
            player.IsFirst = false;
            player.MaxPlayPoints = 6;
            player.CurrentPlayPoints = 6;
            player.PpBoostUsesAvailable = 1;

            var armResult = new PpBoostRequestedEvent(player.PlayerId).Commit(ctx);
            Assert.True(armResult.Success, armResult.ErrorMessage);
            Assert.True(player.IsPpBoostArmed);
            Assert.Equal(6, player.CurrentPlayPoints);
            Assert.Equal(1, player.PpBoostUsesAvailable);
            Assert.DoesNotContain(armResult.GeneratedEvents, evt => evt is PpUpdateEvent);

            var armEvent = Assert.Single(armResult.GeneratedEvents.OfType<PpBoostStateUpdatedEvent>());
            Assert.True(armEvent.IsArmed);
            Assert.Equal(1, armEvent.PpBoostUsesAvailable);

            var cancelResult = new PpBoostRequestedEvent(player.PlayerId).Commit(ctx);
            Assert.True(cancelResult.Success, cancelResult.ErrorMessage);
            Assert.False(player.IsPpBoostArmed);
            Assert.Equal(6, player.CurrentPlayPoints);
            Assert.Equal(1, player.PpBoostUsesAvailable);

            var cancelEvent = Assert.Single(cancelResult.GeneratedEvents.OfType<PpBoostStateUpdatedEvent>());
            Assert.False(cancelEvent.IsArmed);
            Assert.Equal(1, cancelEvent.PpBoostUsesAvailable);
        }

        [Fact]
        public void PreparePlayCard_allows_cost_one_above_current_pp_when_boost_armed()
        {
            using var ctx = CreateContext(out var player, out _);
            player.MaxPlayPoints = 3;
            player.CurrentPlayPoints = 3;
            player.PpBoostUsesAvailable = 1;
            player.IsPpBoostArmed = true;

            var card = new CardInstance(Guid.NewGuid(), 1001, player.PlayerId)
            {
                CurrentZone = CardZone.Hand
            };
            player.Hand.Add(card);

            var result = player.PreparePlayCard(card, paidCost: 4);

            Assert.True(result.Success, result.ErrorMessage);
            var playEvent = Assert.Single(result.GeneratedEvents.OfType<PlayCardEvent>());
            Assert.Equal(4, playEvent.PaidCost);
        }

        [Fact]
        public void SpendPlayPoints_consumes_regular_pp_before_pp_boost()
        {
            using var ctx = CreateContext(out var player, out _);
            player.MaxPlayPoints = 3;
            player.CurrentPlayPoints = 3;
            player.PpBoostUsesAvailable = 1;
            player.IsPpBoostArmed = true;

            var firstSpend = player.SpendPlayPoints(2);
            Assert.True(firstSpend.Success, firstSpend.ErrorMessage);
            Assert.DoesNotContain(firstSpend.GeneratedEvents, evt => evt is PpBoostStateUpdatedEvent);
            CommitGeneratedEvents(firstSpend, ctx);

            Assert.Equal(1, player.CurrentPlayPoints);
            Assert.True(player.IsPpBoostArmed);
            Assert.Equal(1, player.PpBoostUsesAvailable);

            var secondSpend = player.SpendPlayPoints(2);
            Assert.True(secondSpend.Success, secondSpend.ErrorMessage);
            CommitGeneratedEvents(secondSpend, ctx);

            Assert.Equal(0, player.CurrentPlayPoints);
            Assert.False(player.IsPpBoostArmed);
            Assert.Equal(0, player.PpBoostUsesAvailable);

            var boostEvent = Assert.Single(secondSpend.GeneratedEvents.OfType<PpBoostStateUpdatedEvent>());
            Assert.False(boostEvent.IsArmed);
            Assert.Equal(0, boostEvent.PpBoostUsesAvailable);
        }

        [Fact]
        public void CleanupEndOfTurnState_clears_unused_armed_boost_without_spending_a_use()
        {
            using var context = CreateContext(out var player, out var opponent);
            player.PpBoostUsesAvailable = 1;
            player.IsPpBoostArmed = true;

            var result = player.CleanupEndOfTurnState();

            Assert.True(result.Success, result.ErrorMessage);
            Assert.False(player.IsPpBoostArmed);
            Assert.Equal(1, player.PpBoostUsesAvailable);

            var boostEvent = Assert.Single(result.GeneratedEvents.OfType<PpBoostStateUpdatedEvent>());
            Assert.False(boostEvent.IsArmed);
            Assert.Equal(1, boostEvent.PpBoostUsesAvailable);
        }

        private static void CommitGeneratedEvents(DomainOperationResult result, GameContext context)
        {
            foreach (var evt in result.GeneratedEvents.OfType<ICommittableEvent>())
            {
                var commitResult = evt.Commit(context);
                Assert.True(commitResult.Success, commitResult.ErrorMessage);
            }
        }

        private static GameContext CreateContext(out PlayerState player, out PlayerState opponent)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var accessor = new GameContextAccessor();
            services.AddSingleton<IGameContextAccessor>(accessor);
            var buffService = new Mock<IBuffService>();
            buffService
                .Setup(service => service.GetEffectiveKeywords(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(Array.Empty<CardKeyword>().ToList());
            buffService
                .Setup(service => service.GetEffectiveCost(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns<CardInstance, GameContext>((card, _) => card.BaseCost);
            services.AddSingleton(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();

            var serviceProvider = services.BuildServiceProvider();
            var mapper = new MapperConfiguration(_ => { }).CreateMapper();

            player = new PlayerState(Guid.NewGuid())
            {
                TurnNumber = 1
            };
            opponent = new PlayerState(Guid.NewGuid())
            {
                TurnNumber = 1
            };

            var gameState = new GameState("pp-boost-test", player, opponent, player.PlayerId)
            {
                BuffManager = Mock.Of<IBuffManager>(),
                Phase = GamePhase.Main,
                CurrentPlayerId = player.PlayerId
            };

            GameServiceProvider.ContextAccessor = accessor;
            return new GameContext(serviceProvider, mapper, accessor, gameState, player.PlayerId);
        }
    }
}
