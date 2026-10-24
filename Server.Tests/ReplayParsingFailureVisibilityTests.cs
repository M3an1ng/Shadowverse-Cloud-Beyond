using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Persistence.Entities;
using ShadowverseBeyond.Server.Infrastructure.Services;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public sealed class ReplayParsingFailureVisibilityTests
{
    [Fact]
    public async Task ReadRangeAsync_ShouldKeepValidEmptyCollectionsEmpty()
    {
        await using var connection = await CreateDatabaseAsync();
        await using var db = CreateContext(connection);
        var journal = new SqliteReplayJournal(db, NullLogger<SqliteReplayJournal>.Instance);

        db.ReplayEvents.Add(CreateRecord(
            "valid-empty",
            dtosJson: null,
            targetIdsJson: null));
        await db.SaveChangesAsync();

        var records = await journal.ReadRangeAsync("valid-empty", 1, 10);

        var record = Assert.Single(records);
        Assert.Empty(record.ClientDtos);
        Assert.Empty(record.TargetIds);
    }

    [Fact]
    public async Task ReadRangeAsync_ShouldKeepValidHistoricalEnvelope()
    {
        await using var connection = await CreateDatabaseAsync();
        await using var db = CreateContext(connection);
        var journal = new SqliteReplayJournal(db, NullLogger<SqliteReplayJournal>.Instance);

        db.ReplayEvents.Add(CreateRecord(
            "valid-envelope",
            dtosJson: "[{\"Type\":\"ShadowverseBeyond.Server.Dtos.SignalR.Events.LogEventDto\",\"Json\":\"{}\"}]",
            targetIdsJson: "[]"));
        await db.SaveChangesAsync();

        var records = await journal.ReadRangeAsync("valid-envelope", 1, 10);

        var record = Assert.Single(records);
        var envelope = Assert.Single(record.ClientDtos);
        Assert.Equal("ShadowverseBeyond.Server.Dtos.SignalR.Events.LogEventDto", envelope.Type);
        Assert.Equal("{}", envelope.Json);
    }

    [Fact]
    public async Task ReadRangeAsync_ShouldExposeMalformedTargetIdsWithRecordLocation()
    {
        await using var connection = await CreateDatabaseAsync();
        await using var db = CreateContext(connection);
        var journal = new SqliteReplayJournal(db, NullLogger<SqliteReplayJournal>.Instance);

        db.ReplayEvents.Add(CreateRecord(
            "malformed-targets",
            dtosJson: null,
            targetIdsJson: "[\"not-a-guid\"]"));
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => journal.ReadRangeAsync("malformed-targets", 1, 10));

        Assert.Contains("malformed-targets", exception.Message);
        Assert.Contains("Seq=1", exception.Message);
        Assert.Contains("TargetIdsJson", exception.Message);
    }

    [Fact]
    public async Task ReadRangeAsync_ShouldExposeIncompatibleClientDtoEnvelopeWithRecordLocation()
    {
        await using var connection = await CreateDatabaseAsync();
        await using var db = CreateContext(connection);
        var journal = new SqliteReplayJournal(db, NullLogger<SqliteReplayJournal>.Instance);

        db.ReplayEvents.Add(CreateRecord(
            "incompatible-dto",
            dtosJson: "[{\"Type\":null,\"Json\":\"{}\"}]",
            targetIdsJson: null));
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => journal.ReadRangeAsync("incompatible-dto", 1, 10));

        Assert.Contains("incompatible-dto", exception.Message);
        Assert.Contains("Seq=1", exception.Message);
        Assert.Contains("DtosJson", exception.Message);
    }

    private static async Task<SqliteConnection> CreateDatabaseAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();
        typeof(SqliteReplayJournal)
            .GetField("_schemaEnsured", BindingFlags.Static | BindingFlags.NonPublic)!
            .SetValue(null, true);
        return connection;
    }

    private static ApplicationDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static ReplayEventRecord CreateRecord(
        string gameId,
        string? dtosJson,
        string? targetIdsJson)
    {
        return new ReplayEventRecord
        {
            GameId = gameId,
            Seq = 1,
            BundleId = Guid.NewGuid(),
            EventIndex = 0,
            EventId = Guid.NewGuid(),
            EventType = "TestEvent",
            TimestampUtc = DateTime.UtcNow,
            SchemaVersion = 1,
            DtosJson = dtosJson,
            TargetIdsJson = targetIdsJson
        };
    }
}
