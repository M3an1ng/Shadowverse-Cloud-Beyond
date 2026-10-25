using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Mappings;

public static class GameStateProjection
{
    public static GameStateDto Create(GameState game, GameContext? context)
    {
        if (context != null && !ReferenceEquals(context.CurrentGameState, game))
        {
            throw new InvalidOperationException("The projection context belongs to a different game.");
        }

        return new GameStateDto
        {
            GameId = game.GameId,
            CurrentPlayerId = game.CurrentPlayerId ?? Guid.Empty,
            FirstPlayerId = game.FirstPlayerId ?? Guid.Empty,
            Phase = game.Phase,
            GameStatus = game.Status,
            Players = [PlayerStateProjection.Create(game.Player1, context), PlayerStateProjection.Create(game.Player2, context)],
            GameMessage = game.GameMessage,
            TurnTimeoutSeconds = game.TurnTimeoutSeconds,
            TurnStartedAtUtc = game.TurnStartedAtUtc,
            TurnEndsAtUtc = game.TurnEndsAtUtc,
            IsGameOver = game.IsGameOver,
            WinnerId = game.WinnerId?.ToString()
        };
    }
}
