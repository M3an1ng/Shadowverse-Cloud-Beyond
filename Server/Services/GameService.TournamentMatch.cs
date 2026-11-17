using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

public partial class GameService
{
    private async Task<MatchOperationResult> CompleteRoomGameAsync(GameState state)
    {
        var completed = _matchService.CompleteGame(state.GameId, state.WinnerId);
        if (completed.Room?.TournamentMatchId is not { } matchId || !Guid.TryParse(state.GameId, out var gameId))
            return completed;

        try
        {
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var results = scope.ServiceProvider.GetRequiredService<TournamentMatchResults>();
            await results.RecordAsync(matchId, gameId, state.WinnerId, state.FirstPlayerId, CancellationToken.None);
        }
        catch (Exception error)
        {
            // The series controller also reconciles a finished native game before serving the next selection.
            _logger.LogError(error, "Could not save the result for tournament game {GameId}.", gameId);
        }
        return completed;
    }
}
