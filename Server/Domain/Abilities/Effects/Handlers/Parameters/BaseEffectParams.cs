namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Base class for effect parameter objects, containing common parameters
    /// applicable to most or all effects. Inherits from EffectParams to satisfy constraints.
    /// </summary>
    public abstract class BaseEffectParams : EffectParams // Added inheritance
    {
        /// <summary>
        /// Indicates whether the application of this effect should generate
        /// a corresponding entry in the battle log. Defaults to true.
        /// This might be set to false for internal or implicit effects.
        /// </summary>
        public bool GenerateBattleLog { get; set; } = true;

        // Add other common parameters here as needed in the future, e.g.:
        // public Guid? SourceAbilityId { get; set; }
        // public bool IsTriggeredEffect { get; set; } // To differentiate direct vs triggered
    }
}
