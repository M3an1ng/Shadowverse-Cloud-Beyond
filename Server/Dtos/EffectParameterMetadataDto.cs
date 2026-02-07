using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Dtos
{
    /// <summary>
    /// DTO for transferring UI metadata for a single effect parameter.
    /// </summary>
    public class EffectParameterMetadataDto
    {
        public string PropertyName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        /// <summary>
        /// Clean/normalized CLR/DSL type name (supports Nullable and generics like List&lt;T&gt;).
        /// </summary>
        public string TypeName { get; set; } = string.Empty;
        public UiControlType ControlType { get; set; }
        /// <summary>
        /// When ControlType is Dropdown and the parameter is an enum, this provides the enum name for option lookup.
        /// </summary>
        public string? EnumName { get; set; }
        /// <summary>
        /// When ControlType is Dropdown and options are not enum-backed, this identifies an option provider on the backend.
        /// </summary>
        public string? OptionsProviderKey { get; set; }
        public object? DefaultValue { get; set; }
        public string Description { get; set; } = string.Empty;
        public double Min { get; set; }
        public double Max { get; set; }
        public bool AllowDsl { get; set; } = true;
        public string? DslType { get; set; }
        public DslMode DslMode { get; set; } = DslMode.Optional;
    }
}
