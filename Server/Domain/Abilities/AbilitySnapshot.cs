using System;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    /// <summary>
    /// Lightweight, read-only snapshot of an ability for tombstone lookup.
    /// </summary>
    public sealed record AbilitySnapshot(
        Guid RuntimeId,
        Guid? SourceCardId,
        string? SourceCardName,
        string? AbilityName);
}
