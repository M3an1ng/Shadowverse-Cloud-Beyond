using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public sealed class MatchServiceFormatConsistencyTests
{
    [Fact]
    public void ListCurrentRooms_ReturnsCurrentRoomSnapshots()
    {
        var service = new MatchService(NullLogger<MatchService>.Instance);
        var hostId = Guid.NewGuid();
        var guestId = Guid.NewGuid();

        var create = service.Create(hostId, useAiFallback: false);
        Assert.True(create.Success);
        var room = create.Room!;

        var join = service.TryJoin(room.RoomId, room.JoinCode, guestId);
        Assert.True(join.Success, join.Message);

        Assert.True(service.MarkReady(room.RoomId, hostId).Success);
        Assert.True(service.MarkReady(room.RoomId, guestId).Success);
        Assert.True(service.SubmitDeckSelection(room.RoomId, hostId, new MatchDeckSelection
        {
            DeckId = Guid.NewGuid(),
            Environment = DeckEnvironment.Open,
            Rule = DeckRule.Standard
        }).Success);
        Assert.True(service.SubmitDeckSelection(room.RoomId, guestId, new MatchDeckSelection
        {
            DeckId = Guid.NewGuid(),
            Environment = DeckEnvironment.Open,
            Rule = DeckRule.Standard
        }).Success);
        Assert.True(service.Start(room.RoomId, hostId).Success);

        var rooms = service.ListCurrentRooms();

        var listedRoom = Assert.Single(rooms);
        Assert.Equal(room.RoomId, listedRoom.RoomId);
        Assert.Equal(MatchStatus.Started, listedRoom.Status);
        Assert.False(string.IsNullOrWhiteSpace(listedRoom.ActiveGameId));

        listedRoom.Status = MatchStatus.Closed;
        var refreshedRoom = Assert.Single(service.ListCurrentRooms());
        Assert.Equal(MatchStatus.Started, refreshedRoom.Status);
    }

    [Fact]
    public void Start_PvpModeMismatch_ReturnsNotReady()
    {
        var service = new MatchService(NullLogger<MatchService>.Instance);
        var hostId = Guid.NewGuid();
        var guestId = Guid.NewGuid();

        var create = service.Create(hostId, useAiFallback: false);
        Assert.True(create.Success);
        var room = create.Room!;

        var join = service.TryJoin(room.RoomId, room.JoinCode, guestId);
        Assert.True(join.Success, join.Message);

        Assert.True(service.MarkReady(room.RoomId, hostId).Success);
        Assert.True(service.MarkReady(room.RoomId, guestId).Success);

        var hostSelection = new MatchDeckSelection
        {
            DeckId = Guid.NewGuid(),
            Environment = DeckEnvironment.Open,
            Rule = DeckRule.Standard
        };
        var guestSelection = new MatchDeckSelection
        {
            DeckId = Guid.NewGuid(),
            Environment = DeckEnvironment.Open,
            Rule = DeckRule.DualClass
        };

        Assert.True(service.SubmitDeckSelection(room.RoomId, hostId, hostSelection).Success);
        Assert.True(service.SubmitDeckSelection(room.RoomId, guestId, guestSelection).Success);

        var start = service.Start(room.RoomId, hostId);
        Assert.False(start.Success);
        Assert.Equal(MatchOperationError.NotReady, start.Error);
        Assert.Contains("模式不一致", start.Message ?? string.Empty);
    }

    [Fact]
    public void Start_AiFallbackEnvironmentMismatch_ReturnsNotReady()
    {
        var service = new MatchService(NullLogger<MatchService>.Instance);
        var hostId = Guid.NewGuid();

        var create = service.Create(hostId, useAiFallback: true);
        Assert.True(create.Success);
        var room = create.Room!;

        Assert.True(service.MarkReady(room.RoomId, hostId).Success);

        var hostSelection = new MatchDeckSelection
        {
            DeckId = Guid.NewGuid(),
            Environment = DeckEnvironment.Open,
            Rule = DeckRule.Standard
        };
        var aiSelection = new MatchDeckSelection
        {
            DeckId = Guid.NewGuid(),
            Environment = (DeckEnvironment)1,
            Rule = DeckRule.Standard
        };

        Assert.True(service.SubmitDeckSelection(room.RoomId, hostId, hostSelection, aiSelection).Success);

        var start = service.Start(room.RoomId, hostId);
        Assert.False(start.Success);
        Assert.Equal(MatchOperationError.NotReady, start.Error);
        Assert.Contains("环境不一致", start.Message ?? string.Empty);
    }

    [Fact]
    public void Start_DevtoolsIgnoreFormatConsistency_AllowsMismatch()
    {
        var service = new MatchService(NullLogger<MatchService>.Instance);
        var hostId = Guid.NewGuid();
        var guestId = Guid.NewGuid();

        var create = service.Create(hostId, useAiFallback: false, devMatchConfig: new DevMatchConfig
        {
            IgnoreDeckFormatConsistency = true
        });
        Assert.True(create.Success);
        var room = create.Room!;

        var join = service.TryJoin(room.RoomId, room.JoinCode, guestId);
        Assert.True(join.Success, join.Message);

        Assert.True(service.MarkReady(room.RoomId, hostId).Success);
        Assert.True(service.MarkReady(room.RoomId, guestId).Success);

        var hostSelection = new MatchDeckSelection
        {
            DeckId = Guid.NewGuid(),
            Environment = DeckEnvironment.Open,
            Rule = DeckRule.Standard
        };
        var guestSelection = new MatchDeckSelection
        {
            DeckId = Guid.NewGuid(),
            Environment = (DeckEnvironment)1,
            Rule = DeckRule.DualClass
        };

        Assert.True(service.SubmitDeckSelection(room.RoomId, hostId, hostSelection).Success);
        Assert.True(service.SubmitDeckSelection(room.RoomId, guestId, guestSelection).Success);

        var start = service.Start(room.RoomId, hostId);
        Assert.True(start.Success, start.Message);
        Assert.NotNull(start.Room);
    }

    [Fact]
    public void Start_MissingFormatMetadata_FallsBackToOpenAndStandard()
    {
        var service = new MatchService(NullLogger<MatchService>.Instance);
        var hostId = Guid.NewGuid();
        var guestId = Guid.NewGuid();

        var create = service.Create(hostId, useAiFallback: false);
        Assert.True(create.Success);
        var room = create.Room!;

        var join = service.TryJoin(room.RoomId, room.JoinCode, guestId);
        Assert.True(join.Success, join.Message);

        Assert.True(service.MarkReady(room.RoomId, hostId).Success);
        Assert.True(service.MarkReady(room.RoomId, guestId).Success);

        var hostSelection = new MatchDeckSelection
        {
            DeckId = Guid.NewGuid()
        };
        var guestSelection = new MatchDeckSelection
        {
            DeckId = Guid.NewGuid()
        };

        Assert.True(service.SubmitDeckSelection(room.RoomId, hostId, hostSelection).Success);
        Assert.True(service.SubmitDeckSelection(room.RoomId, guestId, guestSelection).Success);

        var start = service.Start(room.RoomId, hostId);
        Assert.True(start.Success, start.Message);
        Assert.NotNull(start.Room);
    }
}
