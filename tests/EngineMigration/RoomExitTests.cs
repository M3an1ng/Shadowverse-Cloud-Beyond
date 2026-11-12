using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Controllers;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Hubs;
using ShadowverseBeyond.Server.Hubs.ClientInterfaces;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class RoomExitTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    internal static async Task RunAsync()
    {
        await PendingChoicesAndBroadcastAsync();
        await FinishedGameBarrierAsync();
        await IdleFinishedGameAsync();
        await RetiringAndReleasedGameAsync();
        await ConcurrentRoomLeavesAsync();
        await TimeoutKeepsSeatAsync();
        DraftHandoff();
        Console.WriteLine("Room exit: pending choices, broadcast/read barrier, idle/retiring/released games, concurrent leaves, timeout and draft handoff passed.");
    }

    private static async Task PendingChoicesAndBroadcastAsync()
    {
        using var fixture = new Fixture();
        var registered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var read = fixture.Games.ReadStateAsync(fixture.Game.GameId, async (game, _) =>
        {
            var first = fixture.Pending.WaitForPlayerChoiceAsync(game.GameId,
                new PlayerChoiceRequest { RequestingPlayerId = game.Player1.PlayerId }, default);
            registered.SetResult();
            Check((await first).WasCancelled, "Room exit did not cancel the existing choice.");
            var next = await fixture.Pending.WaitForPlayerChoiceAsync(game.GameId,
                new PlayerChoiceRequest { RequestingPlayerId = game.Player2.PlayerId }, default);
            Check(next.WasCancelled, "A subsequent choice blocked room exit again.");
            return JsonSerializer.SerializeToElement(new { released = true });
        }, default);
        await registered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var leave = fixture.Controller.Leave(fixture.Room.RoomId,
            new() { PlayerId = fixture.Room.GuestPlayerId!.Value, Token = fixture.Room.GuestToken! });
        var group = await fixture.BroadcastEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Check(group == SignalRGroupNames.Game(fixture.Room.RoomId), "GameOver used the game ID instead of the room group.");
        Check(!leave.IsCompleted && fixture.HasMapping(), "Leave detached the game before GameOver delivery completed.");
        fixture.ReleaseBroadcast.SetResult();
        Check((await leave.WaitAsync(TimeSpan.FromSeconds(3))).Result is OkObjectResult, "Room exit failed.");
        Check((await read).GetProperty("released").GetBoolean(), "The blocked queue did not resume.");
        Check(!fixture.HasMapping() && fixture.Matches.Get(fixture.Room.RoomId).Room!.GuestPlayerId == null,
            "The completed exit retained the guest seat or game mapping.");
        Check(fixture.SettledBeforeDetach, "CompleteGame did not finish before detachment.");
    }

    private static async Task FinishedGameBarrierAsync()
    {
        using var fixture = new Fixture();
        var first = fixture.Games.EndGameForRoomLeaveAsync(fixture.Game.GameId, fixture.Room.HostPlayerId, default);
        await fixture.BroadcastEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Check(fixture.Game.IsGameOver, "Fixture did not reach the committed terminal state.");
        var second = fixture.Games.EndGameForRoomLeaveAsync(fixture.Game.GameId, fixture.Room.HostPlayerId, default);
        Check(!second.IsCompleted && fixture.HasMapping(), "A finished state bypassed the outstanding broadcast.");
        Check(fixture.QueuedActions().Single() is ShadowverseBeyond.Server.Models.MatchStateRead,
            "An already finished game queued Disconnect instead of a read barrier.");
        fixture.ReleaseBroadcast.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(3));
        Check(fixture.Matches.Get(fixture.Room.RoomId).Room!.GuestWins == 1, "Repeated termination counted the result twice.");
        fixture.Matches.Leave(fixture.Room.RoomId, fixture.Room.HostPlayerId, token: fixture.Room.HostToken);
    }

    private static async Task TimeoutKeepsSeatAsync()
    {
        using var fixture = new Fixture();
        var leave = fixture.Controller.Leave(fixture.Room.RoomId,
            new() { PlayerId = fixture.Room.GuestPlayerId!.Value, Token = fixture.Room.GuestToken! });
        await fixture.BroadcastEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var response = await leave.WaitAsync(TimeSpan.FromSeconds(18));
        Check(response.Result is ObjectResult { StatusCode: 504 }, "A blocked broadcast did not produce a bounded HTTP timeout.");
        Check(fixture.HasMapping() && fixture.Matches.Get(fixture.Room.RoomId).Room!.GuestPlayerId.HasValue,
            "A timed-out exit detached the room anyway.");
        fixture.ReleaseBroadcast.SetResult();
        var retry = await fixture.Controller.Leave(fixture.Room.RoomId,
            new() { PlayerId = fixture.Room.GuestPlayerId.Value, Token = fixture.Room.GuestToken! });
        Check(retry.Result is OkObjectResult && !fixture.HasMapping(), "A timed-out exit could not be completed on retry.");
    }

    private static async Task IdleFinishedGameAsync()
    {
        using var fixture = new Fixture();
        fixture.Game.Status = GameStatus.Finished;
        fixture.Pending.CancelChoicesForGame(fixture.Game.GameId);
        foreach (var error in new MatchOperationError?[] { null, MatchOperationError.NotFound, MatchOperationError.AlreadyClosed })
        {
            fixture.CompletionError = error;
            await fixture.Games.EndGameForRoomLeaveAsync(fixture.Game.GameId, fixture.Room.HostPlayerId, default);
            Check(!fixture.HasProcessingLoop() && fixture.QueuedActions().Length == 0,
                "Leaving an idle finished game started a new processing loop.");
            Check(!fixture.HasChoiceCancellation(), "Finished game retained the choice-cancellation marker.");
        }
    }

    private static async Task RetiringAndReleasedGameAsync()
    {
        using var fixture = new Fixture();
        var first = fixture.Games.EndGameForRoomLeaveAsync(fixture.Game.GameId, fixture.Room.HostPlayerId, default);
        await fixture.BroadcastEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        fixture.Matches.Close(fixture.Room.RoomId, "concurrent close");
        await fixture.Games.EndGameForRoomLeaveAsync(fixture.Game.GameId, fixture.Room.GuestPlayerId!.Value, default)
            .WaitAsync(TimeSpan.FromSeconds(1));
        Check(!fixture.HasChoiceCancellation(), "A terminal retiring game retained choice cancellation.");
        fixture.ReleaseBroadcast.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(3));
        await fixture.WaitForProcessingLoopAsync();
        Check(fixture.Games.GetGameState(fixture.Game.GameId) == null, "Retired fixture was not released.");
        await fixture.Games.EndGameForRoomLeaveAsync(fixture.Game.GameId, fixture.Room.HostPlayerId, default);
        Check(!fixture.HasProcessingLoop(), "Released-game exit restarted the processing loop.");
    }

    private static async Task ConcurrentRoomLeavesAsync()
    {
        using var fixture = new Fixture();
        var host = fixture.Controller.Leave(fixture.Room.RoomId,
            new() { PlayerId = fixture.Room.HostPlayerId, Token = fixture.Room.HostToken });
        await fixture.BroadcastEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var guest = fixture.Controller.Leave(fixture.Room.RoomId,
            new() { PlayerId = fixture.Room.GuestPlayerId!.Value, Token = fixture.Room.GuestToken! });
        fixture.ReleaseBroadcast.SetResult();
        var responses = await Task.WhenAll(host, guest).WaitAsync(TimeSpan.FromSeconds(3));
        Check(responses.All(response => response.Result is OkObjectResult), "Concurrent participant exits returned a conflict.");
        Check(!fixture.HasMapping(), "Concurrent participant exits retained the game mapping.");
        await fixture.WaitForProcessingLoopAsync();
    }

    private static void DraftHandoff()
    {
        var matches = new MatchService(NullLogger<MatchService>.Instance);
        var drafts = new TakeTwoService(matches);
        // A real handoff always references a Started match, so prepare that state without draft randomisation.
        var mirage = matches.Create(mode: GameMode.Mirage).Room!;
        var guest = matches.TryJoin(mirage.RoomId, mirage.JoinCode).Room!;
        matches.MarkReady(mirage.RoomId, mirage.HostPlayerId);
        matches.MarkReady(mirage.RoomId, guest.GuestPlayerId!.Value);
        var started = matches.Start(mirage.RoomId, mirage.HostPlayerId).Room!;
        var draft = new TakeTwoRoom { Match = started };
        var rejected = false;
        try { drafts.Leave(draft); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && !draft.Closed && matches.Get(started.RoomId).Room!.Status == MatchStatus.Started,
            "A stale draft exit silently closed the handed-off match.");
        var waiting = new TakeTwoRoom();
        drafts.Leave(waiting); drafts.Leave(waiting);
        Check(waiting.Closed, "Waiting draft exit was not idempotent.");
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly MatchService Matches = new(NullLogger<MatchService>.Instance);
        internal readonly MatchRoom Room;
        internal readonly GameState Game;
        internal readonly GameService Games;
        internal readonly PendingActionService Pending;
        internal readonly MatchController Controller;
        internal readonly TaskCompletionSource<string> BroadcastEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource ReleaseBroadcast = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool SettledBeforeDetach;
        internal MatchOperationError? CompletionError;
        private readonly ServiceProvider services;
        private readonly ILoggerFactory logs = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        internal bool HasMapping() => Matches.TryGetRoomIdByGameId(Game.GameId, out _);
        private ConcurrentDictionary<string, Task> ProcessingLoops =>
            (ConcurrentDictionary<string, Task>)typeof(GameService).GetField("_gameProcessingLoops", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Games)!;
        internal bool HasProcessingLoop() => ProcessingLoops.ContainsKey(Game.GameId);
        internal async Task WaitForProcessingLoopAsync()
        {
            if (ProcessingLoops.TryGetValue(Game.GameId, out var loop)) await loop.WaitAsync(TimeSpan.FromSeconds(3));
        }
        internal bool HasChoiceCancellation() =>
            ((ConcurrentDictionary<string, byte>)typeof(PendingActionService).GetField("_endingGames", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Pending)!)
            .ContainsKey(Game.GameId);
        internal ShadowverseBeyond.Server.Models.PlayerActionWrapper[] QueuedActions() =>
            ((ConcurrentDictionary<string, ConcurrentQueue<ShadowverseBeyond.Server.Models.PlayerActionWrapper>>)typeof(GameService)
                .GetField("_playerActionQueues", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Games)!)
            .TryGetValue(Game.GameId, out var queue) ? queue.ToArray() : [];

        internal Fixture()
        {
            var room = Matches.Create(mode: GameMode.Mirage).Room!;
            var joined = Matches.TryJoin(room.RoomId, room.JoinCode).Room!;
            Matches.MarkReady(room.RoomId, room.HostPlayerId);
            Matches.MarkReady(room.RoomId, joined.GuestPlayerId!.Value);
            Room = Matches.Start(room.RoomId, room.HostPlayerId).Room!;
            var abilities = Proxy<IAbilityManager>.Make((method, _) => method.Name switch
            {
                "GetTriggeredAbilitiesAsync" => Task.FromResult(new List<IAbility>()),
                "GetAbilitiesForCard" or "GetEffectiveAbilitiesForCard" => new List<IAbility>(),
                _ => null
            });
            Game = new GameState(Room.ActiveGameId!, new PlayerState(Room.HostPlayerId),
                new PlayerState(Room.GuestPlayerId!.Value), Room.HostPlayerId)
            {
                Status = GameStatus.InProgress, Phase = GamePhase.Main, AbilityManager = abilities,
                BuffManager = Proxy<IBuffManager>.Make((_, _) => null)
            };
            var clients = Proxy<IHubClients<IGameClient>>.Make((method, args) =>
                Proxy<IGameClient>.Make((call, _) => call.Name == "GameOver" ? HoldBroadcast((string)args![0]!) : Task.CompletedTask));
            var typedHub = Proxy<IHubContext<GameHub, IGameClient>>.Make((_, _) => clients);
            var rawClient = Proxy<IClientProxy>.Make((_, _) => Task.CompletedTask);
            var rawClients = Proxy<IHubClients>.Make((_, _) => rawClient);
            var rawHub = Proxy<IHubContext<GameHub>>.Make((_, _) => rawClients);
            var mapper = new MapperConfiguration(_ => { }).CreateMapper();
            var delivery = new SignalRClientMethodMapper(typedHub, NullLogger<SignalRClientMethodMapper>.Instance);
            var history = Proxy<IEventHistoryService>.Make((method, _) => method.ReturnType == typeof(Task) ? Task.CompletedTask : null);
            var collection = new ServiceCollection().AddSingleton<IMapper>(mapper)
                .AddSingleton(history)
                .AddSingleton<IGameContextAccessor, GameContextAccessor>()
                .AddSingleton(Proxy<IBuffService>.Make((_, _) => null))
                .AddSingleton<IGameService>(_ => Games!)
                .AddSingleton<IPendingActionService>(sp => new PendingActionService(typedHub,
                    NullLogger<PendingActionService>.Instance, sp, mapper, Matches))
                .AddScoped<IGameActionTransactionService>(sp => new GameActionTransactionService(
                    logs.CreateLogger<GameActionTransactionService>(), abilities,
                    new EventProcessor(logs.CreateLogger<EventProcessor>(), history), sp.GetRequiredService<IPendingActionService>(),
                    Proxy<IEffectExecutor>.Make((_, _) => null), delivery,
                    Proxy<IMetaProgressionService>.Make((_, _) => Task.CompletedTask),
                    Proxy<IWinrateStatsService>.Make((_, _) => Task.CompletedTask), Matches));
            services = collection.BuildServiceProvider();
            Pending = (PendingActionService)services.GetRequiredService<IPendingActionService>();
            var settlements = Proxy<IMatchService>.Make((method, args) =>
                method.Name == nameof(IMatchService.CompleteGame) && CompletionError.HasValue
                    ? MatchOperationResult.Fail(CompletionError.Value, "concurrent settlement already released")
                    : method.Invoke(Matches, args));
            Games = new GameService(rawHub, logs.CreateLogger<GameService>(), mapper, delivery,
                Proxy<IGameDataProvider>.Make((_, _) => null), settlements,
                Proxy<IFirstPlayerSelector>.Make((_, _) => null), services.GetRequiredService<IServiceScopeFactory>(),
                Pending, Proxy<IGameStatsRuntimeTracker>.Make((_, _) => null));
            var states = (ConcurrentDictionary<string, GameState>)typeof(GameService)
                .GetField("_gameStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Games)!;
            states[Game.GameId] = Game;
            Matches.GameDetached += _ => SettledBeforeDetach = Matches.Get(Room.RoomId).Room!.LastCompletedGameId == Game.GameId;
            Controller = new MatchController(Matches, NullLogger<MatchController>.Instance, typedHub, Games,
                Proxy<IDeckService>.Make((_, _) => null), Proxy<IDeckValidationService>.Make((_, _) => null),
                Proxy<IDeckCodeService>.Make((_, _) => null))
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        }

        private Task HoldBroadcast(string group)
        {
            BroadcastEntered.TrySetResult(group);
            return ReleaseBroadcast.Task;
        }

        public void Dispose()
        {
            ReleaseBroadcast.TrySetResult();
            services.Dispose();
            logs.Dispose();
        }
    }
}
