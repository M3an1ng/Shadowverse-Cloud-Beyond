using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

internal static class TournamentReportTemplateSheets
{
    public static IEnumerable<TournamentWorksheet> Build(TournamentReportSource source, TournamentReportDto overall,
        IReadOnlyDictionary<int, TournamentReportDto> byStage)
    {
        var plaza = source.Event.Format == TournamentFormat.Plaza;
        var hasFinals = source.Event.Stages.Count > 1;
        var hasBans = source.Event.Stages.Any(stage => stage.MatchFormat == TournamentMatchFormat.Bo3Ban1);
        var reference = plaza ? "Plaza" : hasFinals ? "Major" : "Final";
        var lineups = source.Lineups.ToDictionary(lineup => (lineup.StageNumber, lineup.Entry.Id), lineup => lineup.Decks);
        var decks = source.Lineups.SelectMany(lineup => lineup.Decks).DistinctBy(deck => deck.Id).ToDictionary(deck => deck.Id);
        var archetypeColors = decks.Values.GroupBy(deck => deck.Archetype!).ToDictionary(group => group.Key,
            group => group.Select(DeckColor).Distinct().ToArray() is [var color] ? color : null);
        var classColors = decks.Values.GroupBy(TournamentReportStatistics.ClassName).ToDictionary(group => group.Key,
            group => DeckColor(group.First()));
        var completedGames = source.Matches.SelectMany(match => match.Games.Where(game => game.CompletedAtUtc.HasValue)
            .OrderBy(game => game.Number).Select(game => (Match: match, Game: game))).ToArray();

        if (plaza) yield return PlazaLineups();
        else
            foreach (var stage in source.Event.Stages.OrderBy(stage => stage.Number))
                yield return MajorLineups(stage);
        yield return Metrics("职业", "胜利场次", report => report.Classes, classColors);
        if (!plaza) yield return Metrics("卡组名", "胜场数", report => report.Archetypes, archetypeColors);
        yield return plaza ? PlazaGames() : MajorGames();

        string? Player(Guid? id) => id is { } entry ? source.Entries[entry].PlayerName : null;
        string? Lineup(int stage, Guid? id) => id is { } entry && lineups.TryGetValue((stage, entry), out var cards)
            ? string.Join("、", cards.Select(deck => deck.Archetype)) : null;
        object? DeckType(Guid? id) => id is { } deck ? Tint(decks[deck].Archetype!, DeckColor(decks[deck])) : null;
        TournamentTableFormat Format(string table) => TournamentReportFormats.Reference.Tables[reference + table];

        TournamentWorksheet MajorLineups(TournamentStage stage)
        {
            var rows = source.Lineups.Where(item => item.StageNumber == stage.Number).Select(lineup =>
            {
                var slots = new object?[5];
                slots[0] = lineup.Entry.PlayerName;
                for (var index = 0; index < lineup.Decks.Length; index++)
                    slots[index + 1] = DeckType(lineup.Decks[index].Id);
                slots[4] = Lineup(stage.Number, lineup.Entry.Id);
                return slots;
            }).ToArray();
            return new(Format(stage.Number == 1 ? "Lineups" : "FinalLineups"),
                [("选手", 22), ("卡组1", 18), ("卡组2", 18), ("卡组3", 18), ("卡组列表", 41)], rows);
        }

        TournamentWorksheet PlazaLineups()
        {
            var results = source.Matches.SelectMany(match => match.EntryTwoId is { } other
                ? new[] { (Entry: match.EntryOneId, Match: match), (Entry: other, Match: match) }
                : [(Entry: match.EntryOneId, Match: match)]).ToLookup(row => row.Entry, row => row.Match);
            var standings = TournamentPairings.Standings(source.Lineups.Select(lineup => lineup.Entry.Id).ToArray(), source.Matches);
            var rows = standings.Select((standing, index) =>
            {
                var playerMatches = results[standing.EntryId].ToDictionary(match => match.RoundNumber);
                var decksForPlayer = lineups[(1, standing.EntryId)];
                var deck = decksForPlayer.Length == 1 ? DeckType(decksForPlayer[0].Id) : Lineup(1, standing.EntryId);
                var next = playerMatches.Values.FirstOrDefault(match => !match.CompletedAtUtc.HasValue);
                var opponent = next is null ? null : next.EntryOneId == standing.EntryId ? next.EntryTwoId : next.EntryOneId;
                return new object?[] { Player(standing.EntryId), deck, Mark(1), Mark(2), Mark(3), Mark(4), standing.Wins,
                    Player(opponent) ?? (index == 0 && source.Event.Status == TournamentStatus.Finished ? "已结束" : null) };

                object? Mark(int round)
                {
                    if (!playerMatches.TryGetValue(round, out var match) || !match.CompletedAtUtc.HasValue) return null;
                    if (!match.EntryTwoId.HasValue) return "轮空";
                    return match.WinnerEntryId == standing.EntryId
                        ? new TournamentColoredText("○", TournamentCellColor.Win)
                        : new TournamentColoredText("×", TournamentCellColor.Loss);
                }
            }).ToArray();
            return new(Format("Lineups"), [("选手", 22), ("卡组", 22), ("第一局", 10), ("第二局", 10), ("第三局", 10),
                ("第四局", 10), ("总胜场", 12), ("待进行对局", 25)], rows);
        }

        TournamentWorksheet Metrics(string label, string winHeading, Func<TournamentReportDto, TournamentDeckMetricsDto[]> select,
            IReadOnlyDictionary<string, TournamentCellColor?> colors)
        {
            var first = select(byStage.GetValueOrDefault(1) ?? overall).ToDictionary(row => row.Name, row => row.LineupDecks);
            var second = byStage.TryGetValue(2, out var final) ? select(final).ToDictionary(row => row.Name, row => row.LineupDecks) : [];
            var showSeries = !plaza || hasBans;
            var mixedFormats = hasBans && source.Event.Stages.Any(stage => stage.MatchFormat != TournamentMatchFormat.Bo3Ban1);
            var headings = new List<(string, double)> { (label, 23), (hasFinals ? "初赛数量" : "数量", 13) };
            if (hasFinals) headings.Add(("决赛数量", 13));
            headings.AddRange([("对局数", 12), (winHeading, 13), ("胜率", 13)]);
            if (showSeries) headings.Add(("大局数", 12));
            if (hasBans) headings.AddRange([("ban次数", 13), ("ban率", 13)]);
            if (mixedFormats) headings.Add(("可 Ban 大局", 16));

            var rows = select(overall).Select(row =>
            {
                var cells = new List<object?> { Tint(row.Name, colors.GetValueOrDefault(row.Name)),
                    hasFinals ? first.GetValueOrDefault(row.Name) : row.LineupDecks };
                if (hasFinals) cells.Add(second.GetValueOrDefault(row.Name));
                cells.AddRange([row.Appearances, row.Wins, Rate(row.WinRate)]);
                if (showSeries) cells.Add(row.MatchLineups);
                if (hasBans) cells.AddRange([row.Bans, Rate(row.BanRate)]);
                if (mixedFormats) cells.Add(row.BanChances);
                return cells.ToArray();
            }).ToArray();
            var metricFormat = plaza && hasBans ? TournamentReportFormats.Reference.Tables["FinalClass"]
                : Format(plaza || label == "职业" ? "Class" : "Archetype");
            return new(metricFormat, headings.ToArray(), rows);
        }

        TournamentWorksheet PlazaGames() => new(Format("Games"),
            [("胜者", 22), ("卡组", 21), ("败者", 22), ("卡组", 21), ("备注", 33)],
            completedGames.Select(item =>
            {
                var (match, game) = item;
                var oneWon = game.WinnerEntryId == match.EntryOneId;
                return game.WinnerEntryId.HasValue
                    ? new object?[] { Player(game.WinnerEntryId), DeckType(oneWon ? game.EntryOneDeckId : game.EntryTwoDeckId),
                        Player(oneWon ? match.EntryTwoId : match.EntryOneId), DeckType(oneWon ? game.EntryTwoDeckId : game.EntryOneDeckId),
                        null }
                    : [null, DeckType(game.EntryOneDeckId), null, DeckType(game.EntryTwoDeckId),
                        $"平局 · {Player(match.EntryOneId)} / {Player(match.EntryTwoId)}"];
            }).ToArray(), UniformRowHeight: true);

        TournamentWorksheet MajorGames()
        {
            (string Heading, double Width)[] headings = [("玩家1", 22), ("卡组1", 18), ("卡组2", 18), ("玩家2", 22),
                ("胜利卡组", 20), ("失败卡组", 20), ("禁用卡组1", 19), ("禁用卡组2", 19),
                ("玩家1卡组列表", 36), ("玩家2卡组列表", 36), ("备注", 33)];
            int[] columns = hasBans ? [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10] : [0, 1, 2, 3, 4, 5, 8, 9, 10];
            int[] mergedColumns = hasBans ? [1, 4, 7, 8, 9, 10, 11] : [1, 4, 7, 8, 9];
            var rows = new List<object?[]>();
            var merges = new List<TournamentRowMerge>();
            foreach (var series in completedGames.GroupBy(item => item.Match))
            {
                var match = series.Key;
                var swap = match.EntryTwoId.HasValue && match.WinnerEntryId == match.EntryTwoId;
                var playerOne = swap ? match.EntryTwoId : match.EntryOneId;
                var playerTwo = swap ? match.EntryOneId : match.EntryTwoId;
                var games = series.Select(item => item.Game).ToArray();
                var notes = SeriesNotes(match, games);
                var first = rows.Count + 1;
                foreach (var game in games)
                {
                    var header = rows.Count + 1 == first;
                    var firstDeck = swap ? game.EntryTwoDeckId : game.EntryOneDeckId;
                    var secondDeck = swap ? game.EntryOneDeckId : game.EntryTwoDeckId;
                    var oneWon = game.WinnerEntryId == playerOne;
                    object?[] cells = [header ? Seed(playerOne) : null, DeckType(firstDeck), DeckType(secondDeck),
                        header ? Seed(playerTwo) : null,
                        game.WinnerEntryId.HasValue ? DeckType(oneWon ? firstDeck : secondDeck) : "平局",
                        game.WinnerEntryId.HasValue ? DeckType(oneWon ? secondDeck : firstDeck) : null,
                        header && match.CompletedAtUtc.HasValue ? DeckType(swap ? match.BanByOne : match.BanByTwo) : null,
                        header && match.CompletedAtUtc.HasValue ? DeckType(swap ? match.BanByTwo : match.BanByOne) : null,
                        header ? Lineup(match.StageNumber, playerOne) : null,
                        header ? Lineup(match.StageNumber, playerTwo) : null,
                        header ? notes : null];
                    rows.Add(columns.Select(column => cells[column]).ToArray());
                }
                if (rows.Count > first)
                    foreach (var column in mergedColumns) merges.Add(new(column, first, rows.Count));

                object? Seed(Guid? player) => Player(player) is { } name
                    ? Tint(name, player == match.WinnerEntryId ? TournamentCellColor.Win : null) : null;
            }
            return new(Format("Games").SelectColumns(columns), columns.Select(column => headings[column]).ToArray(), rows,
                Merges: merges, UniformRowHeight: true);
        }

        string? SeriesNotes(TournamentMatch match, TournamentMatchGame[] games)
        {
            var notes = new List<string>();
            foreach (var game in games)
            {
                var type = decks[game.EntryOneDeckId].Archetype;
                if (game.WinnerEntryId is { } winner && type == decks[game.EntryTwoDeckId].Archetype)
                    notes.Add(type + "内战" + Player(winner) + "胜");
            }
            if (!match.CompletedAtUtc.HasValue) notes.Add("进行中");
            return notes.Count == 0 ? null : string.Join("\n", notes);
        }
    }

    private static object Rate(double? value) => value is { } present ? new TournamentPercent(present) : "—";
    private static object Tint(string text, TournamentCellColor? color) =>
        color is { } tint ? new TournamentColoredText(text, tint) : text;

    private static TournamentCellColor? DeckColor(TournamentEntryDeck deck) => deck.SecondaryClass.HasValue ? null : deck.Class switch
    {
        CardClass.Forestcraft => TournamentCellColor.Forest,
        CardClass.Swordcraft => TournamentCellColor.Sword,
        CardClass.Runecraft => TournamentCellColor.Rune,
        CardClass.Dragoncraft => TournamentCellColor.Dragon,
        CardClass.Abysscraft => TournamentCellColor.Abyss,
        CardClass.Havencraft => TournamentCellColor.Haven,
        CardClass.Portalcraft => TournamentCellColor.Portal,
        _ => null
    };
}
