using Microsoft.AspNetCore.Mvc;
using ShadowverseBeyond.Server.Dtos.Replay;
using ShadowverseBeyond.Server.Infrastructure.Services;

namespace ShadowverseBeyond.Server.Controllers;

[ApiController]
[Route("api/replay/{gameId}")]
public sealed class ReplayFramesController(SqliteReplayFrames frames) : ControllerBase
{
    [HttpGet("manifest")]
    public async Task<IActionResult> Manifest(string gameId) => await Read(gameId, archive => archive.Manifest);

    [HttpGet("frames")]
    public async Task<IActionResult> Frames(string gameId, [FromQuery] long fromFrame = 0,
        [FromQuery] long? throughFrame = null, [FromQuery] int limit = 100)
    {
        if (fromFrame < 0 || limit is < 1 or > 500) return BadRequest(new { Code = "invalid_frame_range" });
        return await Read(gameId, archive => new { archive.FormatVersion, archive.Manifest,
            Frames = archive.Frames.Where(f => f.FrameNo >= fromFrame && f.FrameNo <= (throughFrame ?? archive.Manifest.HeadFrame)).Take(limit).ToArray() });
    }

    [HttpGet("frames/{frameNo:long}")]
    public async Task<IActionResult> Frame(string gameId, long frameNo) => await ReadValue(async () =>
        await frames.ReadFrameAsync(gameId, frameNo, HttpContext.RequestAborted));

    [HttpGet("archive")]
    public async Task<IActionResult> Archive(string gameId) => await Read(gameId, archive => archive);

    private async Task<IActionResult> Read(string gameId, Func<ReplayArchive, object> project)
        => await ReadValue(async () => project(await frames.ReadAsync(gameId, HttpContext.RequestAborted)));

    private async Task<IActionResult> ReadValue(Func<Task<object>> read)
    {
        try { return Ok(await read()); }
        catch (KeyNotFoundException ex) { return NotFound(new { Code = "replay_not_found", ex.Message }); }
        catch (NotSupportedException ex) { return Conflict(new { Code = "unsupported_replay_format", ex.Message }); }
        catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException)
        { return UnprocessableEntity(new { Code = "invalid_replay", ex.Message }); }
    }
}
