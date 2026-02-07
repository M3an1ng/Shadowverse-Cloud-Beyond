using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("洗回牌堆", "将手牌目标洗回牌库并洗牌。使用卡牌目标。", AbilityEditorCategories.PositionChangeId, AbilityEditorCategories.PositionChangeLabel)]
    public class ShuffleEffectParams : BaseEffectParams
    {
    }
}
