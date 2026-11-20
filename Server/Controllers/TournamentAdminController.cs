using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;

namespace ShadowverseBeyond.Server.Controllers;

[ApiController]
[Route("api/admin/tournaments")]
[Authorize(Policy = AccountSession.AdminPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TournamentAdminController(ApplicationDbContext db, TimeProvider clock, MatchService rooms) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TournamentDraftDto>>> List(CancellationToken cancellationToken)
    {
        var tournaments = await db.Tournaments.AsNoTracking()
            .Include(tournament => tournament.Stages)
            .OrderByDescending(tournament => tournament.UpdatedAtUtc)
            .ThenBy(tournament => tournament.Id)
            .ToListAsync(cancellationToken);
        return Ok(tournaments.Select(TournamentDraftDto.FromTournament).ToArray());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TournamentDraftDto>> SaveDraft(
        Guid id,
        SaveTournamentDraftRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest("赛事编号无效。");
        if (AccountSession.GetUserId(User) is not Guid userId) return Unauthorized();

        var tournament = await db.Tournaments.Include(entry => entry.Stages)
            .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        if (tournament is null)
        {
            if (request.Revision != 0) return NotFound("找不到这项赛事。");
            tournament = new Tournament { Id = id, CreatedAtUtc = now, CreatedByUserId = userId };
            db.Tournaments.Add(tournament);
        }
        else if (tournament.Status != TournamentStatus.Draft || tournament.Revision != request.Revision)
        {
            return DraftChanged();
        }

        tournament.Name = request.Name.Trim();
        tournament.CardVersion = request.CardVersion.Trim();
        tournament.Format = request.Format;
        tournament.DeckEnvironment = request.DeckEnvironment;
        tournament.DeckRule = request.DeckRule;
        tournament.PlayerLimit = request.PlayerLimit
            ?? (request.Format == TournamentFormat.Plaza ? Tournament.DefaultPlazaPlayerLimit : null);
        tournament.RegistrationOpensAtUtc = request.RegistrationOpensAt?.UtcDateTime;
        tournament.RegistrationClosesAtUtc = request.RegistrationClosesAt?.UtcDateTime;
        ApplyStages(tournament, request.Stages);
        tournament.UpdatedAtUtc = now;
        tournament.Revision++;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return DraftChanged();
        }
        catch (DbUpdateException error) when (error.InnerException is SqliteException { SqliteExtendedErrorCode: 1555 })
        {
            return DraftChanged();
        }

        return Ok(TournamentDraftDto.FromTournament(tournament));
    }

    [HttpPut("{id:guid}/publish")]
    public async Task<ActionResult<TournamentDraftDto>> Publish(
        Guid id,
        PublishTournamentRequest request,
        CancellationToken cancellationToken)
    {
        var tournament = await db.Tournaments.Include(entry => entry.Stages)
            .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);
        if (tournament is null) return NotFound("找不到这项赛事。");
        if (tournament.Status == TournamentStatus.Published && tournament.Revision == request.Revision + 1L)
            return Ok(TournamentDraftDto.FromTournament(tournament));
        if (tournament.Status != TournamentStatus.Draft || tournament.Revision != request.Revision)
            return DraftChanged();

        var now = clock.GetUtcNow().UtcDateTime;
        if (tournament.RegistrationClosesAtUtc <= now)
            return BadRequest("报名截止时间必须在未来。");

        tournament.RegistrationOpensAtUtc ??= now;
        tournament.Status = TournamentStatus.Published;
        tournament.Revision++;
        tournament.UpdatedAtUtc = now;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return DraftChanged();
        }
        return Ok(TournamentDraftDto.FromTournament(tournament));
    }

    [HttpPut("{id:guid}/registration/cutoff")]
    public async Task<ActionResult<TournamentDraftDto>> UpdateRegistrationCutoff(
        Guid id, UpdateTournamentCutoffRequest request, CancellationToken cancellationToken)
    {
        if (request.CloseNow == request.RegistrationClosesAt.HasValue)
            return BadRequest("请选择新的报名截止时间，或立即截止报名。");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var tournament = await db.Tournaments.Include(entry => entry.Stages)
            .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);
        if (tournament is null) return NotFound("找不到这项赛事。");
        if (tournament.Revision != request.Revision) return DraftChanged();

        var now = clock.GetUtcNow().UtcDateTime;
        var closes = request.CloseNow ? now : request.RegistrationClosesAt!.Value.UtcDateTime;

        if (request.StageNumber == 1)
        {
            if (tournament.Status != TournamentStatus.Published || tournament.RegistrationClosesAtUtc <= now)
                return Conflict("报名已截止，无法修改截止时间。");
            if (request.CloseNow && !tournament.IsRegistrationOpen(now))
                return Conflict("报名开始后可以手动截止。");
            if (!request.CloseNow && (closes <= now || closes <= tournament.RegistrationOpensAtUtc))
                return BadRequest("报名截止必须晚于报名开始和当前时间。");
            tournament.RegistrationClosesAtUtc = closes;
        }
        else
        {
            var stage = tournament.Stages.SingleOrDefault(item => item.Number == request.StageNumber);
            if (stage is null) return NotFound("找不到这个比赛阶段。");
            if (!tournament.IsStageDeckSubmissionOpen(stage, now))
                return Conflict("此阶段当前没有开放重交卡组。");
            if (!request.CloseNow && closes <= now)
                return BadRequest("重交截止时间必须晚于当前时间。");
            stage.DeckSubmissionClosesAtUtc = closes;
            tournament.NextScheduleCheckAtUtc = closes;
        }
        tournament.UpdatedAtUtc = now;
        tournament.Revision++;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(TournamentDraftDto.FromTournament(tournament));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] int revision, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var tournament = await db.Tournaments.AsNoTracking().SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (tournament is null) return NoContent();
        if (revision != tournament.Revision) return DraftChanged();

        var matches = db.TournamentMatches.Where(match => match.TournamentId == id);
        var games = db.TournamentMatchGames.Where(game => matches.Any(match => match.Id == game.MatchId));
        var gameIds = await games.Select(game => game.Id).ToArrayAsync(cancellationToken);
        await games.ExecuteDeleteAsync(cancellationToken);
        await matches.ExecuteDeleteAsync(cancellationToken);
        await db.TournamentStageEntries.Where(entry => entry.TournamentId == id).ExecuteDeleteAsync(cancellationToken);
        await db.Tournaments.Where(row => row.Id == id).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        foreach (var gameId in gameIds) rooms.Close(gameId.ToString("D"), "管理员已删除赛事。");
        return NoContent();
    }

    private ConflictObjectResult DraftChanged() =>
        Conflict("这项赛事已保存或更新，请刷新列表后再编辑。");

    private static void ApplyStages(Tournament tournament, SaveTournamentStageRequest[] stages)
    {
        var previousStages = tournament.Stages.ToDictionary(stage => stage.Number);
        for (var index = 0; index < stages.Length; index++)
        {
            var number = index + 1;
            if (!previousStages.Remove(number, out var stage))
            {
                stage = new TournamentStage { TournamentId = tournament.Id, Number = number };
                tournament.Stages.Add(stage);
            }

            var input = stages[index];
            stage.Kind = input.Kind;
            stage.Rounds = input.Kind == TournamentStageKind.Plaza ? Tournament.PlazaRoundCount : input.Rounds;
            stage.MatchFormat = input.MatchFormat;
            stage.AllowDeckResubmission = input.AllowDeckResubmission;
            stage.AllowGrandFinalReset = input.AllowGrandFinalReset;
            stage.DeckSubmissionMinutes = input.DeckSubmissionMinutes;
            stage.AdvancingPlayers = input.AdvancingPlayers;
        }

        foreach (var removed in previousStages.Values)
            tournament.Stages.Remove(removed);
    }
}
