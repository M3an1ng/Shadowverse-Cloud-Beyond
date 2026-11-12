using System.IO.Compression;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;

namespace ShadowverseBeyond.Server.Controllers;

[ApiController]
[Route("api/replay/{gameId}")]
public sealed class ReplayHistoryController(SqliteReplayHistory history,ReplayContinuation continuation) : ControllerBase
{
    [HttpGet("history/resume-points")]
    public Task<IActionResult> ResumePoints(string gameId) => Read(() => continuation.PointsAsync(gameId,HttpContext.RequestAborted));

    [HttpPost("history/resume")]
    public Task<IActionResult> Resume(string gameId,[FromBody] ReplayResumeRequest request)
        => Read(() => continuation.ResumeAsync(gameId,request,HttpContext.RequestAborted));

    [HttpGet("format")]
    public Task<IActionResult> Format(string gameId) => Read(async () => new { FormatVersion = await history.ReadFormatAsync(gameId,HttpContext.RequestAborted) });

    [HttpGet("history/manifest")]
    public Task<IActionResult> Manifest(string gameId) => Read(async () => await history.ManifestAsync(gameId,HttpContext.RequestAborted));

    [HttpGet("history/events")]
    public Task<IActionResult> Events(string gameId,[FromQuery] long fromSeq = 1,[FromQuery] long? throughSeq = null,[FromQuery] int limit = 100)
        => Read(async () => {
            var manifest = await history.ManifestAsync(gameId,HttpContext.RequestAborted);
            var through = throughSeq ?? manifest.HeadSeq;
            if (fromSeq < 1 || through < 0 || through > manifest.HeadSeq || limit is < 1 or > 1000) throw new ArgumentException("invalid_replay_event_range");
            return new { manifest, Events = fromSeq > through ? [] : await history.EventsAsync(gameId,fromSeq,through,limit,HttpContext.RequestAborted) };
        });

    [HttpGet("history/restore")]
    public Task<IActionResult> Restore(string gameId,[FromQuery] long atSeq = 0)
        => Read(async () => await history.RestoreAsync(gameId,atSeq,HttpContext.RequestAborted));

    [HttpGet("history/actions")]
    public Task<IActionResult> Actions(string gameId,[FromQuery] Guid? playerId = null,[FromQuery] int fromStep = 1)
        => Read(async () => {
            if (fromStep < 1) throw new ArgumentException("invalid_replay_operation_step");
            var manifest = await history.ManifestAsync(gameId,HttpContext.RequestAborted);
            var requested = await history.IndexAsync(gameId,"operation.requested",playerId,manifest.HeadSeq,HttpContext.RequestAborted);
            var completed = await history.IndexAsync(gameId,"operation.completed",playerId,manifest.HeadSeq,HttpContext.RequestAborted);
            return new { manifest, Actions = requested.Where(n => playerId == null || n.PlayerOperationNo >= fromStep).Select(n => new {
                n.ActionId,n.ActorPlayerId,n.Origin,n.PlayerOperationNo,n.TurnNo,n.TurnOwnerId,StartSeq = n.Seq,
                EndSeq = completed.SingleOrDefault(c => c.ActionId == n.ActionId)?.Seq,Request = n.Data }).ToArray() };
        });

    [HttpGet("history/turns")]
    public Task<IActionResult> Turns(string gameId) => Read(async () => {
        var manifest = await history.ManifestAsync(gameId,HttpContext.RequestAborted);
        var turns = (await history.IndexAsync(gameId,"TurnStart",null,manifest.HeadSeq,HttpContext.RequestAborted)).Where(n => n.Stage == "committed").ToArray();
        var initial = await history.RestoreAsync(gameId,0,HttpContext.RequestAborted);
        var initialTurnNo = initial.Document.GetProperty("players").EnumerateObject().Sum(p => p.Value.GetProperty("turnNumber").GetInt32());
        return new { manifest, Initial = new { TurnNo = initialTurnNo, StartSeq = 0, EndSeq = turns.Length > 0 ? turns[0].Seq-1 : manifest.HeadSeq },
            Turns = turns.Select((n,i) => new { n.TurnNo,n.TurnOwnerId,StartSeq = n.Seq,EndSeq = i+1 < turns.Length ? turns[i+1].Seq-1 : manifest.HeadSeq }).ToArray() };
    });

    [HttpGet("history/archive")]
    public Task<IActionResult> Archive(string gameId) => Read(async () => await history.ArchiveAsync(gameId,HttpContext.RequestAborted));

    [HttpGet("history/export")]
    public async Task<IActionResult> Export(string gameId)
    {
        try
        {
            var archive = await history.ArchiveAsync(gameId,HttpContext.RequestAborted);
            using var bytes = new MemoryStream();
            using (var gzip = new GZipStream(bytes,CompressionLevel.Optimal,leaveOpen:true))
                await JsonSerializer.SerializeAsync(gzip,archive,SqliteReplayFrames.Json,HttpContext.RequestAborted);
            return File(bytes.ToArray(),"application/gzip",$"replay-events-{gameId}.json.gz");
        }
        catch (Exception e) when (e is KeyNotFoundException or NotSupportedException or InvalidDataException or ArgumentException or JsonException)
        { return Failure(e); }
    }

    private async Task<IActionResult> Read(Func<Task<object>> read)
    {
        try { return Ok(await read()); }
        catch (Exception e) when (e is KeyNotFoundException or NotSupportedException or InvalidDataException or ArgumentException or JsonException)
        { return Failure(e); }
    }
    private IActionResult Failure(Exception error) => error switch
    {
        KeyNotFoundException => NotFound(new { Code = "replay_not_found",error.Message }),
        NotSupportedException => Conflict(new { Code = "unsupported_replay_capability",error.Message }),
        ArgumentException => BadRequest(new { Code = "invalid_replay_cursor",error.Message }),
        _ => UnprocessableEntity(new { Code = "invalid_replay_history",error.Message })
    };
}
