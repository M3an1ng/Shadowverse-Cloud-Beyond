using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Services.Abstractions;

/// <summary>
/// Applies winrate statistics updates based on committed events.
/// </summary>
public interface IWinrateStatsService
{
    Task ApplyPostCommitAsync(
        GameContext gameContext,
        IReadOnlyList<IGameEvent> processedEvents,
        CancellationToken cancellationToken);
}
