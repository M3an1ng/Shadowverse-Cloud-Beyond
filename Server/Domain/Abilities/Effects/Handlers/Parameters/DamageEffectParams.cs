using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Strongly-typed parameters for the Damage effect. Inherits common parameters from BaseEffectParams.
    /// </summary>
    [EffectMetadata("伤害", "对一个或多个目标造成伤害。使用卡牌目标。")]
    public class DamageEffectParams : BaseEffectParams // Inherit from BaseEffectParams
    {
        /// <summary>
        /// The amount of damage to deal.
        /// </summary>
        [ParameterMetadata("伤害值", UiControlType.ValueConfigEditor, DefaultValue = 1, Min = 0, Description = "配置要造成的伤害量。可以是固定数值，也可以是基于游戏状态的动态公式。")]
        public int DamageAmount { get; set; }

        // Future potential parameters:
        // public DamageType DamageType { get; set; } = DamageType.Default;
        // public bool IgnoreWard { get; set; } = false;
    }

    // Optional: Define DamageType enum if needed later
    // public enum DamageType { Default, Effect, Combat }
}
