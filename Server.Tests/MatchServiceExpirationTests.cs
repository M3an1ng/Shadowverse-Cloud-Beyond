using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Services;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public sealed class MatchServiceExpirationTests
{
    [Fact]
    public void RefreshExpiration_ExtendsRoomLifetimeForParticipant()
    {
        var service = new MatchService(NullLogger<MatchService>.Instance);
        var hostId = Guid.NewGuid();

        var create = service.Create(hostId, ttl: TimeSpan.FromMinutes(1));
        Assert.True(create.Success);
        var room = create.Room!;
        var originalExpiresAt = room.ExpiresAt;

        var result = service.RefreshExpiration(room.RoomId, hostId);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Room);
        Assert.Equal(room.RoomId, result.Room!.RoomId);
        Assert.True(result.Room.ExpiresAt > originalExpiresAt);
    }
}
