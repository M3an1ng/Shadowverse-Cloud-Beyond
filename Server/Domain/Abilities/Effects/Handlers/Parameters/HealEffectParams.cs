// File: Server/Domain/Abilities/Effects/Handlers/Parameters/HealEffectParams.cs
namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Parameters for the Heal effect.
    /// </summary>
    public class HealEffectParams : BaseEffectParams // Target can be CardInstance or PlayerState
    {
        /// <summary>
        /// The amount of health to restore.
        /// Can be a static value or resolved from a DSL expression.
        /// Must be a positive value.
        /// </summary>
        public int HealAmount { get; set; }

        // Potential future parameters:
        // public bool OverhealAllowed { get; set; } = false; // If healing can exceed max health (e.g., for specific effects)
        // public string HealSourceType { get; set; } = "Ability"; // e.g., "Spell", "Amulet"
    }
}