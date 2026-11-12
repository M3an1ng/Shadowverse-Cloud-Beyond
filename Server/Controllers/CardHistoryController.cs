using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using AutoMapper;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Controllers;
[ApiController, Route("api/cards/{cardId:int}/history")]
public sealed class CardHistoryController(ApplicationDbContext db, IGameDataProvider data, IGameService games, IMapper mapper) : ControllerBase
{
    private async Task<User?> Actor(CancellationToken ct)
    {
        if (!Guid.TryParse(Request.Headers["X-Editor-UserId"].FirstOrDefault(), out var id)) return null;
        var actor = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id && u.IsActive, ct);
        return actor != null && UserRolePermissions.GetPermissionLevel(actor.Role) >= 10 ? actor : null;
    }

    private Task<CardRevisionRecord?> Entry(int cardId, long id, CancellationToken ct) =>
        db.CardRevisions.SingleOrDefaultAsync(e => e.CardId == cardId && e.Id == id, ct);

    private async Task<(CardDefinition? Card, IActionResult? ErrorResult)> ResolveScopedCardAsync(int cardId, string? scope, bool track, CancellationToken ct)
    {
        var card = track
            ? await db.CardDefinitions.FindAsync(new object[] { cardId }, ct)
            : await db.CardDefinitions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cardId, ct);
        if (card == null)
        {
            return (null, NotFound());
        }

        if (MirageCardPoolIsolationRules.TryParseEditorScope(scope, out var scopedMirage)
            && card.IsMirageCard != scopedMirage)
        {
            return (null, BadRequest(new { error = $"卡牌 #{cardId} 不属于当前编辑环境（{scope}）。" }));
        }

        return (card, null);
    }

    [HttpGet]
    public async Task<IActionResult> List(int cardId, [FromQuery] string? scope = null, CancellationToken ct = default)
    {
        if (await Actor(ct) == null) return StatusCode(403);
        var (_, errorResult) = await ResolveScopedCardAsync(cardId, scope, track: false, ct);
        if (errorResult != null) return errorResult;

        if (!await db.CardRevisions.AnyAsync(h => h.CardId == cardId, ct))
        {
            var snapshot = await db.ReadCardConfigurationAsync(cardId, ct);
            var baseline = new CardRevisionRecord
            {
                CardId = cardId,
                Revision = 1,
                Editor = snapshot!.Card.UpdatedBy ?? "",
                ModifiedAt = snapshot.Card.UpdatedAt ?? DateTime.UtcNow,
                Operation = "baseline",
                Snapshot = JsonSerializer.Serialize(snapshot, ApplicationDbContext.RevisionJson)
            };
            db.CardRevisions.Add(baseline);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
            {
                db.Entry(baseline).State = EntityState.Detached;
                if (!await db.CardRevisions.AnyAsync(h => h.CardId == cardId, ct)) throw;
            }
        }
        var rows = await db.CardRevisions.AsNoTracking().Where(h => h.CardId == cardId).OrderByDescending(h => h.Revision).ToListAsync(ct);
        var revision = rows[0].Revision;
        return Ok(new { revision, entries = rows.Select(h => new { h.Id, h.Revision, h.Editor, h.ModifiedAt, h.Note, h.Operation, current = h.Revision == revision }) });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(int cardId, long id, [FromQuery] string? scope = null, CancellationToken ct = default)
    {
        if (await Actor(ct) == null) return StatusCode(403);
        var (_, errorResult) = await ResolveScopedCardAsync(cardId, scope, track: false, ct);
        if (errorResult != null) return errorResult;

        var entry = await Entry(cardId, id, ct); if (entry == null) return NotFound();
        var snapshot = JsonSerializer.Deserialize<CardConfigurationSnapshot>(entry.Snapshot, ApplicationDbContext.RevisionJson)!;
        return Ok(new { card = mapper.Map<CardDefinitionDto>(snapshot.Card), abilities = snapshot.Abilities });
    }

    public sealed record NoteRequest(string Note);
    [HttpPatch("{id:long}")]
    public async Task<IActionResult> Annotate(int cardId, long id, NoteRequest request, [FromQuery] string? scope = null, CancellationToken ct = default)
    {
        if (await Actor(ct) == null) return StatusCode(403);
        var (_, errorResult) = await ResolveScopedCardAsync(cardId, scope, track: false, ct);
        if (errorResult != null) return errorResult;

        if ((request.Note ?? "").Length > 2000) return BadRequest(new { error = "注释最多2000字" });
        var entry = await Entry(cardId, id, ct); if (entry == null) return NotFound();
        entry.Note = request.Note ?? ""; await db.SaveChangesAsync(ct); return Ok(new { ok = true });
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(int cardId, long id, [FromQuery] string? scope = null, CancellationToken ct = default)
    {
        if (await Actor(ct) == null) return StatusCode(403);
        var (_, errorResult) = await ResolveScopedCardAsync(cardId, scope, track: false, ct);
        if (errorResult != null) return errorResult;

        var entry = await Entry(cardId, id, ct); if (entry == null) return NotFound();
        if (entry.Revision == await db.CardRevisions.Where(h => h.CardId == cardId).MaxAsync(h => h.Revision, ct)) return Conflict(new { error = "不能删除当前版本" });
        db.CardRevisions.Remove(entry); await db.SaveChangesAsync(ct); return Ok(new { ok = true });
    }

    public sealed record RestoreRequest(int Revision);
    [HttpPost("{id:long}/restore")]
    public async Task<IActionResult> Restore(int cardId, long id, RestoreRequest request, [FromQuery] string? scope = null, CancellationToken ct = default)
    {
        var actor = await Actor(ct); if (actor == null) return StatusCode(403);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var (card, errorResult) = await ResolveScopedCardAsync(cardId, scope, track: true, ct);
        if (errorResult != null) return errorResult;

        var currentRevision = await db.CardRevisions.Where(h => h.CardId == cardId).MaxAsync(h => h.Revision, ct);
        if (currentRevision != request.Revision) return Conflict(new { error = "卡牌已被其他窗口修改，请重新打开版本历史" });
        var entry = await Entry(cardId, id, ct); if (entry == null) return NotFound();
        if (entry.Revision == currentRevision) return Conflict(new { error = "该版本已在使用" });
        var snapshot = JsonSerializer.Deserialize<CardConfigurationSnapshot>(entry.Snapshot, ApplicationDbContext.RevisionJson)!;

        var currentIsMirage = card!.IsMirageCard;
        var currentSetId = card.CardSetDefinitionId;
        var currentCardSet = card.CardSet;

        if (snapshot.Card.IsMirageCard != currentIsMirage)
        {
            return Conflict(new { error = "禁止通过历史版本恢复改变卡牌的环境归属。" });
        }

        foreach (var saved in snapshot.Abilities)
        {
            var ability = await db.CardAbilities.FindAsync(new object[] { saved.Id }, ct);
            if (ability != null && ability.CardId != cardId) return Conflict(new { error = "历史能力已移动到其他卡牌，请先处理能力归属" });
            var scopeCheck = await MirageCardPoolIsolationRules.ValidateAbilityJsonScopeAsync(db, saved.JsonConfig, currentIsMirage, ct);
            if (!scopeCheck.Valid)
            {
                return Conflict(new { error = $"历史版本能力配置校验未通过：{scopeCheck.ErrorMessage}" });
            }
        }

        db.Entry(card).CurrentValues.SetValues(snapshot.Card);
        card.IsMirageCard = currentIsMirage;
        if (currentIsMirage)
        {
            card.CardSet = GameConstants.Cards.MirageCardSetName;
            card.CardSetDefinitionId = currentSetId;
        }
        else if (string.Equals(card.CardSet, GameConstants.Cards.MirageCardSetName, StringComparison.OrdinalIgnoreCase))
        {
            card.CardSet = currentCardSet;
            card.CardSetDefinitionId = currentSetId;
        }
        card.UpdatedAt = DateTime.UtcNow; card.UpdatedBy = actor.Username;
        var existing = await db.CardAbilities.Where(a => a.CardId == cardId).ToListAsync(ct);
        var ids = snapshot.Abilities.Select(a => a.Id).ToHashSet();
        foreach (var removed in existing.Where(a => !ids.Contains(a.Id))) db.CardAbilities.Remove(removed);
        foreach (var saved in snapshot.Abilities)
        {
            var ability = await db.CardAbilities.FindAsync(new object[] { saved.Id }, ct);
            if (ability == null) { saved.CardDefinition = null; saved.CardId = cardId; db.CardAbilities.Add(saved); }
            else db.Entry(ability).CurrentValues.SetValues(saved);
        }
        db.RevisionOperation = "restore";
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        try
        {
            await games.NotifyCardDefinitionUpdatedAsync(cardId);
        }
        catch (Exception exception)
        {
            HttpContext.RequestServices.GetRequiredService<ILogger<CardHistoryController>>()
                .LogError(exception, "Card {CardId} restore was saved, but its cache refresh failed", cardId);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "卡牌回退已保存，但运行时缓存刷新失败，尚未生效。请勿重复提交，请排查服务端错误并刷新游戏数据缓存。",
                code = "CARD_RESTORE_CACHE_REFRESH_FAILED",
                saved = true,
                cardId,
                card = mapper.Map<CardDefinitionDto>(card)
            });
        }
        Response.Headers["X-Data-Revision"] = data.DataRevision.ToString();
        return Ok(mapper.Map<CardDefinitionDto>(card));
    }
}
