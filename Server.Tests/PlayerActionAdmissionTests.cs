using System.Collections.Concurrent;
using AutoMapper;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Hubs;
using ShadowverseBeyond.Server.Hubs.ClientInterfaces;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Options;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public class PlayerActionAdmissionTests
{
    [Theory]
    [InlineData("EndTurn")]
    [InlineData("Surrender")]
    public async Task SameRequestIdInDifferentGamesDoesNotSuppressEitherRequest(string action)
    {
        using var fixture = new HubFixture();
        var player = Guid.NewGuid();
        using var first = fixture.CreateHub(Guid.NewGuid(), player);
        using var second = fixture.CreateHub(Guid.NewGuid(), player);

        await Invoke(first, action, "req_same_client_id");
        await Invoke(second, action, "req_same_client_id");

        Assert.Equal(2, fixture.Received.Count);
        Assert.Equal(2, fixture.Received.Select(item => item.GameId).Distinct().Count());
    }

    [Theory]
    [InlineData("EndTurn")]
    [InlineData("Surrender")]
    public async Task SameRequestIdInSameGameAndActionIsAdmittedOnce(string action)
    {
        using var fixture = new HubFixture();
        var gameId = Guid.NewGuid();
        var player = Guid.NewGuid();
        using var first = fixture.CreateHub(gameId, player);
        using var second = fixture.CreateHub(gameId, player);

        await Invoke(first, action, "req_once");
        await Invoke(second, action, "req_once");

        Assert.Single(fixture.Received);
    }

    [Theory]
    [InlineData("EndTurn")]
    [InlineData("Surrender")]
    public async Task ConcurrentDuplicateRequestsAreAdmittedOnce(string action)
    {
        using var fixture = new HubFixture();
        var admission = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        fixture.GameService.Setup(service => service.EnqueuePlayerActionAsync(It.IsAny<string>(), It.IsAny<PlayerActionWrapper>()))
            .Returns<string, PlayerActionWrapper>((_, _) =>
            {
                Interlocked.Increment(ref calls);
                return admission.Task;
            });
        var gameId = Guid.NewGuid();
        var player = Guid.NewGuid();
        using var first = fixture.CreateHub(gameId, player);
        using var second = fixture.CreateHub(gameId, player);

        var firstRequest = Invoke(first, action, "req_concurrent");
        var secondRequest = Invoke(second, action, "req_concurrent");
        admission.SetResult(true);
        await Task.WhenAll(firstRequest, secondRequest);

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task SameRequestIdForDifferentPlayersAndActionsIsNotSuppressed()
    {
        using var fixture = new HubFixture();
        var gameId = Guid.NewGuid();
        using var first = fixture.CreateHub(gameId, Guid.NewGuid());
        using var second = fixture.CreateHub(gameId, Guid.NewGuid());

        await first.EndTurn("req_shared");
        await second.EndTurn("req_shared");
        await first.Surrender("req_shared");

        Assert.Equal(3, fixture.Received.Count);
    }

    [Theory]
    [InlineData("EndTurn")]
    [InlineData("Surrender")]
    public async Task FailedAdmissionDoesNotReserveRequestId(string action)
    {
        using var fixture = new HubFixture();
        var attempts = 0;
        fixture.GameService.Setup(service => service.EnqueuePlayerActionAsync(It.IsAny<string>(), It.IsAny<PlayerActionWrapper>()))
            .Returns<string, PlayerActionWrapper>((_, _) => Task.FromResult(++attempts > 1));
        using var hub = fixture.CreateHub(Guid.NewGuid(), Guid.NewGuid());

        await Invoke(hub, action, "req_retry");
        await Invoke(hub, action, "req_retry");
        await Invoke(hub, action, "req_retry");

        fixture.GameService.Verify(service => service.EnqueuePlayerActionAsync(It.IsAny<string>(), It.IsAny<PlayerActionWrapper>()), Times.Exactly(2));
    }

    [Theory]
    [InlineData("EndTurn")]
    [InlineData("Surrender")]
    public async Task ThrownAdmissionDoesNotReserveRequestIdOrKeepLock(string action)
    {
        using var fixture = new HubFixture();
        var attempts = 0;
        fixture.GameService.Setup(service => service.EnqueuePlayerActionAsync(It.IsAny<string>(), It.IsAny<PlayerActionWrapper>()))
            .Returns<string, PlayerActionWrapper>((_, _) => ++attempts == 1
                ? Task.FromException<bool>(new InvalidOperationException("queue admission failed"))
                : Task.FromResult(true));
        using var hub = fixture.CreateHub(Guid.NewGuid(), Guid.NewGuid());

        await Invoke(hub, action, "req_exception");
        await Invoke(hub, action, "req_exception");
        await Invoke(hub, action, "req_exception");

        Assert.Equal(2, attempts);
    }

    [Theory]
    [InlineData("EndTurn")]
    [InlineData("Surrender")]
    public async Task UnidentifiedRequestDoesNotSuppressLaterValidRequest(string action)
    {
        using var fixture = new HubFixture();
        using var invalid = fixture.CreateHub(Guid.Empty, Guid.NewGuid());
        using var valid = fixture.CreateHub(Guid.NewGuid(), Guid.NewGuid());

        await Invoke(invalid, action, "req_context");
        Assert.Empty(fixture.Received);
        await Invoke(valid, action, "req_context");

        Assert.Single(fixture.Received);
    }

    [Theory]
    [InlineData("EndTurn")]
    [InlineData("Surrender")]
    public async Task RequestExpiresOneMinuteAfterSuccessfulAdmission(string action)
    {
        using var fixture = new HubFixture();
        using var hub = fixture.CreateHub(Guid.NewGuid(), Guid.NewGuid());
        await Invoke(hub, action, "req_expiry");
        fixture.Clock.UtcNow += TimeSpan.FromSeconds(59);
        await Invoke(hub, action, "req_expiry");
        Assert.Single(fixture.Received);

        fixture.Clock.UtcNow += TimeSpan.FromSeconds(2);
        await Invoke(hub, action, "req_expiry");
        Assert.Equal(2, fixture.Received.Count);
        Assert.Equal(2, fixture.Received.Select(item => item.Action.ActionId).Distinct().Count());
        Assert.All(fixture.Received, item => Assert.NotEqual(Guid.Empty, item.Action.ActionId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task EmptyRequestIdIsNotEnqueued(string? requestId)
    {
        using var fixture = new HubFixture();
        using var hub = fixture.CreateHub(Guid.NewGuid(), Guid.NewGuid());

        await hub.EndTurn(requestId!);
        await hub.Surrender(requestId!);

        fixture.GameService.Verify(service => service.EnqueuePlayerActionAsync(It.IsAny<string>(), It.IsAny<PlayerActionWrapper>()), Times.Never);
    }

    private static Task Invoke(GameHub hub, string action, string requestId) =>
        action == "EndTurn" ? hub.EndTurn(requestId) : hub.Surrender(requestId);

    private sealed class HubFixture : IDisposable
    {
        private readonly ServiceProvider _services;
        public ConcurrentQueue<(string GameId, PlayerActionWrapper Action)> Received { get; } = new();
        public Mock<IGameService> GameService { get; } = new();
        public TestClock Clock { get; } = new();

        public HubFixture()
        {
            GameService.Setup(service => service.EnqueuePlayerActionAsync(It.IsAny<string>(), It.IsAny<PlayerActionWrapper>()))
                .Callback<string, PlayerActionWrapper>((game, action) => Received.Enqueue((game, action)))
                .Returns<string, PlayerActionWrapper>((_, _) => Task.FromResult(true));
            _services = new ServiceCollection()
                .AddLogging()
                .AddMemoryCache(options => options.Clock = Clock)
                .AddSingleton(GameService.Object)
                .AddSingleton(Mock.Of<IClientMethodMapper>())
                .AddSingleton(Mock.Of<IMapper>())
                .AddSingleton(Mock.Of<IMatchService>())
                .AddSingleton(Mock.Of<IGameChatService>())
                .AddSingleton(Mock.Of<IEmoteCatalogService>())
                .AddSingleton(Mock.Of<IMatchHistoryProjectionService>())
                .AddSingleton(Mock.Of<ICommittedEventJournal>())
                .AddSingleton(Mock.Of<ISnapshotStore>())
                .AddSingleton(Mock.Of<IRecordedEventMapper>())
                .AddSingleton(Mock.Of<ISpectatorQuestProgressRecorder>())
                .AddSingleton(Microsoft.Extensions.Options.Options.Create(new ReplayOptions()))
                .BuildServiceProvider();
        }

        public GameHub CreateHub(Guid gameId, Guid playerId)
        {
            var context = new Mock<HubCallerContext>();
            context.SetupGet(value => value.ConnectionId).Returns(Guid.NewGuid().ToString("N"));
            context.SetupGet(value => value.Features).Returns(new FeatureCollection());
            context.SetupGet(value => value.Items).Returns(new Dictionary<object, object?>
            {
                ["GameId"] = gameId.ToString("D"), ["PlayerId"] = playerId.ToString("D")
            });
            var caller = Mock.Of<IGameClient>();
            var clients = new Mock<IHubCallerClients<IGameClient>>();
            clients.SetupGet(value => value.Caller).Returns(caller);
            var hub = ActivatorUtilities.CreateInstance<GameHub>(_services);
            hub.Context = context.Object;
            hub.Clients = clients.Object;
            return hub;
        }

        public void Dispose() => _services.Dispose();
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }
}
