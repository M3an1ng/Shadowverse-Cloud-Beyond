using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Services;
namespace ShadowverseBeyond.Server.Controllers;
[ApiController, Route("api/take-two")]
public sealed class TakeTwoController(ApplicationDbContext db, TakeTwoService service) : ControllerBase
{
    private async Task<User?> Actor(CancellationToken ct) { if (!Guid.TryParse(Request.Headers["X-Editor-UserId"].FirstOrDefault(), out var id)) return null; return await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id && u.IsActive, ct); }
    private async Task<TakeTwoPool> Settings(string environment, CancellationToken ct) { var doc = await db.FeatureDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.Id == $"take-two:{environment}", ct); return doc == null ? TakeTwoService.EmptyPool : JsonSerializer.Deserialize<TakeTwoPool>(doc.Json, CardStyleCatalog.JsonOptions)!; }
    private static bool Valid(TakeTwoPool? pool) => pool != null && pool.Packs != null && pool.Packs.Length <= 500 && pool.Packs.All(p => p.Length <= 100) && pool.IncludedIds != null && pool.IncludedIds.Length <= 10000 && pool.ExcludedIds != null && pool.ExcludedIds.Length <= 10000 && !pool.IncludedIds.Intersect(pool.ExcludedIds).Any() && pool.IncludedIds.All(id => id > 0) && pool.ExcludedIds.All(id => id > 0) && pool.IncludedIds.Distinct().Count() == pool.IncludedIds.Length && pool.ExcludedIds.Distinct().Count() == pool.ExcludedIds.Length && pool.NeutralByClass != null && pool.NeutralByClass.Count <= 7 && pool.NeutralByClass.All(p => Enum.TryParse<ShadowverseBeyond.Server.Dtos.Enums.CardClass>(p.Key, out var c) && Enum.IsDefined(c) && c != 0 && p.Value != null && p.Value.Length <= 10000 && p.Value.All(id => id > 0) && p.Value.Distinct().Count() == p.Value.Length) && pool.Redraws != null && pool.Redraws.All(p => Enum.TryParse<ShadowverseBeyond.Server.Dtos.Enums.CardClass>(p.Key, out var c) && c != 0 && p.Value >= 0 && p.Value <= 20);
    [HttpGet("settings/{environment}")] public async Task<IActionResult> GetSettings(string environment, CancellationToken ct) => environment is "rotation" or "unlimited" or "as2p" ? Ok(await Settings(environment, ct)) : BadRequest();
    public sealed record PoolUpdate(int Revision, TakeTwoPool Pool);
    [HttpPut("settings/{environment}")] public async Task<IActionResult> SaveSettings(string environment, PoolUpdate request, CancellationToken ct) { var actor = await Actor(ct); if (actor?.Role != UserRole.Admin) return StatusCode(403); if (environment is not ("rotation" or "unlimited" or "as2p") || !Valid(request.Pool)) return BadRequest(); if (environment == "as2p") { var ids = request.Pool.NeutralByClass.Values.SelectMany(x => x).Distinct().ToArray(); var count = await db.CardDefinitions.CountAsync(c => ids.Contains(c.Id) && c.Class == ShadowverseBeyond.Server.Dtos.Enums.CardClass.Neutral && c.IsCollectible && (c.Type == ShadowverseBeyond.Server.Dtos.Enums.CardType.Follower || c.Type == ShadowverseBeyond.Server.Dtos.Enums.CardType.Spell || c.Type == ShadowverseBeyond.Server.Dtos.Enums.CardType.Amulet), ct); if (count != ids.Length) return BadRequest(new { error = "职业专属补充只能选择可收集的中立卡" }); } return await Store($"take-two:{environment}", request.Pool, request.Revision, ct); }
    [HttpGet("settings/{environment}/revision")] public async Task<IActionResult> SettingsRevision(string environment, CancellationToken ct) => Ok(new { revision = (await db.FeatureDocuments.FindAsync(new object[] { $"take-two:{environment}" }, ct))?.Revision ?? 0 });
    private async Task<IActionResult> Store(string key, object data, int? revision, CancellationToken ct) { var doc = await db.FeatureDocuments.FindAsync(new object[] { key }, ct); if (revision.HasValue && revision != (doc?.Revision ?? 0)) return Conflict(new { error = "配置已更新，请重新打开" }); if (doc == null) { doc = new() { Id = key }; db.Add(doc); } doc.Json = JsonSerializer.Serialize(data, CardStyleCatalog.JsonOptions); doc.Revision++; doc.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); return Ok(new { revision = doc.Revision }); }
    [HttpGet("saved-pools")] public async Task<IActionResult> SavedPools(CancellationToken ct) { var actor = await Actor(ct); if (actor == null) return StatusCode(403); var prefix = $"take-two-saved:{actor.Id}:"; var docs = await db.FeatureDocuments.AsNoTracking().Where(d => d.Id.StartsWith(prefix)).ToListAsync(ct); return Ok(docs.Select(d => new { id = d.Id[prefix.Length..], config = JsonSerializer.Deserialize<JsonElement>(d.Json), d.Revision })); }
    public sealed record SavedPool(string Name, TakeTwoPool Pool);
    [HttpPut("saved-pools/{id:guid}")] public async Task<IActionResult> SavePool(Guid id, SavedPool request, CancellationToken ct) { var actor = await Actor(ct); if (actor == null) return StatusCode(403); if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 60 || !Valid(request.Pool)) return BadRequest(); return await Store($"take-two-saved:{actor.Id}:{id}", request, null, ct); }
    [HttpDelete("saved-pools/{id:guid}")] public async Task<IActionResult> DeletePool(Guid id, CancellationToken ct) { var actor = await Actor(ct); if (actor == null) return StatusCode(403); var doc = await db.FeatureDocuments.FindAsync(new object[] { $"take-two-saved:{actor.Id}:{id}" }, ct); if (doc != null) { db.Remove(doc); await db.SaveChangesAsync(ct); } return Ok(new { ok = true }); }
    [HttpGet("pool/{environment}")] public async Task<IActionResult> Pool(string environment, CancellationToken ct, [FromQuery] string? profession = null) { if (environment is not ("rotation" or "unlimited" or "as2p")) return BadRequest(); var all = await db.CardDefinitions.AsNoTracking().Include(c => c.CardSetDefinition).ToListAsync(ct); var settings = await Settings(environment, ct); var cards = TakeTwoService.PoolCards(all, environment, settings); if (profession != null) { if (!Enum.TryParse<ShadowverseBeyond.Server.Dtos.Enums.CardClass>(profession, out var cls) || !Enum.IsDefined(cls) || cls == 0) return BadRequest(); cards = TakeTwoService.ClassPool(cards, environment, settings, cls); } return Ok(cards.Select(c => c.Id)); }
    public sealed record CreateRequest(string Environment, TakeTwoPool? Pool);
    [HttpPost("rooms")] public async Task<IActionResult> Create(CreateRequest request, CancellationToken ct) { var actor = await Actor(ct); if (actor == null) return StatusCode(403); if (request.Environment is not ("rotation" or "unlimited" or "as2p" or "open") || request.Environment == "open" && !Valid(request.Pool)) return BadRequest(); try { var settings = request.Environment == "open" ? request.Pool! : await Settings(request.Environment, ct); var all = await db.CardDefinitions.AsNoTracking().Include(c => c.CardSetDefinition).ToListAsync(ct); var room = service.Create(actor.Id, request.Environment, settings, all); return Ok(service.View(room, room.Players[0])); } catch (InvalidOperationException e) { return BadRequest(new { error = e.Message }); } }
    [HttpPut("rooms/{id}/pool")]
    public async Task<IActionResult> UpdatePool(string id, CreateRequest request, CancellationToken ct)
    {
        var actor = await Actor(ct); if (actor == null) return StatusCode(403);
        if (request.Environment is not ("rotation" or "unlimited" or "as2p" or "open") || request.Environment == "open" && !Valid(request.Pool)) return BadRequest();
        try {
            var room = service.Find(id);
            var seat = service.Authorize(room, actor.Id, Request.Headers["X-Take-Two-Token"].FirstOrDefault() ?? "");
            var settings = request.Environment == "open" ? request.Pool! : await Settings(request.Environment, ct);
            var all = await db.CardDefinitions.AsNoTracking().Include(c => c.CardSetDefinition).ToListAsync(ct);
            service.UpdatePool(room, seat, request.Environment, settings, all);
            return Ok(service.View(room, seat));
        } catch (UnauthorizedAccessException) { return StatusCode(403); }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
    }
    public sealed record JoinRequest(string Code, string? RoomId = null);
    [HttpPost("join")] public async Task<IActionResult> Join(JoinRequest request, CancellationToken ct) { var actor = await Actor(ct); if (actor == null) return StatusCode(403); try { if (string.IsNullOrWhiteSpace(request.RoomId) || string.IsNullOrWhiteSpace(request.Code)) return BadRequest(new { error = "请输入 GameId 与 JoinCode" }); if (request.RoomId != null) { var requested = service.Find(request.RoomId.Replace("-", "")); if (requested.Code != request.Code) return BadRequest(new { error = "邀请码不正确" }); } var room = service.Join(request.Code, actor.Id); return Ok(service.View(room, room.Players.Single(p => p.PlayerId == actor.Id))); } catch (InvalidOperationException e) { return BadRequest(new { error = e.Message }); } }
    [HttpGet("rooms/{id}/kind")] public IActionResult Kind(string id) { try { var room = service.Find(id.Replace("-", "")); return Ok(new { takeTwo = true, roomId = room.Id }); } catch (InvalidOperationException) { return NotFound(); } }
    [HttpPost("rooms/{id}/local-test")]
    public async Task<IActionResult> LocalTest(string id, CancellationToken ct)
    {
        var actor = await Actor(ct);
        if (actor == null) return StatusCode(403);
        try
        {
            var room = service.Find(id);
            var guest = service.JoinLocalTest(room, actor.Id, Request.Headers["X-Take-Two-Token"].FirstOrDefault() ?? "");
            return Ok(service.View(room, guest));
        }
        catch (UnauthorizedAccessException) { return StatusCode(403); }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
    }
    [HttpGet("rooms/{id}")] public async Task<IActionResult> Get(string id, CancellationToken ct, [FromQuery] bool includePlans = true) { var actor = await Actor(ct); if (actor == null) return StatusCode(403); try { var room = service.Find(id); var seat = service.Authorize(room, actor.Id, Request.Headers["X-Take-Two-Token"].FirstOrDefault() ?? ""); return Ok(service.View(room, seat, includePlans)); } catch (UnauthorizedAccessException) { return StatusCode(403); } catch (InvalidOperationException e) { return NotFound(new { error = e.Message }); } }
    [HttpPost("rooms/{id}/leave")]
    public async Task<IActionResult> Leave(string id, CancellationToken ct)
    {
        var actor = await Actor(ct);
        if (actor == null) return StatusCode(403);
        try
        {
            var room = service.Find(id);
            service.Authorize(room, actor.Id, Request.Headers["X-Take-Two-Token"].FirstOrDefault() ?? "");
            service.Leave(room);
            return Ok(new { ok = true });
        }
        catch (UnauthorizedAccessException) { return StatusCode(403); }
        catch (InvalidOperationException) { return Ok(new { ok = true }); } // An expired room is already left.
    }
    [HttpPost("rooms/leave-current")]
    public async Task<IActionResult> LeaveCurrent(CancellationToken ct)
    {
        var actor = await Actor(ct); if (actor == null) return StatusCode(403);
        service.LeaveCurrent(actor.Id);
        return Ok(new { ok = true });
    }
    public sealed record ActionRequest(string Action, int Sequence, string? Class, int Side = 0);
    [HttpPost("rooms/{id}/action")] public async Task<IActionResult> Act(string id, ActionRequest request, CancellationToken ct) { var actor = await Actor(ct); if (actor == null) return StatusCode(403); try { var room = service.Find(id); var seat = service.Authorize(room, actor.Id, Request.Headers["X-Take-Two-Token"].FirstOrDefault() ?? ""); service.Act(room, seat, request.Action, request.Sequence, request.Class, request.Side); return Ok(service.View(room, seat)); } catch (UnauthorizedAccessException) { return StatusCode(403); } catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); } }
}
