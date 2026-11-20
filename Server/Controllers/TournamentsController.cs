using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;

namespace ShadowverseBeyond.Server.Controllers;

[ApiController]
[Route("api/tournaments")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TournamentsController(ApplicationDbContext db, TimeProvider clock) : ControllerBase
{
    public enum ListScope { All, Running, Finished }

    [HttpGet("visibility")]
    public async Task<IActionResult> Visibility(CancellationToken cancellationToken) =>
        Ok(new { available = await ActiveTournaments().AnyAsync(cancellationToken) });

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TournamentPublicDto>>> List(
        CancellationToken cancellationToken, [FromQuery] bool finished = false, [FromQuery] ListScope? scope = null)
    {
        var visible = db.Tournaments.AsNoTracking().Where(tournament => tournament.Status != TournamentStatus.Draft);
        var source = scope switch
        {
            ListScope.All => visible,
            ListScope.Running => visible.Where(tournament => tournament.Status == TournamentStatus.Running),
            ListScope.Finished => visible.Where(tournament => tournament.Status == TournamentStatus.Finished),
            _ => finished ? visible.Where(tournament => tournament.Status == TournamentStatus.Finished) : ActiveTournaments()
        };
        var tournaments = await source
            .Include(tournament => tournament.Stages)
            .OrderBy(tournament => tournament.Status == TournamentStatus.Finished)
            .ThenBy(tournament => tournament.RegistrationClosesAtUtc)
            .ThenBy(tournament => tournament.Id)
            .ToArrayAsync(cancellationToken);
        if (tournaments.Length == 0) return Ok(Array.Empty<TournamentPublicDto>());

        var tournamentIds = tournaments.Select(tournament => tournament.Id).ToArray();
        var counts = await db.TournamentEntries.AsNoTracking()
            .Where(entry => tournamentIds.Contains(entry.TournamentId))
            .GroupBy(entry => entry.TournamentId)
            .Select(group => new { Id = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Id, group => group.Count, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        return Ok(tournaments.Select(tournament =>
            TournamentPublicDto.FromTournament(tournament, counts.GetValueOrDefault(tournament.Id), now)).ToArray());
    }

    [HttpGet("{id:guid}/version")]
    public async Task<IActionResult> Version(Guid id, CancellationToken cancellationToken)
    {
        var updatedAt = await db.Tournaments.AsNoTracking()
            .Where(tournament => tournament.Id == id && tournament.Status != TournamentStatus.Draft)
            .Select(tournament => (DateTime?)tournament.UpdatedAtUtc)
            .SingleOrDefaultAsync(cancellationToken);
        return updatedAt is { } stamp
            ? Ok(new { updatedAtUtc = DateTime.SpecifyKind(stamp, DateTimeKind.Utc) }) : NotFound("没有找到赛事。");
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TournamentPublicDetailsDto>> Details(Guid id, CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var snapshot = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: true);
        await using var transaction = await db.Database.UseTransactionAsync(snapshot, cancellationToken);
        var tournament = await db.Tournaments.AsNoTracking().Include(item => item.Stages)
            .SingleOrDefaultAsync(item => item.Id == id && item.Status != TournamentStatus.Draft, cancellationToken);
        if (tournament is null) return NotFound("没有找到赛事。");

        var now = clock.GetUtcNow().UtcDateTime;
        var entries = db.TournamentEntries.AsNoTracking().Where(entry => entry.TournamentId == id);
        if (!tournament.AreEntriesPublic(now))
        {
            var count = await entries.CountAsync(cancellationToken);
            return Ok(new TournamentPublicDetailsDto(TournamentPublicDto.FromTournament(tournament, count, now), now,
                null, [], []));
        }

        var userId = AccountSession.GetUserId(User);
        var roster = await entries.OrderBy(entry => entry.PlayerName).ThenBy(entry => entry.Id)
            .Select(entry => new { entry.Id, entry.PlayerName, IsMine = entry.UserId == userId })
            .ToDictionaryAsync(entry => entry.Id, cancellationToken);
        var matches = await db.TournamentMatches.AsNoTracking()
            .Where(match => match.TournamentId == id)
            .Include(match => match.Games).AsSplitQuery()
            .OrderBy(match => match.StageNumber).ThenBy(match => match.Bracket)
            .ThenBy(match => match.RoundNumber).ThenBy(match => match.PairingNumber)
            .ToArrayAsync(cancellationToken);
        var byStage = matches.ToLookup(match => match.StageNumber);
        var participants = await db.TournamentStageEntries.AsNoTracking().Where(entry => entry.TournamentId == id)
            .OrderBy(entry => entry.Seed).ToArrayAsync(cancellationToken);
        var stages = participants.ToLookup(entry => entry.StageNumber);
        var progress = tournament.Stages.OrderBy(stage => stage.Number).Select(stage =>
        {
            var players = stages[stage.Number].Select(entry => new TournamentStageParticipantDto(
                entry.EntryId, roster[entry.EntryId].PlayerName, entry.Seed, roster[entry.EntryId].IsMine, entry.CanPlay)).ToArray();
            var seeds = players.Where(player => player.CanPlay).Select(player => player.EntryId).ToArray();
            var pool = stage.Kind is TournamentStageKind.Swiss or TournamentStageKind.Plaza;
            var standings = stage.StartedAtUtc.HasValue && pool
                ? TournamentPairings.Standings(seeds, byStage[stage.Number]).Select((score, index) =>
                    new TournamentStandingDto(index + 1, score.EntryId, roster[score.EntryId].PlayerName,
                        score.Wins, score.Losses, score.OpponentWins, score.GameWins, score.GameLosses, score.Byes, score.Record)).ToArray()
                : [];
            var bracket = stage.StartedAtUtc.HasValue && !pool
                ? TournamentElimination.Layout(seeds.Length, stage.Kind == TournamentStageKind.DoubleElimination, stage.AllowGrandFinalReset)
                : [];
            return new TournamentStageProgressDto(stage.Number, players, standings, bracket);
        }).ToArray();
        var pairings = matches.Select(match =>
        {
            var one = roster[match.EntryOneId];
            var two = match.EntryTwoId is { } second ? roster[second] : null;
            return new TournamentPairingDto(match.Id, match.StageNumber, match.RoundNumber, match.PairingNumber, match.Bracket,
                one.PlayerName, two?.PlayerName, match.EntryOneId, match.EntryTwoId, one.IsMine || two?.IsMine == true,
                match.WinnerEntryId, match.CompletedAtUtc.HasValue, match.Wins(match.EntryOneId),
                match.EntryTwoId is { } twoId ? match.Wins(twoId) : 0);
        }).ToArray();
        return Ok(new TournamentPublicDetailsDto(TournamentPublicDto.FromTournament(tournament, roster.Count, now), now,
            roster.Values.Select(entry => new TournamentEntrantDto(entry.Id, entry.PlayerName)).ToArray(), pairings, progress));
    }

    [HttpGet("{id:guid}/entries/{entryId:guid}")]
    public async Task<ActionResult<TournamentPublicLineupDto>> Entry(
        Guid id, Guid entryId, CancellationToken cancellationToken, [FromQuery] int stage = 1)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var snapshot = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: true);
        await using var transaction = await db.Database.UseTransactionAsync(snapshot, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var tournament = await db.Tournaments.AsNoTracking().Include(item => item.Stages)
            .SingleOrDefaultAsync(item => item.Id == id && item.Status != TournamentStatus.Draft, cancellationToken);
        if (tournament is null || !tournament.AreEntriesPublic(now))
            return NotFound("报名结束后可以查看选手卡组。");
        var phase = tournament.Stages.SingleOrDefault(item => item.Number == stage);
        if (phase is null) return NotFound("找不到这个比赛阶段。");
        if (stage > 1 && (!phase.StartedAtUtc.HasValue || !await db.TournamentStageEntries.AsNoTracking()
            .AnyAsync(item => item.TournamentId == id && item.StageNumber == stage && item.EntryId == entryId && item.CanPlay, cancellationToken)))
            return NotFound("卡组将在该阶段开赛后公开。");

        var entry = await db.TournamentEntries.AsNoTracking().Include(item => item.Decks.Where(deck => deck.StageNumber <= stage))
            .SingleOrDefaultAsync(item => item.Id == entryId && item.TournamentId == id, cancellationToken);
        if (entry is null) return NotFound("找不到这位选手。");
        var avatarUrl = await db.Users.AsNoTracking().Where(user => user.Id == entry.UserId)
            .Select(user => user.AvatarUrl).SingleOrDefaultAsync(cancellationToken);

        var matches = await db.TournamentMatches.AsNoTracking()
            .Where(match => match.TournamentId == id && (match.EntryOneId == entryId || match.EntryTwoId == entryId))
            .Include(match => match.Games.Where(game => game.CompletedAtUtc.HasValue && game.WinnerEntryId.HasValue))
            .ToArrayAsync(cancellationToken);
        var settled = matches.Where(match => match.CompletedAtUtc.HasValue && match.WinnerEntryId.HasValue).ToArray();
        var wins = settled.Count(match => match.WinnerEntryId == entryId);
        var record = new TournamentPlayerRecordDto(wins, settled.Length - wins);
        var gamesByDeck = matches.SelectMany(match => match.Games.Select(game => new
        {
            DeckId = match.EntryOneId == entryId ? game.EntryOneDeckId : game.EntryTwoDeckId,
            Won = game.WinnerEntryId == entryId,
            GoesFirst = game.FirstPlayerEntryId == entryId,
            GoesSecond = game.FirstPlayerEntryId.HasValue && game.FirstPlayerEntryId != entryId
        })).ToLookup(game => game.DeckId);
        TournamentPublicDeckDto[] BuildLineup(TournamentStage lineupStage)
        {
            var number = lineupStage.Number;
            var bansByDeck = TournamentMatchRules.For(lineupStage.MatchFormat).BansPerPlayer > 0
                ? settled.Where(match => match.StageNumber == number && match.BanByOne.HasValue && match.BanByTwo.HasValue)
                    .ToLookup(match => match.EntryOneId == entryId ? match.BanByTwo!.Value : match.BanByOne!.Value)
                : null;
            return entry.GetDecksForStage(number, tournament.GetRequiredDeckCount(number)).Select(deck =>
            {
                var games = gamesByDeck[deck.Id];
                var stats = new TournamentDeckStatisticsDto(games.Count(), games.Count(game => game.Won),
                    games.Count(game => game.GoesFirst), games.Count(game => game.GoesFirst && game.Won),
                    games.Count(game => game.GoesSecond), games.Count(game => game.GoesSecond && game.Won),
                    bansByDeck?[deck.Id].Count());
                return new TournamentPublicDeckDto(deck.Id, deck.StageNumber, deck.Slot, deck.Name, deck.Class,
                    deck.SecondaryClass, deck.DeckCode, deck.ReadCards(), stats);
            }).ToArray();
        }

        var earlier = tournament.Stages.SingleOrDefault(item => item.Number == stage - 1 && item.StartedAtUtc.HasValue);
        var previous = earlier is null ? null : new TournamentStageLineupDto(earlier.Number, BuildLineup(earlier));
        return Ok(new TournamentPublicLineupDto(entry.Id, entry.PlayerName, avatarUrl, record, BuildLineup(phase), previous));
    }

    private IQueryable<Tournament> ActiveTournaments() => db.Tournaments.AsNoTracking()
        .Where(tournament => tournament.Status == TournamentStatus.Published || tournament.Status == TournamentStatus.Running);
}
