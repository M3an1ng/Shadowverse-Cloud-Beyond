namespace ShadowverseBeyond.Server.Domain.Entities;

public enum TournamentBracket
{
    Pool,
    Winners,
    Losers,
    Finals
}

public readonly record struct TournamentMatchPosition(TournamentBracket Bracket, int Round, int Pairing);

public readonly record struct TournamentBracketSlot(int? Seed, TournamentMatchPosition? Source, bool TakesLoser)
{
    public static TournamentBracketSlot FromSeed(int seed) => new(seed, null, false);
    public static TournamentBracketSlot Winner(TournamentMatchPosition match) => new(null, match, false);
    public static TournamentBracketSlot Loser(TournamentMatchPosition match) => new(null, match, true);
}

public sealed record TournamentBracketNode(
    TournamentMatchPosition Position, TournamentBracketSlot One, TournamentBracketSlot Two);

public sealed class TournamentMatch
{
    public Guid Id { get; init; }
    public Guid TournamentId { get; init; }
    public int StageNumber { get; init; }
    public TournamentBracket Bracket { get; init; }
    public int RoundNumber { get; init; }
    public int PairingNumber { get; init; }
    public Guid EntryOneId { get; init; }
    public Guid? EntryTwoId { get; init; }
    public Guid? BanByOne { get; set; }
    public Guid? BanByTwo { get; set; }
    public Guid? PickByOne { get; set; }
    public Guid? PickByTwo { get; set; }
    public Guid? WinnerEntryId { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public long RoomRevision { get; set; }
    public List<TournamentMatchGame> Games { get; } = [];
    public TournamentMatchPosition Position => new(Bracket, RoundNumber, PairingNumber);

    public int Wins(Guid entryId) => Games.Count(game => game.CompletedAtUtc.HasValue && game.WinnerEntryId == entryId);

    public HashSet<Guid> WinningDecks(Guid entryId)
    {
        var firstSeat = entryId == EntryOneId;
        return Games.Where(game => game.CompletedAtUtc.HasValue && game.WinnerEntryId == entryId)
            .Select(game => firstSeat ? game.EntryOneDeckId : game.EntryTwoDeckId)
            .ToHashSet();
    }
}

public sealed class TournamentMatchGame
{
    // This is also the native battle ID. Each small game gets its own immutable room identity.
    public Guid Id { get; init; }
    public Guid MatchId { get; init; }
    public int Number { get; init; }
    public Guid EntryOneDeckId { get; init; }
    public Guid EntryTwoDeckId { get; init; }
    public DateTime StartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; set; }
    public Guid? WinnerEntryId { get; set; }
    public Guid? FirstPlayerEntryId { get; set; }
}
