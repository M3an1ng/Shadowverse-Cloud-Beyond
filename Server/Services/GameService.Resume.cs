using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

public partial class GameService
{
    public async Task InstallResumedGameAsync(GameState game)
    {
        if (game.ReplayBranch is null) throw new ArgumentException("resumed_game_requires_origin");
        if (!_gameStates.TryAdd(game.GameId,game)) throw new InvalidOperationException("resume_game_id_conflict");
        if (game.Mode == GameMode.Mirage && game.Phase == GamePhase.Recruit && !game.IsGameOver)
        {
            if (game.Mirage != null)
            {
                game.Mirage.ClosingRecruitRoundNumber = null;
            }
            var now = DateTime.UtcNow;
            var timeoutSeconds = game.TurnTimeoutSeconds > 0
                ? game.TurnTimeoutSeconds
                : game.Mirage?.Rules.RecruitTimeoutSeconds ?? MirageRuleConfig.DefaultRecruitTimeoutSeconds;
            game.TurnTimeoutSeconds = timeoutSeconds;
            game.TurnStartedAtUtc = now;
            game.TurnEndsAtUtc = now.AddSeconds(timeoutSeconds);
            StartMirageRecruitTimer(game.GameId, game.Mirage?.RoundNumber ?? 1, game.TurnEndsAtUtc.Value);
        }
        else if (game.Phase == GamePhase.Main)
        {
            StartTurnTimer(game.GameId,game.CurrentPlayerId!.Value);
        }
        if (_replayRecorder != null) await _replayRecorder.StartAsync(game);
    }
}
