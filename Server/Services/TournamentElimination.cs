using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Services;

public sealed record TournamentEliminationProgress(TournamentMatch[] NewMatches, bool IsComplete, Guid? WinnerEntryId);

public static class TournamentElimination
{
    public static TournamentBracketNode[] Layout(int entrants, bool doubleElimination, bool allowGrandFinalReset)
    {
        if (entrants < 2) return [];
        var seedOrder = new[] { 1, 2 };
        while (seedOrder.Length < entrants)
        {
            var next = seedOrder.Length * 2;
            seedOrder = seedOrder.SelectMany(seed => new[] { seed, next + 1 - seed }).ToArray();
        }

        var nodes = new List<TournamentBracketNode>();
        var winnersRounds = new List<TournamentMatchPosition[]>();
        var winnerSlots = seedOrder.Select(TournamentBracketSlot.FromSeed).ToArray();
        for (var round = 1; winnerSlots.Length > 1; round++)
        {
            var winners = PairRound(TournamentBracket.Winners, round, winnerSlots);
            winnersRounds.Add(winners);
            winnerSlots = winners.Select(TournamentBracketSlot.Winner).ToArray();
        }

        if (!doubleElimination) return nodes.ToArray();
        TournamentBracketSlot lowerChampion;
        if (winnersRounds.Count == 1)
        {
            lowerChampion = TournamentBracketSlot.Loser(winnersRounds[0][0]);
        }
        else
        {
            var lower = PairRound(TournamentBracket.Losers, 1,
                winnersRounds[0].Select(TournamentBracketSlot.Loser).ToArray());
            for (var index = 1; index < winnersRounds.Count; index++)
            {
                var upper = winnersRounds[index];
                if (index > 1)
                    lower = PairRound(TournamentBracket.Losers, index * 2 - 1,
                        lower.Select(TournamentBracketSlot.Winner).ToArray());

                // Cross adjacent branches before dropping the next round of upper-bracket losers.
                lower = lower.Select((previous, pair) => AddNode(TournamentBracket.Losers, index * 2, pair + 1,
                    TournamentBracketSlot.Winner(previous),
                    TournamentBracketSlot.Loser(upper[upper.Length > 1 ? pair ^ 1 : pair]))).ToArray();
            }
            lowerChampion = TournamentBracketSlot.Winner(lower[0]);
        }

        var final = AddNode(TournamentBracket.Finals, 1, 1, winnerSlots[0], lowerChampion);
        if (allowGrandFinalReset)
            AddNode(TournamentBracket.Finals, 2, 1, TournamentBracketSlot.Winner(final), TournamentBracketSlot.Loser(final));
        return nodes.ToArray();

        TournamentMatchPosition[] PairRound(TournamentBracket bracket, int round, IReadOnlyList<TournamentBracketSlot> slots)
        {
            var positions = new TournamentMatchPosition[slots.Count / 2];
            for (var pair = 0; pair < positions.Length; pair++)
                positions[pair] = AddNode(bracket, round, pair + 1, slots[pair * 2], slots[pair * 2 + 1]);
            return positions;
        }

        TournamentMatchPosition AddNode(TournamentBracket bracket, int round, int pairing,
            TournamentBracketSlot one, TournamentBracketSlot two)
        {
            var position = new TournamentMatchPosition(bracket, round, pairing);
            nodes.Add(new TournamentBracketNode(position, one, two));
            return position;
        }
    }

    public static TournamentEliminationProgress Advance(
        TournamentStage stage, IReadOnlyList<Guid> seeds, IReadOnlyList<TournamentMatch> matches, DateTime now)
    {
        if (seeds.Count < 2) return new([], true, null);
        var isDouble = stage.Kind == TournamentStageKind.DoubleElimination;
        var hasReset = isDouble && stage.AllowGrandFinalReset;
        var layout = Layout(seeds.Count, isDouble, hasReset);
        var matching = matches.ToDictionary(match => match.Position);
        var skipped = new HashSet<TournamentMatchPosition>();
        var additions = new List<TournamentMatch>();
        foreach (var node in layout)
        {
            if (matching.ContainsKey(node.Position)) continue;
            if (node.Position == new TournamentMatchPosition(TournamentBracket.Finals, 2, 1)
                && matching.TryGetValue(node.One.Source!.Value, out var final)
                && final.CompletedAtUtc.HasValue && final.WinnerEntryId == final.EntryOneId)
                continue;

            var (oneReady, first) = Resolve(node.One);
            var (twoReady, second) = Resolve(node.Two);
            if (!oneReady || !twoReady) continue;
            if (!first.HasValue && !second.HasValue)
            {
                skipped.Add(node.Position);
                continue;
            }

            var bye = !first.HasValue || !second.HasValue;
            var match = new TournamentMatch
            {
                Id = Guid.NewGuid(), TournamentId = stage.TournamentId, StageNumber = stage.Number,
                Bracket = node.Position.Bracket, RoundNumber = node.Position.Round, PairingNumber = node.Position.Pairing,
                EntryOneId = (first ?? second)!.Value, EntryTwoId = bye ? null : second,
                WinnerEntryId = bye ? first ?? second : null, CompletedAtUtc = bye ? now : null
            };
            additions.Add(match);
            matching.Add(node.Position, match);
        }

        var decisive = matching.GetValueOrDefault(layout[^1].Position);
        if (hasReset && decisive is null
            && matching.TryGetValue(layout[^2].Position, out var grandFinal) && grandFinal.WinnerEntryId == grandFinal.EntryOneId)
            decisive = grandFinal;
        return new TournamentEliminationProgress(additions.ToArray(), decisive?.CompletedAtUtc.HasValue == true,
            decisive?.WinnerEntryId);

        (bool Ready, Guid? EntryId) Resolve(TournamentBracketSlot slot)
        {
            if (slot.Seed is { } seed) return (true, seed <= seeds.Count ? seeds[seed - 1] : null);
            var position = slot.Source!.Value;
            if (skipped.Contains(position)) return (true, null);
            if (!matching.TryGetValue(position, out var match) || !match.CompletedAtUtc.HasValue) return (false, null);
            return (true, slot.TakesLoser
                ? match.WinnerEntryId == match.EntryOneId ? match.EntryTwoId : match.EntryOneId
                : match.WinnerEntryId);
        }
    }
}
