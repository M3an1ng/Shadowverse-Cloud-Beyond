using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Services.Abstractions;

/// <summary>
/// Applies meta-progression (economy, profile, quests, rank) updates based on committed in-game events.
/// This service MUST be called after the domain transaction has been committed (state mutation applied),
/// but before the resulting state is considered finalized for persistence/broadcast consistency.
/// </summary>
public interface IMetaProgressionService
{
    Task ApplyPostCommitAsync(
        GameContext gameContext,
        IReadOnlyList<IGameEvent> processedEvents,
        CancellationToken cancellationToken);
}

