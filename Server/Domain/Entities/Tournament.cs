using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Entities;

public enum TournamentFormat
{
    Plaza = 1,
    Major = 2
}

public enum TournamentStageKind
{
    Plaza = 1,
    Swiss,
    SingleElimination,
    DoubleElimination
}

public enum TournamentMatchFormat
{
    Bo1 = 1,
    Bo3,
    Bo3Ban1,
    Bo5
}

public readonly record struct TournamentMatchRules(int DecksPerPlayer, int BansPerPlayer, int WinsRequired)
{
    public static TournamentMatchRules For(TournamentMatchFormat format) => format switch
    {
        TournamentMatchFormat.Bo1 => new(1, 0, 1),
        TournamentMatchFormat.Bo3 => new(2, 0, 2),
        TournamentMatchFormat.Bo3Ban1 => new(3, 1, 2),
        TournamentMatchFormat.Bo5 => new(3, 0, 3),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported match format.")
    };
}

public enum TournamentStatus
{
    Draft,
    Published,
    Running,
    Finished
}

public sealed class Tournament
{
    public const int PlazaRoundCount = 4;
    public const int DefaultPlazaPlayerLimit = 16;

    public Guid Id { get; init; }
    public string Name { get; set; } = string.Empty;
    public string CardVersion { get; set; } = string.Empty;
    public TournamentFormat Format { get; set; }
    public DeckEnvironment DeckEnvironment { get; set; } = DeckEnvironment.Rotation;
    public DeckRule DeckRule { get; set; } = DeckRule.Standard;
    public TournamentStatus Status { get; set; } = TournamentStatus.Draft;
    public int? PlayerLimit { get; set; }
    public DateTime? RegistrationOpensAtUtc { get; set; }
    public DateTime? RegistrationClosesAtUtc { get; set; }
    public Guid CreatedByUserId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; set; }
    public int Revision { get; set; }
    public DateTime? NextScheduleCheckAtUtc { get; set; }

    public List<TournamentStage> Stages { get; } = [];

    public bool IsRegistrationOpen(DateTime now) =>
        Status == TournamentStatus.Published && RegistrationOpensAtUtc <= now
        && (!RegistrationClosesAtUtc.HasValue || now < RegistrationClosesAtUtc);

    public bool AreEntriesPublic(DateTime now) =>
        Status != TournamentStatus.Draft && RegistrationClosesAtUtc <= now;

    public string GetRegistrationStatus(DateTime now) =>
        IsRegistrationOpen(now) ? "open" : RegistrationOpensAtUtc > now ? "scheduled" : "closed";

    public bool IsStageDeckSubmissionPending(TournamentStage stage) =>
        Status == TournamentStatus.Running && stage.Number > 1 && stage.AllowDeckResubmission
        && !stage.StartedAtUtc.HasValue && !stage.CompletedAtUtc.HasValue
        && Stages.Exists(previous => previous.Number == stage.Number - 1 && previous.CompletedAtUtc.HasValue);

    public bool IsStageDeckSubmissionOpen(TournamentStage stage, DateTime now) =>
        IsStageDeckSubmissionPending(stage)
        && (!stage.DeckSubmissionClosesAtUtc.HasValue || stage.DeckSubmissionClosesAtUtc > now);

    public int GetRequiredDeckCount(int stageNumber)
    {
        var stages = Stages.OrderBy(stage => stage.Number).SkipWhile(stage => stage.Number < stageNumber);
        var required = 0;
        foreach (var stage in stages)
        {
            if (stage.Number > stageNumber && stage.AllowDeckResubmission) break;
            required = Math.Max(required, TournamentMatchRules.For(stage.MatchFormat).DecksPerPlayer);
        }
        return required;
    }
}

public sealed class TournamentStage
{
    public Guid TournamentId { get; init; }
    public int Number { get; init; }
    public TournamentStageKind Kind { get; set; }
    public int? Rounds { get; set; }
    public int? AdvancingPlayers { get; set; }
    public TournamentMatchFormat MatchFormat { get; set; }
    public bool AllowDeckResubmission { get; set; }
    public bool AllowGrandFinalReset { get; set; }
    public int? DeckSubmissionMinutes { get; set; }
    public DateTime? DeckSubmissionClosesAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public Guid? WinnerEntryId { get; set; }
}
