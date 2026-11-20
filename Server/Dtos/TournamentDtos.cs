using System.ComponentModel.DataAnnotations;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Dtos;

public sealed class SaveTournamentDraftRequest : IValidatableObject
{
    [Required, StringLength(120)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(40)]
    public string CardVersion { get; init; } = string.Empty;

    [EnumDataType(typeof(TournamentFormat), ErrorMessage = "请选择广场赛或大赛。")]
    public TournamentFormat Format { get; init; }

    [EnumDataType(typeof(DeckEnvironment), ErrorMessage = "请选择有效的对战环境。")]
    public DeckEnvironment DeckEnvironment { get; init; } = DeckEnvironment.Rotation;

    public DeckRule DeckRule { get; init; } = DeckRule.Standard;

    [Range(2, int.MaxValue, ErrorMessage = "报名上限至少为 2 人。")]
    public int? PlayerLimit { get; init; }

    public DateTimeOffset? RegistrationOpensAt { get; init; }
    public DateTimeOffset? RegistrationClosesAt { get; init; }

    public SaveTournamentStageRequest[] Stages { get; init; } = [];

    [Range(0, int.MaxValue)]
    public int Revision { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DeckRule is not (DeckRule.Standard or DeckRule.DualClass))
            yield return new ValidationResult("请选择传统对战或混合职业。", [nameof(DeckRule)]);

        if (RegistrationOpensAt.HasValue && RegistrationClosesAt.HasValue
            && RegistrationClosesAt <= RegistrationOpensAt)
        {
            yield return new ValidationResult("报名截止必须晚于报名开始。", [nameof(RegistrationClosesAt)]);
        }

        if (Stages is not { Length: 1 or 2 } || Stages.Any(stage => stage is null))
        {
            yield return new ValidationResult("请设置一个或两个比赛阶段。", [nameof(Stages)]);
            yield break;
        }

        var first = Stages[0];
        if (Stages.Where((stage, index) => stage.DeckSubmissionMinutes.HasValue
            && (index == 0 || !stage.AllowDeckResubmission)).Any())
            yield return new ValidationResult("仅允许在开放重交卡组的后续阶段设置重交时长。", [nameof(Stages)]);
        if (Stages.Any(stage => stage.AllowGrandFinalReset && stage.Kind != TournamentStageKind.DoubleElimination))
            yield return new ValidationResult("总决赛加赛仅适用于双败淘汰。", [nameof(Stages)]);
        if (Format == TournamentFormat.Plaza)
        {
            if (Stages.Length != 1 || first.Kind != TournamentStageKind.Plaza
                || (first.Rounds.HasValue && first.Rounds != Tournament.PlazaRoundCount)
                || first.AdvancingPlayers.HasValue)
            {
                yield return new ValidationResult("广场赛采用一个阶段，固定四轮。", [nameof(Stages)]);
            }
            yield break;
        }

        var hasBothStages = Stages.Length == 2;
        var hasSwiss = first.Kind == TournamentStageKind.Swiss;
        var last = Stages[^1];
        var hasElimination = last.Kind is TournamentStageKind.SingleElimination or TournamentStageKind.DoubleElimination;
        if ((hasBothStages && (!hasSwiss || !hasElimination)) || (!hasSwiss && !hasElimination))
        {
            yield return new ValidationResult("大赛可设为瑞士轮、淘汰赛，或瑞士轮接淘汰赛。", [nameof(Stages)]);
            yield break;
        }

        if (hasSwiss && (!first.Rounds.HasValue || first.AdvancingPlayers.HasValue))
            yield return new ValidationResult("请填写瑞士轮轮数。晋级人数在淘汰赛阶段设置。", [nameof(Stages)]);
        if (hasElimination && last.Rounds.HasValue)
            yield return new ValidationResult("淘汰赛轮数由参赛人数决定。", [nameof(Stages)]);
        if (hasElimination && hasBothStages != last.AdvancingPlayers.HasValue)
        {
            yield return new ValidationResult(hasBothStages
                ? "请填写从瑞士轮晋级淘汰赛的人数。"
                : "仅淘汰赛时，报名选手直接进入淘汰赛。", [nameof(Stages)]);
        }
        if (last.AdvancingPlayers > PlayerLimit)
            yield return new ValidationResult("晋级人数不能超过报名上限。", [nameof(Stages)]);
    }
}

public sealed class SaveTournamentStageRequest
{
    [EnumDataType(typeof(TournamentStageKind), ErrorMessage = "请选择有效的比赛阶段。")]
    public TournamentStageKind Kind { get; init; }

    [EnumDataType(typeof(TournamentMatchFormat), ErrorMessage = "请选择有效的对局格式。")]
    public TournamentMatchFormat MatchFormat { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "比赛轮数至少为 1 轮。")]
    public int? Rounds { get; init; }

    [Range(2, int.MaxValue, ErrorMessage = "至少需要 2 人晋级淘汰赛。")]
    public int? AdvancingPlayers { get; init; }

    public bool AllowDeckResubmission { get; init; }
    public bool AllowGrandFinalReset { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "重交时长至少为 1 分钟。")]
    public int? DeckSubmissionMinutes { get; init; }
}

public sealed record TournamentStageDto(
    int Number,
    TournamentStageKind Kind,
    TournamentMatchFormat MatchFormat,
    int? Rounds,
    int? AdvancingPlayers,
    bool AllowDeckResubmission,
    bool AllowGrandFinalReset,
    int DecksPerPlayer,
    int BansPerPlayer,
    int WinsRequired,
    int? DeckSubmissionMinutes,
    bool DeckSubmissionPending,
    DateTime? DeckSubmissionClosesAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    Guid? WinnerEntryId)
{
    public static TournamentStageDto FromStage(Tournament tournament, TournamentStage stage)
    {
        var rules = TournamentMatchRules.For(stage.MatchFormat);
        return new TournamentStageDto(stage.Number, stage.Kind, stage.MatchFormat, stage.Rounds,
            stage.AdvancingPlayers, stage.AllowDeckResubmission, stage.AllowGrandFinalReset, rules.DecksPerPlayer, rules.BansPerPlayer,
            rules.WinsRequired, stage.DeckSubmissionMinutes, tournament.IsStageDeckSubmissionPending(stage),
            AsUtc(stage.DeckSubmissionClosesAtUtc),
            AsUtc(stage.StartedAtUtc), AsUtc(stage.CompletedAtUtc), stage.WinnerEntryId);
    }

    private static DateTime? AsUtc(DateTime? value) =>
        value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
}

public sealed record TournamentDraftDto(
    Guid Id,
    string Name,
    string CardVersion,
    TournamentFormat Format,
    DeckEnvironment DeckEnvironment,
    DeckRule DeckRule,
    TournamentStatus Status,
    int? PlayerLimit,
    DateTime? RegistrationOpensAtUtc,
    DateTime? RegistrationClosesAtUtc,
    IReadOnlyList<TournamentStageDto> Stages,
    DateTime UpdatedAtUtc,
    int Revision)
{
    public static TournamentDraftDto FromTournament(Tournament tournament) => new(
        tournament.Id,
        tournament.Name,
        tournament.CardVersion,
        tournament.Format,
        tournament.DeckEnvironment,
        tournament.DeckRule,
        tournament.Status,
        tournament.PlayerLimit,
        AsUtc(tournament.RegistrationOpensAtUtc),
        AsUtc(tournament.RegistrationClosesAtUtc),
        tournament.Stages.OrderBy(stage => stage.Number).Select(stage => TournamentStageDto.FromStage(tournament, stage)).ToArray(),
        DateTime.SpecifyKind(tournament.UpdatedAtUtc, DateTimeKind.Utc),
        tournament.Revision);

    private static DateTime? AsUtc(DateTime? value) =>
        value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
}

public sealed class PublishTournamentRequest
{
    [Range(1, int.MaxValue)]
    public int Revision { get; init; }
}

public sealed class UpdateTournamentCutoffRequest
{
    [Range(1, int.MaxValue)]
    public int Revision { get; init; }

    public DateTimeOffset? RegistrationClosesAt { get; init; }
    public bool CloseNow { get; init; }

    [Range(1, int.MaxValue)]
    public int StageNumber { get; init; } = 1;
}
