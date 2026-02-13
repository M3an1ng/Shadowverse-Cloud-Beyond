using System;

namespace ShadowverseBeyond.Server.Hubs
{
    public static class SignalRGroupNames
    {
        private const string GamePrefix = "Game_";
        private const string PlayerPrefix = "Player_";

        public static string Game(string gameId)
        {
            return $"{GamePrefix}{NormalizeGameId(gameId)}";
        }

        public static string Game(Guid gameId)
        {
            return Game(gameId.ToString("D"));
        }

        public static string Player(string gameId, Guid playerId)
        {
            return $"{PlayerPrefix}{playerId:D}_{NormalizeGameId(gameId)}";
        }

        public static string Player(Guid gameId, Guid playerId)
        {
            return Player(gameId.ToString("D"), playerId);
        }

        private static string NormalizeGameId(string gameId)
        {
            if (Guid.TryParse(gameId, out var parsed))
            {
                return parsed.ToString("D");
            }

            return gameId?.Trim() ?? string.Empty;
        }
    }
}
