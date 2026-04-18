using System;

namespace ShadowverseBeyond.Server.Hubs
{
    public static class SignalRGroupNames
    {
        private const string GamePrefix = "Game_";
        private const string PlayerPrefix = "Player_";

        // Group is room-scoped: all rounds in a room share the same channel.
        public static string Game(string roomId)
        {
            return $"{GamePrefix}{NormalizeRoomId(roomId)}";
        }

        public static string Game(Guid roomId)
        {
            return Game(roomId.ToString("D"));
        }

        public static string Player(string roomId, Guid playerId)
        {
            return $"{PlayerPrefix}{playerId:D}_{NormalizeRoomId(roomId)}";
        }

        public static string Player(Guid roomId, Guid playerId)
        {
            return Player(roomId.ToString("D"), playerId);
        }

        private static string NormalizeRoomId(string roomId)
        {
            if (Guid.TryParse(roomId, out var parsed))
            {
                return parsed.ToString("D");
            }

            return roomId?.Trim() ?? string.Empty;
        }
    }
}
