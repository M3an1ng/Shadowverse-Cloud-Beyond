using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Dtos.Replay;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Persistence.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Infrastructure.Services
{
    public class SqliteReplayJournal : ICommittedEventJournal
    {
        private static readonly SemaphoreSlim SchemaGate = new(1, 1);
        private static volatile bool _schemaEnsured;

        private readonly ApplicationDbContext _db;
        private readonly ILogger<SqliteReplayJournal> _logger;

        public SqliteReplayJournal(ApplicationDbContext db, ILogger<SqliteReplayJournal> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<CommittedBundleAppendResult> AppendBundleAsync(
            string gameId,
            Guid bundleId,
            IReadOnlyList<CommittedEventWriteDto> events,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(gameId)) throw new ArgumentException("gameId is required", nameof(gameId));
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (events.Count == 0) return new CommittedBundleAppendResult(0, 0, 0);

            var sw = Stopwatch.StartNew();
            await EnsureSchemaAsync(cancellationToken);

            var lastSeq = await _db.ReplayEvents
                              .Where(x => x.GameId == gameId)
                              .MaxAsync(x => (long?)x.Seq, cancellationToken)
                          ?? 0;

            var lastBundleSeq = await _db.ReplayBundles
                                    .Where(x => x.GameId == gameId)
                                    .MaxAsync(x => (long?)x.BundleSeq, cancellationToken)
                                ?? 0;

            var records = new List<ReplayEventRecord>(events.Count);
            for (var i = 0; i < events.Count; i++)
            {
                var ev = events[i];

                records.Add(new ReplayEventRecord
                {
                    GameId = gameId,
                    Seq = lastSeq + i + 1,
                    BundleId = bundleId,
                    EventIndex = i,
                    EventId = ev.EventId,
                    EventType = ev.EventType,
                    TimestampUtc = ev.TimestampUtc,
                    SchemaVersion = 1,
                    DtosJson = ev.ClientDtos.Count == 0 ? null : JsonSerializer.Serialize(ev.ClientDtos),
                    RawEventJson = string.IsNullOrWhiteSpace(ev.RawEventJson) ? null : ev.RawEventJson,
                    Announcement = string.IsNullOrWhiteSpace(ev.Announcement) ? null : ev.Announcement,
                    SourceId = ev.SourceId,
                    TargetIdsJson = ev.TargetIds.Length == 0 ? null : JsonSerializer.Serialize(ev.TargetIds)
                });
            }

            var startSeq = records[0].Seq;
            var endSeq = records[^1].Seq;
            var bundleSeq = lastBundleSeq + 1;

            var existingGame = await _db.ReplayGames
                .FirstOrDefaultAsync(g => g.GameId == gameId, cancellationToken);
            if (existingGame == null)
            {
                _db.ReplayGames.Add(new ReplayGameRecord
                {
                    GameId = gameId,
                    CreatedAtUtc = DateTime.UtcNow,
                    ServerBuild = GetServerBuild(),
                    SchemaVersion = 1
                });
            }

            _db.ReplayBundles.Add(new ReplayBundleRecord
            {
                GameId = gameId,
                BundleSeq = bundleSeq,
                BundleId = bundleId,
                StartSeq = startSeq,
                EndSeq = endSeq,
                EventCount = records.Count,
                CommittedAtUtc = DateTime.UtcNow
            });

            _db.ReplayEvents.AddRange(records);

            if (events.Any(e => string.Equals(e.EventType, "GameEnd", StringComparison.Ordinal)))
            {
                var game = existingGame ?? await _db.ReplayGames.FirstOrDefaultAsync(g => g.GameId == gameId, cancellationToken);
                if (game != null && game.EndedAtUtc == null)
                {
                    game.EndedAtUtc = DateTime.UtcNow;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Appended {Count} replay events for GameId={GameId}, BundleId={BundleId}, SeqRange=[{StartSeq}..{EndSeq}]",
                records.Count, gameId, bundleId, records[0].Seq, records[^1].Seq);

            sw.Stop();
            ReplayMetrics.JournalAppendMs.Record(sw.Elapsed.TotalMilliseconds);
            ReplayMetrics.JournalAppendedEvents.Add(records.Count);

            return new CommittedBundleAppendResult(bundleSeq, startSeq, endSeq);
        }

        public async Task<IReadOnlyList<RecordedCommittedEventDto>> ReadRangeAsync(
            string gameId,
            long fromSeqInclusive,
            int limit,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(gameId)) throw new ArgumentException("gameId is required", nameof(gameId));

            await EnsureSchemaAsync(cancellationToken);

            if (fromSeqInclusive < 1) fromSeqInclusive = 1;
            if (limit < 1) limit = 1;
            limit = Math.Min(limit, 20000);

            var records = await _db.ReplayEvents
                .AsNoTracking()
                .Where(e => e.GameId == gameId && e.Seq >= fromSeqInclusive)
                .OrderBy(e => e.Seq)
                .Take(limit)
                .ToListAsync(cancellationToken);

            return records.Select(Map).ToList();
        }

        public async Task<long> GetHeadSeqAsync(string gameId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(gameId)) throw new ArgumentException("gameId is required", nameof(gameId));

            await EnsureSchemaAsync(cancellationToken);

            return await _db.ReplayEvents
                       .Where(e => e.GameId == gameId)
                       .MaxAsync(e => (long?)e.Seq, cancellationToken)
                   ?? 0;
        }

        public async Task<RecordedCommittedEventDto?> FindByEventIdAsync(
            string gameId,
            Guid eventId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(gameId)) throw new ArgumentException("gameId is required", nameof(gameId));
            if (eventId == Guid.Empty) throw new ArgumentException("eventId is required", nameof(eventId));

            await EnsureSchemaAsync(cancellationToken);

            var record = await _db.ReplayEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.GameId == gameId && e.EventId == eventId, cancellationToken);
            return record == null ? null : Map(record);
        }

        private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
        {
            if (_schemaEnsured) return;

            await SchemaGate.WaitAsync(cancellationToken);
            try
            {
                if (_schemaEnsured) return;

                await _db.Database.MigrateAsync(cancellationToken);
                _schemaEnsured = true;
            }
            finally
            {
                SchemaGate.Release();
            }
        }

        private static RecordedCommittedEventDto Map(ReplayEventRecord record)
        {
            Guid[] targets;
            try
            {
                targets = string.IsNullOrWhiteSpace(record.TargetIdsJson)
                    ? Array.Empty<Guid>()
                    : JsonSerializer.Deserialize<Guid[]>(record.TargetIdsJson) ?? Array.Empty<Guid>();
            }
            catch
            {
                targets = Array.Empty<Guid>();
            }

            IReadOnlyList<RecordedClientDtoEnvelopeDto> dtos;
            try
            {
                dtos = string.IsNullOrWhiteSpace(record.DtosJson)
                    ? Array.Empty<RecordedClientDtoEnvelopeDto>()
                    : JsonSerializer.Deserialize<RecordedClientDtoEnvelopeDto[]>(record.DtosJson) ??
                      Array.Empty<RecordedClientDtoEnvelopeDto>();
            }
            catch
            {
                dtos = Array.Empty<RecordedClientDtoEnvelopeDto>();
            }

            return new RecordedCommittedEventDto(
                record.Seq,
                record.BundleId,
                record.EventIndex,
                record.EventId,
                record.EventType,
                record.TimestampUtc,
                record.SchemaVersion,
                record.Announcement,
                record.SourceId,
                targets,
                dtos,
                record.RawEventJson);
        }

        private static string GetServerBuild()
        {
            try
            {
                var version = typeof(SqliteReplayJournal).Assembly.GetName().Version?.ToString();
                return string.IsNullOrWhiteSpace(version) ? "unknown" : version;
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
