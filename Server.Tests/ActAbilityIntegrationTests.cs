using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
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
            var accessor = new GameContextAccessor();
            services.AddSingleton<IGameContextAccessor>(accessor);
            services.AddSingleton<IBuffService>(Mock.Of<IBuffService>());

            var sp = services.BuildServiceProvider();
            var mapper = new MapperConfiguration(_ => { }).CreateMapper();

            var p1Id = Guid.NewGuid();
            var p2Id = Guid.NewGuid();
            var player1 = new PlayerState(p1Id) { TurnNumber = 1 };
            var player2 = new PlayerState(p2Id) { TurnNumber = 1 };

            var abilityManager = new Mock<IAbilityManager>();
            var actAbility = Mock.Of<IAbility>(a => a.Type == AbilityType.Activated);
            abilityManager.Setup(m => m.GetAbilitiesForCard(It.IsAny<Guid>())).Returns(new List<IAbility>());

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

            abilityManager.Setup(m => m.GetAbilitiesForCard(card.InstanceId)).Returns(new List<IAbility> { actAbility });

            GameServiceProvider.ContextAccessor = accessor;

            using var ctx = new GameContext(sp, mapper, accessor, gameState, p1Id);

            var first = new ActRequestedEvent(p1Id, new ActActionDto { CardUid = card.InstanceId }).Commit(ctx);
            Assert.True(first.Success);
            Assert.True(card.HasUsedActThisTurn(player1.TurnNumber));
            Assert.Contains(first.GeneratedEvents, e => e is ActStateUpdatedEvent);
            Assert.Contains(first.GeneratedEvents, e => e is ActInvokedEvent);

            var second = new ActRequestedEvent(p1Id, new ActActionDto { CardUid = card.InstanceId }).Commit(ctx);
            Assert.False(second.Success);
            Assert.Empty(second.GeneratedEvents);

            // Next turn for the same player: TurnNumber increments.
            player1.TurnNumber = 2;

            var third = new ActRequestedEvent(p1Id, new ActActionDto { CardUid = card.InstanceId }).Commit(ctx);
            Assert.True(third.Success);
            Assert.True(card.HasUsedActThisTurn(player1.TurnNumber));
        }

        [Fact]
        public void Opponent_cannot_use_your_act_even_on_their_turn()
        {
            var services = new ServiceCollection();
            var accessor = new GameContextAccessor();
            services.AddSingleton<IGameContextAccessor>(accessor);
            services.AddSingleton<IBuffService>(Mock.Of<IBuffService>());

            var sp = services.BuildServiceProvider();
            var mapper = new MapperConfiguration(_ => { }).CreateMapper();

            var p1Id = Guid.NewGuid();
            var p2Id = Guid.NewGuid();
            var player1 = new PlayerState(p1Id) { TurnNumber = 1 };
            var player2 = new PlayerState(p2Id) { TurnNumber = 1 };

            var abilityManager = new Mock<IAbilityManager>();
            var actAbility = Mock.Of<IAbility>(a => a.Type == AbilityType.Activated);

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
