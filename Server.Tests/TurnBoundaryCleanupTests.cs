using System;
using System.Linq;
using Moq;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class TurnBoundaryCleanupTests
    {
        [Fact]
        public void CollectEndCleanupEvents_ResetsComboWithCounterChangedEvent()
        {
            var player = new PlayerState(Guid.NewGuid()) { Combo = 4 };
            var cleanup = new TurnBoundaryCleanupService(Mock.Of<IBuffService>());

            var events = cleanup.CollectEndOfTurnCleanupEvents(null!, player);

            Assert.Equal(0, player.Combo);
            var counterEvent = Assert.Single(events.OfType<CounterChangedEvent>());
            Assert.Equal(PlayerState.ComboCounterKey, counterEvent.CounterName);
            Assert.Equal(4, counterEvent.OldValue);
            Assert.Equal(0, counterEvent.NewValue);
        }

        [Fact]
        public void CollectEndCleanupEvents_ResetsPpBoostArmedWithEvent()
        {
            var player = new PlayerState(Guid.NewGuid()) { IsPpBoostArmed = true };
            var cleanup = new TurnBoundaryCleanupService(Mock.Of<IBuffService>());

            var events = cleanup.CollectEndOfTurnCleanupEvents(null!, player);

            Assert.False(player.IsPpBoostArmed);
            Assert.Single(events.OfType<PpBoostStateUpdatedEvent>());
        }

        [Fact]
        public void CollectEndCleanupEvents_PreservesPersistentCounters()
        {
            var player = new PlayerState(Guid.NewGuid());
            player.SetCounter(PlayerState.GraveyardCounterKey, 7);
            player.SetCounter(PlayerState.BloodPoolCounterKey, 5);
            player.SetCounter(PlayerState.RallyCounterKey, 3);
            var cleanup = new TurnBoundaryCleanupService(Mock.Of<IBuffService>());

            cleanup.CollectEndOfTurnCleanupEvents(null!, player);

            Assert.Equal(7, player.GetCounter(PlayerState.GraveyardCounterKey));
            Assert.Equal(5, player.GetCounter(PlayerState.BloodPoolCounterKey));
            Assert.Equal(3, player.GetCounter(PlayerState.RallyCounterKey));
        }
    }
}
