using System;

namespace ShadowverseBeyond.Server.Dtos
{
    /// <summary>
    /// Summary info for a buff grouped by EffectKey for UI display.
    /// </summary>
    public record BuffSummaryDto
    {
        public string EffectKey { get; init; } = string.Empty;
        public string SourceCardName { get; init; } = string.Empty;
        public string SourceAbilityName { get; init; } = string.Empty;
    }
}
