using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Services;

public sealed record TournamentStanding(
    Guid EntryId, int Seed, int Wins, int Losses, int OpponentWins, int GameWins, int GameLosses, int Byes, string Record);

public readonly record struct TournamentPairing(Guid One, Guid? Two);

public static class TournamentPairings
{
    public static TournamentStanding[] Standings(IReadOnlyList<Guid> seeds, IEnumerable<TournamentMatch> matches)
    {
        var scores = seeds.Select((id, index) => new Score(id, index + 1)).ToDictionary(score => score.Id);
        foreach (var match in matches.Where(match => match.CompletedAtUtc.HasValue)
            .OrderBy(match => match.RoundNumber).ThenBy(match => match.PairingNumber))
        {
            var one = scores[match.EntryOneId];
            one.Add(match.WinnerEntryId == one.Id, match.Wins(one.Id));
            if (match.EntryTwoId is not { } other)
            {
                one.Byes++;
                continue;
            }

            var two = scores[other];
            two.Add(match.WinnerEntryId == two.Id, match.Wins(two.Id));
            one.GameLosses += match.Wins(two.Id);
            two.GameLosses += match.Wins(one.Id);
            one.Opponents.Add(two);
            two.Opponents.Add(one);
        }

        return scores.Values.Select(score => new TournamentStanding(score.Id, score.Seed, score.Wins,
                score.Record.Count - score.Wins, score.Opponents.Sum(opponent => opponent.Wins),
                score.GameWins, score.GameLosses, score.Byes, new string(score.Record.ToArray())))
            .OrderByDescending(score => score.Wins).ThenByDescending(score => score.OpponentWins)
            .ThenByDescending(score => score.GameWins - score.GameLosses).ThenBy(score => score.Seed).ToArray();
    }

    public static TournamentPairing[] PlazaRound(IReadOnlyList<TournamentStanding> standings)
    {
        var pairs = new List<TournamentPairing>();
        foreach (var history in standings.GroupBy(score => score.Record).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var group = history.ToArray();
            Random.Shared.Shuffle(group);
            if (group.Length % 2 != 0)
            {
                // For an odd group, avoid a repeat bye when another player can take it.
                var bye = group.MinBy(score => score.Byes)!;
                pairs.Add(new TournamentPairing(bye.EntryId, null));
                group = group.Where(score => score.EntryId != bye.EntryId).ToArray();
            }
            for (var index = 0; index < group.Length; index += 2)
                pairs.Add(new TournamentPairing(group[index].EntryId, group[index + 1].EntryId));
        }
        return pairs.ToArray();
    }

    public static TournamentPairing[] SwissRound(
        IReadOnlyList<TournamentStanding> standings, IReadOnlyList<TournamentMatch> matches)
    {
        var previous = matches.Where(match => match.EntryTwoId.HasValue)
            .Select(match => Opponents(match.EntryOneId, match.EntryTwoId!.Value)).ToHashSet();
        var count = standings.Count;
        var byeSlot = count % 2 != 0 ? count : -1;
        var graphSize = count + (byeSlot >= 0 ? 1 : 0);
        var fewestByes = standings.Min(score => score.Byes);

        int[][] BuildGraph(int maxWinGap) => Enumerable.Range(0, graphSize).Select(from =>
        {
            if (from == byeSlot)
                return Enumerable.Range(0, count).Reverse().Where(to => standings[to].Byes == fewestByes).ToArray();

            var current = standings[from];
            var neighbors = Enumerable.Range(0, count).Where(to => from != to
                    && Math.Abs(current.Wins - standings[to].Wins) <= maxWinGap
                    && !previous.Contains(Opponents(current.EntryId, standings[to].EntryId)))
                .OrderBy(to => Math.Abs(current.Wins - standings[to].Wins))
                .ThenBy(to => Math.Abs(from - to)).ToList();
            if (byeSlot >= 0 && current.Byes == fewestByes) neighbors.Add(byeSlot);
            return neighbors.ToArray();
        }).ToArray();

        // First try within one win, then favor fresh opponents even across score groups.
        var partners = TournamentSwissMatching.Find(BuildGraph(1), byeSlot);
        if (partners.Contains(-1)) partners = TournamentSwissMatching.Find(BuildGraph(int.MaxValue), byeSlot);
        var pairs = new List<TournamentPairing>(graphSize / 2);
        for (var index = 0; index < count; index++)
        {
            var partner = partners[index];
            if (partner >= 0 && index < partner)
                pairs.Add(new TournamentPairing(standings[index].EntryId,
                    partner == byeSlot ? null : standings[partner].EntryId));
        }
        // A maximum matching of unused opponents leaves only unavoidable repeat pairs.
        var unmatched = Enumerable.Range(0, count).Where(index => partners[index] < 0).ToArray();
        for (var index = 0; index < unmatched.Length; index += 2)
            pairs.Add(new TournamentPairing(standings[unmatched[index]].EntryId, standings[unmatched[index + 1]].EntryId));
        return pairs.ToArray();
    }

    private static (Guid, Guid) Opponents(Guid one, Guid two) => one.CompareTo(two) < 0 ? (one, two) : (two, one);

    private sealed class Score(Guid id, int seed)
    {
        public Guid Id { get; } = id;
        public int Seed { get; } = seed;
        public int Wins { get; private set; }
        public int GameWins { get; private set; }
        public int GameLosses { get; set; }
        public int Byes { get; set; }
        public List<char> Record { get; } = [];
        public List<Score> Opponents { get; } = [];

        public void Add(bool won, int gameWins)
        {
            if (won) Wins++;
            Record.Add(won ? 'W' : 'L');
            GameWins += gameWins;
        }
    }
}
