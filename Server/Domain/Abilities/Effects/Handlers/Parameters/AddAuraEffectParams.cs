using System.Collections.Generic;
using System.Text.Json.Serialization;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.JsonConverters;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("添加光环", "注册一个可刷新的持续光环，并对目标施加持久性效果。", AbilityEditorCategories.TempStateId)]
    public class AddAuraEffectParams : BaseEffectParams
    {
        [JsonConverter(typeof(SingleTargetConfigToDictionaryConverter))]
        [ParameterMetadata("目标配置", UiControlType.TextArea, Description = "光环目标配置（TargetConfig 字典）。")]
        public Dictionary<string, TargetConfig> Targets { get; set; } = new();

        [ParameterMetadata("效果列表", UiControlType.TextArea, Description = "光环对目标施加的效果列表（EffectConfig）。")]
        public List<EffectConfig> Effects { get; set; } = new();

        [ParameterMetadata("条件列表", UiControlType.TextArea, Description = "光环生效条件列表（ConditionConfig）。")]
        public List<ConditionConfig> Conditions { get; set; } = new();

        [ParameterMetadata("持续时间", UiControlType.Dropdown, DefaultValue = DurationType.Permanent, Description = "光环产生的Buff持续时间。")]
        public DurationType DurationType { get; set; } = DurationType.Permanent;

        [ParameterMetadata("持续参数", UiControlType.NumberStepper, DefaultValue = 0, Min = 0, Description = "持续时间参数（回合开始/结束时生效）。0表示默认一次。")]
        public int DurationParameter { get; set; } = 0;

        [ParameterMetadata("移除条件", UiControlType.TextArea, DefaultValue = null, Description = "当持续时间为条件移除时，使用该 ConditionConfig 判断是否移除。")]
        public ConditionConfig? RemoveCondition { get; set; }

        [ParameterMetadata("可叠加", UiControlType.Checkbox, DefaultValue = false, Description = "是否允许同一光环在同一目标上重复叠加。")]
        public bool CanStack { get; set; } = false;
    }
}
