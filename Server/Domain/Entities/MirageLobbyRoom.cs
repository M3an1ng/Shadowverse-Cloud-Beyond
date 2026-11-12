using System;
using System.Collections.Generic;
using System.Threading;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Services;

namespace ShadowverseBeyond.Server.Domain.Entities
{
    public enum MirageLobbyPhase
    {
        Waiting = 0,
        Recruit = 1,
        Closing = 2,
        Simulating = 3,
        Applying = 4,
        Finished = 5,
        Closed = 6
    }

    public enum MirageSeatOccupancy
    {
        Empty = 0,
        Human = 1,
        Ai = 2
    }

    public sealed record MirageRoomPairing(
        int SeatIndexA,
        int? SeatIndexB, // null when facing an eliminated seat's Ghost
        int? GhostSourceSeatIndex,
        Guid CombatId);

    public sealed class MirageEliminatedGhostEntry
    {
        public int SourceSeatIndex { get; init; }
        public Guid SourcePlayerId { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string AvatarUrl { get; init; } = string.Empty;
        public int EliminatedRound { get; init; }
        public int TavernTier { get; init; } = 1;
        public int LeaderCardDefinitionId { get; init; }
        public MirageCombatSideSnapshot FrozenSideSnapshot { get; init; } = null!;
    }

    public sealed class MirageLobbySeat
    {
        public int SeatIndex { get; init; }
        public MirageSeatOccupancy Occupancy { get; set; } = MirageSeatOccupancy.Empty;
        public Guid PlayerId { get; set; }
        public string SeatToken { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
        public bool IsHost { get; set; }
        public bool IsReady { get; set; }

        // Tournament runtime identity & facts (HP/Tier truth lives in SeatGame; cached summary is read-only projection)
        public string? SeatGameId { get; set; }
        public bool IsEliminated { get; set; }
        public int? EliminatedRound { get; set; }
        public int? FinalPlacement { get; set; }
        public bool PendingForfeit { get; set; }
        public long PendingForfeitSequence { get; set; }
        public string? ForfeitReason { get; set; }
        public bool HasLeftPostElimination { get; set; }
        public bool MetaSettled { get; set; }

        public int? LastOpponentSeatIndex { get; set; }
        public int? LastFacedGhostRound { get; set; }
        public int ConsecutiveWins { get; set; }
        public int LastCombatDeltaHp { get; set; }

        // Boundary-projected read-only summary values
        public int ProjectedCurrentHp { get; set; } = 20;
        public int ProjectedMaxHp { get; set; } = 20;
        public int ProjectedTavernTier { get; set; } = 1;
        public int ProjectedLeaderCardId { get; set; }
        public int ProjectedKnownWarbandCount { get; set; }
        public bool ProjectedIsRoundLocked { get; set; }
    }

    public sealed class MirageLobbyRoom
    {
        public const int MaxSeatCount = 8;

        public object SyncRoot { get; } = new();
        public string RoomId { get; init; } = string.Empty;
        public string JoinCode { get; init; } = string.Empty;
        public bool IsPublic { get; set; } = true;
        public int RecruitTimeoutSeconds { get; set; } = MirageRuleConfig.DefaultRecruitTimeoutSeconds;
        public long RoomVersion { get; set; } = 1;
        public long NextForfeitSequence { get; set; }
        public MirageLobbyPhase Phase { get; set; } = MirageLobbyPhase.Waiting;
        public int RoundNumber { get; set; } = 0;
        public int? LastSettledRoundNumber { get; set; }
        public Guid? ActiveRoundCombatBatchId { get; set; }
        public DateTime? RoundStartedAtUtc { get; set; }
        public DateTime? RoundEndsAtUtc { get; set; }
        public Guid? ChampionPlayerId { get; set; }
        public string? ClosedReason { get; set; }

        public TimeSpan RoomTtl { get; set; } = TimeSpan.FromMinutes(30);
        public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
        public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddMinutes(30);

        public List<MirageLobbySeat> Seats { get; } = new(MaxSeatCount);
        public List<MirageRoomPairing> CurrentRoundPairings { get; set; } = new();
        public List<MirageEliminatedGhostEntry> EliminatedSeatGhosts { get; } = new();
        public MirageStandingsSnapshotDto? LatestStandingsSnapshot { get; set; }

        // Shared room-wide Mirage card pool referenced by all 8 SeatGames
        public object SharedPoolSyncRoot { get; } = new();
        public Dictionary<int, int> SharedCardPool { get; } = new();
        public Dictionary<int, int> InitialCardPoolCaps { get; } = new();
        public HashSet<Guid> ReturnedEliminatedPlayerIds { get; } = new();

        // Active spectator & disconnect grace tracking for safe AI cleanup
        public HashSet<string> ActiveSpectatorConnectionIds { get; } = new(StringComparer.Ordinal);
        public Dictionary<Guid, DateTime> HumanGraceDeadlinesUtc { get; } = new();
        public HashSet<System.Threading.Tasks.Task> InFlightRecruitForfeits { get; } = new();

        public CancellationTokenSource? RoomLifetimeCts { get; set; }
        public Timer? RoundRecruitTimer { get; set; }
        public Guid ActiveTimerId { get; set; }
    }
}
