using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Infrastructure.Data;

namespace ShadowverseBeyond.Server.Services;

public sealed record TournamentReportLineup(int StageNumber, TournamentEntry Entry, TournamentEntryDeck[] Decks);

public sealed record TournamentReportSource(
    Tournament Event, TournamentStage[] Stages, Dictionary<Guid, TournamentEntry> Entries,
    TournamentStageEntry[] Participants, TournamentReportLineup[] Lineups, TournamentMatch[] Matches)
{
    public TournamentReportStageDto[] StageOptions() => Stages.Select(stage =>
        new TournamentReportStageDto(stage.Number, stage.Kind, stage.MatchFormat)).ToArray();

    public TournamentClassificationDto[] Classifications() => Lineups
        .SelectMany(lineup => lineup.Decks.Select(deck => (lineup.StageNumber, lineup.Entry, Deck: deck)))
        .GroupBy(row => row.Deck.Id).Select(group =>
        {
            var row = group.First();
            return new TournamentClassificationDto(row.Deck.Id, row.Entry.Id, row.Entry.PlayerName,
                row.Deck.Name, row.Deck.Class, row.Deck.SecondaryClass, row.Deck.Archetype,
                group.Select(item => item.StageNumber).Order().ToArray());
        }).OrderBy(row => row.PlayerName, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.DeckName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(row => row.DeckId).ToArray();
}

public sealed class TournamentReportReader(ApplicationDbContext db, TimeProvider clock)
{
    public async Task<TournamentReportSource?> ReadSnapshotAsync(
        Guid id, bool includeMatches, CancellationToken cancellationToken, int stage = 0)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var snapshot = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: true);
        await using var transaction = await db.Database.UseTransactionAsync(snapshot, cancellationToken);
        return await ReadAsync(id, includeMatches, cancellationToken, stage);
    }

    public async Task<TournamentReportSource?> ReadAsync(
        Guid id, bool includeMatches, CancellationToken cancellationToken, int stageNumber = 0)
    {
        var tournament = await db.Tournaments.AsNoTracking().Include(row => row.Stages)
            .SingleOrDefaultAsync(row => row.Id == id && row.Status != TournamentStatus.Draft, cancellationToken);
        if (tournament is null || !tournament.AreEntriesPublic(clock.GetUtcNow().UtcDateTime)) return null;
        var started = tournament.Stages.Where(stage => stage.StartedAtUtc.HasValue).OrderBy(stage => stage.Number).ToArray();
        if (started.Length == 0) return null;

        var entries = await db.TournamentEntries.AsNoTracking().Where(entry => entry.TournamentId == id)
            .Select(entry => new TournamentEntry { Id = entry.Id, PlayerName = entry.PlayerName })
            .ToDictionaryAsync(entry => entry.Id, cancellationToken);
        var lastStage = started[^1].Number;
        var entryIds = entries.Keys.ToArray();
        var decks = await db.TournamentEntryDecks.AsNoTracking()
            .Where(deck => entryIds.Contains(deck.EntryId) && deck.StageNumber <= lastStage)
            .Select(deck => new TournamentEntryDeck
            {
                Id = deck.Id, EntryId = deck.EntryId, StageNumber = deck.StageNumber, Slot = deck.Slot, Name = deck.Name,
                Class = deck.Class, SecondaryClass = deck.SecondaryClass, DeckCode = deck.DeckCode, Archetype = deck.Archetype
            }).ToArrayAsync(cancellationToken);
        foreach (var deck in decks) entries[deck.EntryId].Decks.Add(deck);

        var participants = await db.TournamentStageEntries.AsNoTracking().Where(row => row.TournamentId == id)
            .OrderBy(row => row.StageNumber).ThenBy(row => row.Seed).ToArrayAsync(cancellationToken);
        var byStage = participants.ToLookup(row => row.StageNumber);
        var lineups = started.SelectMany(stage =>
        {
            var count = TournamentMatchRules.For(stage.MatchFormat).DecksPerPlayer;
            return byStage[stage.Number].Where(row => row.CanPlay).Select(row =>
            {
                var entry = entries[row.EntryId];
                return new TournamentReportLineup(stage.Number, entry, entry.GetDecksForStage(stage.Number, count));
            });
        }).ToArray();

        var ready = includeMatches && !lineups.Where(lineup => stageNumber == 0 || lineup.StageNumber == stageNumber)
            .SelectMany(lineup => lineup.Decks).Any(deck => string.IsNullOrWhiteSpace(deck.Archetype));
        var matches = ready ? await db.TournamentMatches.AsNoTracking().Where(match => match.TournamentId == id
                && (stageNumber == 0 || match.StageNumber == stageNumber))
            .Include(match => match.Games).AsSplitQuery()
            .OrderBy(match => match.StageNumber).ThenBy(match => match.Bracket)
            .ThenBy(match => match.RoundNumber).ThenBy(match => match.PairingNumber)
            .ToArrayAsync(cancellationToken) : [];
        return new TournamentReportSource(tournament, started, entries, participants, lineups, matches);
    }
}
