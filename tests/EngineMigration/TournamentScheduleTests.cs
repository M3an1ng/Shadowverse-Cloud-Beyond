using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Services;

internal static class TournamentScheduleTests
{
    public static void Run()
    {
        var sizes = new[] { 2, 3, 4, 5, 6, 7, 8, 13, 16, 31, 32 };
        foreach (var size in sizes)
        {
            VerifyElimination(size, false);
            VerifyElimination(size, true);
            VerifyElimination(size, true, lowerWins: true);
            VerifyElimination(size, true, allowReset: true);
            VerifyElimination(size, true, lowerWins: true, allowReset: true);
        }
        VerifySwissRematches();
        Console.WriteLine("Single elimination, double elimination and bracket resets passed for "
            + string.Join(", ", sizes) + " players. Swiss found non-greedy pairings and handled unavoidable rematches.");
    }

    private static void VerifyElimination(int count, bool isDouble, bool lowerWins = false, bool allowReset = false)
    {
        var seeds = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();
        var stage = new TournamentStage
        {
            TournamentId = Guid.NewGuid(), Number = 1,
            Kind = isDouble ? TournamentStageKind.DoubleElimination : TournamentStageKind.SingleElimination,
            AllowGrandFinalReset = allowReset
        };
        var matches = new List<TournamentMatch>();
        var now = DateTime.UtcNow;
        var winner = Guid.Empty;
        for (var attempt = 0; attempt <= count * 2; attempt++)
        {
            var advance = TournamentElimination.Advance(stage, seeds, matches, now);
            matches.AddRange(advance.NewMatches);
            if (advance.IsComplete)
            {
                winner = advance.WinnerEntryId ?? throw new InvalidOperationException("Completed bracket has no winner.");
                break;
            }
            var open = matches.Where(match => !match.CompletedAtUtc.HasValue).ToArray();
            Check(open.Length > 0, "Bracket stalled.");
            var activePlayers = open.SelectMany(match => new[] { match.EntryOneId, match.EntryTwoId!.Value });
            Check(activePlayers.Distinct().Count() == open.Length * 2, "Player double-booked in one bracket.");
            foreach (var match in open)
            {
                match.WinnerEntryId = lowerWins && match.Bracket == TournamentBracket.Finals && match.RoundNumber == 1
                    ? match.EntryTwoId : match.EntryOneId;
                match.CompletedAtUtc = now;
            }
        }
        Check(winner != Guid.Empty, "Bracket failed to reach a champion.");
        var played = matches.Where(match => match.EntryTwoId.HasValue).ToArray();
        var reset = allowReset && lowerWins;
        Check(played.Length == (isDouble ? count * 2 - 2 + (reset ? 1 : 0) : count - 1),
            "Unexpected number of played matches.");
        var finals = played.Where(match => match.Bracket == TournamentBracket.Finals).ToArray();
        if (isDouble)
        {
            Check(finals.Length == (reset ? 2 : 1), "Grand final count does not match the reset rule.");
            Check(finals[^1].WinnerEntryId == winner, "The deciding final did not produce the champion.");
        }
        foreach (var player in seeds)
        {
            var losses = played.Count(match => (match.EntryOneId == player || match.EntryTwoId == player)
                && match.WinnerEntryId != player);
            var endsWithOneLoss = player == winner && lowerWins
                || isDouble && lowerWins && !reset && player == finals[0].EntryOneId;
            var expectedLosses = endsWithOneLoss ? 1 : player == winner ? 0 : isDouble ? 2 : 1;
            Check(losses == expectedLosses, "Incorrect number of losses before elimination.");
        }
    }

    private static void VerifySwissRematches()
    {
        var ids = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        var standings = ids.Select((id, index) => new TournamentStanding(id, index + 1, 0, 0, 0, 0, 0, 0, "")).ToArray();
        TournamentMatch Previous(int one, int two) => new() { EntryOneId = ids[one], EntryTwoId = ids[two] };
        var pairings = TournamentPairings.SwissRound(standings, [Previous(0, 3), Previous(1, 2), Previous(2, 3)]);
        Check(pairings.Contains(new TournamentPairing(ids[0], ids[2])) && pairings.Contains(new TournamentPairing(ids[1], ids[3])),
            "Swiss matching failed to find an available non-rematch pairing.");
        var last = TournamentPairings.SwissRound(standings[..2], [Previous(0, 1)]);
        Check(last is [var pair] && pair == new TournamentPairing(ids[0], ids[1]),
            "Swiss matching failed to pair players when a rematch was unavoidable.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
