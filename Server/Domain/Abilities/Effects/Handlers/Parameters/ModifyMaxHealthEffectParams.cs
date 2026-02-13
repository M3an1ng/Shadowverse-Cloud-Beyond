using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Parameters for the AdjustMaxHealth effect.
    /// This effect changes max health without increasing current health.
    /// </summary>
    [EffectMetadata("调整生命上限", "仅修改目标最大生命值，不会抬高当前生命值；若新上限低于当前生命值，则当前生命值被截断。", AbilityEditorCategories.ResourceValueId)]
    public class ModifyMaxHealthEffectParams : BaseEffectParams
    {
        [ParameterMetadata("持续时间", UiControlType.Dropdown, DefaultValue = DurationType.Permanent, Description = "修改最大生命值的持续时间。")]
        public DurationType DurationType { get; set; } = DurationType.Permanent;

        [ParameterMetadata("持续参数", UiControlType.NumberStepper, DefaultValue = 0, Min = 0, Description = "持续时间参数（回合开始/结束时生效）。0表示默认一次。")]
        public int DurationParameter { get; set; } = 0;

        [ParameterMetadata("生命值修正", UiControlType.NumberStepper, DefaultValue = null, Description = "用于调整最大生命值。Additive/Set/Base 模式使用数值；Multiplicative 模式使用百分比（50 表示 0.5 倍，150 表示 1.5 倍）。")]
        public int? HealthModifier { get; set; }

        [ParameterMetadata("调整类型", UiControlType.Dropdown, DefaultValue = ModifierLayer.Set, Description = "最大生命值调整方式（基础/修正/设置/倍乘）。")]
        public ModifierLayer ModifierLayer { get; set; } = ModifierLayer.Set;

        public bool CanStack { get; set; } = true;
    }
}
