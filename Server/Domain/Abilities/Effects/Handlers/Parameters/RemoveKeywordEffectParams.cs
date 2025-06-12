// File: Server/Domain/Abilities/Effects/Handlers/Parameters/RemoveKeywordEffectParams.cs
using ShadowverseBeyond.Server.Domain.Constants; // For CardKeyword enum

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Parameters for the RemoveKeyword effect.
    /// </summary>
    public class RemoveKeywordEffectParams : BaseTargetEffectParams // Target is usually one or more CardInstances
    {
        /// <summary>
        /// The keyword to remove from the target(s).
        /// This value is typically resolved from the effect configuration.
        /// </summary>
        public CardKeyword Keyword { get; set; }

        // Potential future parameters:
        // public List<CardKeyword> Keywords { get; set; } // To remove multiple keywords at once
        // public bool RemoveAll { get; set; } // To remove all keywords
    }
}