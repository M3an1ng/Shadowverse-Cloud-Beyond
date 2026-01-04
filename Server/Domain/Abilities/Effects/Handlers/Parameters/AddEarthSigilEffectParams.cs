using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("土之印增加", "为指定玩家增加土之印计数；若没有土之印护符则召唤指定护符。")]
    public class AddEarthSigilEffectParams : BaseEffectParams
    {
        [ParameterMetadata("玩家范围", UiControlType.Dropdown, DefaultValue = PlayerScope.Self, Description = "选择受到影响的玩家（自己/对手/双方）。")]
        public PlayerScope PlayerScope { get; set; } = PlayerScope.Self;

        [ParameterMetadata("增加数量", UiControlType.ValueConfigEditor, DefaultValue = 1, Min = 1, Description = "要增加的土之印计数。")]
        public int Amount { get; set; } = 1;

        [ParameterMetadata("土之印护符ID", UiControlType.NumberStepper, DefaultValue = 0, Min = 0, Description = "当场上没有土之印护符时召唤的卡牌定义ID。")]
        public int TokenDefinitionId { get; set; }
    }
}
