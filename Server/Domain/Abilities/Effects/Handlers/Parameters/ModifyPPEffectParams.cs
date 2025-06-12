namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters // Corrected namespace
{
    /// <summary>
    /// Parameters for the ModifyPP effect.
    /// </summary>
    public class ModifyPPEffectParams : BaseEffectParams // Corrected base class
    {
        /// <summary>
        /// The amount to modify the player's current Play Points by (can be positive or negative).
        /// </summary>
        public int ModifyAmount { get; set; }
    }
}