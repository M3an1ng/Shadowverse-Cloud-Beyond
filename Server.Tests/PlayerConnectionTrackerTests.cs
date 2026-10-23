using ShadowverseBeyond.Server.Hubs;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public class PlayerConnectionTrackerTests
{
    [Fact]
    public void UnregisterUnknownOldConnectionKeepsPlayerOnlineWhenNewConnectionExists()
    {
        var tracker = new PlayerConnectionTracker();
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        tracker.Register(gameId, playerId, "new-connection");

        var stillConnected = tracker.Unregister(gameId, playerId, "old-connection");

        Assert.True(stillConnected);
        Assert.True(tracker.IsConnected(gameId, playerId));
    }

    [Fact]
    public void UnregisterLastConnectionMarksPlayerOffline()
    {
        var tracker = new PlayerConnectionTracker();
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        tracker.Register(gameId, playerId, "only-connection");

        var stillConnected = tracker.Unregister(gameId, playerId, "only-connection");

        Assert.False(stillConnected);
        Assert.False(tracker.IsConnected(gameId, playerId));
    }
}
