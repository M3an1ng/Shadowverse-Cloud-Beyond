using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutoMapper;
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
    private Task<CardRevisionRecord?> Entry(int cardId, long id, CancellationToken ct) => db.CardRevisions.SingleOrDefaultAsync(e => e.CardId == cardId && e.Id == id, ct);
    [HttpGet]
    public async Task<IActionResult> List(int cardId, CancellationToken ct)
    {
        if (await Actor(ct) == null) return StatusCode(403);
        if (!await db.CardDefinitions.AnyAsync(c => c.Id == cardId, ct)) return NotFound();
        if (!await db.CardRevisions.AnyAsync(h => h.CardId == cardId, ct))
        {
            var snapshot = await db.ReadCardConfigurationAsync(cardId, ct);
            db.CardRevisions.Add(new() { CardId = cardId, Revision = 1, Editor = snapshot!.Card.UpdatedBy ?? "", ModifiedAt = snapshot.Card.UpdatedAt ?? DateTime.UtcNow, Operation = "baseline", Snapshot = JsonSerializer.Serialize(snapshot, ApplicationDbContext.RevisionJson) });
            await db.SaveChangesAsync(ct);
        }
        var rows = await db.CardRevisions.AsNoTracking().Where(h => h.CardId == cardId).OrderByDescending(h => h.Revision).ToListAsync(ct);
        var revision = rows[0].Revision;
        return Ok(new { revision, entries = rows.Select(h => new { h.Id, h.Revision, h.Editor, h.ModifiedAt, h.Note, h.Operation, current = h.Revision == revision }) });
    }
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(int cardId, long id, CancellationToken ct)
    {
        if (await Actor(ct) == null) return StatusCode(403);
        var entry = await Entry(cardId, id, ct); if (entry == null) return NotFound();
        var snapshot = JsonSerializer.Deserialize<CardConfigurationSnapshot>(entry.Snapshot, ApplicationDbContext.RevisionJson)!;
        return Ok(new { card = mapper.Map<CardDefinitionDto>(snapshot.Card), abilities = snapshot.Abilities });
    }
    public sealed record NoteRequest(string Note);
    [HttpPatch("{id:long}")]
    public async Task<IActionResult> Annotate(int cardId, long id, NoteRequest request, CancellationToken ct)
    {
        if (await Actor(ct) == null) return StatusCode(403);
        if ((request.Note ?? "").Length > 2000) return BadRequest(new { error = "注释最多2000字" });
        var entry = await Entry(cardId, id, ct); if (entry == null) return NotFound();
        entry.Note = request.Note ?? ""; await db.SaveChangesAsync(ct); return Ok(new { ok = true });
    }
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(int cardId, long id, CancellationToken ct)
    {
        if (await Actor(ct) == null) return StatusCode(403);
        var entry = await Entry(cardId, id, ct); if (entry == null) return NotFound();
        if (entry.Revision == await db.CardRevisions.Where(h => h.CardId == cardId).MaxAsync(h => h.Revision, ct)) return Conflict(new { error = "不能删除当前版本" });
        db.CardRevisions.Remove(entry); await db.SaveChangesAsync(ct); return Ok(new { ok = true });
    }
    public sealed record RestoreRequest(int Revision);
    [HttpPost("{id:long}/restore")]
    public async Task<IActionResult> Restore(int cardId, long id, RestoreRequest request, CancellationToken ct)
    {
        var actor = await Actor(ct); if (actor == null) return StatusCode(403);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var currentRevision = await db.CardRevisions.Where(h => h.CardId == cardId).MaxAsync(h => h.Revision, ct);
        if (currentRevision != request.Revision) return Conflict(new { error = "卡牌已被其他窗口修改，请重新打开版本历史" });
        var entry = await Entry(cardId, id, ct); if (entry == null) return NotFound();
        if (entry.Revision == currentRevision) return Conflict(new { error = "该版本已在使用" });
        var snapshot = JsonSerializer.Deserialize<CardConfigurationSnapshot>(entry.Snapshot, ApplicationDbContext.RevisionJson)!;
        var card = await db.CardDefinitions.FindAsync(new object[] { cardId }, ct); if (card == null) return NotFound();
        db.Entry(card).CurrentValues.SetValues(snapshot.Card);
        card.UpdatedAt = DateTime.UtcNow; card.UpdatedBy = actor.Username;
        var existing = await db.CardAbilities.Where(a => a.CardId == cardId).ToListAsync(ct);
        var ids = snapshot.Abilities.Select(a => a.Id).ToHashSet();
        foreach (var removed in existing.Where(a => !ids.Contains(a.Id))) db.CardAbilities.Remove(removed);
        var remapped = new Dictionary<long, long>();
        foreach (var saved in snapshot.Abilities)
        {
            var ability = await db.CardAbilities.FindAsync(new object[] { saved.Id }, ct);
            if (ability != null && ability.CardId != cardId) return Conflict(new { error = "历史能力已移动到其他卡牌，请先处理能力归属" });
            if (ability == null) { saved.CardDefinition = null; db.CardAbilities.Add(saved); }
            else db.Entry(ability).CurrentValues.SetValues(saved);
        }
        db.RevisionOperation = "restore";
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        await games.NotifyCardDefinitionUpdatedAsync(cardId);
        Response.Headers["X-Data-Revision"] = data.DataRevision.ToString();
        return Ok(mapper.Map<CardDefinitionDto>(card));
    }
}
