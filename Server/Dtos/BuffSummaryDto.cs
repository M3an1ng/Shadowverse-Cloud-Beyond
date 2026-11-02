using System;

namespace ShadowverseBeyond.Server.Dtos
{
    /// <summary>
    /// Summary info for a buff grouped by EffectKey for UI display.
    /// </summary>
    public record BuffSummaryDto
    {
        public string EffectKey { get; init; } = string.Empty;
        public int? SourceCardDefinitionId { get; init; }
        public string SourceCardName { get; init; } = string.Empty;
        public string SourceCardArtPreview { get; init; } = string.Empty;
        public string SourceAbilityName { get; init; } = string.Empty;
        public System.Collections.Generic.List<AttachedNumericEffectDto> NumericEffects { get; init; } = new();
        public System.Collections.Generic.List<GrantedAbilityDetailDto> GrantedAbilities { get; init; } = new();
        public string Description { get; init; } = string.Empty;
        public System.Collections.Generic.List<string> Details { get; init; } = new();
    }
}

namespace ShadowverseBeyond.Server.Dtos
{
    public sealed record GrantedAbilityDetailDto
    {
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
    }
    public sealed record AttachedNumericEffectDto
    {
        public string Field { get; init; } = "";
        public string Layer { get; init; } = "";
        public double Value { get; init; }
        public bool RemoveAfterUse { get; init; }
        public string PolicyKey { get; init; } = "";
        public string Duration { get; init; } = "";
        public string DisplayText { get; init; } = "";
    }
}
