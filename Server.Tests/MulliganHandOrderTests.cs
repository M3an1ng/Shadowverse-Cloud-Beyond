using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class MulliganHandOrderTests
    {
        [Fact]
        public void PerformMulligan_PreservesOriginalHandSlots_WhenReplacingSeparatedCards()
        {
            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("mulligan-hand-order", player, opponent, player.PlayerId);

            var left = CreateCard(1001, player.PlayerId, CardZone.Hand);
            var middle = CreateCard(1002, player.PlayerId, CardZone.Hand);
            var right = CreateCard(1003, player.PlayerId, CardZone.Hand);
            player.Hand.AddRange(new[] { left, middle, right });

            var result = player.PerformMulligan(new List<Guid> { left.InstanceId, right.InstanceId }, gameState);

            Assert.True(result.Success);
            Assert.Equal(3, player.Hand.Count);
            Assert.Equal(middle.InstanceId, player.Hand[1].InstanceId);
            Assert.Collection(
                player.Hand,
                card => Assert.Equal(0, card.ZoneIndex),
                card => Assert.Equal(1, card.ZoneIndex),
                card => Assert.Equal(2, card.ZoneIndex));
        }

        private static CardInstance CreateCard(int definitionId, Guid ownerPlayerId, CardZone zone)
        {
            return new CardInstance(Guid.NewGuid(), definitionId, ownerPlayerId)
            {
                CurrentZone = zone
            };
        }
    }
}
