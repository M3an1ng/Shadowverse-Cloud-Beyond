using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("重置攻击次数", "重置目标随从本回合已攻击次数。使用卡牌目标。", AbilityEditorCategories.TempStateId)]
    public class ResetAttackTimesEffectParams : BaseEffectParams
    {
    }
}
