using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos
{
    public sealed record MirageLobbySeatDto
    {
        public int SeatIndex { get; init; }
        public string Occupancy { get; init; } = "Empty"; // Empty | Human | Ai
        public Guid? PlayerId { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string AvatarUrl { get; init; } = string.Empty;
        public bool IsHost { get; init; }
        public bool IsReady { get; init; }
        public bool IsEliminated { get; init; }
        public int? FinalPlacement { get; init; }
    }

    public sealed record MirageLobbyRoomDto
    {
        public string RoomId { get; init; } = string.Empty;
        public long RoomVersion { get; init; }
        public string JoinCode { get; init; } = string.Empty;
        public bool IsPublic { get; init; } = true;
        public string Phase { get; init; } = "Waiting";
        public int MaxSeats { get; init; } = 8;
        public int HumanCount { get; init; }
        public int ReadyHumanCount { get; init; }
        public int AliveCount { get; init; }
        public int RoundNumber { get; init; }
        public int RecruitTimeoutSeconds { get; init; } = 75;
        public Guid HostPlayerId { get; init; }
        public bool CanHostStart { get; init; }
        public string? StartBlockReason { get; init; }
        public string? ClosedReason { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime ExpiresAtUtc { get; init; }

        // Caller-scoped credentials (only populated when caller authenticates or creates/joins)
        public int? CallerSeatIndex { get; init; }
        public Guid? CallerPlayerId { get; init; }
        public string? CallerSeatToken { get; init; }
        public string? CallerSeatGameId { get; init; }
        public string? CallerRole { get; init; } // host | guest | spectator
        public bool CallerIsEliminated { get; init; }
        public int? CallerFinalPlacement { get; init; }
        public Guid? RecommendedSpectatePlayerId { get; init; }

        public List<MirageLobbySeatDto> Seats { get; init; } = new();
    }

    public sealed record MirageLobbyRoomListItemDto
    {
        public string RoomId { get; init; } = string.Empty;
        public bool IsPublic { get; init; } = true;
        public string Phase { get; init; } = "Waiting";
        public string StatusLabel { get; init; } = "等待中";
        public string HostDisplayName { get; init; } = string.Empty;
        public int MaxSeats { get; init; } = 8;
        public int HumanCount { get; init; }
        public int AliveCount { get; init; }
        public int RoundNumber { get; init; }
        public int RecruitTimeoutSeconds { get; init; } = 75;
        public bool CanJoin { get; init; }
        public bool CanSpectate { get; init; }
        public DateTime CreatedAtUtc { get; init; }
    }

    public sealed record MirageLobbyRoomListResponseDto
    {
        public List<MirageLobbyRoomListItemDto> Items { get; init; } = new();
    }

    public sealed record MirageNextOpponentSummaryDto
    {
        public Guid? PlayerId { get; init; }
        public int? SeatIndex { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string AvatarUrl { get; init; } = string.Empty;
        public bool IsGhost { get; init; }
        public bool IsAi { get; init; }
        public int TavernTier { get; init; } = 1;
        public int CurrentHp { get; init; } = 20;
        public int MaxHp { get; init; } = 20;
        public int LeaderCardDefinitionId { get; init; }
        public string LeaderName { get; init; } = string.Empty;
        public string? LeaderArtPreview { get; init; }
        public int KnownWarbandCount { get; init; }
    }

    public sealed record MirageStandingEntryDto
    {
        public int Rank { get; init; }
        public int SeatIndex { get; init; }
        public Guid PlayerId { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string AvatarUrl { get; init; } = string.Empty;
        public bool IsAi { get; init; }
        public bool IsEliminated { get; init; }
        public int? EliminatedRound { get; init; }
        public int? FinalPlacement { get; init; }
        public int CurrentHp { get; init; } = 20;
        public int MaxHp { get; init; } = 20;
        public int TavernTier { get; init; } = 1;
        public int LeaderCardDefinitionId { get; init; }
        public string LeaderName { get; init; } = string.Empty;
        public string? LeaderArtPreview { get; init; }
        public bool IsReady { get; init; }
        public int ConsecutiveWins { get; init; }
        public int LastCombatDeltaHp { get; init; }
        public Guid? NextOpponentPlayerId { get; init; }
        public bool NextOpponentIsGhost { get; init; }
    }

    public sealed record MirageStandingsSnapshotDto
    {
        public string RoomId { get; init; } = string.Empty;
        public long RoomVersion { get; init; }
        public string Phase { get; init; } = "Recruit";
        public int RoundNumber { get; init; } = 1;
        public int? SettledRoundNumber { get; init; }
        public Guid? ActiveCombatId { get; init; }
        public int ReadyCount { get; init; }
        public int AliveCount { get; init; }
        public DateTime? RoundEndsAtUtc { get; init; }
        public Guid? ChampionPlayerId { get; init; }
        public Dictionary<Guid, MirageNextOpponentSummaryDto> NextOpponentsByPlayerId { get; init; } = new();
        public List<MirageStandingEntryDto> Entries { get; init; } = new();
    }

    public sealed record MirageResolveCodeResponseDto
    {
        public bool Found { get; init; }
        public string Kind { get; init; } = string.Empty; // Classic | TakeTwo | Mirage8P
        public string RoomId { get; init; } = string.Empty;
        public string JoinCode { get; init; } = string.Empty;
    }
}
