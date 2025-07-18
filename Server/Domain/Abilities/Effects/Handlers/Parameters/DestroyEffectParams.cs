// File: Server/Domain/Abilities/Effects/Handlers/Parameters/DestroyUnitEffectParams.cs
using ShadowverseBeyond.Server.Domain.Entities; // For CardZone if needed later
using ShadowverseBeyond.Server.Domain.Constants; // For potential enums like DestroyReason

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Parameters for the DestroyUnit effect.
    /// </summary>
    public class DestroyEffectParams : BaseEffectParams
    {
        /// <summary>
        /// The reason for the destruction (e.g., "Effect", "Combat", "Ability").
        /// Useful for logging or triggering specific Last Words variations.
        /// Defaults to "Effect".
        /// </summary>
        public string DestroyReason { get; set; } = "Effect";

        // Potential future parameters:
        // public bool IgnoreWard { get; set; } = false; // If destruction targets through Ward
        // public bool IgnoreDestroyResist { get; set; } = false; // If destruction bypasses immunity
    }
}