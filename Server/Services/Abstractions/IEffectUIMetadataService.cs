using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Dtos;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Services.Abstractions
{
    /// <summary>
    /// Provides access to the UI metadata for card effects.
    /// </summary>
    public interface IEffectUIMetadataService
    {
        /// <summary>
        /// Gets the UI metadata for a specific effect type.
        /// </summary>
        EffectUIMetadataDto? GetEffectUIMetadata(EffectType effectType);

        /// <summary>
        /// Gets all available effect UI metadata.
        /// </summary>
        IEnumerable<EffectUIMetadataDto> GetAllEffectUIMetadata();
    }
}