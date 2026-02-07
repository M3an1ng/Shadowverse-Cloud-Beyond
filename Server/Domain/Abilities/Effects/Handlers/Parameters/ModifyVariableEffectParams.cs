using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("修改变量", "修改玩家或卡牌的计数器/临时变量。", AbilityEditorCategories.ResourceValueId)]
    public class ModifyCounterEffectParams : BaseEffectParams
    {
        [ParameterMetadata("变量名", UiControlType.TextInput, DefaultValue = "", Description = "计数器名称（大小写不敏感）。")]
        public string CounterName { get; set; } = string.Empty;

        [ParameterMetadata("操作", UiControlType.Dropdown, DefaultValue = CounterOperation.Add, Description = "计数器操作类型。")]
        public CounterOperation Operation { get; set; } = CounterOperation.Add;

        [ParameterMetadata("目标类型", UiControlType.Dropdown, DefaultValue = TargetType.Player, Description = "选择修改玩家计数器还是卡牌计数器。")]
        public TargetType TargetType { get; set; } = TargetType.Player;

        [ParameterMetadata("数值", UiControlType.NumberStepper, DefaultValue = 1, Description = "增加/设置/消耗的数值。")]
        public int Amount { get; set; } = 1;

        [ParameterMetadata("玩家范围", UiControlType.Dropdown, DefaultValue = PlayerScope.Self, Description = "当未选择卡牌目标时，按玩家范围结算。")]
        public PlayerScope PlayerScope { get; set; } = PlayerScope.Self;
    }
}
