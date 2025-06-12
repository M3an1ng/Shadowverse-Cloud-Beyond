namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters // Corrected namespace
{
    /// <summary>
    /// Parameters for the Evolve effect. Currently empty as the target is the primary input.
    /// </summary>
    public class EvolveEffectParams : BaseEffectParams // Corrected base class
    {
        // No specific parameters needed for the basic Evolve effect itself.
        // The target CardInstance is provided via the GameContext.
    }
}