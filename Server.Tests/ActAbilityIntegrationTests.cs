using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using System;
using System.Collections.Generic;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class ActAbilityIntegrationTests
    {
        [Fact]
        public void Act_is_usable_once_per_turn_and_resets_next_turn()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var accessor = new GameContextAccessor();
            services.AddSingleton<IGameContextAccessor>(accessor);
            var buffService = new Mock<IBuffService>();
            buffService
                .Setup(b => b.GetEffectiveKeywords(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(new List<CardKeyword>());
            services.AddSingleton<IBuffService>(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();

            var sp = services.BuildServiceProvider();
            var mapper = new MapperConfiguration(_ => { }).CreateMapper();

            var p1Id = Guid.NewGuid();
            var p2Id = Guid.NewGuid();
            var player1 = new PlayerState(p1Id) { TurnNumber = 1 };
            var player2 = new PlayerState(p2Id) { TurnNumber = 1 };

            var abilityManager = new Mock<IAbilityManager>();
            var actAbilityMock = new Mock<IAbility>();
            actAbilityMock.SetupGet(a => a.Type).Returns(AbilityType.Activated);
            actAbilityMock.SetupGet(a => a.AbilityKeywords).Returns(new List<AbilityKeywordConfig>
            {
                new AbilityKeywordConfig { Keyword = AbilityKeyword.Act }
            });
            var actAbility = actAbilityMock.Object;

            var gameState = new GameState("act-test", player1, player2, p1Id)
            {
                AbilityManager = abilityManager.Object,
                BuffManager = Mock.Of<IBuffManager>(),
                Phase = GamePhase.Main,
                CurrentPlayerId = p1Id
            };

            var card = new CardInstance(Guid.NewGuid(), 1001, p1Id) { CurrentZone = CardZone.Field };
            player1.Field.Add(card);
            gameState.RegisterCardInstance(card);

            abilityManager
                .Setup(m => m.GetAbilitiesForCard(It.IsAny<Guid>()))
                .Returns((Guid cardId) => cardId == card.InstanceId
                    ? new List<IAbility> { actAbility }
                    : new List<IAbility>());

            GameServiceProvider.ContextAccessor = accessor;

            using var ctx = new GameContext(sp, mapper, accessor, gameState, p1Id);

            var first = new ActRequestedEvent(p1Id, new ActActionDto { CardUid = card.InstanceId }).Commit(ctx);
            Assert.True(first.Success, first.ErrorMessage);
            var invoked = Assert.Single(first.GeneratedEvents.OfType<ActInvokedEvent>());
            var invokedResult = invoked.Commit(ctx);
            Assert.True(invokedResult.Success, invokedResult.ErrorMessage);
            Assert.True(card.HasUsedActThisTurn(player1.TurnNumber));
            Assert.Contains(invokedResult.GeneratedEvents, e => e is ActStateUpdatedEvent);

            var second = new ActRequestedEvent(p1Id, new ActActionDto { CardUid = card.InstanceId }).Commit(ctx);
            Assert.False(second.Success);
            Assert.Empty(second.GeneratedEvents);

            // Next turn for the same player: reset per-turn act usage.
            var turnStart = player1.StartTurn(ctx);
            Assert.True(turnStart.Success, turnStart.ErrorMessage);

            var third = new ActRequestedEvent(p1Id, new ActActionDto { CardUid = card.InstanceId }).Commit(ctx);
            Assert.True(third.Success, third.ErrorMessage);
            var invokedThird = Assert.Single(third.GeneratedEvents.OfType<ActInvokedEvent>());
            var thirdResult = invokedThird.Commit(ctx);
            Assert.True(thirdResult.Success, thirdResult.ErrorMessage);
            Assert.True(card.HasUsedActThisTurn(player1.TurnNumber));
        }

        [Fact]
        public void Opponent_cannot_use_your_act_even_on_their_turn()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var accessor = new GameContextAccessor();
            services.AddSingleton<IGameContextAccessor>(accessor);
            var buffService = new Mock<IBuffService>();
            buffService
                .Setup(b => b.GetEffectiveKeywords(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(new List<CardKeyword>());
            services.AddSingleton<IBuffService>(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();

            var sp = services.BuildServiceProvider();
            var mapper = new MapperConfiguration(_ => { }).CreateMapper();

            var p1Id = Guid.NewGuid();
            var p2Id = Guid.NewGuid();
            var player1 = new PlayerState(p1Id) { TurnNumber = 1 };
            var player2 = new PlayerState(p2Id) { TurnNumber = 1 };

            var abilityManager = new Mock<IAbilityManager>();
            var actAbilityMock = new Mock<IAbility>();
            actAbilityMock.SetupGet(a => a.Type).Returns(AbilityType.Activated);
            actAbilityMock.SetupGet(a => a.AbilityKeywords).Returns(new List<AbilityKeywordConfig>
            {
                new AbilityKeywordConfig { Keyword = AbilityKeyword.Act }
            });
            var actAbility = actAbilityMock.Object;

            var gameState = new GameState("act-test-2", player1, player2, p1Id)
            {
                AbilityManager = abilityManager.Object,
                BuffManager = Mock.Of<IBuffManager>(),
                Phase = GamePhase.Main,
                CurrentPlayerId = p2Id
            };

            var card = new CardInstance(Guid.NewGuid(), 1001, p1Id) { CurrentZone = CardZone.Field };
            player1.Field.Add(card);
            gameState.RegisterCardInstance(card);

            abilityManager.Setup(m => m.GetAbilitiesForCard(card.InstanceId)).Returns(new List<IAbility> { actAbility });

            GameServiceProvider.ContextAccessor = accessor;
            using var ctx = new GameContext(sp, mapper, accessor, gameState, p2Id);

            var result = new ActRequestedEvent(p2Id, new ActActionDto { CardUid = card.InstanceId }).Commit(ctx);
            Assert.False(result.Success);
        }
    }
}
