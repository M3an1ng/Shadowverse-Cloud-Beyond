using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("无效果", "占位符效果，不执行任何操作。", AbilityEditorCategories.SpecialId)]
    public class NoneEffectParams : BaseEffectParams
    {
    }
}
