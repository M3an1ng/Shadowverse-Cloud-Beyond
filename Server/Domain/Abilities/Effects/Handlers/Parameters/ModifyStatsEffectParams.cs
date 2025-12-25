// File: Server/Domain/Abilities/Effects/Handlers/Parameters/ModifyStatsEffectParams.cs
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Parameters for the ModifyStats effect, used for simple stat additions/subtractions.
    /// HealthModifier affects MaxHealth, and CurrentHealth is adjusted accordingly.
    /// </summary>
    [EffectMetadata("修改属性", "修改目标的攻击力、生命值或费用等属性。")]
    public class ModifyStatsEffectParams : BaseEffectParams
    {
        /// <summary>
        /// The amount to add to the target's Attack. Can be negative.
        /// Null or 0 means no change to Attack.
        /// </summary>
        [ParameterMetadata("攻击力修正", UiControlType.ValueConfigEditor, DefaultValue = null, Description = "修改目标攻击力的数值。可以为正数（增加）或负数（减少）。留空表示不修改攻击力。")]
        public int? AttackModifier { get; set; }

        /// <summary>
        /// The amount to add to the target's MaxHealth. Can be negative.
        /// CurrentHealth will be adjusted by the same amount, maintaining the proportion or being capped.
        /// Null or 0 means no change to Health.
        /// </summary>
        [ParameterMetadata("生命值修正", UiControlType.ValueConfigEditor, DefaultValue = null, Description = "修改目标生命值的数值。可以为正数（增加）或负数（减少）。留空表示不修改生命值。")]
        public int? HealthModifier { get; set; }

        /// <summary>
        /// The amount to add to the target's Cost. Can be negative.
        /// Null or 0 means no change to Cost.
        /// </summary>
        [ParameterMetadata("费用修正", UiControlType.ValueConfigEditor, DefaultValue = null, Description = "修改目标费用的数值。可以为正数（增加）或负数（减少）。留空表示不修改费用。")]
        public int? CostModifier { get; set; }
    }
}