using System;

namespace ShadowverseBeyond.Server.Dtos
{
    /// <summary>
    /// Summary information for an ability-based card action (Act/Fusion/etc.).
    /// </summary>
    public class AbilityActionSummaryDto
    {
        public Guid RuntimeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
