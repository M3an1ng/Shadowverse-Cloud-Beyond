using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Metadata;
using ShadowverseBeyond.Server.Services;
using System.Linq;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class EffectMetadataCategoryTests
    {
        [Fact]
        public void Effect_metadata_exposes_category()
        {
            var registry = new EffectHandlerRegistry(NullLogger<EffectHandlerRegistry>.Instance);
            var svc = new EffectUIMetadataService(registry);

            var damage = svc.GetAllEffectUIMetadata().First(m => m.EffectTypeName == EffectType.Damage.ToString());

            Assert.Equal(AbilityEditorCategories.BaseActionId, damage.CategoryId);
            Assert.Equal(AbilityEditorCategories.BaseActionLabel, damage.CategoryLabel);
        }
    }
}
