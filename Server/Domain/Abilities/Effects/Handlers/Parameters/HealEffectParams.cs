// File: Server/Domain/Abilities/Effects/Handlers/Parameters/HealEffectParams.cs
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Parameters for the Heal effect.
    /// </summary>
    [EffectMetadata("治疗", "恢复一个或多个目标的生命值。使用卡牌目标。")]
    public class HealEffectParams : BaseEffectParams // Target can be CardInstance or PlayerState
    {
        /// <summary>
        /// The amount of health to restore.
        /// Can be a static value or resolved from a DSL expression.
        /// Must be a positive value.
        /// </summary>
        [ParameterMetadata("治疗量", UiControlType.ValueConfigEditor, DefaultValue = 1, Min = 1, Description = "配置要恢复的生命值。可以是固定数值，也可以是基于游戏状态的动态公式。必须为正数。")]
        public int HealAmount { get; set; }

        // Potential future parameters:
        // public bool OverhealAllowed { get; set; } = false; // If healing can exceed max health (e.g., for specific effects)
        // public string HealSourceType { get; set; } = "Ability"; // e.g., "Spell", "Amulet"
    }
}
