using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("重置启动次数", "重置目标战场卡牌本回合已启动次数。使用卡牌目标。", AbilityEditorCategories.TempStateId)]
    public class ResetActTimesEffectParams : BaseEffectParams
    {
    }
}
