using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("阻止事件", "在 Before 截取阶段阻止触发事件继续执行。", AbilityEditorCategories.SpecialId)]
    public class PreventEventEffectParams : BaseEffectParams
    {
    }
}
