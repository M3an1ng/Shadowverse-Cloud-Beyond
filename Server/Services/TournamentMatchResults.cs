using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Infrastructure.Data;

namespace ShadowverseBeyond.Server.Services;

public sealed class TournamentMatchResults(ApplicationDbContext db, TimeProvider clock)
{
    public async Task RecordAsync(
        Guid matchId, Guid gameId, Guid? winnerUserId, Guid? firstUserId, CancellationToken cancellationToken)
    {
        if (!await db.TournamentMatchGames.AsNoTracking().AnyAsync(game =>
            game.MatchId == matchId && game.Id == gameId && !game.CompletedAtUtc.HasValue, cancellationToken))
            return;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var match = await db.TournamentMatches.Include(item => item.Games)
            .SingleOrDefaultAsync(item => item.Id == matchId, cancellationToken);
        var game = match?.Games.SingleOrDefault(item => item.Id == gameId);
        if (match == null || game == null || game.CompletedAtUtc.HasValue || match.CompletedAtUtc.HasValue)
            return;

        var users = await db.TournamentEntries
            .Where(entry => entry.Id == match.EntryOneId || entry.Id == match.EntryTwoId)
            .Select(entry => new { entry.UserId, entry.Id })
            .ToDictionaryAsync(entry => entry.UserId, entry => entry.Id, cancellationToken);

        Guid? EntryId(Guid? userId) => userId is { } id && users.TryGetValue(id, out var entryId) ? entryId : null;
        if (winnerUserId.HasValue && winnerUserId != Guid.Empty && !users.ContainsKey(winnerUserId.Value))
            throw new InvalidOperationException("Native tournament winner did not occupy either seat.");

        var now = clock.GetUtcNow().UtcDateTime;
        game.CompletedAtUtc = now;
        game.WinnerEntryId = EntryId(winnerUserId);
        game.FirstPlayerEntryId = EntryId(firstUserId);
        match.RoomRevision++;

        if (game.WinnerEntryId is { } winner)
        {
            var format = await db.TournamentStages
                .Where(stage => stage.TournamentId == match.TournamentId && stage.Number == match.StageNumber)
                .Select(stage => stage.MatchFormat)
                .SingleAsync(cancellationToken);
            if (match.Wins(winner) >= TournamentMatchRules.For(format).WinsRequired)
            {
                match.WinnerEntryId = winner;
                match.CompletedAtUtc = now;
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        await db.Tournaments.Where(tournament => tournament.Id == match.TournamentId && tournament.Status == TournamentStatus.Running)
            .ExecuteUpdateAsync(update => update.SetProperty(tournament => tournament.UpdatedAtUtc, now)
                .SetProperty(tournament => tournament.NextScheduleCheckAtUtc,
                    tournament => match.CompletedAtUtc.HasValue ? now : tournament.NextScheduleCheckAtUtc), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
