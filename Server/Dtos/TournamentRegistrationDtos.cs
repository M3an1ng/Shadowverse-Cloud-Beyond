using System.ComponentModel.DataAnnotations;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Dtos;

public sealed record TournamentPublicDto(
    Guid Id,
    string Name,
    string CardVersion,
    TournamentFormat Format,
    TournamentStatus Status,
    DeckEnvironment DeckEnvironment,
    DeckRule DeckRule,
    DateTime RegistrationOpensAtUtc,
    DateTime? RegistrationClosesAtUtc,
    string RegistrationStatus,
    int? PlayerLimit,
    int RegisteredCount,
    int RequiredDeckCount,
    IReadOnlyList<TournamentStageDto> Stages,
    DateTime UpdatedAtUtc)
{
    public static TournamentPublicDto FromTournament(Tournament tournament, int count, DateTime now) => new(
        tournament.Id,
        tournament.Name,
        tournament.CardVersion,
        tournament.Format,
        tournament.Status,
        tournament.DeckEnvironment,
        tournament.DeckRule,
        DateTime.SpecifyKind(tournament.RegistrationOpensAtUtc!.Value, DateTimeKind.Utc),
        tournament.RegistrationClosesAtUtc is { } closes ? DateTime.SpecifyKind(closes, DateTimeKind.Utc) : null,
        tournament.GetRegistrationStatus(now),
        tournament.PlayerLimit,
        count,
        tournament.GetRequiredDeckCount(1),
        tournament.Stages.OrderBy(stage => stage.Number).Select(stage => TournamentStageDto.FromStage(tournament, stage)).ToArray(),
        DateTime.SpecifyKind(tournament.UpdatedAtUtc, DateTimeKind.Utc));
}

public sealed record TournamentPublicDetailsDto(
    TournamentPublicDto Tournament, DateTime ServerTimeUtc, IReadOnlyList<TournamentEntrantDto>? Entrants,
    IReadOnlyList<TournamentPairingDto> Matches, IReadOnlyList<TournamentStageProgressDto> Progress);

public sealed record TournamentStageProgressDto(
    int StageNumber, TournamentStageParticipantDto[] Participants, TournamentStandingDto[] Standings,
    TournamentBracketNode[] Bracket);

public sealed record TournamentStageParticipantDto(Guid EntryId, string PlayerName, int Seed, bool IsMine, bool CanPlay);

public sealed record TournamentStandingDto(int Rank, Guid EntryId, string PlayerName, int Wins, int Losses,
    int OpponentWins, int GameWins, int GameLosses, int Byes, string Record);

public sealed record TournamentEntrantDto(Guid EntryId, string PlayerName);

public sealed record TournamentPublicLineupDto(
    Guid EntryId, string PlayerName, string? AvatarUrl, TournamentPlayerRecordDto Record,
    IReadOnlyList<TournamentPublicDeckDto> Decks, TournamentStageLineupDto? PreviousLineup);

public sealed record TournamentStageLineupDto(int StageNumber, IReadOnlyList<TournamentPublicDeckDto> Decks);

public sealed record TournamentPlayerRecordDto(int Wins, int Losses);

public sealed record TournamentDeckStatisticsDto(
    int TotalGames, int TotalWins, int GoingFirstGames, int GoingFirstWins, int GoingSecondGames, int GoingSecondWins,
    int? BansReceived);

public sealed record TournamentPublicDeckDto(
    Guid Id, int StageNumber, int Slot, string Name, CardClass Class, CardClass? SecondaryClass,
    string DeckCode, IReadOnlyList<TournamentDeckCard> Cards, TournamentDeckStatisticsDto Stats);

public sealed class SubmitTournamentEntryRequest
{
    [Range(0, int.MaxValue)]
    public int Revision { get; init; }

    [Required, MinLength(1), MaxLength(3)]
    public Guid[] DeckIds { get; init; } = [];
}

public sealed record TournamentDeckOptionDto(Guid DeckId, string Name, CardClass Class, CardClass? SecondaryClass);

public sealed record TournamentRegistrationDto(
    Guid? EntryId, int Revision, int RequiredDeckCount, string? BlockedReason,
    IReadOnlyList<TournamentDeckOptionDto> SubmittedDecks, IReadOnlyList<TournamentDeckOptionDto> AvailableDecks);
