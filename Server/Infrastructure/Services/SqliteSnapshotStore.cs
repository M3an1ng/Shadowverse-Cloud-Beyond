using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Replay;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Persistence.Entities;
using ShadowverseBeyond.Server.Options;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Infrastructure.Services
{
    public class SqliteSnapshotStore : ISnapshotStore
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<SqliteSnapshotStore> _logger;
        private readonly ReplayOptions _options;

        public SqliteSnapshotStore(
            ApplicationDbContext db,
            ILogger<SqliteSnapshotStore> logger,
            IOptions<ReplayOptions> options)
        {
            _db = db;
            _logger = logger;
            _options = options.Value;
        }

        public async Task SaveSnapshotAsync(
            string gameId,
            long atSeq,
            GameStateDto snapshot,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(gameId)) throw new ArgumentException("gameId is required", nameof(gameId));
            if (atSeq < 1) throw new ArgumentOutOfRangeException(nameof(atSeq), "atSeq must be >= 1");
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var sw = Stopwatch.StartNew();

            var exists = await _db.ReplaySnapshots
                .AsNoTracking()
                .AnyAsync(s => s.GameId == gameId && s.AtSeq == atSeq, cancellationToken);
            if (exists) return;

            var json = JsonSerializer.Serialize(snapshot);
            var raw = Encoding.UTF8.GetBytes(json);
            var payloadHash = Sha256Hex(raw);
            var payload = raw;
            var compression = "none";

            if (_options.CompressSnapshots)
            {
                payload = Gzip(payload);
                compression = "gzip";
            }

            _db.ReplaySnapshots.Add(new ReplaySnapshotRecord
            {
                GameId = gameId,
                AtSeq = atSeq,
                CreatedAtUtc = DateTime.UtcNow,
                SchemaVersion = 1,
                Compression = compression,
                PayloadHash = payloadHash,
                Payload = payload
            });

            await _db.SaveChangesAsync(cancellationToken);
            sw.Stop();
            ReplayMetrics.SnapshotSaveMs.Record(sw.Elapsed.TotalMilliseconds);
            ReplayMetrics.SnapshotBytes.Record(payload.Length);
            _logger.LogDebug("Saved snapshot for GameId={GameId} AtSeq={AtSeq} Compression={Compression} Bytes={Bytes}",
                gameId, atSeq, compression, payload.Length);
        }

        public Task<RecordedSnapshotDto?> GetLatestAsync(string gameId, CancellationToken cancellationToken = default)
        {
            return GetAtOrBeforeAsync(gameId, long.MaxValue, cancellationToken);
        }

        public async Task<RecordedSnapshotDto?> GetAtOrBeforeAsync(
            string gameId,
            long atSeq,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(gameId)) throw new ArgumentException("gameId is required", nameof(gameId));
            if (atSeq < 1) atSeq = 1;

            var record = await _db.ReplaySnapshots
                .AsNoTracking()
                .Where(s => s.GameId == gameId && s.AtSeq <= atSeq)
                .OrderByDescending(s => s.AtSeq)
                .FirstOrDefaultAsync(cancellationToken);
            if (record == null) return null;

            var payload = record.Payload ?? Array.Empty<byte>();
            if (string.Equals(record.Compression, "gzip", StringComparison.OrdinalIgnoreCase))
            {
                payload = Gunzip(payload);
            }

            var json = Encoding.UTF8.GetString(payload);
            GameStateDto snapshot;
            try
            {
                snapshot = JsonSerializer.Deserialize<GameStateDto>(json) ?? new GameStateDto();
            }
            catch
            {
                snapshot = new GameStateDto();
            }

            return new RecordedSnapshotDto(
                record.GameId,
                record.AtSeq,
                record.CreatedAtUtc,
                record.SchemaVersion,
                record.Compression,
                record.PayloadHash,
                snapshot);
        }

        public async Task DeleteForGameAsync(string gameId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(gameId)) throw new ArgumentException("gameId is required", nameof(gameId));

            var rows = await _db.ReplaySnapshots
                .Where(s => s.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken);

            _logger.LogInformation("Deleted {Count} snapshots for GameId={GameId}", rows, gameId);
        }

        private static byte[] Gzip(byte[] input)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
            {
                gzip.Write(input, 0, input.Length);
            }
            return output.ToArray();
        }

        private static byte[] Gunzip(byte[] input)
        {
            using var inputStream = new MemoryStream(input);
            using var gzip = new GZipStream(inputStream, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }

        private static string Sha256Hex(byte[] data)
        {
            var hash = SHA256.HashData(data);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
