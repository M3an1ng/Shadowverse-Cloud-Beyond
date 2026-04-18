using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;
using System.Data;
using System.Globalization;
using System.Text.Json;

namespace ShadowverseBeyond.Server.Controllers;

[ApiController]
[Route("api/devtools/winrate-stats")]
public class DevToolsWinrateStatsController : ControllerBase
{
    private const int DefaultTop = 50;
    private const int MaxTop = 2000;

    private readonly ApplicationDbContext _db;
    private readonly ILogger<DevToolsWinrateStatsController> _logger;

    public DevToolsWinrateStatsController(
        ApplicationDbContext db,
        ILogger<DevToolsWinrateStatsController> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(WinrateStatsSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<WinrateStatsSummaryResponse>> GetSummary(
        [FromQuery] Guid userId,
        [FromQuery] string? environment = null,
        [FromQuery] string? deckRule = null,
        [FromQuery] int top = DefaultTop,
        [FromQuery] string? cardName = null,
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return BadRequest("userId is required.");
        }

        var userExists = await _db.Users.AnyAsync(user => user.Id == userId, cancellationToken);
        if (!userExists)
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Only registered users can access devtools winrate stats.");
        }

        if (!TryParseEnvironment(environment, out var targetEnvironment))
        {
            return BadRequest("Unsupported environment.");
        }
        if (!TryParseDeckRule(deckRule, out var targetDeckRule))
        {
            return BadRequest("Unsupported deck rule.");
        }
        if (!TryParseDateRange(startDate, endDate, out var rangeStartUtc, out var rangeEndUtcInclusive, out var dateError))
        {
            return BadRequest(dateError ?? "Unsupported date range.");
        }

        var take = Math.Clamp(top, 1, MaxTop);
        var hasEndTurnNumberColumn = await HasColumnAsync("GameParticipantStatsFacts", "EndTurnNumber", cancellationToken);
        var factsQuery = _db.GameParticipantStatsFacts
            .AsNoTracking()
            .Where(item =>
                item.IsOfficial &&
                item.Environment == targetEnvironment &&
                item.Rule == targetDeckRule);
        if (rangeStartUtc.HasValue)
        {
            factsQuery = factsQuery.Where(item => item.EndedAtUtc >= rangeStartUtc.Value);
        }
        if (rangeEndUtcInclusive.HasValue)
        {
            factsQuery = factsQuery.Where(item => item.EndedAtUtc <= rangeEndUtcInclusive.Value);
        }

        var cardFactRows = await factsQuery
            .Select(item => new CardFactRow
            {
                IsWinner = item.IsWinner,
                EndedAtUtc = item.EndedAtUtc,
                InitialDeckCardCountsJson = item.InitialDeckCardCountsJson,
                DrawnCardIdsJson = item.DrawnCardIdsJson,
                PlayedCardIdsJson = item.PlayedCardIdsJson
            })
            .ToListAsync(cancellationToken);

        var cardAggMap = BuildCardAggMap(cardFactRows);
        var normalizedCardNameKeyword = NormalizeKeyword(cardName);
        HashSet<int>? matchedCardIds = null;
        if (!string.IsNullOrEmpty(normalizedCardNameKeyword))
        {
            matchedCardIds = (await _db.CardDefinitions
                .AsNoTracking()
                .Where(item => EF.Functions.Like(item.Name, $"%{normalizedCardNameKeyword}%"))
                .Select(item => item.Id)
                .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        var topCardAggs = cardAggMap.Values
            .Where(item => matchedCardIds == null || matchedCardIds.Contains(item.CardDefinitionId))
            .OrderByDescending(item => item.DeckGames)
            .ThenByDescending(item => item.DrawnGames)
            .Take(take)
            .ToList();

        var cardIds = topCardAggs.Select(item => item.CardDefinitionId).Distinct().ToList();
        var cardNames = await _db.CardDefinitions
            .AsNoTracking()
            .Where(item => cardIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

        List<DeckAggregateRow> deckAggs;
        if (hasEndTurnNumberColumn)
        {
            deckAggs = await factsQuery
                .GroupBy(item => new { item.DeckShapeHash, item.DeckClass })
                .Select(group => new DeckAggregateRow
                {
                    DeckShapeHash = group.Key.DeckShapeHash,
                    DeckClass = group.Key.DeckClass,
                    TotalGames = group.Count(),
                    TotalWins = group.Sum(item => item.IsWinner ? 1 : 0),
                    GoingFirstGames = group.Sum(item => item.IsFirstPlayer == true ? 1 : 0),
                    GoingFirstWins = group.Sum(item => item.IsFirstPlayer == true && item.IsWinner ? 1 : 0),
                    GoingSecondGames = group.Sum(item => item.IsFirstPlayer == false ? 1 : 0),
                    GoingSecondWins = group.Sum(item => item.IsFirstPlayer == false && item.IsWinner ? 1 : 0),
                    AverageEndTurn = group.Average(item => (double?)item.EndTurnNumber),
                    GoingFirstAverageEndTurn = group.Average(item => item.IsFirstPlayer == true ? (double?)item.EndTurnNumber : null),
                    GoingSecondAverageEndTurn = group.Average(item => item.IsFirstPlayer == false ? (double?)item.EndTurnNumber : null),
                    UpdatedAtUtc = group.Max(item => item.EndedAtUtc)
                })
                .OrderByDescending(item => item.TotalGames)
                .ThenByDescending(item => item.TotalWins)
                .Take(take)
                .ToListAsync(cancellationToken);
        }
        else
        {
            deckAggs = await factsQuery
                .GroupBy(item => new { item.DeckShapeHash, item.DeckClass })
                .Select(group => new DeckAggregateRow
                {
                    DeckShapeHash = group.Key.DeckShapeHash,
                    DeckClass = group.Key.DeckClass,
                    TotalGames = group.Count(),
                    TotalWins = group.Sum(item => item.IsWinner ? 1 : 0),
                    GoingFirstGames = group.Sum(item => item.IsFirstPlayer == true ? 1 : 0),
                    GoingFirstWins = group.Sum(item => item.IsFirstPlayer == true && item.IsWinner ? 1 : 0),
                    GoingSecondGames = group.Sum(item => item.IsFirstPlayer == false ? 1 : 0),
                    GoingSecondWins = group.Sum(item => item.IsFirstPlayer == false && item.IsWinner ? 1 : 0),
                    AverageEndTurn = null,
                    GoingFirstAverageEndTurn = null,
                    GoingSecondAverageEndTurn = null,
                    UpdatedAtUtc = group.Max(item => item.EndedAtUtc)
                })
                .OrderByDescending(item => item.TotalGames)
                .ThenByDescending(item => item.TotalWins)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        List<ClassAggregateRow> classAggs;
        if (hasEndTurnNumberColumn)
        {
            classAggs = await factsQuery
                .GroupBy(item => item.DeckClass)
                .Select(group => new ClassAggregateRow
                {
                    DeckClass = group.Key,
                    TotalGames = group.Count(),
                    TotalWins = group.Sum(item => item.IsWinner ? 1 : 0),
                    GoingFirstGames = group.Sum(item => item.IsFirstPlayer == true ? 1 : 0),
                    GoingFirstWins = group.Sum(item => item.IsFirstPlayer == true && item.IsWinner ? 1 : 0),
                    GoingSecondGames = group.Sum(item => item.IsFirstPlayer == false ? 1 : 0),
                    GoingSecondWins = group.Sum(item => item.IsFirstPlayer == false && item.IsWinner ? 1 : 0),
                    AverageEndTurn = group.Average(item => (double?)item.EndTurnNumber),
                    GoingFirstAverageEndTurn = group.Average(item => item.IsFirstPlayer == true ? (double?)item.EndTurnNumber : null),
                    GoingSecondAverageEndTurn = group.Average(item => item.IsFirstPlayer == false ? (double?)item.EndTurnNumber : null),
                    UpdatedAtUtc = group.Max(item => item.EndedAtUtc)
                })
                .OrderByDescending(item => item.TotalGames)
                .ThenByDescending(item => item.TotalWins)
                .Take(take)
                .ToListAsync(cancellationToken);
        }
        else
        {
            classAggs = await factsQuery
                .GroupBy(item => item.DeckClass)
                .Select(group => new ClassAggregateRow
                {
                    DeckClass = group.Key,
                    TotalGames = group.Count(),
                    TotalWins = group.Sum(item => item.IsWinner ? 1 : 0),
                    GoingFirstGames = group.Sum(item => item.IsFirstPlayer == true ? 1 : 0),
                    GoingFirstWins = group.Sum(item => item.IsFirstPlayer == true && item.IsWinner ? 1 : 0),
                    GoingSecondGames = group.Sum(item => item.IsFirstPlayer == false ? 1 : 0),
                    GoingSecondWins = group.Sum(item => item.IsFirstPlayer == false && item.IsWinner ? 1 : 0),
                    AverageEndTurn = null,
                    GoingFirstAverageEndTurn = null,
                    GoingSecondAverageEndTurn = null,
                    UpdatedAtUtc = group.Max(item => item.EndedAtUtc)
                })
                .OrderByDescending(item => item.TotalGames)
                .ThenByDescending(item => item.TotalWins)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        var response = new WinrateStatsSummaryResponse
        {
            Environment = targetEnvironment.ToString(),
            Rule = targetDeckRule.ToString(),
            Top = take,
            GeneratedAtUtc = DateTime.UtcNow,
            CardStats = topCardAggs.Select(item => new CardWinrateSummary
            {
                CardDefinitionId = item.CardDefinitionId,
                CardName = cardNames.TryGetValue(item.CardDefinitionId, out var name) ? name : $"Card #{item.CardDefinitionId}",
                DeckGames = item.DeckGames,
                DeckWins = item.DeckWins,
                DeckWinRate = ToPercentage(item.DeckWins, item.DeckGames),
                DrawnGames = item.DrawnGames,
                DrawnWins = item.DrawnWins,
                DrawnWinRate = ToPercentage(item.DrawnWins, item.DrawnGames),
                PlayedGames = item.PlayedGames,
                PlayedWins = item.PlayedWins,
                PlayedWinRate = ToPercentage(item.PlayedWins, item.PlayedGames),
                UpdatedAtUtc = item.UpdatedAtUtc
            }).ToList(),
            DeckStats = deckAggs.Select(item => new DeckWinrateSummary
            {
                DeckShapeHash = item.DeckShapeHash,
                DeckClass = item.DeckClass.ToString(),
                TotalGames = item.TotalGames,
                TotalWins = item.TotalWins,
                WinRate = ToPercentage(item.TotalWins, item.TotalGames),
                GoingFirstGames = item.GoingFirstGames,
                GoingFirstWins = item.GoingFirstWins,
                GoingFirstWinRate = ToPercentage(item.GoingFirstWins, item.GoingFirstGames),
                GoingSecondGames = item.GoingSecondGames,
                GoingSecondWins = item.GoingSecondWins,
                GoingSecondWinRate = ToPercentage(item.GoingSecondWins, item.GoingSecondGames),
                AverageEndTurn = RoundAverage(item.AverageEndTurn),
                GoingFirstAverageEndTurn = RoundAverage(item.GoingFirstAverageEndTurn),
                GoingSecondAverageEndTurn = RoundAverage(item.GoingSecondAverageEndTurn),
                UpdatedAtUtc = item.UpdatedAtUtc
            }).ToList(),
            ClassStats = classAggs.Select(item => new ClassWinrateSummary
            {
                DeckClass = item.DeckClass.ToString(),
                TotalGames = item.TotalGames,
                TotalWins = item.TotalWins,
                WinRate = ToPercentage(item.TotalWins, item.TotalGames),
                GoingFirstGames = item.GoingFirstGames,
                GoingFirstWins = item.GoingFirstWins,
                GoingFirstWinRate = ToPercentage(item.GoingFirstWins, item.GoingFirstGames),
                GoingSecondGames = item.GoingSecondGames,
                GoingSecondWins = item.GoingSecondWins,
                GoingSecondWinRate = ToPercentage(item.GoingSecondWins, item.GoingSecondGames),
                AverageEndTurn = RoundAverage(item.AverageEndTurn),
                GoingFirstAverageEndTurn = RoundAverage(item.GoingFirstAverageEndTurn),
                GoingSecondAverageEndTurn = RoundAverage(item.GoingSecondAverageEndTurn),
                UpdatedAtUtc = item.UpdatedAtUtc
            }).ToList()
        };

        _logger.LogDebug(
            "Loaded devtools winrate summary. UserId={UserId} Environment={Environment} Rule={Rule} CardName={CardName} Start={StartDate} End={EndDate} Top={Top} CardRows={CardRows} DeckRows={DeckRows} ClassRows={ClassRows}",
            userId,
            targetEnvironment,
            targetDeckRule,
            normalizedCardNameKeyword,
            startDate,
            endDate,
            take,
            response.CardStats.Count,
            response.DeckStats.Count,
            response.ClassStats.Count);

        return Ok(response);
    }

    [HttpGet("deck-composition")]
    [ProducesResponseType(typeof(DeckCompositionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeckCompositionResponse>> GetDeckComposition(
        [FromQuery] Guid userId,
        [FromQuery] string deckShapeHash,
        [FromQuery] string? environment = null,
        [FromQuery] string? deckRule = null,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return BadRequest("userId is required.");
        }

        if (string.IsNullOrWhiteSpace(deckShapeHash))
        {
            return BadRequest("deckShapeHash is required.");
        }

        var userExists = await _db.Users.AnyAsync(user => user.Id == userId, cancellationToken);
        if (!userExists)
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Only registered users can access devtools winrate stats.");
        }

        if (!TryParseEnvironment(environment, out var targetEnvironment))
        {
            return BadRequest("Unsupported environment.");
        }
        if (!TryParseDeckRule(deckRule, out var targetDeckRule))
        {
            return BadRequest("Unsupported deck rule.");
        }

        var normalizedHash = deckShapeHash.Trim().ToUpperInvariant();
        var fact = await _db.GameParticipantStatsFacts
            .AsNoTracking()
            .Where(item =>
                item.IsOfficial &&
                item.Environment == targetEnvironment &&
                item.Rule == targetDeckRule &&
                item.DeckShapeHash == normalizedHash)
            .OrderByDescending(item => item.EndedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (fact == null)
        {
            return NotFound("Deck composition not found.");
        }

        if (!TryDeserializeDeckCounts(fact.InitialDeckCardCountsJson, out var deckCounts) || deckCounts.Count == 0)
        {
            return NotFound("Deck composition payload is empty.");
        }

        var cardIds = deckCounts.Keys.Distinct().ToList();
        var cardDefinitions = await _db.CardDefinitions
            .AsNoTracking()
            .Where(item => cardIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item, cancellationToken);

        var cards = deckCounts
            .Select(pair =>
            {
                cardDefinitions.TryGetValue(pair.Key, out var definition);
                return new DeckCompositionCard
                {
                    CardDefinitionId = pair.Key,
                    Count = pair.Value,
                    Name = definition?.Name ?? $"Card #{pair.Key}",
                    Cost = definition?.Cost ?? 0,
                    Type = definition?.Type.ToString() ?? CardType.Follower.ToString(),
                    Rarity = definition?.Rarity.ToString() ?? CardRarity.Bronze.ToString(),
                    ArtPreview = definition?.ArtPreview
                };
            })
            .OrderBy(item => item.Cost)
            .ThenBy(item => item.Name)
            .ThenBy(item => item.CardDefinitionId)
            .ToList();

        var response = new DeckCompositionResponse
        {
            DeckShapeHash = normalizedHash,
            DeckClass = fact.DeckClass.ToString(),
            Environment = targetEnvironment.ToString(),
            Rule = targetDeckRule.ToString(),
            TotalCount = cards.Sum(item => item.Count),
            Cards = cards
        };

        _logger.LogDebug(
            "Loaded deck composition by hash. UserId={UserId} Environment={Environment} Rule={Rule} DeckShapeHash={DeckShapeHash} CardRows={CardRows}",
            userId,
            targetEnvironment,
            targetDeckRule,
            normalizedHash,
            response.Cards.Count);

        return Ok(response);
    }

    private static bool TryParseEnvironment(string? raw, out DeckEnvironment environment)
    {
        environment = DeckEnvironment.Rotation;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        return Enum.TryParse(raw.Trim(), ignoreCase: true, out environment);
    }

    private static bool TryParseDeckRule(string? raw, out DeckRule rule)
    {
        rule = DeckRule.Standard;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        return Enum.TryParse(raw.Trim(), ignoreCase: true, out rule);
    }

    private static string? NormalizeKeyword(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var normalized = raw.Trim();
        return normalized.Length == 0 ? null : normalized;
    }

    private static bool TryParseDateRange(
        string? rawStartDate,
        string? rawEndDate,
        out DateTime? rangeStartUtc,
        out DateTime? rangeEndUtcInclusive,
        out string? error)
    {
        rangeStartUtc = null;
        rangeEndUtcInclusive = null;
        error = null;

        if (!string.IsNullOrWhiteSpace(rawStartDate))
        {
            if (!TryParseDateOrDateTimeUtc(rawStartDate.Trim(), isEndBoundary: false, out var startUtc))
            {
                error = "Unsupported startDate format. Expected yyyy-MM-dd or ISO-8601 datetime.";
                return false;
            }
            rangeStartUtc = startUtc;
        }

        if (!string.IsNullOrWhiteSpace(rawEndDate))
        {
            if (!TryParseDateOrDateTimeUtc(rawEndDate.Trim(), isEndBoundary: true, out var endUtc))
            {
                error = "Unsupported endDate format. Expected yyyy-MM-dd or ISO-8601 datetime.";
                return false;
            }
            rangeEndUtcInclusive = endUtc;
        }

        if (rangeStartUtc.HasValue && rangeEndUtcInclusive.HasValue && rangeStartUtc.Value > rangeEndUtcInclusive.Value)
        {
            error = "startDate must be earlier than or equal to endDate.";
            return false;
        }

        return true;
    }

    private static bool TryParseDateOrDateTimeUtc(string raw, bool isEndBoundary, out DateTime utcValue)
    {
        utcValue = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();

        if (DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
        {
            if (isEndBoundary)
            {
                var endOfDayUtc = dateOnly
                    .ToDateTime(TimeOnly.MinValue)
                    .AddDays(1)
                    .AddTicks(-1);
                utcValue = DateTime.SpecifyKind(endOfDayUtc, DateTimeKind.Utc);
            }
            else
            {
                utcValue = DateTime.SpecifyKind(dateOnly.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            }

            return true;
        }

        if (DateTimeOffset.TryParse(
            trimmed,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
            out var dateTimeOffset))
        {
            utcValue = dateTimeOffset.UtcDateTime;
            return true;
        }

        return false;
    }

    private static double ToPercentage(int wins, int games)
    {
        if (games <= 0)
        {
            return 0;
        }

        return Math.Round((double)wins / games * 100, 2);
    }

    private static double? RoundAverage(double? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return Math.Round(value.Value, 2);
    }

    private async Task<bool> HasColumnAsync(string tableName, string columnName, CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info('{tableName}')";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var nameOrdinal = reader.GetOrdinal("name");
            while (await reader.ReadAsync(cancellationToken))
            {
                var currentName = reader.IsDBNull(nameOrdinal) ? string.Empty : reader.GetString(nameOrdinal);
                if (string.Equals(currentName, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static bool TryDeserializeDeckCounts(string? rawJson, out Dictionary<int, int> deckCounts)
    {
        deckCounts = new Dictionary<int, int>();
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<int, int>>(rawJson);
            if (parsed != null && parsed.Count > 0)
            {
                deckCounts = parsed.Where(item => item.Value > 0).ToDictionary(item => item.Key, item => item.Value);
                return deckCounts.Count > 0;
            }
        }
        catch
        {
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, int>>(rawJson);
            if (parsed == null || parsed.Count == 0)
            {
                return false;
            }

            foreach (var pair in parsed)
            {
                if (!int.TryParse(pair.Key, out var cardDefinitionId) || pair.Value <= 0)
                {
                    continue;
                }

                deckCounts[cardDefinitionId] = pair.Value;
            }

            return deckCounts.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDeserializeCardIds(string? rawJson, out HashSet<int> cardIds)
    {
        cardIds = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<int[]>(rawJson);
            if (parsed != null)
            {
                foreach (var cardId in parsed)
                {
                    if (cardId > 0)
                    {
                        cardIds.Add(cardId);
                    }
                }

                return cardIds.Count > 0;
            }
        }
        catch
        {
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<string[]>(rawJson);
            if (parsed == null)
            {
                return false;
            }

            foreach (var raw in parsed)
            {
                if (int.TryParse(raw, out var cardId) && cardId > 0)
                {
                    cardIds.Add(cardId);
                }
            }
        }
        catch
        {
        }

        return cardIds.Count > 0;
    }

    private static Dictionary<int, CardAggregateRow> BuildCardAggMap(IReadOnlyList<CardFactRow> facts)
    {
        var result = new Dictionary<int, CardAggregateRow>();
        foreach (var fact in facts)
        {
            if (!TryDeserializeDeckCounts(fact.InitialDeckCardCountsJson, out var deckCounts) || deckCounts.Count == 0)
            {
                continue;
            }

            TryDeserializeCardIds(fact.DrawnCardIdsJson, out var drawnCardIds);
            TryDeserializeCardIds(fact.PlayedCardIdsJson, out var playedCardIds);

            foreach (var cardDefinitionId in deckCounts.Keys)
            {
                if (!result.TryGetValue(cardDefinitionId, out var agg))
                {
                    agg = new CardAggregateRow
                    {
                        CardDefinitionId = cardDefinitionId
                    };
                    result[cardDefinitionId] = agg;
                }

                agg.DeckGames++;
                if (fact.IsWinner)
                {
                    agg.DeckWins++;
                }

                if (drawnCardIds.Contains(cardDefinitionId))
                {
                    agg.DrawnGames++;
                    if (fact.IsWinner)
                    {
                        agg.DrawnWins++;
                    }
                }

                if (playedCardIds.Contains(cardDefinitionId))
                {
                    agg.PlayedGames++;
                    if (fact.IsWinner)
                    {
                        agg.PlayedWins++;
                    }
                }

                if (fact.EndedAtUtc > agg.UpdatedAtUtc)
                {
                    agg.UpdatedAtUtc = fact.EndedAtUtc;
                }
            }
        }

        return result;
    }

    public sealed class WinrateStatsSummaryResponse
    {
        public string Environment { get; set; } = DeckEnvironment.Rotation.ToString();
        public string Rule { get; set; } = DeckRule.Standard.ToString();
        public int Top { get; set; } = DefaultTop;
        public DateTime GeneratedAtUtc { get; set; }
        public List<CardWinrateSummary> CardStats { get; set; } = new();
        public List<DeckWinrateSummary> DeckStats { get; set; } = new();
        public List<ClassWinrateSummary> ClassStats { get; set; } = new();
    }

    public sealed class CardWinrateSummary
    {
        public int CardDefinitionId { get; set; }
        public string CardName { get; set; } = string.Empty;
        public int DeckGames { get; set; }
        public int DeckWins { get; set; }
        public double DeckWinRate { get; set; }
        public int DrawnGames { get; set; }
        public int DrawnWins { get; set; }
        public double DrawnWinRate { get; set; }
        public int PlayedGames { get; set; }
        public int PlayedWins { get; set; }
        public double PlayedWinRate { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    public sealed class DeckWinrateSummary
    {
        public string DeckShapeHash { get; set; } = string.Empty;
        public string DeckClass { get; set; } = CardClass.Neutral.ToString();
        public int TotalGames { get; set; }
        public int TotalWins { get; set; }
        public double WinRate { get; set; }
        public int GoingFirstGames { get; set; }
        public int GoingFirstWins { get; set; }
        public double GoingFirstWinRate { get; set; }
        public int GoingSecondGames { get; set; }
        public int GoingSecondWins { get; set; }
        public double GoingSecondWinRate { get; set; }
        public double? AverageEndTurn { get; set; }
        public double? GoingFirstAverageEndTurn { get; set; }
        public double? GoingSecondAverageEndTurn { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    public sealed class ClassWinrateSummary
    {
        public string DeckClass { get; set; } = CardClass.Neutral.ToString();
        public int TotalGames { get; set; }
        public int TotalWins { get; set; }
        public double WinRate { get; set; }
        public int GoingFirstGames { get; set; }
        public int GoingFirstWins { get; set; }
        public double GoingFirstWinRate { get; set; }
        public int GoingSecondGames { get; set; }
        public int GoingSecondWins { get; set; }
        public double GoingSecondWinRate { get; set; }
        public double? AverageEndTurn { get; set; }
        public double? GoingFirstAverageEndTurn { get; set; }
        public double? GoingSecondAverageEndTurn { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    private sealed class CardFactRow
    {
        public bool IsWinner { get; set; }
        public DateTime EndedAtUtc { get; set; }
        public string InitialDeckCardCountsJson { get; set; } = "{}";
        public string DrawnCardIdsJson { get; set; } = "[]";
        public string PlayedCardIdsJson { get; set; } = "[]";
    }

    private sealed class CardAggregateRow
    {
        public int CardDefinitionId { get; set; }
        public int DeckGames { get; set; }
        public int DeckWins { get; set; }
        public int DrawnGames { get; set; }
        public int DrawnWins { get; set; }
        public int PlayedGames { get; set; }
        public int PlayedWins { get; set; }
        public DateTime UpdatedAtUtc { get; set; } = DateTime.MinValue;
    }

    private sealed class DeckAggregateRow
    {
        public string DeckShapeHash { get; set; } = string.Empty;
        public CardClass DeckClass { get; set; } = CardClass.Neutral;
        public int TotalGames { get; set; }
        public int TotalWins { get; set; }
        public int GoingFirstGames { get; set; }
        public int GoingFirstWins { get; set; }
        public int GoingSecondGames { get; set; }
        public int GoingSecondWins { get; set; }
        public double? AverageEndTurn { get; set; }
        public double? GoingFirstAverageEndTurn { get; set; }
        public double? GoingSecondAverageEndTurn { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    private sealed class ClassAggregateRow
    {
        public CardClass DeckClass { get; set; } = CardClass.Neutral;
        public int TotalGames { get; set; }
        public int TotalWins { get; set; }
        public int GoingFirstGames { get; set; }
        public int GoingFirstWins { get; set; }
        public int GoingSecondGames { get; set; }
        public int GoingSecondWins { get; set; }
        public double? AverageEndTurn { get; set; }
        public double? GoingFirstAverageEndTurn { get; set; }
        public double? GoingSecondAverageEndTurn { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    public sealed class DeckCompositionResponse
    {
        public string DeckShapeHash { get; set; } = string.Empty;
        public string DeckClass { get; set; } = CardClass.Neutral.ToString();
        public string Environment { get; set; } = DeckEnvironment.Rotation.ToString();
        public string Rule { get; set; } = DeckRule.Standard.ToString();
        public int TotalCount { get; set; }
        public List<DeckCompositionCard> Cards { get; set; } = new();
    }

    public sealed class DeckCompositionCard
    {
        public int CardDefinitionId { get; set; }
        public int Count { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Cost { get; set; }
        public string Type { get; set; } = CardType.Follower.ToString();
        public string Rarity { get; set; } = CardRarity.Bronze.ToString();
        public string? ArtPreview { get; set; }
    }
}



