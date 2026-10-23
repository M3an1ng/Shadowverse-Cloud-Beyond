using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class TriggerCountLimitTests
    {
        [Fact]
        public void PerTurnLimit_PreventsAdditionalTriggersInSameTurn()
        {
            var trigger = CreateTrigger(new TriggerCountLimitConfig { MaxPerTurn = 1 });
            var gameEvent = CreateTurnStartEvent();

            Assert.True(trigger.CanTrigger(gameEvent, null));
            trigger.RecordTrigger();
            trigger.ResetEventFlags();

            Assert.False(trigger.CanTrigger(gameEvent, null));

            trigger.ResetTurnCounters();
            trigger.ResetEventFlags();

            Assert.True(trigger.CanTrigger(gameEvent, null));
        }

        [Fact]
        public void PerGameLimit_PreventsAdditionalTriggersAcrossTurns()
        {
            var trigger = CreateTrigger(new TriggerCountLimitConfig { MaxPerGame = 2 });
            var gameEvent = CreateTurnStartEvent();

            Assert.True(TriggerAndReset(trigger, gameEvent));
            Assert.True(TriggerAndReset(trigger, gameEvent));

            Assert.False(trigger.CanTrigger(gameEvent, null));
        }

        [Fact]
        public void ZeroOrNegativeLimits_AreUnlimited()
        {
            var trigger = CreateTrigger(new TriggerCountLimitConfig { MaxPerTurn = 0, MaxPerGame = -1 });
            var gameEvent = CreateTurnStartEvent();

            for (var i = 0; i < 3; i++)
            {
                Assert.True(trigger.CanTrigger(gameEvent, null));
                trigger.RecordTrigger();
                trigger.ResetEventFlags();
            }
        }

        private static Trigger CreateTrigger(TriggerCountLimitConfig? limit)
        {
            var config = new TriggerConfig
            {
                EventTypes = new List<EventType> { EventType.TurnStart },
                RelationType = TriggerRelationType.Any,
                CountLimit = limit
            };

            return new Trigger(config);
        }

        private static TurnStartEvent CreateTurnStartEvent()
        {
            var player = new PlayerState(Guid.NewGuid());
            return new TurnStartEvent(player);
        }

        private static bool TriggerAndReset(Trigger trigger, IGameEvent gameEvent)
        {
            if (!trigger.CanTrigger(gameEvent, null))
            {
                return false;
            }

            trigger.RecordTrigger();
            trigger.ResetEventFlags();
            return true;
        }
    }
}
