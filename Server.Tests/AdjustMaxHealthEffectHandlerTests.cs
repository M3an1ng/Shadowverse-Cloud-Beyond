using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Effects;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class AdjustMaxHealthEffectHandlerTests
    {
        [Fact]
        public void EffectHandlerRegistry_Registers_AdjustMaxHealth()
        {
            var registry = new EffectHandlerRegistry(NullLogger<EffectHandlerRegistry>.Instance);

            Assert.True(registry.TryGetHandler(EffectType.AdjustMaxHealth, out var registration));
            Assert.NotNull(registration);
            Assert.Equal(typeof(ModifyMaxHealthEffectHandler), registration?.handlerType);
        }
    }
}
