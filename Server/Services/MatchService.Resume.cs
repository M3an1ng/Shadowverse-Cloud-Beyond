using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Services;

public partial class MatchService
{
    public MatchRoom CreateResumedRoom(GameState game)
    {
        if (game.ReplayBranch is null) throw new ArgumentException("resumed_game_requires_origin");
        var created = Create(game.Player1.PlayerId);
        if (!created.Success) throw new InvalidOperationException(created.Message);
        var joined = TryJoin(created.Room!.RoomId,created.Room.JoinCode,game.Player2.PlayerId);
        if (!joined.Success) throw new InvalidOperationException(joined.Message);
        var room = _rooms[created.Room.RoomId];
        lock (room)
        {
            room.IsResumedRoom = true;
            room.IsPublic = false;
            room.ActiveGameId = game.GameId;room.Round = 1;room.Status = MatchStatus.Started;
            room.HostReady = true;room.GuestReady = true;
            room.HostDeckSelection = Selection(game.Player1);room.GuestDeckSelection = Selection(game.Player2);
            _gameToRoom[game.GameId] = room.RoomId;
            return Clone(room);
        }
    }

    private static MatchDeckSelection Selection(PlayerState player) => new() {
        UserId = player.PlayerId,DeckId = player.UserDeckId,DeckClass = player.Class,SecondaryClass = player.SecondaryClass,
        Environment = player.Environment,Rule = player.Rule
    };
}
