using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("调整能力关键字", "根据选择的调整模式变更目标卡牌的能力关键字数值，如启动费用或增强费用。使用卡牌目标。", AbilityEditorCategories.TempStateId)]
    public class ModifyAbilityKeywordEffectParams : BaseEffectParams
    {
        [ParameterMetadata("持续时间", UiControlType.Dropdown, DefaultValue = DurationType.Permanent, Description = "修改能力关键字数值的持续时间。")]
        public DurationType DurationType { get; set; } = DurationType.Permanent;

        [ParameterMetadata("持续参数", UiControlType.NumberStepper, DefaultValue = 0, Min = 0, Description = "持续时间参数（回合开始/结束时生效）。0表示默认一次。")]
        public int DurationParameter { get; set; } = 0;

        [ParameterMetadata("移除条件", UiControlType.TextArea, DefaultValue = null, Description = "当持续时间为条件移除时，使用该 ConditionConfig 判断是否移除。")]
        public ConditionConfig? RemoveCondition { get; set; }

        [ParameterMetadata("能力关键字", UiControlType.Dropdown, DefaultValue = AbilityKeyword.Act, Description = "要调整数值的能力关键字，如启动或增强。")]
        public AbilityKeyword Keyword { get; set; } = AbilityKeyword.Act;

        [ParameterMetadata("数值修正", UiControlType.NumberStepper, DefaultValue = null, Description = "修改能力关键字数值。Additive/Set/Base 模式使用数值；Multiplicative 模式使用百分比（50 表示 0.5 倍，150 表示 1.5 倍）。留空表示不修改。")]
        public int? ValueModifier { get; set; }

        [ParameterMetadata("调整类型", UiControlType.Dropdown, DefaultValue = ModifierLayer.Additive, Description = "能力关键字数值调整的类型（基础/修正/设置/倍乘）。")]
        public ModifierLayer ModifierLayer { get; set; } = ModifierLayer.Additive;

        public bool CanStack { get; set; } = true;
    }
}
