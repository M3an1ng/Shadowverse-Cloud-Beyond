namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters // Corrected namespace
{
    /// <summary>
    /// Parameters for the ModifyEP effect.
    /// </summary>
    public class ModifyEPEffectParams : BaseEffectParams // Corrected base class
    {
        /// <summary>
        /// The amount to modify the player's current Evolution Points by (can be positive or negative).
        /// </summary>
        public int ModifyAmount { get; set; }
    }
}