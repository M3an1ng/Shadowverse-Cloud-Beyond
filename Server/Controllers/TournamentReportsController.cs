using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;

namespace ShadowverseBeyond.Server.Controllers;

[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TournamentReportsController(ApplicationDbContext db, TournamentReportReader reports) : ControllerBase
{
    [HttpGet("api/admin/tournaments/{id:guid}/classifications")]
    [Authorize(Policy = AccountSession.AdminPolicy)]
    public async Task<ActionResult<TournamentClassificationsDto>> Classifications(Guid id, CancellationToken cancellationToken)
    {
        var source = await reports.ReadSnapshotAsync(id, includeMatches: false, cancellationToken);
        return source is null ? Unavailable() : Ok(new TournamentClassificationsDto(
            source.Event.Name, source.Event.DeckRule, source.StageOptions(), source.Classifications()));
    }

    [HttpPut("api/admin/tournaments/{id:guid}/classifications")]
    [Authorize(Policy = AccountSession.AdminPolicy)]
    public async Task<IActionResult> Save(
        Guid id, SaveTournamentClassificationsRequest request, CancellationToken cancellationToken)
    {
        if (request.Changes.Any(change => change is null || change.DeckId == Guid.Empty)
            || request.Changes.Select(change => change.DeckId).Distinct().Count() != request.Changes.Length)
            return BadRequest("每套参赛卡组只可归类一次。");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var source = await reports.ReadAsync(id, includeMatches: false, cancellationToken);
        if (source is null) return Unavailable();
        var allowed = source.Lineups.SelectMany(lineup => lineup.Decks).Select(deck => deck.Id).ToHashSet();
        var requested = request.Changes.Select(change => change.DeckId).ToArray();
        if (requested.Any(deckId => !allowed.Contains(deckId))) return BadRequest("请选择已锁定的本赛事参赛卡组。");

        var decks = await db.TournamentEntryDecks.Where(deck => requested.Contains(deck.Id))
            .ToDictionaryAsync(deck => deck.Id, cancellationToken);
        foreach (var change in request.Changes)
        {
            var text = change.Archetype?.Trim();
            if (text?.Any(char.IsControl) == true) return BadRequest("卡组类型请使用单行文字。");
            try { text = text?.Normalize(NormalizationForm.FormC); }
            catch (ArgumentException) { return BadRequest("卡组类型包含无效字符。"); }
            if (text?.Length > 64) return BadRequest("卡组类型最多 64 个字符。");
            var label = string.IsNullOrEmpty(text) ? null : text;
            var deck = decks[change.DeckId];
            if (deck.Archetype != label && deck.Archetype != change.Previous)
                return Conflict($"「{deck.Name}」的分类已有更新，请重新打开后重试。");
            deck.Archetype = label;
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("api/tournaments/{id:guid}/report")]
    public async Task<ActionResult<TournamentReportDto>> Report(Guid id, CancellationToken cancellationToken, [FromQuery] int stage = 0)
    {
        var source = await reports.ReadSnapshotAsync(id, includeMatches: true, cancellationToken, stage);
        if (source is null) return Unavailable();
        if (stage != 0 && source.Stages.All(item => item.Number != stage)) return NotFound("找不到可统计的比赛阶段。");
        return Ok(TournamentReportStatistics.Build(source, stage));
    }

    [HttpGet("api/tournaments/{id:guid}/report.xlsx")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var source = await reports.ReadSnapshotAsync(id, includeMatches: true, cancellationToken);
        if (source is null) return Unavailable();
        var report = TournamentReportStatistics.Build(source);
        if (report.Unclassified > 0) return Conflict($"还有 {report.Unclassified} 套卡组需要分类。");

        var content = TournamentReportWorkbook.Write(source, report, cancellationToken);
        var name = string.Concat(source.Event.Name.Select(ch => "\\/:*?\"<>|".Contains(ch) || char.IsControl(ch) ? '_' : ch));
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name + "-星云战报.xlsx");
    }

    private NotFoundObjectResult Unavailable() => NotFound("开始赛程后可以查看赛事战报。");
}
