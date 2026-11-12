using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Dtos.Replay;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Persistence.Entities;

namespace ShadowverseBeyond.Server.Infrastructure.Services;

// V2 uses the existing game/snapshot tables. AtSeq is a frame number only for SchemaVersion=2.
public sealed class SqliteReplayFrames(DbContextOptions<ApplicationDbContext> options)
{
    public const int FormatVersion = 2;
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task AppendAsync(ReplayFrame frame, Guid host, Guid guest, CancellationToken cancellationToken)
    {
        Validate(frame);
        await using var db = new ApplicationDbContext(options);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var game = await db.ReplayGames.SingleOrDefaultAsync(g => g.GameId == frame.GameId, cancellationToken);
        var previous = await db.ReplaySnapshots.AsNoTracking().Where(s => s.GameId == frame.GameId)
            .MaxAsync(s => (long?)s.AtSeq, cancellationToken);
        if (previous != (frame.FrameNo == 0 ? null : frame.FrameNo - 1))
            throw new InvalidOperationException($"replay_head_conflict: {frame.GameId}/{frame.FrameNo}");
        if (game is null)
        {
            if (frame.FrameNo != 0 || frame.Boundary != "initial") throw new InvalidOperationException("replay_missing_initial_frame");
            game = new ReplayGameRecord { GameId = frame.GameId, CreatedAtUtc = frame.RecordedAtUtc,
                HostPlayerId = host.ToString(), GuestPlayerId = guest.ToString(), SchemaVersion = FormatVersion,
                ServerBuild = typeof(SqliteReplayFrames).Assembly.GetName().Version?.ToString() };
            db.ReplayGames.Add(game);
        }
        if (game.SchemaVersion != FormatVersion || game.EndedAtUtc != null)
            throw new InvalidOperationException("replay_not_open_for_append");
        if (frame.Boundary is "ended" or "interrupted")
        {
            game.EndedAtUtc = frame.RecordedAtUtc;
            game.WinnerPlayerId = frame.State.GetProperty("winnerId").GetString();
        }
        using var bytes = new MemoryStream();
        using (var gzip = new GZipStream(bytes, CompressionLevel.Optimal, leaveOpen: true))
            await JsonSerializer.SerializeAsync(gzip, frame, Json, cancellationToken);
        db.ReplaySnapshots.Add(new ReplaySnapshotRecord { GameId = frame.GameId, AtSeq = frame.FrameNo,
            CreatedAtUtc = frame.RecordedAtUtc, SchemaVersion = FormatVersion, Compression = "gzip",
            PayloadHash = "", Payload = bytes.ToArray() });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ReplayArchive> ReadAsync(string gameId, CancellationToken cancellationToken)
    {
        await using var db = new ApplicationDbContext(options);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var game = await db.ReplayGames.AsNoTracking().SingleOrDefaultAsync(g => g.GameId == gameId, cancellationToken)
            ?? throw new KeyNotFoundException("replay_not_found: " + gameId);
        if (game.SchemaVersion != FormatVersion) throw new NotSupportedException($"unsupported_replay_format: {game.SchemaVersion}");
        var rows = await db.ReplaySnapshots.AsNoTracking().Where(s => s.GameId == gameId).OrderBy(s => s.AtSeq).ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var frames = rows.Select(Decode).ToArray();
        if (frames.Length == 0 || frames[0].Boundary != "initial") throw new InvalidDataException("replay_missing_initial_frame");
        for (var i = 0; i < frames.Length; i++)
            if (frames[i].FrameNo != i || frames[i].GameId != gameId) throw new InvalidDataException($"replay_frame_gap: {gameId}/{i}");
        var last = frames[^1];
        var status = last.Boundary == "ended" ? "closed" : last.Boundary == "interrupted" ? "interrupted" : "recording";
        if ((game.EndedAtUtc != null) != (status is "closed" or "interrupted")) throw new InvalidDataException("replay_terminal_metadata_mismatch");
        return new ReplayArchive(FormatVersion,
            new ReplayManifest(FormatVersion, gameId, last.FrameNo, frames.Length, status, last.SampleKind, game.WinnerPlayerId,
                game.CreatedAtUtc, game.EndedAtUtc, game.ServerBuild, game.HostPlayerId, game.GuestPlayerId), frames);
    }

    public async Task<bool> IsCurrentFormatAsync(string gameId, CancellationToken cancellationToken)
    {
        await using var db = new ApplicationDbContext(options);
        return await db.ReplayGames.AsNoTracking().AnyAsync(g => g.GameId == gameId && g.SchemaVersion == FormatVersion, cancellationToken);
    }

    public async Task<ReplayFrame> ReadFrameAsync(string gameId, long frameNo, CancellationToken cancellationToken)
    {
        await using var db = new ApplicationDbContext(options);
        var row = await db.ReplaySnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.GameId == gameId && s.AtSeq == frameNo, cancellationToken)
            ?? throw new KeyNotFoundException("replay_frame_not_found");
        return Decode(row);
    }

    public async Task RecoverInterruptedAsync(CancellationToken cancellationToken)
    {
        await using var db = new ApplicationDbContext(options);
        var games = await db.ReplayGames.AsNoTracking().Where(g => g.SchemaVersion == FormatVersion && g.EndedAtUtc == null)
            .Select(g => new { g.GameId, g.HostPlayerId, g.GuestPlayerId }).ToListAsync(cancellationToken);
        foreach (var game in games)
        {
            var archive = await ReadAsync(game.GameId, cancellationToken);
            var last = archive.Frames[^1];
            await AppendAsync(last with { FrameNo = last.FrameNo + 1, CaptureId = Guid.NewGuid(), ActionId = null,
                Boundary = "interrupted", ActionStatus = "interrupted", Reason = "server_restarted_last_durable_state_only",
                RecordedAtUtc = DateTime.UtcNow, Messages = [], PendingChoices = [] },
                Guid.Parse(game.HostPlayerId!), Guid.Parse(game.GuestPlayerId!), cancellationToken);
        }
    }

    private static ReplayFrame Decode(ReplaySnapshotRecord row)
    {
        if (row.SchemaVersion != FormatVersion || row.Compression != "gzip") throw new InvalidDataException("unsupported_replay_frame_encoding");
        using var bytes = new MemoryStream(row.Payload);
        using var gzip = new GZipStream(bytes, CompressionMode.Decompress);
        var frame = JsonSerializer.Deserialize<ReplayFrame>(gzip, Json) ?? throw new InvalidDataException("replay_frame_is_null");
        Validate(frame);
        if (frame.FrameNo != row.AtSeq) throw new InvalidDataException("replay_frame_index_mismatch");
        return frame;
    }

    private static void Validate(ReplayFrame frame)
    {
        if (frame.FormatVersion != FormatVersion || frame.FrameNo < 0 || string.IsNullOrWhiteSpace(frame.GameId)
            || frame.State.ValueKind != JsonValueKind.Object || frame.Messages is null || frame.PendingChoices is null)
            throw new InvalidDataException("invalid_replay_frame");
        if (!frame.State.TryGetProperty("gameId", out var gameId) || gameId.ValueKind != JsonValueKind.String || gameId.GetString() != frame.GameId
            || !frame.State.TryGetProperty("players", out var players) || players.ValueKind != JsonValueKind.Array || players.GetArrayLength() != 2
            || !frame.State.TryGetProperty("isGameOver", out var gameOver) || gameOver.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidDataException("replay_state_identity_mismatch");
        if (frame.Boundary is not ("initial" or "waiting_for_choice" or "completed_action" or "partial_action" or "ended" or "interrupted"))
            throw new InvalidDataException("unsupported_replay_boundary");
        if (frame.SampleKind is not ("standard" or "debug" or "unknown"))
            throw new InvalidDataException("invalid_replay_sample_kind");
        if (frame.Boundary == "ended" && !gameOver.GetBoolean())
            throw new InvalidDataException("replay_end_without_game_over");
    }
}
