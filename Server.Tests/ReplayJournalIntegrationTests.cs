using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Replay;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Options;
using ShadowverseBeyond.Server.Services;
using System;
using System.Collections.Generic;
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

            var gameService = new Mock<IGameService>();
            gameService.Setup(s => s.GetGameState(gameId)).Returns((ShadowverseBeyond.Server.Domain.Entities.GameState?)null);
            var rehydrator = new BestEffortGameEventRehydrator(gameService.Object);

            var hydrated = await rehydrator.RehydrateAsync(gameId, range[0]);
            var rawBacked = Assert.IsAssignableFrom<IRawEventBackedGameEvent>(hydrated);
            Assert.Equal(2, rawBacked.OutputPayload["Damage"]!.GetValue<int>());
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
    }
}
