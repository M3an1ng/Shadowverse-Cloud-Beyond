using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("血祭", "消耗血池计数，不足时返回失败以触发后续处理。")]
    public class SacrificeEffectParams : BaseEffectParams
    {
        [ParameterMetadata("玩家范围", UiControlType.Dropdown, DefaultValue = PlayerScope.Self, Description = "选择受到影响的玩家（自己/对手/双方）。")]
        public PlayerScope PlayerScope { get; set; } = PlayerScope.Self;

        [ParameterMetadata("消耗数量", UiControlType.NumberStepper, DefaultValue = 1, Min = 1, Description = "要消耗的血池计数。")]
        public int Amount { get; set; } = 1;
    }
}
