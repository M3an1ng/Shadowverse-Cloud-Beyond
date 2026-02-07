using System;
using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Domain.Events
{
    public static class AnnouncementTokens
    {
        private const string UnknownCard = "未知卡牌";

        public static string Card(CardInstance? card)
        {
            if (card == null || card.InstanceId == Guid.Empty)
            {
                return CardName(UnknownCard);
            }

            var name = SanitizeTokenValue(card.Name);
            return $"[[card:{card.InstanceId}|name:{name}|owner:{card.OwnerPlayerId}]]";
        }

        public static string CardName(string? name)
        {
            var safeName = SanitizeTokenValue(name);
            return $"[[cardname:{safeName}]]";
        }

        public static string Player(PlayerState? player)
        {
            if (player == null || player.PlayerId == Guid.Empty)
            {
                return PlayerId(Guid.Empty);
            }

            return PlayerId(player.PlayerId);
        }

        public static string PlayerId(Guid playerId)
        {
            return playerId == Guid.Empty ? "[[player:unknown]]" : $"[[player:{playerId}]]";
        }

        public static string Number(int value)
        {
            return $"[[num:{value}]]";
        }

        public static string WithTurn(GameState gameState, string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return string.Empty;
            }

            if (message.StartsWith("[Turn ", StringComparison.OrdinalIgnoreCase))
            {
                return message;
            }

            var turnNumber = GetTurnNumber(gameState);
            return $"[Turn {turnNumber}] {message}";
        }

        public static int GetTurnNumber(GameState gameState)
        {
            var currentId = gameState.CurrentPlayerId;
            if (!currentId.HasValue || currentId.Value == Guid.Empty)
            {
                return 0;
            }

            return gameState.GetPlayerState(currentId.Value)?.TurnNumber ?? 0;
        }

        private static string SanitizeTokenValue(string? value)
        {
            var safe = string.IsNullOrWhiteSpace(value) ? UnknownCard : value.Trim();
            return safe
                .Replace("[[", string.Empty)
                .Replace("]]", string.Empty)
                .Replace("|", "/");
        }
    }
}
