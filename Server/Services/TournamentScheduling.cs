using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Infrastructure.Data;

namespace ShadowverseBeyond.Server.Services;

public sealed class TournamentScheduling(ApplicationDbContext db, TimeProvider clock)
{
    public Task<Guid[]> DueEventsAsync(CancellationToken cancellationToken) =>
        Due(clock.GetUtcNow().UtcDateTime).AsNoTracking().Select(tournament => tournament.Id).ToArrayAsync(cancellationToken);

    public async Task AdvanceAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var tournament = await Due(now).Include(item => item.Stages)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (tournament is null) return;

        tournament.Status = TournamentStatus.Running;
        tournament.NextScheduleCheckAtUtc = null;
        tournament.UpdatedAtUtc = now;
        var stages = tournament.Stages.OrderBy(stage => stage.Number).ToArray();
        for (var index = 0; index < stages.Length; index++)
        {
            var stage = stages[index];
            if (stage.CompletedAtUtc.HasValue) continue;
            var participants = await GetParticipantsAsync(stage, cancellationToken);
            if (stage.Number == 1 && stage.StartedAtUtc == null && participants.Count == 0)
            {
                var entrants = await db.TournamentEntries.AsNoTracking().Where(entry => entry.TournamentId == id)
                    .Select(entry => entry.Id).ToArrayAsync(cancellationToken);
                Random.Shared.Shuffle(entrants);
                participants = AddStageParticipants(stage, entrants);
            }

            if (stage.StartedAtUtc == null)
            {
                if (tournament.IsStageDeckSubmissionOpen(stage, now))
                {
                    tournament.NextScheduleCheckAtUtc = stage.DeckSubmissionClosesAtUtc;
                    break;
                }
                if (stage.Number > 1 && stage.AllowDeckResubmission)
                    await LockLineupsAsync(tournament, stage, participants, cancellationToken);
                stage.StartedAtUtc = now;
            }

            var seeds = participants.Where(entry => entry.CanPlay).Select(entry => entry.EntryId).ToArray();
            var progress = await BuildScheduleAsync(stage, seeds, now, cancellationToken);
            if (!progress.IsComplete) break;

            stage.CompletedAtUtc = now;
            stage.WinnerEntryId = progress.Winner;
            var hasNext = index + 1 < stages.Length;
            if (!hasNext || seeds.Length < 2)
            {
                tournament.Status = TournamentStatus.Finished;
                break;
            }

            var nextStage = stages[index + 1];
            AddStageParticipants(nextStage, progress.Ranking.Take(nextStage.AdvancingPlayers!.Value));
            if (nextStage.AllowDeckResubmission && nextStage.DeckSubmissionMinutes is { } minutes)
                nextStage.DeckSubmissionClosesAtUtc = now.AddMinutes(minutes);
            // Keep the entrants and any matches created in this transaction visible to the next stage.
            await db.SaveChangesAsync(cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private IQueryable<Tournament> Due(DateTime now) => db.Tournaments.Where(tournament =>
        tournament.Status == TournamentStatus.Published && tournament.RegistrationClosesAtUtc <= now
        || tournament.Status == TournamentStatus.Running && tournament.NextScheduleCheckAtUtc <= now);

    private Task<List<TournamentStageEntry>> GetParticipantsAsync(TournamentStage stage, CancellationToken cancellationToken) =>
        db.TournamentStageEntries.Where(entry => entry.TournamentId == stage.TournamentId && entry.StageNumber == stage.Number)
            .OrderBy(entry => entry.Seed).ToListAsync(cancellationToken);

    private List<TournamentStageEntry> AddStageParticipants(TournamentStage stage, IEnumerable<Guid> seeds)
    {
        var participants = seeds.Select((id, index) => new TournamentStageEntry
        {
            TournamentId = stage.TournamentId, StageNumber = stage.Number, EntryId = id, Seed = index + 1
        }).ToList();
        db.TournamentStageEntries.AddRange(participants);
        return participants;
    }

    private async Task LockLineupsAsync(Tournament tournament, TournamentStage stage,
        IReadOnlyList<TournamentStageEntry> participants, CancellationToken cancellationToken)
    {
        var required = tournament.GetRequiredDeckCount(stage.Number);
        var ids = participants.Select(entry => entry.EntryId).ToArray();
        var latestLineups = await db.TournamentEntryDecks.AsNoTracking()
            .Where(deck => ids.Contains(deck.EntryId) && deck.StageNumber <= stage.Number)
            .GroupBy(deck => new { deck.EntryId, deck.StageNumber })
            .Select(group => new { group.Key.EntryId, group.Key.StageNumber, Count = group.Count() })
            .ToArrayAsync(cancellationToken);
        var usable = latestLineups.GroupBy(lineup => lineup.EntryId)
            .Where(group => group.MaxBy(lineup => lineup.StageNumber)!.Count >= required)
            .Select(group => group.Key).ToHashSet();
        foreach (var participant in participants)
            participant.CanPlay = usable.Contains(participant.EntryId);
    }

    private async Task<ScheduleProgress> BuildScheduleAsync(
        TournamentStage stage, Guid[] seeds, DateTime now, CancellationToken cancellationToken)
    {
        if (seeds.Length < 2) return new(true, null, seeds);
        var matches = await db.TournamentMatches.Where(match =>
            match.TournamentId == stage.TournamentId && match.StageNumber == stage.Number)
            .Include(match => match.Games).ToListAsync(cancellationToken);

        if (stage.Kind is TournamentStageKind.SingleElimination or TournamentStageKind.DoubleElimination)
        {
            var bracket = TournamentElimination.Advance(stage, seeds, matches, now);
            db.TournamentMatches.AddRange(bracket.NewMatches);
            return new(bracket.IsComplete, bracket.WinnerEntryId, []);
        }
        return AdvancePool(stage, seeds, matches, now);
    }

    private ScheduleProgress AdvancePool(TournamentStage stage, Guid[] seeds, List<TournamentMatch> matches, DateTime now)
    {
        var round = matches.Count == 0 ? 0 : matches.Max(match => match.RoundNumber);
        if (matches.Exists(match => !match.CompletedAtUtc.HasValue)) return new(false, null, []);

        while (round < stage.Rounds.GetValueOrDefault())
        {
            var standings = TournamentPairings.Standings(seeds, matches);
            var pairs = stage.Kind == TournamentStageKind.Plaza
                ? TournamentPairings.PlazaRound(standings)
                : TournamentPairings.SwissRound(standings, matches);
            round++;
            var current = pairs.Select((pair, index) => new TournamentMatch
            {
                Id = Guid.NewGuid(), TournamentId = stage.TournamentId, StageNumber = stage.Number,
                RoundNumber = round, PairingNumber = index + 1, EntryOneId = pair.One, EntryTwoId = pair.Two,
                CompletedAtUtc = pair.Two.HasValue ? null : now, WinnerEntryId = pair.Two.HasValue ? null : pair.One
            }).ToArray();
            db.TournamentMatches.AddRange(current);
            matches.AddRange(current);
            if (current.Any(match => !match.CompletedAtUtc.HasValue)) return new(false, null, []);
        }
        var ranking = TournamentPairings.Standings(seeds, matches).Select(standing => standing.EntryId).ToArray();
        return new(true, ranking[0], ranking);
    }

    private sealed record ScheduleProgress(bool IsComplete, Guid? Winner, Guid[] Ranking);
}
