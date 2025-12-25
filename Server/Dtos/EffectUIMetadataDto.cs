using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos
{
    /// <summary>
    /// DTO for transferring the complete UI metadata for a single effect type.
    /// </summary>
    public class EffectUIMetadataDto
    {
        public string EffectTypeName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CategoryId { get; set; } = string.Empty;
        public string CategoryLabel { get; set; } = string.Empty;
        public List<EffectParameterMetadataDto> Parameters { get; set; } = new();
    }
}
