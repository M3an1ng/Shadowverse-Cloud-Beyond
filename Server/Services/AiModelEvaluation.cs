using System.Security.Cryptography;
using System.Text.Json;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

public sealed record AiBenchmarkOpening(string DeckA, string DeckB)
{
    internal static AiBenchmarkOpening? Capture(GameState game, Guid deckAPlayer)
    {
        if (!game.IsLocalAiTraining || game.Phase != GamePhase.Mulligan) return null;
        return CaptureZones(game, deckAPlayer, afterMulligan: false);
    }

    // Called immediately after the second committed mulligan, before the first AI decision.
    internal static AiBenchmarkOpening? CaptureAfterMulligan(GameState game, Guid deckAPlayer)
    {
        if (!game.IsLocalAiTraining || game.Phase != GamePhase.Main) return null;
        return CaptureZones(game, deckAPlayer, afterMulligan: true);
    }

    private static AiBenchmarkOpening? CaptureZones(GameState game, Guid deckAPlayer, bool afterMulligan)
    {
        string? Fingerprint(PlayerState? player)
        {
            if (player == null || player.HasCompletedMulligan != afterMulligan || player.Hand.Count == 0 || player.Deck.Count == 0)
                return null;
            var hand = player.Hand.Select(card => card.BaseDefinitionId).ToArray();
            var deck = player.Deck.Select(card => card.BaseDefinitionId).ToArray();
            if (!afterMulligan && !hand.Concat(deck).SequenceEqual(player.InitialDeckDefinitionIds)) return null;
            return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { hand, deck })));
        }

        var a = Fingerprint(game.GetPlayerState(deckAPlayer));
        var b = Fingerprint(game.GetOpponentPlayerState(deckAPlayer));
        return a == null || b == null ? null : new(a, b);
    }
}

public sealed record AiModelEvaluation(string CandidateVersion, string IncumbentVersion, bool Accepted,
    int Games, int Wins, int Losses, int Draws, int Failed, double WinRateLowerBound,
    string Message, StrategyMatchResult[] Matches, int SeedWins=0, int SeedLosses=0, int SeedDraws=0, double PairedSignP=1);

public sealed record AiStrategyBenchmarkSummary(
    int PlannedGroups, int[] CompleteGroups, int UnpairedGames,
    int Wins, int Losses, int Draws, int GroupsAhead, int GroupsBehind, int GroupsTied,
    int GamesWithDifferentActions, int DifferentCompletedActions, int GamesWithDifferentOptions, int DifferentCommittedOptions, int OpeningMismatchGroups)
{
    internal static AiStrategyBenchmarkSummary Calculate(
        IReadOnlyList<StrategyMatchResult> matches, int planned, int seed, Guid deckA, Guid deckB)
    {
        var scheduled = matches.Where(match => match.Episode >= 1 && match.Episode <= planned)
            .GroupBy(match => (match.Episode - 1) / 4)
            .Where(group => group.Count() == 4 && group.Select(match => match.Episode).Distinct().Count() == 4
                && group.All(match => match.ShuffleSeed == unchecked(seed + group.Key)
                    && match.DeckId == (match.Episode % 2 == 1 ? deckA : deckB)
                    && match.First == (match.Episode % 4 is 0 or 1)
                    && match.Result is "win" or "loss" or "draw"))
            .ToArray();
        var groups = scheduled.Where(group => group.First().Opening is { } opening
                && group.All(match => match.Opening == opening))
            .OrderBy(group => group.Key)
            .Select(group => new
            {
                group.Key,
                Wins = group.Count(match => match.Result == "win"),
                Losses = group.Count(match => match.Result == "loss"),
                GamesWithDifferentActions = group.Count(match => match.DifferentCompletedActions > 0),
                DifferentCompletedActions = group.Sum(match => match.DifferentCompletedActions),
                GamesWithDifferentOptions = group.Count(match => match.DifferentCommittedOptions > 0),
                DifferentCommittedOptions = group.Sum(match => match.DifferentCommittedOptions)
            })
            .ToArray();
        var games = groups.Length * 4;
        var wins = groups.Sum(group => group.Wins);
        var losses = groups.Sum(group => group.Losses);
        var ahead = groups.Count(group => group.Wins > group.Losses);
        var behind = groups.Count(group => group.Wins < group.Losses);
        return new(planned / 4, groups.Select(group => group.Key).ToArray(), matches.Count - games,
            wins, losses, games - wins - losses, ahead, behind, groups.Length - ahead - behind,
            groups.Sum(group => group.GamesWithDifferentActions), groups.Sum(group => group.DifferentCompletedActions),
            groups.Sum(group => group.GamesWithDifferentOptions), groups.Sum(group => group.DifferentCommittedOptions),
            scheduled.Length - groups.Length);
    }
}

public sealed record AiStrategyExperienceArm(int Games, int Wins, int Losses, int Draws, int GamesWithHistory,
    int PlanningAttempts, int AcceptedPlans, int ChoiceAttempts, int AcceptedChoices,
    int DifferentCompletedActions, int DifferentCommittedOptions)
{
    internal static AiStrategyExperienceArm Count(IEnumerable<StrategyMatchResult> matches)
    {
        var rows = matches.ToArray();
        return new(rows.Length, rows.Count(row => row.Result == "win"), rows.Count(row => row.Result == "loss"),
            rows.Count(row => row.Result == "draw"), rows.Count(row => row.ExperienceGames > 0),
            rows.Sum(row => row.Turn.Attempts), rows.Sum(row => row.Turn.Accepted),
            rows.Sum(row => row.Choice.Attempts), rows.Sum(row => row.Choice.Accepted),
            rows.Sum(row => row.DifferentCompletedActions), rows.Sum(row => row.DifferentCommittedOptions));
    }
}

public sealed record AiStrategyExperiencePairOutcomes(int Improved, int Regressed, int Unchanged, int WithAdviceInBoth);

public sealed record AiStrategyExperienceBenchmark(int PlannedGroups, int[] CompleteGroups, int UnpairedGames,
    int OpeningMismatchGroups, int PostMulliganMismatchGroups, AiStrategyExperienceArm WithHistory, AiStrategyExperienceArm WithoutHistory,
    AiStrategyExperiencePairOutcomes PairOutcomes, AiExperienceRootComparison[] RootComparisons)
{
    internal static (int MatchSlot, bool WithHistory) Schedule(int episode)
    {
        var matchSlot = episode / 2;
        return (matchSlot, (episode + matchSlot / 4) % 2 == 0);
    }

    internal static AiStrategyExperienceBenchmark Calculate(IReadOnlyList<StrategyMatchResult> matches, int planned,
        int seed, Guid deckA, Guid deckB)
    {
        var scheduled = matches.Where(match => match.Episode >= 1 && match.Episode <= planned)
            .GroupBy(match => (match.Episode - 1) / 8)
            .Where(group => group.Count() == 8 && group.Select(match => match.Episode).Distinct().Count() == 8
                && group.All(match => MatchesSchedule(match, seed, deckA, deckB))).ToArray();
        var sameDeal = scheduled.Where(group => group.First().Opening is { } opening
            && group.All(match => match.Opening == opening)).ToArray();
        var complete = sameDeal.Where(group => group.GroupBy(match => Schedule(match.Episode - 1).MatchSlot)
            .All(pair => pair.First().AfterMulligan is { } opening && pair.All(match => match.AfterMulligan == opening)))
            .OrderBy(group => group.Key).ToArray();
        var rows = complete.SelectMany(group => group).ToArray();
        static int Score(string result) => result switch { "win" => 2, "draw" => 1, _ => 0 };
        var pairs = rows.GroupBy(match => Schedule(match.Episode - 1).MatchSlot).Select(pair =>
        {
            var history = pair.Single(match => match.WithStrategyExperience == true);
            var control = pair.Single(match => match.WithStrategyExperience == false);
            return new
            {
                Difference = Score(history.Result) - Score(control.Result),
                Roots = AiExperienceRootTrace.Compare(history, control),
                BothUsedAdvice = history.Turn.Accepted + history.Choice.Accepted > 0
                    && control.Turn.Accepted + control.Choice.Accepted > 0
            };
        }).ToArray();
        var outcomes = new AiStrategyExperiencePairOutcomes(pairs.Count(pair => pair.Difference > 0),
            pairs.Count(pair => pair.Difference < 0), pairs.Count(pair => pair.Difference == 0),
            pairs.Count(pair => pair.BothUsedAdvice));
        return new(planned / 8, complete.Select(group => group.Key).ToArray(), matches.Count - rows.Length,
            scheduled.Length - sameDeal.Length, sameDeal.Length - complete.Length,
            AiStrategyExperienceArm.Count(rows.Where(row => row.WithStrategyExperience == true)),
            AiStrategyExperienceArm.Count(rows.Where(row => row.WithStrategyExperience == false)), outcomes,
            pairs.Select(pair => pair.Roots).OrderBy(pair => Math.Min(pair.HistoryEpisode, pair.ControlEpisode)).ToArray());
    }

    private static bool MatchesSchedule(StrategyMatchResult match, int seed, Guid deckA, Guid deckB)
    {
        var (slot, history) = Schedule(match.Episode - 1);
        return match.ShuffleSeed == unchecked(seed + slot / 4) && match.WithStrategyExperience == history
            && match.ExperienceGames >= 0 && (history || match.ExperienceGames == 0)
            && match.DeckId == (slot % 2 == 0 ? deckA : deckB)
            && match.First == (slot % 4 is 0 or 3) && match.Result is "win" or "loss" or "draw";
    }
}

// A local screening gate. Its confidence bound is evidence for this matchup set only,
// not a claim about all decks or future card revisions. Draws do not count as wins.
internal static class AiModelStrengthGate
{
    internal const int Games = 24;
    internal static AiModelEvaluation Evaluate(string candidate, string incumbent,
        IReadOnlyList<StrategyMatchResult> matches, int failed, IReadOnlySet<Guid> deckIds)
    {
        var wins = matches.Count(m => m.Result == "win");
        var losses = matches.Count(m => m.Result == "loss");
        var draws = matches.Count(m => m.Result == "draw");
        var n = matches.Count; const double z = 1.96;
        var p = n == 0 ? 0 : wins / (double)n;
        var lower = n == 0 ? 0 : (p + z * z / (2 * n)
            - z * Math.Sqrt((p * (1 - p) + z * z / (4 * n)) / n)) / (1 + z * z / n);
        var blocks = matches.GroupBy(m => (m.Episode - 1) / 4).ToArray();
        var seedWins = blocks.Count(b => b.Count(m => m.Result == "win") > b.Count(m => m.Result == "loss"));
        var seedLosses = blocks.Count(b => b.Count(m => m.Result == "win") < b.Count(m => m.Result == "loss"));
        var seedDraws = blocks.Length - seedWins - seedLosses;
        // Starting-seed blocks are paired; do not pretend their four games are independent.
        var effective = seedWins + seedLosses;
        var signP = 1d;
        if (effective > 0 && effective <= 6) {
            var coefficient = 1d; var tail = 0d;
            for (var k = 0; k <= effective; k++) {
                if (k >= seedWins) tail += coefficient;
                coefficient = coefficient * (effective - k) / (k + 1);
            }
            signP = tail / Math.Pow(2, effective);
        }
        string? rejection = null;
        if (failed != 0 || n != Games || wins + losses + draws != n)
            rejection = "实战验证未完整完成，保留旧模型";
        else if (deckIds.Count is < 1 or > 2 || matches.Any(m => !deckIds.Contains(m.DeckId))
            || !matches.Select(m => m.Episode).Order().SequenceEqual(Enumerable.Range(1, Games)))
            rejection = "实战验证记录不完整或牌组发生变化，保留旧模型";
        else if (deckIds.Any(id => matches.Count(m => m.DeckId == id) != Games / deckIds.Count
            || matches.Count(m => m.DeckId == id && m.First) != Games / deckIds.Count / 2))
            rejection = "牌组和先后手未均衡，保留旧模型";
        else if (matches.Any(m => m.ShuffleSeed != unchecked(matches.Single(first => first.Episode == 1).ShuffleSeed + (m.Episode - 1) / 4))
            || matches.GroupBy(m => (m.Episode - 1) / 4).Any(block => deckIds.Any(id =>
                block.Count(m => m.DeckId == id) != 4 / deckIds.Count
                || block.Count(m => m.DeckId == id && m.First) != 2 / deckIds.Count)))
            rejection = "起手种子或分组对照未配对，保留旧模型";
        else if (lower <= .5)
            rejection = "本组实战优势证据不足，保留旧模型";
        else if (signP > .05)
            rejection = "不同起手分组尚未显示一致优势，保留旧模型";
        else if (matches.GroupBy(m => m.DeckId).Any(g => g.Count(m => m.Result == "win") + .5 * g.Count(m => m.Result == "draw") < g.Count() / 2d)
            || matches.GroupBy(m => m.First).Any(g => g.Count(m => m.Result == "win") + .5 * g.Count(m => m.Result == "draw") < g.Count() / 2d))
            rejection = "部分牌组或先后手表现退步，保留旧模型";
        return new(candidate, incumbent, rejection == null, n, wins, losses, draws, failed, lower,
            rejection ?? "候选通过本组实战验证，可以替换旧模型", matches.ToArray(),seedWins,seedLosses,seedDraws,signP);
    }
}
