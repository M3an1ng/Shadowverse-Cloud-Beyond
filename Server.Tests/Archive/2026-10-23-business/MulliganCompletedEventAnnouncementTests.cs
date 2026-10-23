using System;
using System.Collections.Generic;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class MulliganCompletedEventAnnouncementTests
    {
        [Fact]
        public void GetAnnouncement_IncludesPlayerAndReplacedCount_WhenCardsWereReplaced()
        {
            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("mulligan-announcement-replaced", player1, player2, player1.PlayerId)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            var replaced = new CardInstance(Guid.NewGuid(), 1001, player1.PlayerId);
            var gameEvent = new MulliganCompletedEvent(player1, new List<CardInstance> { replaced });

            var announcement = gameEvent.GetAnnouncement(gameState);

            Assert.Contains($"[[player:{player1.PlayerId}]]", announcement);
            Assert.Contains("[[num:1]]", announcement);
            Assert.Contains("completed mulligan", announcement);
        }

        [Fact]
        public void GetAnnouncement_UsesKeepAllText_WhenNoCardsWereReplaced()
        {
            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("mulligan-announcement-keep-all", player1, player2, player1.PlayerId)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            var gameEvent = new MulliganCompletedEvent(player1, new List<CardInstance>());

            var announcement = gameEvent.GetAnnouncement(gameState);

            Assert.Contains($"[[player:{player1.PlayerId}]]", announcement);
            Assert.Contains("kept all opening hand cards", announcement);
            Assert.DoesNotContain("[[num:", announcement);
        }
    }
}
