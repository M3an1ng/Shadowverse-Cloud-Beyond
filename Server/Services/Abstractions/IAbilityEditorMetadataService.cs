using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Services.Abstractions
{
    /// <summary>
    /// A top-level service that aggregates metadata from various subsystems
    /// to provide a single point of data for the Ability Editor UI.
    /// </summary>
    public interface IAbilityEditorMetadataService
    {
        /// <summary>
        /// Gets the aggregated metadata for the entire Ability Editor.
        /// </summary>
        /// <returns>A DTO containing all necessary metadata for the UI.</returns>
        AbilityEditorMetadataDto GetAbilityEditorMetadata();
    }
}