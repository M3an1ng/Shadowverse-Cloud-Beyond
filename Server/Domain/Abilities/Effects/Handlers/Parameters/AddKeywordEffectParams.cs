// File: Server/Domain/Abilities/Effects/Handlers/Parameters/AddKeywordEffectParams.cs
using ShadowverseBeyond.Server.Domain.Constants; // For CardKeyword enum

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Parameters for the AddKeyword effect.
    /// </summary>
    public class AddKeywordEffectParams : BaseTargetEffectParams // Target is usually one or more CardInstances
    {
        /// <summary>
        /// The keyword to add to the target(s).
        /// This value is typically resolved from the effect configuration.
        /// </summary>
        public CardKeyword Keyword { get; set; }

        // Potential future parameters:
        // public List<CardKeyword> Keywords { get; set; } // To add multiple keywords at once
        // public int Duration { get; set; } = -1; // For temporary keywords (-1 means permanent)
    }
}