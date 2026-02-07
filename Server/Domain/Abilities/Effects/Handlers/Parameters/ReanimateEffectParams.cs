using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("亡者召还", "从已被破坏的随从中召唤费用不高于指定值的随从。", AbilityEditorCategories.PositionChangeId)]
    public class ReanimateEffectParams : BaseEffectParams
    {
        [ParameterMetadata("玩家范围", UiControlType.Dropdown, DefaultValue = PlayerScope.Self, Description = "选择受到影响的玩家（自己/对手/双方）。")]
        public PlayerScope PlayerScope { get; set; } = PlayerScope.Self;

        [ParameterMetadata("费用上限", UiControlType.NumberStepper, DefaultValue = 1, Min = 0, Description = "可召还的随从费用上限。")]
        public int MaxCost { get; set; } = 1;
    }
}
