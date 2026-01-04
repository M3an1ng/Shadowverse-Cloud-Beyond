using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("死灵术", "消耗墓场计数，不足时返回失败以触发 OnFailure 处理。")]
    public class NecromancyEffectParams : BaseEffectParams
    {
        [ParameterMetadata("玩家范围", UiControlType.Dropdown, DefaultValue = PlayerScope.Self, Description = "选择受到影响的玩家（自己/对手/双方）。")]
        public PlayerScope PlayerScope { get; set; } = PlayerScope.Self;

        [ParameterMetadata("消耗数量", UiControlType.ValueConfigEditor, DefaultValue = 1, Min = 1, Description = "要消耗的墓场计数。")]
        public int Amount { get; set; } = 1;
    }
}
