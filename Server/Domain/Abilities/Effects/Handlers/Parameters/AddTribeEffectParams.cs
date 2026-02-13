using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("添加种类", "为目标附加增加新的种类。", AbilityEditorCategories.TempStateId)]
    public class AddTribeEffectParams : BaseEffectParams
    {
        [ParameterMetadata("种类", UiControlType.TextInput, DefaultValue = "", Description = "要添加的种类名称。多个种类可使用逗号分隔。")]
        public string Tribe { get; set; } = string.Empty;

        [ParameterMetadata("持续时间", UiControlType.Dropdown, DefaultValue = DurationType.Permanent, Description = "种类Buff的持续时间。")]
        public DurationType DurationType { get; set; } = DurationType.Permanent;

        [ParameterMetadata("持续参数", UiControlType.NumberStepper, DefaultValue = 0, Min = 0, Description = "持续时间参数（回合开始/结束时生效）。0表示默认一次。")]
        public int DurationParameter { get; set; } = 0;

        [ParameterMetadata("可叠加", UiControlType.Checkbox, DefaultValue = true, Description = "是否允许同效果重复叠加。")]
        public bool CanStack { get; set; } = true;
    }
}
