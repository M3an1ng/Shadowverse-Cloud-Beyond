using System;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class EventParameterCacheTests
    {
        [Fact]
        public void EventParameterCache_ResolvesDisplayName_WithSetter()
        {
            var playerId = Guid.NewGuid();
            var target = new CardInstance(Guid.NewGuid(), 1, playerId);
            var source = new CardInstance(Guid.NewGuid(), 2, playerId);
            var damageEvent = new DamageEvent(target, 1, source);

            Assert.True(EventParameterCache.TryGetRewritableBinding(typeof(DamageEvent), "伤害值", out var binding, out var error));
            Assert.NotNull(binding.Setter);

            binding.Setter!(damageEvent, 5);
            Assert.Equal(5, damageEvent.Damage);
            Assert.Null(error);
        }
    }
}
