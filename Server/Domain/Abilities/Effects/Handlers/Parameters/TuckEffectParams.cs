using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("置入牌堆", "将目标卡牌洗回牌库。使用卡牌目标。", AbilityEditorCategories.PositionChangeId, AbilityEditorCategories.PositionChangeLabel)]
    public class TuckEffectParams : BaseEffectParams
    {
    }
}
