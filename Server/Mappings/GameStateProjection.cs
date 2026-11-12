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
            Mode = game.Mode,
            Mirage = game.Mirage == null
                ? null
                : new MirageMatchStateDto
                {
                    RoundNumber = game.Mirage.RoundNumber,
                    ReadyPlayerIds = game.Mirage.ReadyPlayerIds.ToList(),
                    ActiveCombatId = game.Mirage.ActiveCombatId,
                    CommittedCombatIds = game.Mirage.CommittedCombatIds.ToList(),
                    LastCombatId = game.Mirage.LastCombatId,
                    LastCombatRoundNumber = game.Mirage.LastCombatRoundNumber,
                    LastCombatWinnerPlayerId = game.Mirage.LastCombatWinnerPlayerId,
                    LastCombatLoserPlayerId = game.Mirage.LastCombatLoserPlayerId,
                    LastCombatLeaderDamage = game.Mirage.LastCombatLeaderDamage,
                    LastCombatStepCount = game.Mirage.LastCombatStepCount,
                    HardGoldCap = game.Mirage.Rules.HardGoldCap,
                    MaxTavernTier = game.Mirage.Rules.MaxTavernTier
                },
            GameStatus = game.Status,
            Players = [PlayerStateProjection.Create(game.Player1, context, game), PlayerStateProjection.Create(game.Player2, context, game)],
            GameMessage = game.GameMessage,
            TurnTimeoutSeconds = game.TurnTimeoutSeconds,
            TurnStartedAtUtc = game.TurnStartedAtUtc,
            TurnEndsAtUtc = game.TurnEndsAtUtc,
            IsGameOver = game.IsGameOver,
            WinnerId = game.WinnerId?.ToString()
        };
    }
}
