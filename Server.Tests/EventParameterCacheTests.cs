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

        [Fact]
        public void EventParameterCache_KeepsHiddenBaseParametersAvailableForRuntime()
        {
            Assert.True(
                EventParameterCache.TryGetBinding(typeof(DamageEvent), nameof(GameEventBase.CurrentPlayerId), out var binding));

            Assert.Equal(nameof(GameEventBase.CurrentPlayerId), binding.PropertyName);
            Assert.False(binding.ShowInTriggerTips);
            Assert.Equal(GameEventParameterScope.Output, binding.Scope);
        }

        [Fact]
        public void EventParameterCache_Exposes_CounterChanged_Computed_TargetAndSource()
        {
            var player = new PlayerState(Guid.NewGuid());
            var card = new CardInstance(Guid.NewGuid(), 1, player.PlayerId);
            var playerCounterEvent = new CounterChangedEvent(player, "combo", 0, 1);
            var cardCounterEvent = new CounterChangedEvent(card, "spellboost", 1, 2);

            Assert.True(EventParameterCache.TryGetBinding(typeof(CounterChangedEvent), "SourcePlayer", out var sourceBinding));
            Assert.True(EventParameterCache.TryGetBinding(typeof(CounterChangedEvent), "TargetCard", out var targetBinding));

            Assert.Equal(player, sourceBinding.Getter(playerCounterEvent));
            Assert.Equal(card, targetBinding.Getter(cardCounterEvent));
            Assert.Equal(GameEventParameterScope.Output, sourceBinding.Scope);
            Assert.Equal(GameEventParameterScope.Output, targetBinding.Scope);
        }
    }
}
