namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters // Corrected namespace
{
    /// <summary>
    /// Parameters for the ModifyPPMax effect (e.g., GainPPOrb).
    /// </summary>
    public class ModifyPPMaxEffectParams : BaseEffectParams // Corrected base class
    {
        /// <summary>
        /// The amount to modify the player's maximum Play Points by (usually positive, e.g., 1 for GainPPOrb).
        /// </summary>
        public int ModifyAmount { get; set; } = 1; // Default to 1 for common GainPPOrb case
    }
}