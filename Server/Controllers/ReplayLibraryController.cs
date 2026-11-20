using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Persistence.Entities;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Options;
using ShadowverseBeyond.Server.Services;

namespace ShadowverseBeyond.Server.Controllers;

[ApiController]
[Route("api/replays")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReplayLibraryController(ApplicationDbContext db, ReplaySeatTicket tickets,
    SqliteReplayHistory history, SqliteReplayFrames frames, IOptions<ReplayOptions> options) : ControllerBase
{
    public sealed record LibraryRequest(string[]? Tickets, int Offset = 0);
    private const int PageSize = 24;

    private async Task<string?> AccountIdAsync()
    {
        var session = await HttpContext.AuthenticateAsync(AccountSession.Scheme);
        if (session.Principal is not ClaimsPrincipal principal) return null;
        var auth = HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(principal, HttpContext, AccountSession.PlayerPolicy);
        return result.Succeeded ? AccountSession.GetUserId(principal)?.ToString() : null;
    }

    private IQueryable<ReplayGameRecord> Finished() => db.ReplayGames.AsNoTracking().Where(game =>
        game.EndedAtUtc != null && (game.SchemaVersion == SqliteReplayFrames.FormatVersion ||
        (game.SchemaVersion == SqliteReplayHistory.FormatVersion &&
        db.ReplayEvents.Any(e => e.GameId == game.GameId && e.EventType == "recording.closed"))));

    [HttpPost("library")]
    public async Task<IActionResult> Library(LibraryRequest request, CancellationToken cancel)
    {
        if (request.Offset < 0 || request.Tickets?.Length > 256) return BadRequest();
        var account = await AccountIdAsync();
        var seats = new Dictionary<string, string>();
        foreach (var ticket in request.Tickets ?? [])
            if (tickets.Read(ticket) is { } seat) seats[seat.GameId] = seat.PlayerId;
        var ids = seats.Keys.ToArray();
        var rows = await Finished().Where(g => (account != null && (g.HostPlayerId == account || g.GuestPlayerId == account))
                || ids.Contains(g.GameId))
            .OrderByDescending(g => g.EndedAtUtc).ThenBy(g => g.GameId)
            .Skip(request.Offset).Take(PageSize + 1).ToListAsync(cancel);
        var playerIds = rows.SelectMany(g => new[] { g.HostPlayerId, g.GuestPlayerId })
            .Where(id => Guid.TryParse(id, out _)).Select(id => Guid.Parse(id!)).Distinct().ToArray();
        var users = await db.Users.AsNoTracking().Where(u => playerIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username }).ToDictionaryAsync(u => u.Id.ToString(), u => u.Username, cancel);
        var items = new List<object>();
        foreach (var game in rows.Take(PageSize))
        {
            if (game.SchemaVersion == SqliteReplayFrames.FormatVersion)
            {
                var last = await db.ReplaySnapshots.Where(s => s.GameId == game.GameId).MaxAsync(s => (long?)s.AtSeq, cancel);
                if (last == null || (await frames.ReadFrameAsync(game.GameId, last.Value, cancel)).Boundary != "ended") continue;
            }
            var viewer = account == game.HostPlayerId || account == game.GuestPlayerId ? account : seats.GetValueOrDefault(game.GameId);
            items.Add(new { game.GameId, game.EndedAtUtc, game.WinnerPlayerId, viewerPlayerId = viewer, game.HostPlayerId,
                hostName = users.GetValueOrDefault(game.HostPlayerId ?? "") ?? "房主",
                guestName = users.GetValueOrDefault(game.GuestPlayerId ?? "") ?? "客方" });
        }
        return Ok(new { items, nextOffset = rows.Count > PageSize ? (int?)(request.Offset + PageSize) : null,
            recordingEnabled = options.Value.EnableJournalWrites });
    }

    [HttpGet("{id:guid}/playback")]
    public async Task<IActionResult> Playback(Guid id, CancellationToken cancel)
    {
        var gameId = id.ToString();
        var game = await Finished().SingleOrDefaultAsync(g => g.GameId == gameId, cancel);
        if (game == null) return NotFound();
        var account = await AccountIdAsync();
        var seat = tickets.Read(Request.Headers["X-Replay-Ticket"].FirstOrDefault());
        var player = seat?.GameId == gameId ? seat?.PlayerId : null;
        if (!(account != null && (game.HostPlayerId == account || game.GuestPlayerId == account))
            && !(player != null && (game.HostPlayerId == player || game.GuestPlayerId == player))) return NotFound();
        try
        {
            if (game.SchemaVersion == SqliteReplayFrames.FormatVersion)
            {
                var archive = await frames.ReadAsync(gameId, cancel);
                if (archive.Manifest.Status != "closed") return NotFound();
                return new JsonResult(new { frames = archive.Frames.Select(f => new { f.State, f.Boundary,
                    effects = f.Messages.Select(m => ReplayVisual.FromMessage(m.Method, m.Payload, m.Audience)).OfType<ReplayVisual>()
                }) }, SqliteReplayFrames.Json);
            }
            return new JsonResult(await ReplayPlayback.ReadAsync(history, gameId, cancel), SqliteReplayFrames.Json);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidDataException) { return UnprocessableEntity(new { message = "这局回放记录不完整。" }); }
    }
}
