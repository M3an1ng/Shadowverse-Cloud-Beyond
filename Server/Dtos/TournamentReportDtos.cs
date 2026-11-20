using System.ComponentModel.DataAnnotations;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Dtos;

public sealed record TournamentClassificationsDto(
    string Name, DeckRule DeckRule, TournamentReportStageDto[] Stages, TournamentClassificationDto[] Decks);

public sealed record TournamentClassificationDto(
    Guid DeckId, Guid EntryId, string PlayerName, string DeckName, CardClass Class,
    CardClass? SecondaryClass, string? Archetype, int[] Stages);

public sealed class SaveTournamentClassificationsRequest
{
    [Required, MinLength(1)]
    public TournamentClassificationChange[] Changes { get; init; } = [];
}

public sealed class TournamentClassificationChange
{
    public Guid DeckId { get; init; }

    [StringLength(64, ErrorMessage = "卡组类型最多 64 个字符。")]
    public string? Archetype { get; init; }

    [StringLength(64)]
    public string? Previous { get; init; }
}

public sealed record TournamentReportStageDto(int Number, TournamentStageKind Kind, TournamentMatchFormat MatchFormat);

public sealed record TournamentReportDto(
    string Name, string CardVersion, TournamentStatus Status, int Stage, TournamentReportStageDto[] Stages,
    int Unclassified, int AllUnclassified, TournamentReportTotalsDto? Totals,
    TournamentDeckMetricsDto[] Classes, TournamentDeckMetricsDto[] Archetypes,
    TournamentMatchupDto[] ClassMatchups, TournamentMatchupDto[] ArchetypeMatchups, TournamentAdvancementDto[] Advancement);

public sealed record TournamentReportTotalsDto(
    int Players, int LineupDecks, int CompletedMatches, int PendingMatches, int Byes, int Games, int Draws,
    int GamesWithKnownOrder, int FirstPlayerWins);

public sealed record TournamentDeckMetricsDto(
    string Name, int LineupDecks, int Appearances, int Wins, int MatchLineups, int BanChances, int Bans,
    int FirstAppearances, int FirstWins, int SecondAppearances, int SecondWins)
{
    public double? WinRate => Ratio(Wins, Appearances);
    public double? BanRate => Ratio(Bans, BanChances);
    public double? FirstWinRate => Ratio(FirstWins, FirstAppearances);
    public double? SecondWinRate => Ratio(SecondWins, SecondAppearances);

    private static double? Ratio(int numerator, int denominator) => denominator > 0 ? (double)numerator / denominator : null;
}

public sealed record TournamentMatchupDto(string Name, string Opponent, int Games, int Wins)
{
    public double WinRate => (double)Wins / Games;
}

public sealed record TournamentAdvancementDto(
    int Rank, string PlayerName, int Wins, int Losses, int OpponentWins, int GameWins, int GameLosses,
    bool Qualified, bool? AcceptedLineup, string[] Archetypes);
