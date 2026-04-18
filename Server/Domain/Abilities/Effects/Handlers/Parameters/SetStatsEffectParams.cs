using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Parameters for the SetStats effect, used for setting stat values directly.
    /// </summary>
    public class SetStatsEffectParams : BaseEffectParams
    {
        [ParameterMetadata("持续时间", UiControlType.Dropdown, DefaultValue = DurationType.Permanent, Description = "设置属性的持续时间。")]
        public DurationType DurationType { get; set; } = DurationType.Permanent;

        [ParameterMetadata("持续参数", UiControlType.NumberStepper, DefaultValue = 0, Min = 0, Description = "持续时间参数（回合开始/结束时生效）。0表示默认一次。")]
        public int DurationParameter { get; set; } = 0;

        [ParameterMetadata("移除条件", UiControlType.TextArea, DefaultValue = null, Description = "当持续时间为条件移除时，使用该 ConditionConfig 判断是否移除。")]
        public ConditionConfig? RemoveCondition { get; set; }

        [ParameterMetadata("攻击力设定值", UiControlType.NumberStepper, DefaultValue = null, Description = "将目标攻击力设定为指定值。留空表示不修改攻击力。")]
        public int? AttackModifier { get; set; }

        [ParameterMetadata("生命值设定值", UiControlType.NumberStepper, DefaultValue = null, Description = "将目标生命值设定为指定值。留空表示不修改生命值。")]
        public int? HealthModifier { get; set; }

        [ParameterMetadata("费用设定值", UiControlType.NumberStepper, DefaultValue = null, Description = "将目标费用设定为指定值。留空表示不修改费用。")]
        public int? CostModifier { get; set; }

        public bool CanStack { get; set; } = true;
    }
}
