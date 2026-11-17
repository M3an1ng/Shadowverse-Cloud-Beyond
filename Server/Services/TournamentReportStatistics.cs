using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

public static class TournamentReportStatistics
{
    public static TournamentReportDto Build(TournamentReportSource source, int stage = 0)
    {
        var selectedLineups = source.Lineups.Where(lineup => stage == 0 || lineup.StageNumber == stage).ToArray();
        var selectedDecks = selectedLineups.SelectMany(lineup => lineup.Decks).DistinctBy(deck => deck.Id).ToArray();
        var unclassified = selectedDecks.Count(deck => string.IsNullOrWhiteSpace(deck.Archetype));
        var allUnclassified = stage == 0 ? unclassified : source.Lineups.SelectMany(lineup => lineup.Decks)
            .Where(deck => string.IsNullOrWhiteSpace(deck.Archetype)).Select(deck => deck.Id).Distinct().Count();

        TournamentReportDto Describe(TournamentReportTotalsDto? totals = null,
            TournamentDeckMetricsDto[]? classes = null, TournamentDeckMetricsDto[]? archetypes = null,
            TournamentMatchupDto[]? classMatchups = null, TournamentMatchupDto[]? archetypeMatchups = null,
            TournamentAdvancementDto[]? advancement = null) => new(
            source.Event.Name, source.Event.CardVersion, source.Event.Status, stage, source.StageOptions(),
            unclassified, allUnclassified, totals, classes ?? [], archetypes ?? [], classMatchups ?? [],
            archetypeMatchups ?? [], advancement ?? []);

        if (unclassified != 0) return Describe();
        var decks = selectedDecks.ToDictionary(deck => deck.Id);
        var lineups = selectedLineups.ToDictionary(lineup => (lineup.StageNumber, lineup.Entry.Id), lineup => lineup.Decks);
        var classes = new GroupCounters();
        var archetypes = new GroupCounters();
        var deckCounters = decks.Values.ToDictionary(deck => deck.Id,
            deck => (Class: classes.Get(ClassName(deck)), Archetype: archetypes.Get(deck.Archetype!)));
        foreach (var deck in selectedLineups.SelectMany(lineup => lineup.Decks))
            ForDeck(deck.Id, counter => counter.LineupDecks++);

        var completed = 0;
        var pending = 0;
        var byes = 0;
        var games = 0;
        var draws = 0;
        var knownOrder = 0;
        var firstWins = 0;
        var formats = source.Stages.ToDictionary(item => item.Number, item => item.MatchFormat);
        foreach (var match in source.Matches.Where(match => stage == 0 || match.StageNumber == stage))
        {
            if (match.EntryTwoId is not { } entryTwo)
            {
                byes++;
                continue;
            }

            if (match.CompletedAtUtc.HasValue)
            {
                completed++;
                var allDecks = lineups[(match.StageNumber, match.EntryOneId)]
                    .Concat(lineups[(match.StageNumber, entryTwo)]);
                var canBan = formats[match.StageNumber] == TournamentMatchFormat.Bo3Ban1
                    && match.BanByOne.HasValue && match.BanByTwo.HasValue;
                foreach (var deck in allDecks)
                {
                    ForDeck(deck.Id, counter =>
                    {
                        counter.MatchLineups++;
                        if (canBan) counter.BanChances++;
                    });
                }
                if (canBan)
                {
                    ForDeck(match.BanByOne!.Value, counter => counter.Bans++);
                    ForDeck(match.BanByTwo!.Value, counter => counter.Bans++);
                }
            }
            else
            {
                pending++;
            }

            foreach (var game in match.Games.Where(game => game.CompletedAtUtc.HasValue))
            {
                games++;
                if (!game.WinnerEntryId.HasValue) draws++;
                var knownFirst = game.FirstPlayerEntryId == match.EntryOneId || game.FirstPlayerEntryId == entryTwo;
                if (knownFirst)
                {
                    knownOrder++;
                    if (game.FirstPlayerEntryId == game.WinnerEntryId) firstWins++;
                }
                var first = knownFirst ? game.FirstPlayerEntryId : null;
                GameSeat(game.EntryOneDeckId, game.EntryTwoDeckId, match.EntryOneId, game.WinnerEntryId, first);
                GameSeat(game.EntryTwoDeckId, game.EntryOneDeckId, entryTwo, game.WinnerEntryId, first);
            }
        }

        var totals = new TournamentReportTotalsDto(selectedLineups.Select(lineup => lineup.Entry.Id).Distinct().Count(),
            selectedLineups.Sum(lineup => lineup.Decks.Length), completed, pending, byes, games, draws, knownOrder, firstWins);
        var advancement = stage is 0 or 1 ? BuildAdvancement(source, lineups) : [];
        return Describe(totals, classes.Results(), archetypes.Results(), classes.Matchups(), archetypes.Matchups(), advancement);

        void ForDeck(Guid id, Action<Counter> action)
        {
            var metrics = deckCounters[id];
            action(metrics.Class);
            action(metrics.Archetype);
        }

        void GameSeat(Guid deckId, Guid oppositeDeckId, Guid entryId, Guid? winner, Guid? firstPlayer)
        {
            var own = deckCounters[deckId];
            var opponent = deckCounters[oppositeDeckId];
            var won = winner == entryId;
            bool? first = firstPlayer.HasValue ? firstPlayer == entryId : null;
            classes.AddGame(own.Class, opponent.Class, won, first);
            archetypes.AddGame(own.Archetype, opponent.Archetype, won, first);
        }
    }

    private static TournamentAdvancementDto[] BuildAdvancement(
        TournamentReportSource source, IReadOnlyDictionary<(int Stage, Guid Entry), TournamentEntryDeck[]> lineups)
    {
        var next = source.Event.Stages.SingleOrDefault(stage => stage.Number == 2);
        var first = source.Stages.SingleOrDefault(stage => stage.Number == 1);
        if (next is null || first?.CompletedAtUtc.HasValue != true) return [];
        var qualified = source.Participants.Where(player => player.StageNumber == 2).ToDictionary(player => player.EntryId);
        var seeds = source.Participants.Where(player => player.StageNumber == 1 && player.CanPlay)
            .Select(player => player.EntryId).ToArray();
        return TournamentPairings.Standings(seeds, source.Matches.Where(match => match.StageNumber == 1))
            .Select((row, index) =>
            {
                var advance = qualified.GetValueOrDefault(row.EntryId);
                return new TournamentAdvancementDto(index + 1, source.Entries[row.EntryId].PlayerName,
                    row.Wins, row.Losses, row.OpponentWins, row.GameWins, row.GameLosses, advance is not null,
                    advance is not null && next.StartedAtUtc.HasValue ? advance.CanPlay : null,
                    lineups.GetValueOrDefault((1, row.EntryId))?.Select(deck => deck.Archetype!).ToArray() ?? []);
            }).ToArray();
    }

    public static string ClassName(TournamentEntryDeck deck) => deck.SecondaryClass is { } secondary
        ? ClassLabel(deck.Class) + " + " + ClassLabel(secondary) : ClassLabel(deck.Class);

    private static string ClassLabel(CardClass cardClass) => cardClass switch
    {
        CardClass.Forestcraft => "妖精",
        CardClass.Swordcraft => "皇家护卫",
        CardClass.Runecraft => "巫师",
        CardClass.Dragoncraft => "龙族",
        CardClass.Abysscraft => "梦魇",
        CardClass.Havencraft => "主教",
        CardClass.Portalcraft => "复仇者",
        CardClass.Neutral => "中立",
        _ => cardClass.ToString()
    };

    private sealed class Counter(string name)
    {
        public string Name { get; } = name;
        public int LineupDecks { get; set; }
        public int Appearances { get; set; }
        public int Wins { get; set; }
        public int MatchLineups { get; set; }
        public int BanChances { get; set; }
        public int Bans { get; set; }
        public int FirstAppearances { get; set; }
        public int FirstWins { get; set; }
        public int SecondAppearances { get; set; }
        public int SecondWins { get; set; }
    }

    private sealed class GroupCounters
    {
        private readonly Dictionary<string, Counter> metrics = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<(string Name, string Opponent), (int Games, int Wins)> matchups = [];

        public Counter Get(string name)
        {
            if (!metrics.TryGetValue(name, out var counter)) metrics.Add(name, counter = new Counter(name));
            return counter;
        }

        public void AddGame(Counter own, Counter opponent, bool won, bool? first)
        {
            own.Appearances++;
            if (won) own.Wins++;
            if (first == true)
            {
                own.FirstAppearances++;
                if (won) own.FirstWins++;
            }
            else if (first == false)
            {
                own.SecondAppearances++;
                if (won) own.SecondWins++;
            }
            var key = (own.Name, opponent.Name);
            var current = matchups.GetValueOrDefault(key);
            matchups[key] = (current.Games + 1, current.Wins + (won ? 1 : 0));
        }

        public TournamentDeckMetricsDto[] Results() => metrics.Values.OrderByDescending(row => row.Appearances)
            .ThenByDescending(row => row.LineupDecks).ThenBy(row => row.Name, StringComparer.Ordinal)
            .Select(row => new TournamentDeckMetricsDto(row.Name, row.LineupDecks, row.Appearances, row.Wins,
                row.MatchLineups, row.BanChances, row.Bans, row.FirstAppearances, row.FirstWins,
                row.SecondAppearances, row.SecondWins)).ToArray();

        public TournamentMatchupDto[] Matchups() => matchups.Select(row =>
            new TournamentMatchupDto(row.Key.Name, row.Key.Opponent, row.Value.Games, row.Value.Wins))
            .OrderBy(row => row.Name, StringComparer.Ordinal).ThenBy(row => row.Opponent, StringComparer.Ordinal).ToArray();
    }
}
