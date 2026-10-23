using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Replay;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Options;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class ReplayJournalIntegrationTests
    {
        [Fact]
        public async Task AppendAndReadRange_ShouldRoundTripClientDtos_AndAllowFindByEventId()
        {
            await using var connection = new SqliteConnection("Filename=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            await using (var db = new ApplicationDbContext(options))
            {
                await db.Database.MigrateAsync();
            }

            await using var db2 = new ApplicationDbContext(options);
            var journal = new SqliteReplayJournal(db2, NullLogger<SqliteReplayJournal>.Instance);

            var gameId = "test-game";
            var bundleId = Guid.NewGuid();
            var eventId = Guid.NewGuid();
            var dto = new LogEventDto(eventId, "TestEvent", "hello", Guid.Empty);
            var envelope = new RecordedClientDtoEnvelopeDto(
                dto.GetType().AssemblyQualifiedName ?? dto.GetType().FullName ?? dto.GetType().Name,
                JsonSerializer.Serialize(dto, dto.GetType()));

            var appendResult = await journal.AppendBundleAsync(
                gameId,
                bundleId,
                new List<CommittedEventWriteDto>
                {
                    new(
                        eventId,
                        "TestEvent",
                        DateTime.UtcNow,
                        "hello",
                        null,
                        Array.Empty<Guid>(),
                        new[] { envelope },
                        null)
                });

            Assert.Equal(1, appendResult.BundleSeq);
            Assert.Equal(1, appendResult.StartSeq);
            Assert.Equal(1, appendResult.EndSeq);

            var range = await journal.ReadRangeAsync(gameId, 1, 10);
            Assert.Single(range);
            Assert.Equal(eventId, range[0].EventId);
            Assert.Single(range[0].ClientDtos);
            Assert.Equal(envelope.Type, range[0].ClientDtos[0].Type);

            var found = await journal.FindByEventIdAsync(gameId, eventId);
            Assert.NotNull(found);
            Assert.Equal(1, found!.Seq);
        }

        [Fact]
        public async Task AppendAndReadRange_ShouldRoundTripRawEventJson_AndAllowRehydratePayload()
        {
            await using var connection = new SqliteConnection("Filename=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            await using (var db = new ApplicationDbContext(options))
            {
                await db.Database.MigrateAsync();
            }

            await using var db2 = new ApplicationDbContext(options);
            var journal = new SqliteReplayJournal(db2, NullLogger<SqliteReplayJournal>.Instance);

            var gameId = "test-game-raw";
            var bundleId = Guid.NewGuid();
            var eventId = Guid.NewGuid();
            var ts = DateTime.UtcNow;

            var rawEnvelope = new RawEventEnvelopeDto(
                2,
                "Damage",
                "EV-000001",
                ts,
                null,
                Array.Empty<string>(),
                new JsonObject { ["Damage"] = 2 });
            var rawEventJson = JsonSerializer.Serialize(rawEnvelope);

            await journal.AppendBundleAsync(
                gameId,
                bundleId,
                new List<CommittedEventWriteDto>
                {
                    new(
                        eventId,
                        "Damage",
                        ts,
                        null,
                        null,
                        Array.Empty<Guid>(),
                        Array.Empty<RecordedClientDtoEnvelopeDto>(),
                        rawEventJson)
                });

            var range = await journal.ReadRangeAsync(gameId, 1, 10);
            Assert.Single(range);
            Assert.Equal(rawEventJson, range[0].RawEventJson);

        }

        [Fact]
        public async Task SnapshotStore_ShouldSaveAndLoadSnapshot()
        {
            await using var connection = new SqliteConnection("Filename=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            await using (var db = new ApplicationDbContext(options))
            {
                await db.Database.MigrateAsync();
            }

            await using var db2 = new ApplicationDbContext(options);
            var store = new SqliteSnapshotStore(
                db2,
                NullLogger<SqliteSnapshotStore>.Instance,
                Microsoft.Extensions.Options.Options.Create(new ReplayOptions { CompressSnapshots = true }));

            var snapshot = new GameStateDto
            {
                GameId = "snap-game",
                GameMessage = "test"
            };

            await store.SaveSnapshotAsync(snapshot.GameId, 1, snapshot);
            var loaded = await store.GetLatestAsync(snapshot.GameId);

            Assert.NotNull(loaded);
            Assert.Equal(snapshot.GameId, loaded!.GameId);
            Assert.Equal(1, loaded.AtSeq);
            Assert.Equal("test", loaded.Snapshot.GameMessage);
        }

        [Fact]
        public async Task LegacyDtoAndRawProjection_ShouldProduceEquivalentDamageDto()
        {
            var fixture = CreateReplayProjectionFixture("parity-test");
            var recordedMapper = new JsonRecordedEventMapper();
            var rawRegistry = new RawEventGameEventRegistry();

            var source = fixture.GameState.Player1.Leader;
            var target = fixture.GameState.Player2.Leader;
            var damageEvent = new DamageEvent(target, 4, source);

            using var legacyContext = new GameContext(
                fixture.ServiceProvider,
                fixture.Mapper,
                fixture.ContextAccessor,
                fixture.GameState,
                fixture.RequestingPlayerId);

            var legacyResult = await fixture.EventProcessor.ProcessEventAsync(damageEvent, legacyContext);
            var envelopes = recordedMapper.MapClientDtos(legacyResult.ResultingDtos);
            var legacyDtos = envelopes
                .Select(recordedMapper.TryDeserializeClientDto)
                .Where(dto => dto != null)
                .Cast<GameEventDto>()
                .ToList();

            var rawEnvelope = BuildDamageRawEnvelope(
                fixture.GameState,
                damageEvent.EventId,
                damageEvent.Timestamp,
                source.InstanceId,
                target.InstanceId,
                4);

            Assert.True(rawRegistry.TryCreateGameEvent(rawEnvelope!, fixture.GameState, fixture.RequestingPlayerId, out var replayEvent, out var error), error);
            Assert.NotNull(replayEvent);

            using var replayContext = new GameContext(
                fixture.ServiceProvider,
                fixture.Mapper,
                fixture.ContextAccessor,
                fixture.GameState,
                fixture.RequestingPlayerId);

            var projectedResult = await fixture.EventProcessor.ProcessEventAsync(replayEvent!, replayContext);

            var legacyDamage = Assert.IsType<DamageDealtEventDto>(Assert.Single(legacyDtos));
            var projectedDamage = Assert.IsType<DamageDealtEventDto>(Assert.Single(projectedResult.ResultingDtos));

            Assert.Equal(legacyDamage.Type, projectedDamage.Type);
            Assert.Equal(legacyDamage.SourcePlayerId, projectedDamage.SourcePlayerId);
            Assert.Equal(legacyDamage.TargetUid, projectedDamage.TargetUid);
            Assert.Equal(legacyDamage.TargetPlayerId, projectedDamage.TargetPlayerId);
            Assert.Equal(legacyDamage.DamageAmount, projectedDamage.DamageAmount);
            Assert.Equal(legacyDamage.SourceUid, projectedDamage.SourceUid);
            Assert.Equal(legacyDamage.DamageType, projectedDamage.DamageType);
        }

        [Fact]
        public async Task SnapshotAndRawRangeReplay_ShouldProjectExpectedOrderedStream()
        {
            var journal = new InMemoryCommittedEventJournal();
            var snapshotStore = new InMemorySnapshotStore();

            var fixture = CreateReplayProjectionFixture("seek-raw-test");
            var rawRegistry = new RawEventGameEventRegistry();

            var source = fixture.GameState.Player1.Leader;
            var target = fixture.GameState.Player2.Leader;
            var event1 = new DamageEvent(target, 2, source);
            var event2 = new DamageEvent(target, 5, source);

            var writes = new List<CommittedEventWriteDto>
            {
                new(
                    event1.EventId,
                    event1.EventType.ToString(),
                    event1.Timestamp,
                    null,
                    source.InstanceId,
                    new[] { target.InstanceId },
                    Array.Empty<RecordedClientDtoEnvelopeDto>(),
                    JsonSerializer.Serialize(BuildDamageRawEnvelope(
                        fixture.GameState,
                        event1.EventId,
                        event1.Timestamp,
                        source.InstanceId,
                        target.InstanceId,
                        2))),
                new(
                    event2.EventId,
                    event2.EventType.ToString(),
                    event2.Timestamp,
                    null,
                    source.InstanceId,
                    new[] { target.InstanceId },
                    Array.Empty<RecordedClientDtoEnvelopeDto>(),
                    JsonSerializer.Serialize(BuildDamageRawEnvelope(
                        fixture.GameState,
                        event2.EventId,
                        event2.Timestamp,
                        source.InstanceId,
                        target.InstanceId,
                        5)))
            };

            var append = await journal.AppendBundleAsync(
                fixture.GameState.GameId,
                Guid.NewGuid(),
                writes);

            Assert.Equal(1, append.StartSeq);
            Assert.Equal(2, append.EndSeq);

            await snapshotStore.SaveSnapshotAsync(
                fixture.GameState.GameId,
                1,
                new GameStateDto { GameId = fixture.GameState.GameId, GameMessage = "snapshot@1" });

            var snapshot = await snapshotStore.GetAtOrBeforeAsync(fixture.GameState.GameId, 2);
            Assert.NotNull(snapshot);
            Assert.Equal(1, snapshot!.AtSeq);

            var range = await journal.ReadRangeAsync(fixture.GameState.GameId, snapshot.AtSeq + 1, 100);
            Assert.Single(range);

            // Simulate duplicate rows across pages and ensure replay only applies each identity once.
            var duplicatedRange = range.Concat(range).ToList();
            var seenSeq = new HashSet<long>();
            var seenEventIds = new HashSet<Guid>();
            var projected = new List<DamageDealtEventDto>();

            foreach (var record in duplicatedRange)
            {
                if (!seenSeq.Add(record.Seq) || !seenEventIds.Add(record.EventId))
                {
                    continue;
                }

                var rawEnvelope = JsonSerializer.Deserialize<RawEventEnvelopeDto>(record.RawEventJson!);
                Assert.NotNull(rawEnvelope);
                Assert.True(rawRegistry.TryCreateGameEvent(rawEnvelope!, fixture.GameState, fixture.RequestingPlayerId, out var gameEvent, out var err), err);
                Assert.NotNull(gameEvent);

                using var replayContext = new GameContext(
                    fixture.ServiceProvider,
                    fixture.Mapper,
                    fixture.ContextAccessor,
                    fixture.GameState,
                    fixture.RequestingPlayerId);

                var result = await fixture.EventProcessor.ProcessEventAsync(gameEvent!, replayContext);
                projected.Add(Assert.IsType<DamageDealtEventDto>(Assert.Single(result.ResultingDtos)));
            }

            var finalDto = Assert.Single(projected);
            Assert.Equal(target.InstanceId, finalDto.TargetUid);
            Assert.Equal(5, finalDto.DamageAmount);
        }

        private static RawEventEnvelopeDto BuildDamageRawEnvelope(
            GameState gameState,
            Guid eventId,
            DateTime timestampUtc,
            Guid sourceCardId,
            Guid targetCardId,
            int damage)
        {
            return new RawEventEnvelopeDto(
                2,
                EventType.Damage.ToString(),
                gameState.InGameIds.Format(InGameIdKind.Event, eventId),
                timestampUtc,
                gameState.InGameIds.Format(InGameIdKind.Card, sourceCardId),
                new[] { gameState.InGameIds.Format(InGameIdKind.Card, targetCardId) },
                new JsonObject { ["Damage"] = damage });
        }

        private static ReplayProjectionFixture CreateReplayProjectionFixture(string gameId)
        {
            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());

            player1.Leader = new CardInstance(Guid.NewGuid(), 1, player1.PlayerId);
            player2.Leader = new CardInstance(Guid.NewGuid(), 2, player2.PlayerId);

            var gameState = new GameState(gameId, player1, player2, player1.PlayerId)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            gameState.RegisterCardInstance(player1.Leader);
            gameState.RegisterCardInstance(player2.Leader);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IBuffService>(Mock.Of<IBuffService>());
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var mapper = Mock.Of<AutoMapper.IMapper>();
            var contextAccessor = new GameContextAccessor();
            var eventProcessor = new EventProcessor(
                NullLogger<EventProcessor>.Instance,
                mapper,
                Mock.Of<IEffectExecutor>(),
                Mock.Of<IGameDataProvider>(),
                serviceProvider.GetRequiredService<IEventHistoryService>(),
                serviceProvider);

            return new ReplayProjectionFixture(
                gameState,
                serviceProvider,
                mapper,
                contextAccessor,
                eventProcessor,
                player1.PlayerId);
        }

        private sealed record ReplayProjectionFixture(
            GameState GameState,
            IServiceProvider ServiceProvider,
            AutoMapper.IMapper Mapper,
            GameContextAccessor ContextAccessor,
            EventProcessor EventProcessor,
            Guid RequestingPlayerId);

        private sealed class InMemoryCommittedEventJournal : ICommittedEventJournal
        {
            private readonly Dictionary<string, List<RecordedCommittedEventDto>> _recordsByGame = new(StringComparer.Ordinal);

            public Task<CommittedBundleAppendResult> AppendBundleAsync(
                string gameId,
                Guid bundleId,
                IReadOnlyList<CommittedEventWriteDto> events,
                CancellationToken cancellationToken = default)
            {
                if (!_recordsByGame.TryGetValue(gameId, out var records))
                {
                    records = new List<RecordedCommittedEventDto>();
                    _recordsByGame[gameId] = records;
                }

                var startSeq = records.Count + 1L;
                for (var i = 0; i < events.Count; i++)
                {
                    var item = events[i];
                    records.Add(new RecordedCommittedEventDto(
                        Seq: startSeq + i,
                        BundleId: bundleId,
                        EventIndex: i,
                        EventId: item.EventId,
                        EventType: item.EventType,
                        TimestampUtc: item.TimestampUtc,
                        SchemaVersion: 1,
                        Announcement: item.Announcement,
                        SourceId: item.SourceId,
                        TargetIds: item.TargetIds.ToArray(),
                        ClientDtos: item.ClientDtos.ToArray(),
                        RawEventJson: item.RawEventJson));
                }

                var endSeq = records.Count;
                return Task.FromResult(new CommittedBundleAppendResult(BundleSeq: 1, StartSeq: startSeq, EndSeq: endSeq));
            }

            public Task<IReadOnlyList<RecordedCommittedEventDto>> ReadRangeAsync(
                string gameId,
                long fromSeqInclusive,
                int limit,
                CancellationToken cancellationToken = default)
            {
                if (!_recordsByGame.TryGetValue(gameId, out var records))
                {
                    return Task.FromResult<IReadOnlyList<RecordedCommittedEventDto>>(Array.Empty<RecordedCommittedEventDto>());
                }

                var result = records
                    .Where(record => record.Seq >= fromSeqInclusive)
                    .OrderBy(record => record.Seq)
                    .Take(limit)
                    .ToList();

                return Task.FromResult<IReadOnlyList<RecordedCommittedEventDto>>(result);
            }

            public Task<long> GetHeadSeqAsync(string gameId, CancellationToken cancellationToken = default)
            {
                if (!_recordsByGame.TryGetValue(gameId, out var records))
                {
                    return Task.FromResult(0L);
                }

                return Task.FromResult(records.Count == 0 ? 0L : records[^1].Seq);
            }

            public Task<RecordedCommittedEventDto?> FindByEventIdAsync(string gameId, Guid eventId, CancellationToken cancellationToken = default)
            {
                if (!_recordsByGame.TryGetValue(gameId, out var records))
                {
                    return Task.FromResult<RecordedCommittedEventDto?>(null);
                }

                return Task.FromResult(records.FirstOrDefault(record => record.EventId == eventId));
            }
        }

        private sealed class InMemorySnapshotStore : ISnapshotStore
        {
            private readonly Dictionary<string, List<RecordedSnapshotDto>> _snapshotsByGame = new(StringComparer.Ordinal);

            public Task SaveSnapshotAsync(string gameId, long atSeq, GameStateDto snapshot, CancellationToken cancellationToken = default)
            {
                if (!_snapshotsByGame.TryGetValue(gameId, out var snapshots))
                {
                    snapshots = new List<RecordedSnapshotDto>();
                    _snapshotsByGame[gameId] = snapshots;
                }

                snapshots.Add(new RecordedSnapshotDto(
                    GameId: gameId,
                    AtSeq: atSeq,
                    CreatedAtUtc: DateTime.UtcNow,
                    SchemaVersion: 1,
                    Compression: "none",
                    PayloadHash: string.Empty,
                    Snapshot: snapshot));

                return Task.CompletedTask;
            }

            public Task<RecordedSnapshotDto?> GetLatestAsync(string gameId, CancellationToken cancellationToken = default)
            {
                if (!_snapshotsByGame.TryGetValue(gameId, out var snapshots) || snapshots.Count == 0)
                {
                    return Task.FromResult<RecordedSnapshotDto?>(null);
                }

                return Task.FromResult<RecordedSnapshotDto?>(snapshots.OrderByDescending(s => s.AtSeq).First());
            }

            public Task<RecordedSnapshotDto?> GetAtOrBeforeAsync(string gameId, long atSeq, CancellationToken cancellationToken = default)
            {
                if (!_snapshotsByGame.TryGetValue(gameId, out var snapshots) || snapshots.Count == 0)
                {
                    return Task.FromResult<RecordedSnapshotDto?>(null);
                }

                return Task.FromResult<RecordedSnapshotDto?>(
                    snapshots
                        .Where(s => s.AtSeq <= atSeq)
                        .OrderByDescending(s => s.AtSeq)
                        .FirstOrDefault());
            }

            public Task DeleteForGameAsync(string gameId, CancellationToken cancellationToken = default)
            {
                _snapshotsByGame.Remove(gameId);
                return Task.CompletedTask;
            }
        }
    }
}
