// File: Server/Domain/Abilities/Effects/Handlers/Parameters/ModifyStatsEffectParams.cs
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Parameters for the ModifyStats effect, used for simple stat additions/subtractions.
    /// HealthModifier affects MaxHealth, and CurrentHealth is adjusted accordingly.
    /// </summary>
    [EffectMetadata("调整属性", "根据选择的调整模式变更目标的攻击力、生命值或费用等属性。使用卡牌目标。")]
    public class ModifyStatsEffectParams : BaseEffectParams
    {
        [ParameterMetadata("持续时间", UiControlType.Dropdown, DefaultValue = DurationType.Permanent, Description = "修改属性的持续时间。")]
        public DurationType DurationType { get; set; } = DurationType.Permanent;

        [ParameterMetadata("持续参数", UiControlType.NumberStepper, DefaultValue = 0, Min = 0, Description = "持续时间参数（回合开始/结束时生效）。0表示默认一次。")]
        public int DurationParameter { get; set; } = 0;

        /// <summary>
        /// The amount to add to the target's Attack. Can be negative.
        /// Null or 0 means no change to Attack.
        /// </summary>
        [ParameterMetadata("攻击力修正", UiControlType.NumberStepper, DefaultValue = null, Description = "修改目标攻击力的数值。可以为正数（增加）或负数（减少）。留空表示不修改攻击力。")]
        public int? AttackModifier { get; set; }

        /// <summary>
        /// The amount to add to the target's MaxHealth. Can be negative.
        /// CurrentHealth will be adjusted by the same amount, maintaining the proportion or being capped.
        /// Null or 0 means no change to Health.
        /// </summary>
        [ParameterMetadata("生命值修正", UiControlType.NumberStepper, DefaultValue = null, Description = "修改目标生命值的数值。可以为正数（增加）或负数（减少）。留空表示不修改生命值。")]
        public int? HealthModifier { get; set; }

        /// <summary>
        /// The amount to add to the target's Cost. Can be negative.
        /// Null or 0 means no change to Cost.
        /// </summary>
        [ParameterMetadata("费用修正", UiControlType.NumberStepper, DefaultValue = null, Description = "修改目标费用的数值。可以为正数（增加）或负数（减少）。留空表示不修改费用。")]
        public int? CostModifier { get; set; }

        [ParameterMetadata("调整类型", UiControlType.Dropdown, DefaultValue = ModifierLayer.Additive, Description = "属性调整的类型（基础/修正/设置/倍乘）。")]
        public ModifierLayer ModifierLayer { get; set; } = ModifierLayer.Additive;

        public bool CanStack { get; set; } = true;
    }
}
