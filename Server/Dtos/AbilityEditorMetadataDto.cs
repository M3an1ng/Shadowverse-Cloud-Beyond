using ShadowverseBeyond.Server.Dtos.Dsl;
using ShadowverseBeyond.Server.Dtos.Query;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos
{
    /// <summary>
    /// A top-level DTO that aggregates all metadata required by the Ability Editor UI.
    /// </summary>
    public class AbilityEditorMetadataDto
    {
        /// <summary>
        /// Metadata for all available effects and their parameters.
        /// </summary>
        public IEnumerable<EffectUIMetadataDto> Effects { get; set; } = new List<EffectUIMetadataDto>();

        /// <summary>
        /// Metadata for all available game events and their parameters.
        /// </summary>
        public IEnumerable<EventMetadataDto> Events { get; set; } = new List<EventMetadataDto>();

        /// <summary>
        /// Metadata for the Domain Specific Language (DSL), including functions and context variables.
        /// </summary>
        public DslMetadataDto Dsl { get; set; } = new DslMetadataDto();

        /// <summary>
        /// Metadata for all available query operations used in target selectors.
        /// </summary>
        public IEnumerable<QueryOperationMetadataDto> QueryOperations { get; set; } = new List<QueryOperationMetadataDto>();

        /// <summary>
        /// Metadata for trigger relation types.
        /// </summary>
        public IEnumerable<EnumMetadataDto> RelationTypes { get; set; } = new List<EnumMetadataDto>();

        /// <summary>
        /// Enum option sources referenced by effect UI metadata (for Dropdown controls).
        /// The frontend can use these identifiers to fetch localized options via `/api/localization/enums/{enumName}`.
        /// </summary>
        public IEnumerable<string> EnumOptionSources { get; set; } = new List<string>();

        /// <summary>
        /// Non-enum option source keys referenced by effect UI metadata (for Dropdown controls).
        /// </summary>
        public IEnumerable<string> OptionsProviderKeys { get; set; } = new List<string>();
    }
}
