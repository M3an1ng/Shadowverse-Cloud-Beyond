using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Hubs;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public class GameServiceLifecycleTests
{
    [Fact]
    public void ClosingRoom_ReleasesIdleGameState()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var matchService = new MatchService(NullLogger<MatchService>.Instance);
        var hostId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var room = matchService.Create(hostId).Room!;
        Assert.True(matchService.TryJoin(room.RoomId, room.JoinCode, guestId).Success);
        Assert.True(matchService.MarkReady(room.RoomId, hostId).Success);
        Assert.True(matchService.MarkReady(room.RoomId, guestId).Success);
        Assert.True(matchService.SubmitDeckSelection(room.RoomId, hostId, new MatchDeckSelection { DeckId = Guid.NewGuid() }).Success);
        Assert.True(matchService.SubmitDeckSelection(room.RoomId, guestId, new MatchDeckSelection { DeckId = Guid.NewGuid() }).Success);
        var gameId = matchService.Start(room.RoomId, hostId).Room!.ActiveGameId!;
        var pendingActions = new Mock<IPendingActionService>();
        var choiceId = Guid.NewGuid();
        var pendingChoice = new PendingChoiceContext(
            new PlayerChoiceRequest { GameId = gameId, PendingChoiceId = choiceId, RequestingPlayerId = hostId },
            new TaskCompletionSource<PlayerChoiceResult>(),
            CancellationToken.None);
        pendingActions.Setup(p => p.GetPendingChoiceForPlayer(gameId, hostId)).Returns(pendingChoice);
        var gameService = CreateGameService(services, matchService, pendingActions.Object);
        var gameState = new GameState(gameId, new PlayerState(hostId), new PlayerState(guestId), hostId);
        GetStore<GameState>(gameService, "_gameStates")[gameId] = gameState;

        matchService.Close(room.RoomId, "test close");

        Assert.Null(gameService.GetGameState(gameId));
        Assert.Empty(GetStore<byte>(gameService, "_retiringGameIds"));
        pendingActions.Verify(p => p.ForceResolveChoice(choiceId, It.Is<PlayerChoiceResult>(choice => choice.WasCancelled)), Times.Once);
    }

    [Fact]
    public async Task FinishedGame_StopsProcessingLoop_ButRetainsFinalState()
    {
        using var services = new ServiceCollection()
            .AddSingleton<IGameContextAccessor, GameContextAccessor>()
            .BuildServiceProvider();
        var player1 = new PlayerState(Guid.NewGuid());
        var player2 = new PlayerState(Guid.NewGuid());
        var gameState = new GameState(Guid.NewGuid().ToString("D"), player1, player2, player1.PlayerId)
        {
            Status = GameStatus.Finished
        };
        var gameService = new GameService(
            Mock.Of<IHubContext<GameHub>>(),
            NullLogger<GameService>.Instance,
            Mock.Of<IMapper>(),
            Mock.Of<IClientMethodMapper>(),
            Mock.Of<IGameDataProvider>(),
            Mock.Of<IMatchService>(),
            Mock.Of<IFirstPlayerSelector>(),
            services.GetRequiredService<IServiceScopeFactory>(),
            Mock.Of<IPendingActionService>());

        var states = GetStore<GameState>(gameService, "_gameStates");
        states[gameState.GameId] = gameState;

        await gameService.EnqueuePlayerActionAsync(gameState.GameId, new PlayerActionWrapper
        {
            ActionType = "EndTurn",
            PlayerId = player1.PlayerId
        });

        var loops = GetStore<Task>(gameService, "_gameProcessingLoops");
        var cancellations = GetStore<CancellationTokenSource>(gameService, "_gameLoopCancellationTokens");
        var queues = GetStore<ConcurrentQueue<PlayerActionWrapper>>(gameService, "_playerActionQueues");
        Assert.True(SpinWait.SpinUntil(
            () => !loops.ContainsKey(gameState.GameId) && !cancellations.ContainsKey(gameState.GameId)
                && !queues.ContainsKey(gameState.GameId),
            TimeSpan.FromSeconds(3)));
        Assert.Same(gameState, gameService.GetGameState(gameState.GameId));

        await gameService.EnqueuePlayerActionAsync(gameState.GameId, new PlayerActionWrapper
        {
            ActionType = "EndTurn",
            PlayerId = player2.PlayerId
        });
        Assert.True(SpinWait.SpinUntil(
            () => !loops.ContainsKey(gameState.GameId) && !queues.ContainsKey(gameState.GameId),
            TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public async Task DetachedGame_DrainsQueuedActionBeforeReleasingState()
    {
        using var services = new ServiceCollection()
            .AddSingleton<IGameContextAccessor, GameContextAccessor>()
            .BuildServiceProvider();
        var matchService = new Mock<IMatchService>();
        var gameService = CreateGameService(services, matchService.Object, Mock.Of<IPendingActionService>());
        var player1 = new PlayerState(Guid.NewGuid());
        var player2 = new PlayerState(Guid.NewGuid());
        var gameId = Guid.NewGuid().ToString("D");
        GetStore<GameState>(gameService, "_gameStates")[gameId] = new GameState(gameId, player1, player2, player1.PlayerId);

        await gameService.EnqueuePlayerActionAsync(gameId, new PlayerActionWrapper
        {
            ActionType = "EndTurn",
            PlayerId = player1.PlayerId
        });
        matchService.Raise(m => m.GameDetached += null, gameId);

        Assert.True(SpinWait.SpinUntil(
            () => gameService.GetGameState(gameId) == null
                && !GetStore<Task>(gameService, "_gameProcessingLoops").ContainsKey(gameId)
                && !GetStore<ConcurrentQueue<PlayerActionWrapper>>(gameService, "_playerActionQueues").ContainsKey(gameId),
            TimeSpan.FromSeconds(3)));
    }

    private static GameService CreateGameService(IServiceProvider services, IMatchService matchService, IPendingActionService pendingActions)
    {
        return new GameService(
            Mock.Of<IHubContext<GameHub>>(),
            NullLogger<GameService>.Instance,
            Mock.Of<IMapper>(),
            Mock.Of<IClientMethodMapper>(),
            Mock.Of<IGameDataProvider>(),
            matchService,
            Mock.Of<IFirstPlayerSelector>(),
            services.GetRequiredService<IServiceScopeFactory>(),
            pendingActions);
    }

    private static ConcurrentDictionary<string, TValue> GetStore<TValue>(GameService gameService, string fieldName)
    {
        var field = typeof(GameService).GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return (ConcurrentDictionary<string, TValue>)field!.GetValue(gameService)!;
    }
}
