using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Parameters for the GrantAbility effect.
    /// </summary>
    [EffectMetadata("授予能力", "为目标授予配置的能力。", AbilityEditorCategories.TempStateId)]
    public class GrantAbilityEffectParams : BaseEffectParams
    {
        [ParameterMetadata("能力列表", UiControlType.AbilityListEditor, Description = "要授予的能力配置列表。")]
        public List<AbilityConfig> AbilityConfigs { get; set; } = new();

        [ParameterMetadata("持续时间", UiControlType.Dropdown, DefaultValue = DurationType.Permanent, Description = "授予能力的持续时间。")]
        public DurationType DurationType { get; set; } = DurationType.Permanent;

        [ParameterMetadata("持续参数", UiControlType.NumberStepper, DefaultValue = 0, Min = 0, Description = "持续时间的附加参数（如持续回合数）。0表示无参数。")]
        public int DurationParameter { get; set; } = 0;

        [ParameterMetadata("移除条件", UiControlType.TextArea, DefaultValue = null, Description = "当持续时间为条件移除时，使用该 ConditionConfig 判断是否移除。")]
        public ConditionConfig? RemoveCondition { get; set; }

        [ParameterMetadata("可叠加", UiControlType.Checkbox, DefaultValue = true, Description = "该效果产生的Buff是否允许与相同EffectKey叠加。")]
        public bool CanStack { get; set; } = true;
    }
}
