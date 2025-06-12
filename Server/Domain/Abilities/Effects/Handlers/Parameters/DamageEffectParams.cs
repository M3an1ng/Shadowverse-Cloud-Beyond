namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Strongly-typed parameters for the Damage effect. Inherits common parameters from BaseEffectParams.
    /// </summary>
    public class DamageEffectParams : BaseEffectParams // Inherit from BaseEffectParams
    {
        /// <summary>
        /// The amount of damage to deal.
        /// </summary>
        public int DamageAmount { get; set; }

        // Future potential parameters:
        // public DamageType DamageType { get; set; } = DamageType.Default;
        // public bool IgnoreWard { get; set; } = false;
    }

    // Optional: Define DamageType enum if needed later
    // public enum DamageType { Default, Effect, Combat }
}