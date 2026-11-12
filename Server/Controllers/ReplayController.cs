using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Replay;
using ShadowverseBeyond.Server.Options;
using Microsoft.Extensions.Options;
using ShadowverseBeyond.Server.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Infrastructure.Services;
using System.IO.Compression;

namespace ShadowverseBeyond.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableRateLimiting("replay-read")]
    public class ReplayController : ControllerBase
    {
        private readonly ICommittedEventJournal _journal;
        private readonly ISnapshotStore _snapshots;
        private readonly IMatchService _matchService;
        private readonly ReplayOptions _options;
        private readonly IRecordedEventMapper _recordedEventMapper;
        private readonly SqliteReplayFrames? _frames;
        private readonly SqliteReplayHistory? _history;

        public ReplayController(
            ICommittedEventJournal journal,
            ISnapshotStore snapshots,
            IMatchService matchService,
            IRecordedEventMapper recordedEventMapper,
            IOptions<ReplayOptions> options, SqliteReplayFrames? frames = null, SqliteReplayHistory? history = null)
        {
            _journal = journal;
            _snapshots = snapshots;
            _matchService = matchService;
            _recordedEventMapper = recordedEventMapper;
            _options = options.Value;
            _frames = frames;
            _history = history;
        }

        [HttpGet("{gameId}/events")]
        public async Task<ActionResult<IReadOnlyList<RecordedCommittedEventDto>>> GetEvents(
            string gameId,
            [FromQuery] long fromSeq = 1,
            [FromQuery] int limit = 5000,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(gameId)) return BadRequest("gameId is required");
            if (!HasReplayReadAccess(gameId)) return Forbid();
            if (_history != null && await _history.IsCurrentFormatAsync(gameId,cancellationToken))
                return Conflict(new { Code = "event_history_use_history_events_endpoint" });
            if (_frames != null && await _frames.IsCurrentFormatAsync(gameId, cancellationToken))
                return Conflict(new { Code = "frame_recording_use_frames_endpoint" });
            var events = await _journal.ReadRangeAsync(gameId, fromSeq, limit, cancellationToken);
            return Ok(events);
        }

        [HttpGet("{gameId}/export")]
        public async Task<IActionResult> Export(
            string gameId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(gameId)) return BadRequest("gameId is required");
            if (!HasReplayReadAccess(gameId)) return Forbid();

            if (_history != null && await _history.IsCurrentFormatAsync(gameId,cancellationToken))
            {
                var eventArchive = await _history.ArchiveAsync(gameId,cancellationToken);
                using var eventBytes = new MemoryStream();
                using (var gzip = new GZipStream(eventBytes,CompressionLevel.Optimal,leaveOpen:true))
                    await JsonSerializer.SerializeAsync(gzip,eventArchive,SqliteReplayFrames.Json,cancellationToken);
                return File(eventBytes.ToArray(),"application/gzip",$"replay-events-{gameId}.json.gz");
            }

            if (_frames != null && await _frames.IsCurrentFormatAsync(gameId, cancellationToken))
            {
                var archive = await _frames.ReadAsync(gameId, cancellationToken);
                using var archiveBytes = new MemoryStream();
                using (var gzip = new GZipStream(archiveBytes, CompressionLevel.Optimal, leaveOpen: true))
                    await JsonSerializer.SerializeAsync(gzip, archive, SqliteReplayFrames.Json, cancellationToken);
                return File(archiveBytes.ToArray(), "application/gzip", $"replay-{gameId}.json.gz");
            }

            var headSeq = await _journal.GetHeadSeqAsync(gameId, cancellationToken);
            var all = new List<RecordedCommittedEventDto>();

            const int PageSize = 5000;
            var fromSeq = 1L;
            while (fromSeq <= headSeq)
            {
                var page = await _journal.ReadRangeAsync(gameId, fromSeq, PageSize, cancellationToken);
                if (page.Count == 0) break;

                all.AddRange(page);
                fromSeq = page[^1].Seq + 1;
            }

            var payload = new
            {
                GameId = gameId,
                ExportedAtUtc = DateTime.UtcNow,
                HeadSeq = headSeq,
                EventCount = all.Count,
                Events = all
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            return File(bytes, "application/json", $"replay-{gameId}.json");
        }

        [HttpGet("{gameId}/head")]
        public async Task<ActionResult<object>> GetHead(
            string gameId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(gameId)) return BadRequest("gameId is required");
            if (!HasReplayReadAccess(gameId)) return Forbid();
            if (_history != null && await _history.IsCurrentFormatAsync(gameId,cancellationToken))
                return Ok(await _history.ManifestAsync(gameId,cancellationToken));
            if (_frames != null && await _frames.IsCurrentFormatAsync(gameId, cancellationToken))
                return Ok((await _frames.ReadAsync(gameId, cancellationToken)).Manifest);
            var headSeq = await _journal.GetHeadSeqAsync(gameId, cancellationToken);
            var snapshot = await _snapshots.GetLatestAsync(gameId, cancellationToken);
            return Ok(new
            {
                GameId = gameId,
                HeadSeq = headSeq,
                LatestSnapshotSeq = snapshot?.AtSeq ?? 0
            });
        }

        [HttpGet("{gameId}/snapshot")]
        public async Task<ActionResult<RecordedSnapshotDto>> GetSnapshot(
            string gameId,
            [FromQuery] long atSeq = long.MaxValue,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(gameId)) return BadRequest("gameId is required");
            if (!HasReplayReadAccess(gameId)) return Forbid();
            if (_history != null && await _history.IsCurrentFormatAsync(gameId,cancellationToken))
                return Conflict(new { Code = "use_history_restore_at_event_sequence" });
            if (_frames != null && await _frames.IsCurrentFormatAsync(gameId, cancellationToken))
                return Conflict(new { Code = "event_sequence_not_supported_use_frame_number" });
            var snapshot = await _snapshots.GetAtOrBeforeAsync(gameId, atSeq, cancellationToken);
            if (snapshot == null) return NotFound();
            return Ok(snapshot);
        }

        [HttpGet("{gameId}/enter")]
        public async Task<ActionResult<object>> Enter(
            string gameId,
            [FromQuery] long atSeq = long.MaxValue,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(gameId)) return BadRequest("gameId is required");
            if (!HasReplayReadAccess(gameId)) return Forbid();
            if (_history != null && await _history.IsCurrentFormatAsync(gameId,cancellationToken))
                return Ok(await _history.RestoreAsync(gameId,atSeq == long.MaxValue ? (await _history.ManifestAsync(gameId,cancellationToken)).HeadSeq : atSeq,cancellationToken));
            if (_frames != null && await _frames.IsCurrentFormatAsync(gameId, cancellationToken))
                return Ok(await _frames.ReadAsync(gameId, cancellationToken));

            var headSeq = await _journal.GetHeadSeqAsync(gameId, cancellationToken);
            var effectiveSeq = atSeq == long.MaxValue ? headSeq : Math.Min(atSeq, headSeq);
            if (effectiveSeq < 1) effectiveSeq = headSeq;

            var snapshot = await _snapshots.GetAtOrBeforeAsync(gameId, effectiveSeq, cancellationToken);
            if (snapshot == null)
            {
                return Ok(new
                {
                    GameId = gameId,
                    HeadSeq = headSeq,
                    Snapshot = (RecordedSnapshotDto?)null,
                    FromSeq = 1L
                });
            }

            return Ok(new
            {
                GameId = gameId,
                HeadSeq = headSeq,
                Snapshot = snapshot,
                FromSeq = snapshot.AtSeq + 1
            });
        }

        [HttpGet("{gameId}/validate")]
        public async Task<ActionResult<object>> Validate(
            string gameId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(gameId)) return BadRequest("gameId is required");
            if (!HasReplayReadAccess(gameId)) return Forbid();
            if (_history != null && await _history.IsCurrentFormatAsync(gameId,cancellationToken))
            {
                var eventArchive = await _history.ArchiveAsync(gameId,cancellationToken);
                await _history.RestoreAsync(gameId,eventArchive.Manifest.HeadSeq,cancellationToken);
                return Ok(new { FormatVersion = 3, Status = "valid", eventArchive.Manifest });
            }
            if (_frames != null && await _frames.IsCurrentFormatAsync(gameId, cancellationToken))
            {
                var archive = await _frames.ReadAsync(gameId, cancellationToken);
                return Ok(new { FormatVersion = 2, Status = "valid", archive.Manifest, MessageCount = archive.Frames.Sum(f => f.Messages.Count) });
            }

            var headSeq = await _journal.GetHeadSeqAsync(gameId, cancellationToken);
            var expectedSeq = 1L;
            var dtoDeserializeFailures = 0;
            var gaps = new List<object>();

            const int PageSize = 5000;
            var fromSeq = 1L;
            while (fromSeq <= headSeq)
            {
                var page = await _journal.ReadRangeAsync(gameId, fromSeq, PageSize, cancellationToken);
                if (page.Count == 0) break;

                foreach (var record in page)
                {
                    if (record.Seq != expectedSeq)
                    {
                        gaps.Add(new { Expected = expectedSeq, Actual = record.Seq });
                        expectedSeq = record.Seq;
                    }

                    foreach (var env in record.ClientDtos)
                    {
                        if (_recordedEventMapper.TryDeserializeClientDto(env) == null)
                        {
                            dtoDeserializeFailures++;
                        }
                    }

                    expectedSeq++;
                }

                fromSeq = page[^1].Seq + 1;
            }

            var latestSnapshot = await _snapshots.GetLatestAsync(gameId, cancellationToken);
            bool snapshotHashOk = true;
            if (latestSnapshot != null)
            {
                snapshotHashOk = string.Equals(latestSnapshot.PayloadHash, ComputeSnapshotHash(latestSnapshot.Snapshot), StringComparison.Ordinal);
            }

            return Ok(new
            {
                GameId = gameId,
                HeadSeq = headSeq,
                SeqContinuityOk = gaps.Count == 0 && headSeq == 0 ? true : gaps.Count == 0 && expectedSeq - 1 == headSeq,
                Gaps = gaps,
                DtoDeserializeFailures = dtoDeserializeFailures,
                LatestSnapshotSeq = latestSnapshot?.AtSeq ?? 0,
                LatestSnapshotHashOk = snapshotHashOk
            });
        }

        private bool HasReplayReadAccess(string gameId)
        {
            if (!_options.RequireJoinCodeForReplayReads) return true;

            var roomResult = _matchService.GetByGameId(gameId);
            if (!roomResult.Success || roomResult.Room == null)
            {
                // No active room (ended or expired) -> allow reads for now.
                return true;
            }

            var joinCode = HttpContext?.Request.Query["joinCode"].ToString();
            return !string.IsNullOrWhiteSpace(joinCode) &&
                   string.Equals(roomResult.Room.JoinCode, joinCode, StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeSnapshotHash(GameStateDto snapshot)
        {
            var json = JsonSerializer.Serialize(snapshot);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
