using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Constants;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    /// <summary>
    /// Immutable transaction-scoped snapshot captured when an ability is unregistered.
    /// </summary>
    public sealed record AbilitySnapshot(
        Guid RuntimeId,
        Guid? SourceCardId,
        string? SourceCardName,
        string? AbilityName,
        long DefinitionId,
        Guid? SourceBuffId,
        IReadOnlyList<AbilityKeyword> Keywords,
        string? KeywordSummary,
        string RemovalReason,
        bool WasSuppressedWhenUnregistered,
        bool ShouldCancelQueuedEffects,
        bool BuffDisplay,
        long QueuedEffectInvalidationVersion = 0,
        string? QueuedEffectInvalidationReason = null);
}
