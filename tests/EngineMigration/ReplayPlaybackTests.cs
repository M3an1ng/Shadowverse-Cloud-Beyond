using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Dtos.Replay;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;

internal static class ReplayPlaybackTests
{
    public static async Task RunAsync()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var history = new SqliteReplayHistory(options);
        var host = Guid.NewGuid();
        var guest = Guid.NewGuid();
        var game = Guid.NewGuid().ToString();
        var card = Guid.NewGuid().ToString();
        var action = Guid.NewGuid();
        var checkpoint = new ReplayHistoryCheckpoint(3, game, 0, JsonSerializer.SerializeToElement(new {
            game = new { gameId = game }, zones = new { field = Array.Empty<string>() }, execution = new { secret = 1 }
        }));
        long seq = 0;
        ReplayHistoryNode Node(string type, string stage, object data, params ReplayStateChange[] changes)
        {
            seq++;
            return new(3, game, seq, seq - 1, Guid.NewGuid(), Guid.NewGuid(), null, action, host, "player", 1,
                1, host, "domain", type, stage, DateTime.UtcNow, 0, "standard", JsonSerializer.SerializeToElement(data), changes);
        }
        var blockedHit = Guid.NewGuid();
        ReplayHistoryNode Message(string method, object payload, Guid? parent = null, string audience = "Group (All): Game_room")
            => Node("message", "emitted", new { method, payload, audience }) with { Kind = "presentation", ParentEventId = parent };
        var summoned = new ReplayStateChange("set", ["zones", "field"], JsonSerializer.SerializeToElement(new[]{card}));
        var executionChange = new ReplayStateChange("set", ["execution", "secret"], JsonSerializer.SerializeToElement(2));
        var events = new[] {
            Node("operation.requested", "accepted", new { actionType = "PlayCard" }),
            Node("Summon", "committed", new { parameters = new { SummonedCard = new { instanceId = card } } }),
            Node("CardMoved", "committed", new { }, summoned),
            Node("Damage", "committed", new { }) with { EventId = blockedHit },
            Node("Damage", "committed", new { }),
            Message("CardMoved", new { cardUid = card, fromZone = "Hand", toZone = "Field" }),
            Message("DamageDealt", new { targetUid = card, damageAmount = 0 }, blockedHit),
            Message("DamageDealt", new { targetUid = card, damageAmount = 9 }, audience: "Group: Player_guest"),
            Message("DamageDealt", new { targetUid = card, damageAmount = 2 }),
            Message("PlayerChoiceRequested", new { secret = true }),
            Node("operation.completed", "completed", new { }, executionChange),
            Node("GameEnd", "committed", new { }),
            Node("recording.closed", "closed", new { winnerId = host })
        };
        await history.AppendAsync(game, host, guest, 0, events, [checkpoint], default);
        var replay = await ReplayPlayback.ReadAsync(history, game, default);
        var summon = replay.Steps[0];
        if (summon.Kind != "Summon" || summon.CardId != card || summon.Changes.Single().Path[^1] != "field")
            throw new Exception("The summon step must already show the follower on the board.");
        if (replay.Opening.ContainsKey("execution") || replay.Steps.SelectMany(s => s.Changes).Any(c => c.Path[0] == "execution"))
            throw new Exception("Gameplay playback must only contain presentation state.");
        if (!replay.Steps.Select(s => s.Kind).SequenceEqual(new[] {"Summon", "Damage", "Damage", "GameEnd"}))
            throw new Exception("Unexpected presentation steps.");
        if (summon.Effects.Single().Method != "CardMoved" || replay.Steps[1].Effects.Single().Payload["damageAmount"].GetInt32() != 0 ||
            replay.Steps[2].Effects.Single().Payload["damageAmount"].GetInt32() != 2)
            throw new Exception("Public cast and hit effects must be attached exactly once to their own step.");
        var turn = ReplayVisual.FromMessage("TurnStarted", JsonSerializer.SerializeToElement(new {
            SourcePlayerId = host, GameState = new { hidden = true }
        }), "Group: Game_room");
        if (turn?.Payload.Count != 1 || turn.Payload["sourcePlayerId"].GetGuid() != host)
            throw new Exception("Legacy turn effects must not include another board snapshot.");
    }
}
