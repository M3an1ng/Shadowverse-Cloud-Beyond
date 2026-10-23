using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public sealed class GameContextLifetimeTests
{
    [Fact]
    public void RootScope_ActivatesAndClearsItsContext()
    {
        using var fixture = new ContextFixture();
        var context = fixture.Open("root");
        Assert.Same(context, fixture.Accessor.GameContext);
        context.Dispose();
        Assert.Null(fixture.Accessor.GameContext);
    }

    [Fact]
    public void AccessorInstances_DoNotShareOrClearEachOthersContext()
    {
        using var first = new ContextFixture();
        using var second = new ContextFixture();
        using var firstContext = first.Open("first");
        Assert.Null(second.Accessor.GameContext);
        var secondContext = second.Open("second");
        Assert.Same(firstContext, first.Accessor.GameContext);
        Assert.Same(secondContext, second.Accessor.GameContext);
        Assert.NotSame(firstContext.BuffService, secondContext.BuffService);
        secondContext.Dispose();
        Assert.Same(firstContext, first.Accessor.GameContext);
    }

    [Fact]
    public void NestedScope_RestoresItsParent()
    {
        using var fixture = new ContextFixture();
        using var parent = fixture.Open("parent");
        using (var child = fixture.Open("child"))
        {
            Assert.Same(child, fixture.Accessor.GameContext);
        }
        Assert.Same(parent, fixture.Accessor.GameContext);
    }

    [Fact]
    public void RecordCopy_DoesNotOwnOrDisposeTheOriginalScope()
    {
        using var fixture = new ContextFixture();
        using var parent = fixture.Open("parent");
        using (var copy = parent with { IsCancellable = true })
        {
            Assert.True(copy.IsCancellable);
            Assert.False(parent.IsCancellable);
            Assert.Same(parent, fixture.Accessor.GameContext);
        }
        Assert.Same(parent, fixture.Accessor.GameContext);
    }

    [Fact]
    public void RepeatedDisposal_DoesNotClearANewerScope()
    {
        using var fixture = new ContextFixture();
        var previous = fixture.Open("previous");
        previous.Dispose();
        using var current = fixture.Open("current");
        previous.Dispose();
        Assert.Same(current, fixture.Accessor.GameContext);
    }

    [Fact]
    public async Task ContextAndEnvironment_SurviveAwait()
    {
        using var fixture = new ContextFixture();
        using var context = fixture.Open("await", DeckEnvironment.Unlimited);
        await Task.Yield();
        Assert.Same(context, fixture.Accessor.GameContext);
        Assert.Equal(DeckEnvironment.Unlimited, context.CurrentEnvironment);
    }

    [Fact]
    public async Task ChildTaskScope_DoesNotRevokeTheCallingTaskScope()
    {
        using var fixture = new ContextFixture();
        using var parent = fixture.Open("parent");
        await Task.Run(async () =>
        {
            Assert.Same(parent, fixture.Accessor.GameContext);
            using (var child = fixture.Open("child"))
            {
                await Task.Yield();
                Assert.Same(child, fixture.Accessor.GameContext);
            }
            Assert.Same(parent, fixture.Accessor.GameContext);
        });
        Assert.Same(parent, fixture.Accessor.GameContext);
    }

    [Fact]
    public async Task ConcurrentGames_DoNotObserveEachOthersGamePlayerOrEnvironment()
    {
        using var fixture = new ContextFixture();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrivals = 0;
        async Task Run(string id, DeckEnvironment environment)
        {
            using var context = fixture.Open(id, environment);
            var player = context.RequestingPlayerId;
            if (Interlocked.Increment(ref arrivals) == 2) ready.SetResult();
            await release.Task;
            Assert.Same(context, fixture.Accessor.GameContext);
            Assert.Equal(id, fixture.Accessor.GameContext!.CurrentGameState.GameId);
            Assert.Equal(player, fixture.Accessor.GameContext.RequestingPlayerId);
            Assert.Equal(environment, fixture.Accessor.GameContext.CurrentEnvironment);
        }
        var games = new[]
        {
            Task.Run(() => Run("rotation", DeckEnvironment.Rotation)),
            Task.Run(() => Run("unlimited", DeckEnvironment.Unlimited)),
        };
        try { await ready.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { release.TrySetResult(); }
        await Task.WhenAll(games);
        Assert.Null(fixture.Accessor.GameContext);
    }

    [Fact]
    public async Task DisposedRoot_IsRevokedInAnEscapedExecutionContext()
    {
        using var fixture = new ContextFixture();
        var root = fixture.Open("root");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var escaped = Task.Run(async () =>
        {
            Assert.Same(root, fixture.Accessor.GameContext);
            entered.SetResult();
            await release.Task;
            Assert.Null(fixture.Accessor.GameContext);
        });
        try { await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { root.Dispose(); release.TrySetResult(); }
        await escaped;
    }

    [Fact]
    public async Task RestoredParent_IsStillRevokedInItsOriginalCapturedExecutionContext()
    {
        using var fixture = new ContextFixture();
        var root = fixture.Open("root");
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var escaped = Task.Run(async () =>
        {
            await release.Task;
            Assert.Null(fixture.Accessor.GameContext);
        });
        try
        {
            using (fixture.Open("nested")) { }
            Assert.Same(root, fixture.Accessor.GameContext);
        }
        finally { root.Dispose(); release.TrySetResult(); }
        await escaped;
    }

    [Fact]
    public void ExceptionUnwinding_RestoresTheParentAndThenClearsTheRoot()
    {
        using var fixture = new ContextFixture();
        using (var parent = fixture.Open("parent"))
        {
            Assert.Throws<InvalidOperationException>((Action)(() =>
            {
                using var child = fixture.Open("child");
                throw new InvalidOperationException("expected failure");
            }));
            Assert.Same(parent, fixture.Accessor.GameContext);
        }
        Assert.Null(fixture.Accessor.GameContext);
    }

    [Fact]
    public void CancellationUnwinding_ClearsTheContextAndPreservesTheToken()
    {
        using var fixture = new ContextFixture();
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() =>
        {
            using var context = fixture.Open("cancel", token: cancellation.Token);
            Assert.Equal(cancellation.Token, context.CancellationToken);
            cancellation.Cancel();
            context.CancellationToken.ThrowIfCancellationRequested();
        });
        Assert.Null(fixture.Accessor.GameContext);
    }

    [Fact]
    public void Environment_IsReadFromTheCurrentGameAndRejectsMismatchedPlayers()
    {
        using var fixture = new ContextFixture();
        using var context = fixture.Open("environment");
        Assert.Equal(DeckEnvironment.Rotation, context.CurrentEnvironment);
        context.CurrentGameState.Player2.Environment = DeckEnvironment.Open;
        Assert.Throws<InvalidOperationException>(() => context.CurrentEnvironment);
    }

    private sealed class ContextFixture : IDisposable
    {
        private readonly ServiceProvider _services;
        public GameContextAccessor Accessor { get; } = new();
        public ContextFixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton(Mock.Of<IBuffService>());
            services.AddSingleton(Mock.Of<IEventHistoryService>());
            _services = services.BuildServiceProvider();
        }
        public GameContext Open(string id, DeckEnvironment environment = DeckEnvironment.Rotation, CancellationToken token = default)
        {
            var player = new PlayerState(Guid.NewGuid()) { Environment = environment };
            var opponent = new PlayerState(Guid.NewGuid()) { Environment = environment };
            var game = new GameState(id, player, opponent, player.PlayerId)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>(),
            };
            return new GameContext(_services, Mock.Of<IMapper>(), Accessor, game, player.PlayerId, token);
        }
        public void Dispose() => _services.Dispose();
    }
}
