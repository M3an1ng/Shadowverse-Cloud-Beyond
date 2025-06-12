namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters // Corrected namespace
{
    /// <summary>
    /// Parameters for the Transform effect.
    /// </summary>
    public class TransformEffectParams : BaseEffectParams // Corrected base class
    {
        /// <summary>
        /// The ID of the CardDefinition the target card instance should transform into.
        /// </summary>
        public int TargetCardDefinitionId { get; set; }
    }
}