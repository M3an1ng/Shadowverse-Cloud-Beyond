using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services.Abstractions;

/// <summary>
/// Tracks per-game runtime signals required for post-game winrate settlement.
/// </summary>
public interface IGameStatsRuntimeTracker
{
    void TrackCommittedEvents(GameState gameState, IReadOnlyList<IGameEvent> processedEvents);
    GameRuntimeStatsSnapshot? GetSnapshot(string gameId);
    void Clear(string gameId);
}

public sealed record GameRuntimeStatsSnapshot(
    string GameId,
    IReadOnlyDictionary<Guid, ParticipantRuntimeStatsSnapshot> Participants);

public sealed record ParticipantRuntimeStatsSnapshot(
    Guid PlayerId,
    bool IsFirstPlayer,
    CardClass DeckClass,
    DeckEnvironment Environment,
    DeckRule Rule,
    Guid? UserDeckId,
    string DeckShapeHash,
    IReadOnlyDictionary<int, int> InitialDeckCardCounts,
    IReadOnlySet<int> DrawnCardDefinitionIds,
    IReadOnlySet<int> PlayedCardDefinitionIds);


