using System;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class ActInvokedEventAnnouncementTests
    {
        [Fact]
        public void GetAnnouncement_IncludesPlayerAndCardTokens()
        {
            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("act-invoked-announcement", player1, player2, player1.PlayerId)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            var actCard = new CardInstance(Guid.NewGuid(), 1001, player1.PlayerId);
            var gameEvent = new ActInvokedEvent(player1, actCard, paidCost: 1);

            var announcement = gameEvent.GetAnnouncement(gameState);

            Assert.Contains($"[[player:{player1.PlayerId}]]", announcement);
            Assert.Contains("启动", announcement);
            Assert.Contains($"[[card:{actCard.InstanceId}|name:", announcement);
            Assert.Contains($"owner:{actCard.OwnerPlayerId}]]", announcement);
        }
    }
}
