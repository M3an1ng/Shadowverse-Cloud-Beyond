using System;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.Enums;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class StatChangedEventAnnouncementVisibilityTests
    {
        [Fact]
        public void GetAnnouncement_ReturnsEmpty_WhenTargetIsNotOnField()
        {
            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("stat-announcement-visibility-hand", player1, player2, player1.PlayerId)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            var target = new CardInstance(Guid.NewGuid(), 1001, player1.PlayerId)
            {
                CurrentZone = CardZone.Hand
            };
            player1.Hand.Add(target);
            gameState.RegisterCardInstance(target);

            var gameEvent = new StatChangedEvent(target, attackModifier: 1, healthModifier: null, costModifier: null);

            var announcement = gameEvent.GetAnnouncement(gameState);

            Assert.Equal(string.Empty, announcement);
        }

        [Fact]
        public void GetAnnouncement_ReturnsContent_WhenTargetIsOnField()
        {
            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("stat-announcement-visibility-field", player1, player2, player1.PlayerId)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            var target = new CardInstance(Guid.NewGuid(), 1001, player1.PlayerId)
            {
                CurrentZone = CardZone.Field
            };
            player1.Field.Add(target);
            gameState.RegisterCardInstance(target);

            var gameEvent = new StatChangedEvent(target, attackModifier: 1, healthModifier: null, costModifier: null);

            var announcement = gameEvent.GetAnnouncement(gameState);

            Assert.Contains("属性变化", announcement);
        }
    }
}
