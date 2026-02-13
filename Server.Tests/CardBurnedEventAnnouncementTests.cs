using System;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class CardBurnedEventAnnouncementTests
    {
        [Fact]
        public void CardBurnedEvent_GetAnnouncement_IncludesBurnedCardToken()
        {
            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("card-burned-announcement", player1, player2, player1.PlayerId)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            var burnedCard = new CardInstance(Guid.NewGuid(), 1, player1.PlayerId);
            var gameEvent = new CardBurnedEvent(player1, burnedCard);

            var announcement = gameEvent.GetAnnouncement(gameState);

            Assert.Contains($"[[player:{player1.PlayerId}]]", announcement);
            Assert.Contains($"[[card:{burnedCard.InstanceId}|name:", announcement);
            Assert.Contains($"owner:{burnedCard.OwnerPlayerId}]]", announcement);
        }
    }
}
