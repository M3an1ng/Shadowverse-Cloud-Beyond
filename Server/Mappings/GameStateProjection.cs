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

        MirageMatchStateDto? mirageDto = null;
        if (game.Mirage != null)
        {
            List<Guid> readyIds;
            List<Guid> committedIds;
            bool gateClosed;
            int roundNum;
            Guid? activeCombatId;
            Guid? lastCombatId;
            int? lastCombatRound;
            Guid? lastWinnerId;
            Guid? lastLoserId;
            int lastDamage;
            int lastSteps;
            lock (game.Mirage.RoundSyncLock)
            {
                gateClosed = game.Mirage.IsNextRoundGateClosed;
                roundNum = game.Mirage.RoundNumber;
                readyIds = game.Mirage.ReadyPlayerIds.ToList();
                activeCombatId = game.Mirage.ActiveCombatId;
                committedIds = game.Mirage.CommittedCombatIds.ToList();
                lastCombatId = game.Mirage.LastCombatId;
                lastCombatRound = game.Mirage.LastCombatRoundNumber;
                lastWinnerId = game.Mirage.LastCombatWinnerPlayerId;
                lastLoserId = game.Mirage.LastCombatLoserPlayerId;
                lastDamage = game.Mirage.LastCombatLeaderDamage;
                lastSteps = game.Mirage.LastCombatStepCount;
            }

            mirageDto = new MirageMatchStateDto
            {
                TournamentRoomId = game.Mirage.TournamentRoomId,
                TournamentSeatIndex = game.Mirage.TournamentSeatIndex,
                IsNextRoundGateClosed = gateClosed,
                RoundNumber = roundNum,
                ReadyPlayerIds = readyIds,
                ActiveCombatId = activeCombatId,
                CommittedCombatIds = committedIds,
                LastCombatId = lastCombatId,
                LastCombatRoundNumber = lastCombatRound,
                LastCombatWinnerPlayerId = lastWinnerId,
                LastCombatLoserPlayerId = lastLoserId,
                LastCombatLeaderDamage = lastDamage,
                LastCombatStepCount = lastSteps,
                HardGoldCap = game.Mirage.Rules.HardGoldCap,
                MaxTavernTier = game.Mirage.Rules.MaxTavernTier,
                SharedCardPool = game.Mirage.SnapshotSharedCardPool(),
                InitialCardPoolCaps = game.Mirage.SnapshotInitialCardPoolCaps()
            };
        }

        return new GameStateDto
        {
            GameId = game.GameId,
            CurrentPlayerId = game.CurrentPlayerId ?? Guid.Empty,
            FirstPlayerId = game.FirstPlayerId ?? Guid.Empty,
            Phase = game.Phase,
            Mode = game.Mode,
            Mirage = mirageDto,
            GameStatus = game.Status,
            Players = [PlayerStateProjection.Create(game.Player1, context, game), PlayerStateProjection.Create(game.Player2, context, game)],
            GameMessage = game.GameMessage,
            TurnTimeoutSeconds = game.TurnTimeoutSeconds,
            TurnStartedAtUtc = game.TurnStartedAtUtc,
            TurnEndsAtUtc = game.TurnEndsAtUtc,
            IsGameOver = game.IsGameOver,
            WinnerId = game.WinnerId?.ToString(),
            ViewingPlayerId = game.IsMirageTournamentSeat ? game.Player1.PlayerId : null
        };
    }
}
