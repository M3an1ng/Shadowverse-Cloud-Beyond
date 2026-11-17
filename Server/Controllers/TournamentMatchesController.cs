using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;

namespace ShadowverseBeyond.Server.Controllers;

[ApiController]
[Route("api/tournament-matches/{matchId:guid}")]
[Authorize(Policy = AccountSession.PlayerPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TournamentMatchesController(
    ApplicationDbContext db, MatchService liveRooms, IGameService games, TournamentMatchResults results, TimeProvider clock)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TournamentMatchRoomDto>> Get(Guid matchId, CancellationToken cancellationToken)
    {
        if (Request.Headers.IfNoneMatch.Count > 0 && AccountSession.GetUserId(User) is { } playerId)
        {
            var stamp = await db.TournamentMatches.AsNoTracking().Where(match => match.Id == matchId && match.EntryTwoId.HasValue
                && db.TournamentEntries.Any(entry => entry.TournamentId == match.TournamentId && entry.UserId == playerId
                    && (entry.Id == match.EntryOneId || entry.Id == match.EntryTwoId)))
                .Select(match => new
                {
                    match.RoomRevision,
                    ActiveGameId = match.Games.Where(game => !game.CompletedAtUtc.HasValue).Select(game => (Guid?)game.Id).FirstOrDefault()
                }).SingleOrDefaultAsync(cancellationToken);
            if (stamp is null) return RoomUnavailable();
            var tag = RoomTag(matchId, stamp.RoomRevision);
            if (Request.Headers.IfNoneMatch.Any(value => value == tag) && !NativeGameFinished(stamp.ActiveGameId))
            {
                Response.Headers.ETag = tag;
                return StatusCode(StatusCodes.Status304NotModified);
            }
        }
        var room = await ReadRoomAsync(matchId, tracking: false, cancellationToken);
        if (room is null) return RoomUnavailable();
        if (await ReconcileAsync(room, cancellationToken))
            room = await ReadRoomAsync(matchId, tracking: false, cancellationToken);
        return room is null ? RoomUnavailable() : RoomSnapshot(room);
    }

    [HttpPut("ban")]
    public async Task<ActionResult<TournamentMatchRoomDto>> Ban(
        Guid matchId, TournamentDeckChoiceRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var room = await ReadRoomAsync(matchId, tracking: true, cancellationToken);
        if (room is null) return RoomUnavailable();
        if (room.MyBan == request.DeckId) return RoomSnapshot(room);
        if (room.Phase != TournamentMatchPhase.Ban || room.MyBan.HasValue)
            return Conflict("本场禁牌已经锁定。");
        if (!room.OpponentDecks.Any(deck => deck.Id == request.DeckId))
            return BadRequest("请选择对手阵容中的一套卡组。");

        if (room.IsOne) room.Match.BanByOne = request.DeckId;
        else room.Match.BanByTwo = request.DeckId;
        room.Match.RoomRevision++;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RoomSnapshot(room);
    }

    [HttpPut("games/{number:int}/deck")]
    public async Task<ActionResult<TournamentMatchRoomDto>> Pick(
        Guid matchId, int number, TournamentDeckChoiceRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var room = await ReadRoomAsync(matchId, tracking: true, cancellationToken);
        if (room is null) return RoomUnavailable();
        var match = room.Match;
        var previous = match.Games.SingleOrDefault(game => game.Number == number);
        if (previous != null && (room.IsOne ? previous.EntryOneDeckId : previous.EntryTwoDeckId) == request.DeckId)
            return RoomSnapshot(room);
        if (number != room.NextGameNumber || room.Phase != TournamentMatchPhase.Pick)
            return Conflict("请刷新房间后选择当前小局的卡组。");
        if (room.MyPick.HasValue)
            return room.MyPick == request.DeckId ? RoomSnapshot(room) : Conflict("本局卡组已经锁定。");
        if (!room.MyDecks.Any(deck => deck.Id == request.DeckId))
            return BadRequest("请选择自己的参赛卡组。");
        if (room.OpponentBan == request.DeckId || match.WinningDecks(room.MyEntryId).Contains(request.DeckId))
            return Conflict("被禁用或已经赢过的卡组无法再次出场。");

        if (room.IsOne) match.PickByOne = request.DeckId;
        else match.PickByTwo = request.DeckId;
        match.RoomRevision++;
        if (match.PickByOne is { } one && match.PickByTwo is { } two)
        {
            match.Games.Add(new TournamentMatchGame
            {
                Id = Guid.NewGuid(), MatchId = match.Id, Number = number,
                EntryOneDeckId = one, EntryTwoDeckId = two, StartedAtUtc = clock.GetUtcNow().UtcDateTime
            });
            match.PickByOne = null;
            match.PickByTwo = null;
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RoomSnapshot(room);
    }

    [HttpPost("enter")]
    public async Task<ActionResult<TournamentGameSeatDto>> Enter(
        Guid matchId, EnterTournamentGameRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var room = await ReadRoomAsync(matchId, tracking: false, cancellationToken);
        if (room is null) return RoomUnavailable();
        if (room.Phase != TournamentMatchPhase.Battle || room.ActiveGame is not { } game || request.GameId != game.Id)
            return Conflict("双方确认卡组后才能进入当前对局。");
        if (NativeGameFinished(game.Id))
            return Conflict("小局已经结算，请返回赛事房间。");

        var liveRoom = liveRooms.OpenTournamentDuel(matchId, game.Id, room.EntryOne, room.EntryTwo,
            room.OneDecks.Single(deck => deck.Id == game.EntryOneDeckId),
            room.TwoDecks.Single(deck => deck.Id == game.EntryTwoDeckId),
            room.Tournament.DeckEnvironment, room.Tournament.DeckRule);
        await transaction.CommitAsync(cancellationToken);
        return Ok(new TournamentGameSeatDto(game.Id, room.UserId, room.IsOne ? liveRoom.HostToken : liveRoom.GuestToken!,
            room.IsOne ? "host" : "guest"));
    }

    private NotFoundObjectResult RoomUnavailable() => NotFound("找不到有你参赛的赛事房间。");

    private static string RoomTag(Guid id, long revision) => $"\"tournament-{id:N}-{revision}\"";

    private OkObjectResult RoomSnapshot(MatchRoomData room)
    {
        Response.Headers.ETag = RoomTag(room.Match.Id, room.Match.RoomRevision);
        return Ok(room.ToDto());
    }

    private bool NativeGameFinished(Guid? gameId) => gameId.HasValue
        && games.GetGameState(gameId.Value.ToString("D"))?.IsGameOver == true;

    private async Task<bool> ReconcileAsync(MatchRoomData room, CancellationToken cancellationToken)
    {
        if (room.ActiveGame is not { } game || games.GetGameState(game.Id.ToString("D")) is not { IsGameOver: true } state)
            return false;
        await results.RecordAsync(room.Match.Id, game.Id, state.WinnerId, state.FirstPlayerId, cancellationToken);
        return true;
    }

    private async Task<MatchRoomData?> ReadRoomAsync(Guid matchId, bool tracking, CancellationToken cancellationToken)
    {
        if (AccountSession.GetUserId(User) is not { } userId) return null;
        var query = tracking ? db.TournamentMatches : db.TournamentMatches.AsNoTracking();
        var match = await query.Include(row => row.Games).SingleOrDefaultAsync(row => row.Id == matchId, cancellationToken);
        if (match?.EntryTwoId is not { } entryTwoId) return null;

        var entrants = await db.TournamentEntries.AsNoTracking().Include(entry => entry.Decks.Where(deck => deck.StageNumber <= match.StageNumber))
            .Where(entry => entry.TournamentId == match.TournamentId && (entry.Id == match.EntryOneId || entry.Id == entryTwoId))
            .ToDictionaryAsync(entry => entry.Id, cancellationToken);
        if (!entrants.TryGetValue(match.EntryOneId, out var one) || !entrants.TryGetValue(entryTwoId, out var two)
            || (one.UserId != userId && two.UserId != userId))
            return null;

        var tournament = await db.Tournaments.AsNoTracking().Include(row => row.Stages.Where(stage => stage.Number == match.StageNumber))
            .SingleOrDefaultAsync(row => row.Id == match.TournamentId &&
                (row.Status == TournamentStatus.Running || row.Status == TournamentStatus.Finished), cancellationToken);
        if (tournament?.Stages.SingleOrDefault() is not { } stage) return null;

        var avatars = await db.Users.AsNoTracking().Where(user => user.Id == one.UserId || user.Id == two.UserId)
            .ToDictionaryAsync(user => user.Id, user => user.AvatarUrl, cancellationToken);
        var rules = TournamentMatchRules.For(stage.MatchFormat);
        return new MatchRoomData(tournament, stage, rules, match, userId, one, two,
            one.GetDecksForStage(stage.Number, rules.DecksPerPlayer),
            two.GetDecksForStage(stage.Number, rules.DecksPerPlayer), avatars);
    }

    private sealed record MatchRoomData(
        Tournament Tournament, TournamentStage Stage, TournamentMatchRules Rules, TournamentMatch Match, Guid UserId,
        TournamentEntry EntryOne, TournamentEntry EntryTwo, TournamentEntryDeck[] OneDecks, TournamentEntryDeck[] TwoDecks,
        Dictionary<Guid, string?> Avatars)
    {
        public bool IsOne => UserId == EntryOne.UserId;
        public Guid MyEntryId => IsOne ? EntryOne.Id : EntryTwo.Id;
        public Guid? MyBan => IsOne ? Match.BanByOne : Match.BanByTwo;
        public Guid? OpponentBan => IsOne ? Match.BanByTwo : Match.BanByOne;
        public Guid? MyPick => IsOne ? Match.PickByOne : Match.PickByTwo;
        public TournamentEntryDeck[] MyDecks => IsOne ? OneDecks : TwoDecks;
        public TournamentEntryDeck[] OpponentDecks => IsOne ? TwoDecks : OneDecks;
        public int NextGameNumber => Match.Games.Count + 1;
        public TournamentMatchGame? ActiveGame => Match.Games.SingleOrDefault(game => !game.CompletedAtUtc.HasValue);
        private bool BansRevealed => Match.BanByOne.HasValue && Match.BanByTwo.HasValue;

        public TournamentMatchPhase Phase => Match.CompletedAtUtc.HasValue || Tournament.Status == TournamentStatus.Finished
            ? TournamentMatchPhase.Completed
            : ActiveGame != null ? TournamentMatchPhase.Battle
            : OneDecks.Length != Rules.DecksPerPlayer || TwoDecks.Length != Rules.DecksPerPlayer
                ? TournamentMatchPhase.WaitingForLineups
                : Rules.BansPerPlayer > 0 && !BansRevealed ? TournamentMatchPhase.Ban : TournamentMatchPhase.Pick;

        public TournamentMatchRoomDto ToDto()
        {
            var active = ActiveGame;
            var one = Seat(EntryOne, OneDecks, Match.BanByOne, Match.BanByTwo, Match.PickByOne);
            var two = Seat(EntryTwo, TwoDecks, Match.BanByTwo, Match.BanByOne, Match.PickByTwo);
            return new TournamentMatchRoomDto(Match.Id, Tournament.Id, Tournament.Name, Stage.Kind, Stage.Number,
                Match.RoundNumber, Match.PairingNumber, Match.Bracket, Stage.MatchFormat, Tournament.DeckRule, Phase, one, two,
                active?.Number ?? NextGameNumber, active?.Id, MyBan, MyPick, Match.WinnerEntryId,
                Match.Games.Where(game => game.CompletedAtUtc.HasValue).OrderBy(game => game.Number)
                    .Select(game => new TournamentGameResultDto(game.Number, game.EntryOneDeckId, game.EntryTwoDeckId,
                        game.WinnerEntryId, game.FirstPlayerEntryId)).ToArray());
        }

        private TournamentMatchSeatDto Seat(
            TournamentEntry entry, TournamentEntryDeck[] decks, Guid? ownBan, Guid? opponentBan, Guid? pick)
        {
            var won = Match.WinningDecks(entry.Id);
            var view = decks.Select(deck => new TournamentMatchDeckDto(deck.Id, deck.Slot, deck.Name, deck.Class,
                deck.SecondaryClass, BansRevealed && deck.Id == opponentBan, won.Contains(deck.Id))).ToArray();
            return new TournamentMatchSeatDto(entry.Id, entry.PlayerName, Avatars.GetValueOrDefault(entry.UserId),
                entry.UserId == UserId, Match.Wins(entry.Id), ownBan.HasValue, pick.HasValue, view);
        }
    }
}
