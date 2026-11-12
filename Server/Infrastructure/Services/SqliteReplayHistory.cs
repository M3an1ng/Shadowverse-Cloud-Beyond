using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Dtos.Replay;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Persistence.Entities;
using ShadowverseBeyond.Server.Services;

namespace ShadowverseBeyond.Server.Infrastructure.Services;

public sealed class SqliteReplayHistory(DbContextOptions<ApplicationDbContext> options)
{
    public const int FormatVersion = 3;

    public async Task AppendAsync(string gameId, Guid host, Guid guest, long expectedHead,
        IReadOnlyList<ReplayHistoryNode> nodes, IReadOnlyList<ReplayHistoryCheckpoint> checkpoints,
        CancellationToken cancellationToken, JsonElement? executionRules = null)
    {
        for (var i = 0; i < nodes.Count; i++)
            if (nodes[i].GameId != gameId || nodes[i].FormatVersion != FormatVersion || nodes[i].Seq != expectedHead + i + 1
                || nodes[i].PreviousSeq != nodes[i].Seq - 1)
                throw new InvalidDataException("invalid_replay_event_sequence");
        var encoded = nodes.Select(node => JsonSerializer.Serialize(node, SqliteReplayFrames.Json)).ToArray();
        var snapshots = new List<ReplaySnapshotRecord>();
        foreach (var checkpoint in checkpoints)
        {
            if (checkpoint.FormatVersion != FormatVersion || checkpoint.GameId != gameId || checkpoint.AtSeq < 0)
                throw new InvalidDataException("invalid_replay_checkpoint");
            using var bytes = new MemoryStream();
            using (var gzip = new GZipStream(bytes, CompressionLevel.Optimal, leaveOpen: true))
                await JsonSerializer.SerializeAsync(gzip, checkpoint, SqliteReplayFrames.Json, cancellationToken);
            snapshots.Add(new ReplaySnapshotRecord { GameId = gameId, AtSeq = checkpoint.AtSeq, SchemaVersion = FormatVersion,
                CreatedAtUtc = DateTime.UtcNow, Compression = "gzip", PayloadHash = "", Payload = bytes.ToArray() });
        }
        await using var db = new ApplicationDbContext(options);
        if (executionRules is { } rules)
        {
            using var bytes = new MemoryStream();
            using (var gzip = new GZipStream(bytes,CompressionLevel.Optimal,leaveOpen:true))
                await JsonSerializer.SerializeAsync(gzip,rules,SqliteReplayFrames.Json,cancellationToken);
            snapshots.Add(new ReplaySnapshotRecord { GameId = gameId,AtSeq = -1,SchemaVersion = 4,CreatedAtUtc = DateTime.UtcNow,
                Compression = "gzip",PayloadHash = "",Payload = bytes.ToArray() });
        }
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var game = await db.ReplayGames.SingleOrDefaultAsync(g => g.GameId == gameId, cancellationToken);
        var head = await db.ReplayEvents.Where(e => e.GameId == gameId).MaxAsync(e => (long?)e.Seq, cancellationToken) ?? 0;
        if (head != expectedHead) throw new InvalidOperationException("replay_head_conflict");
        if (game is null)
        {
            if (expectedHead != 0 || !checkpoints.Any(c => c.AtSeq == 0)) throw new InvalidOperationException("replay_missing_initial_checkpoint");
            game = new ReplayGameRecord { GameId = gameId, CreatedAtUtc = DateTime.UtcNow, SchemaVersion = FormatVersion,
                HostPlayerId = host.ToString(), GuestPlayerId = guest.ToString(),
                ServerBuild = executionRules != null ? "resume1/" + ReplayExecutionState.EngineBuild : typeof(SqliteReplayHistory).Assembly.GetName().Version?.ToString() };
            db.ReplayGames.Add(game);
        }
        else if (game.SchemaVersion != FormatVersion || game.EndedAtUtc != null)
            throw new InvalidOperationException("replay_not_open_for_append");
        if (nodes.Count > 0)
        {
            var bundleId = Guid.NewGuid();
            var bundleSeq = (await db.ReplayBundles.Where(b => b.GameId == gameId).MaxAsync(b => (long?)b.BundleSeq, cancellationToken) ?? 0) + 1;
            db.ReplayBundles.Add(new ReplayBundleRecord { GameId = gameId, BundleId = bundleId, BundleSeq = bundleSeq,
                StartSeq = nodes[0].Seq, EndSeq = nodes[^1].Seq, EventCount = nodes.Count, CommittedAtUtc = DateTime.UtcNow });
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                db.ReplayEvents.Add(new ReplayEventRecord { GameId = gameId, Seq = node.Seq, BundleId = bundleId, EventIndex = i,
                    EventId = node.NodeId, EventType = node.EventType, TimestampUtc = node.RecordedAtUtc, SchemaVersion = FormatVersion,
                    SourceId = node.ActorPlayerId, RawEventJson = encoded[i] });
            }
            if (nodes[^1].EventType is "recording.closed" or "recording.interrupted")
            {
                game.EndedAtUtc = nodes[^1].RecordedAtUtc;
                game.WinnerPlayerId = nodes[^1].Data.TryGetProperty("winnerId", out var winner) ? winner.GetString() : null;
            }
        }
        db.ReplaySnapshots.AddRange(snapshots);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<int> ReadFormatAsync(string gameId, CancellationToken cancellationToken)
    {
        await using var db = new ApplicationDbContext(options);
        return await db.ReplayGames.AsNoTracking().Where(g => g.GameId == gameId).Select(g => (int?)g.SchemaVersion).SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("replay_not_found");
    }

    public async Task<bool> IsCurrentFormatAsync(string gameId, CancellationToken cancellationToken)
    {
        await using var db = new ApplicationDbContext(options);
        return await db.ReplayGames.AsNoTracking().AnyAsync(g => g.GameId == gameId && g.SchemaVersion == FormatVersion, cancellationToken);
    }

    public async Task<ReplayHistoryManifest> ManifestAsync(string gameId, CancellationToken cancellationToken)
    {
        await using var db = new ApplicationDbContext(options);
        await using var read = await db.Database.BeginTransactionAsync(cancellationToken);
        var game = await db.ReplayGames.AsNoTracking().SingleOrDefaultAsync(g => g.GameId == gameId, cancellationToken)
            ?? throw new KeyNotFoundException("replay_not_found");
        if (game.SchemaVersion != FormatVersion) throw new NotSupportedException("event_history_not_available_for_this_format");
        var lastRow = await db.ReplayEvents.AsNoTracking().Where(e => e.GameId == gameId).OrderByDescending(e => e.Seq).FirstOrDefaultAsync(cancellationToken);
        var last = lastRow is null ? null : Decode(lastRow);
        var count = await db.ReplayEvents.CountAsync(e => e.GameId == gameId, cancellationToken);
        var checkpoints = await db.ReplaySnapshots.CountAsync(c => c.GameId == gameId && c.SchemaVersion == FormatVersion, cancellationToken);
        if (count != (last?.Seq ?? 0) || checkpoints == 0) throw new InvalidDataException("replay_index_incomplete");
        await read.CommitAsync(cancellationToken);
        var status = last?.EventType == "recording.closed" ? "closed" : last?.EventType == "recording.interrupted" ? "interrupted" : "recording";
        return new ReplayHistoryManifest(FormatVersion, gameId, "event_seq", last?.Seq ?? 0, count, checkpoints,
            status, last?.SampleKind ?? "standard", game.WinnerPlayerId, game.CreatedAtUtc, game.EndedAtUtc,
            game.ServerBuild, game.HostPlayerId, game.GuestPlayerId,
            new ReplayRecoveryCapabilities(true, game.ServerBuild?.StartsWith("resume1/",StringComparison.Ordinal) == true,
                game.ServerBuild?.StartsWith("resume1/",StringComparison.Ordinal) == true ? "idle_operation_boundaries_only_new_random_future" : "execution_checkpoint_not_recorded"));
    }

    public async Task<IReadOnlyList<ReplayHistoryNode>> EventsAsync(string gameId, long fromSeq, long throughSeq, int limit, CancellationToken cancellationToken)
    {
        if (fromSeq < 1 || throughSeq < fromSeq || limit is < 1 or > 1000) throw new ArgumentException("invalid_replay_event_range");
        await using var db = new ApplicationDbContext(options);
        var rows = await db.ReplayEvents.AsNoTracking().Where(e => e.GameId == gameId && e.Seq >= fromSeq && e.Seq <= throughSeq)
            .OrderBy(e => e.Seq).Take(limit).ToListAsync(cancellationToken);
        var nodes = rows.Select(Decode).ToArray();
        for (var i = 0; i < nodes.Length; i++)
            if (nodes[i].Seq != fromSeq + i) throw new InvalidDataException("replay_event_gap");
        return nodes;
    }

    public async Task<ReplayHistoryRestore> RestoreAsync(string gameId, long atSeq, CancellationToken cancellationToken)
    {
        var manifest = await ManifestAsync(gameId, cancellationToken);
        if (atSeq < 0 || atSeq > manifest.HeadSeq) throw new ArgumentException("replay_event_out_of_range");
        await using var db = new ApplicationDbContext(options);
        var row = await db.ReplaySnapshots.AsNoTracking().Where(s => s.GameId == gameId && s.SchemaVersion == FormatVersion && s.AtSeq <= atSeq)
            .OrderByDescending(s => s.AtSeq).FirstOrDefaultAsync(cancellationToken) ?? throw new InvalidDataException("replay_checkpoint_missing");
        var checkpoint = DecodeCheckpoint(row);
        var document = JsonNode.Parse(checkpoint.Document.GetRawText())!.AsObject();
        ReplayHistoryNode? last = null;
        for (var next = checkpoint.AtSeq + 1; next <= atSeq;)
        {
            var nodes = await EventsAsync(gameId, next, atSeq, 1000, cancellationToken);
            if (nodes.Count == 0) throw new InvalidDataException("replay_event_gap");
            foreach (var node in nodes) { ReplayStateChanges.Apply(document, node.Changes); last = node; }
            next = nodes[^1].Seq + 1;
        }
        if (last is null && atSeq > 0)
            last = (await EventsAsync(gameId, atSeq, atSeq, 1, cancellationToken)).Single();
        return new ReplayHistoryRestore(FormatVersion, gameId, atSeq, checkpoint.AtSeq,
            JsonSerializer.SerializeToElement(document, SqliteReplayFrames.Json), last);
    }

    public async Task<IReadOnlyList<ReplayHistoryNode>> IndexAsync(string gameId, string eventType, Guid? playerId, long throughSeq, CancellationToken cancellationToken)
    {
        await using var db = new ApplicationDbContext(options);
        var query = db.ReplayEvents.AsNoTracking().Where(e => e.GameId == gameId && e.SchemaVersion == FormatVersion && e.EventType == eventType && e.Seq <= throughSeq);
        if (playerId != null) query = query.Where(e => e.SourceId == playerId);
        return (await query.OrderBy(e => e.Seq).ToListAsync(cancellationToken)).Select(Decode).ToArray();
    }

    public async Task<ReplayHistoryArchive> ArchiveAsync(string gameId, CancellationToken cancellationToken)
    {
        var manifest = await ManifestAsync(gameId, cancellationToken);
        await using var db = new ApplicationDbContext(options);
        var checkpoints = (await db.ReplaySnapshots.AsNoTracking().Where(s => s.GameId == gameId && s.SchemaVersion == FormatVersion && s.AtSeq <= manifest.HeadSeq)
            .OrderBy(s => s.AtSeq).ToListAsync(cancellationToken)).Select(DecodeCheckpoint).ToArray();
        var nodes = new List<ReplayHistoryNode>();
        for (var next = 1L; next <= manifest.HeadSeq;)
        {
            var page = await EventsAsync(gameId, next, manifest.HeadSeq, 1000, cancellationToken);
            if (page.Count == 0) throw new InvalidDataException("replay_export_gap");
            nodes.AddRange(page); next = page[^1].Seq + 1;
        }
        if (checkpoints.Length == 0 || checkpoints[0].AtSeq != 0) throw new InvalidDataException("replay_initial_checkpoint_missing");
        return new ReplayHistoryArchive(FormatVersion, manifest, checkpoints, nodes,await ExecutionRulesAsync(gameId,cancellationToken));
    }

    public async Task<JsonElement?> ExecutionRulesAsync(string gameId,CancellationToken cancellationToken)
    {
        await using var db = new ApplicationDbContext(options);
        var row = await db.ReplaySnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.GameId == gameId && s.AtSeq == -1 && s.SchemaVersion == 4,cancellationToken);
        if (row is null) return null;
        if (row.Compression != "gzip") throw new InvalidDataException("invalid_execution_rules_compression");
        using var bytes = new MemoryStream(row.Payload);
        using var gzip = new GZipStream(bytes,CompressionMode.Decompress);
        return await JsonSerializer.DeserializeAsync<JsonElement>(gzip,SqliteReplayFrames.Json,cancellationToken);
    }

    public async Task RecoverInterruptedAsync(CancellationToken cancellationToken)
    {
        await using var db = new ApplicationDbContext(options);
        var games = await db.ReplayGames.AsNoTracking().Where(g => g.SchemaVersion == FormatVersion && g.EndedAtUtc == null)
            .Select(g => new { g.GameId, g.HostPlayerId, g.GuestPlayerId }).ToListAsync(cancellationToken);
        foreach (var game in games)
        {
            var manifest = await ManifestAsync(game.GameId, cancellationToken);
            var restored = await RestoreAsync(game.GameId, manifest.HeadSeq, cancellationToken);
            var winner = restored.Document.GetProperty("game").GetProperty("winnerId");
            var node = new ReplayHistoryNode(FormatVersion, game.GameId, manifest.HeadSeq + 1, manifest.HeadSeq,
                Guid.NewGuid(), null, null, null, null, "system", null, restored.Node?.TurnNo ?? 0, restored.Node?.TurnOwnerId,
                "lifecycle", "recording.interrupted", "interrupted", DateTime.UtcNow, restored.Node?.StateRevision ?? 0,
                manifest.SampleKind, JsonSerializer.SerializeToElement(new { reason = "server_restarted_durable_prefix_only", winnerId = winner }, SqliteReplayFrames.Json), []);
            await AppendAsync(game.GameId, Guid.Parse(game.HostPlayerId!), Guid.Parse(game.GuestPlayerId!), manifest.HeadSeq, [node], [], cancellationToken);
        }
    }

    private static ReplayHistoryNode Decode(ReplayEventRecord row)
    {
        if (row.SchemaVersion != FormatVersion || row.RawEventJson is null) throw new InvalidDataException("unsupported_replay_event_format");
        var node = JsonSerializer.Deserialize<ReplayHistoryNode>(row.RawEventJson, SqliteReplayFrames.Json) ?? throw new InvalidDataException("replay_event_is_null");
        if (node.FormatVersion != FormatVersion || node.GameId != row.GameId || node.Seq != row.Seq || node.PreviousSeq != node.Seq - 1
            || node.NodeId != row.EventId || node.EventType != row.EventType || node.Changes is null)
            throw new InvalidDataException("replay_event_identity_mismatch");
        return node;
    }

    private static ReplayHistoryCheckpoint DecodeCheckpoint(ReplaySnapshotRecord row)
    {
        if (row.SchemaVersion != FormatVersion || row.Compression != "gzip") throw new InvalidDataException("unsupported_replay_checkpoint_format");
        using var bytes = new MemoryStream(row.Payload);
        using var gzip = new GZipStream(bytes, CompressionMode.Decompress);
        var checkpoint = JsonSerializer.Deserialize<ReplayHistoryCheckpoint>(gzip, SqliteReplayFrames.Json) ?? throw new InvalidDataException("replay_checkpoint_is_null");
        if (checkpoint.GameId != row.GameId || checkpoint.AtSeq != row.AtSeq || checkpoint.FormatVersion != FormatVersion)
            throw new InvalidDataException("replay_checkpoint_identity_mismatch");
        return checkpoint;
    }
}
