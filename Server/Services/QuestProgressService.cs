using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

public sealed class QuestProgressService : IQuestProgressService
{
    private readonly ApplicationDbContext _db;
    private readonly ITransactionService _transactionService;
    private readonly ILogger<QuestProgressService> _logger;

    public QuestProgressService(
        ApplicationDbContext db,
        ITransactionService transactionService,
        ILogger<QuestProgressService> logger)
    {
        _db = db;
        _transactionService = transactionService;
        _logger = logger;
    }

    public async Task<QuestProgressResult> RecordActivityAsync(
        Guid userId,
        QuestActivityType activityType,
        string sourceEntityType,
        string sourceEntityId,
        int increment = 1,
        DateTime? occurredAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || increment <= 0)
        {
            return new QuestProgressResult { UserId = userId, ActivityType = activityType };
        }

        var userExists = await _db.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId, cancellationToken);
        if (!userExists)
        {
            _logger.LogDebug("[QuestProgress] Skipping activity for missing user. UserId={UserId}", userId);
            return new QuestProgressResult { UserId = userId, ActivityType = activityType };
        }

        var now = DateTime.SpecifyKind(occurredAtUtc ?? DateTime.UtcNow, DateTimeKind.Utc);
        var definitions = await _db.QuestDefinitions
            .Where(definition => definition.IsActive && definition.ActivityType == activityType)
            .OrderBy(definition => definition.Id)
            .ToListAsync(cancellationToken);

        if (definitions.Count == 0)
        {
            return new QuestProgressResult { UserId = userId, ActivityType = activityType };
        }

        return await _transactionService.ExecuteInTransactionAsync(async () =>
        {
            var updatedQuestIds = new List<Guid>();
            var completedQuestIds = new List<Guid>();
            var ignoredAsDuplicate = false;
            var lastPeriodKey = string.Empty;

            foreach (var definition in definitions)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var period = ResolvePeriod(definition, now);
                lastPeriodKey = period.Key;
                var userQuest = await GetOrCreateUserQuestAsync(
                    userId,
                    definition,
                    period,
                    cancellationToken);

                var dedupSource = ResolveDedupSource(
                    definition.DeduplicationPolicy,
                    activityType,
                    sourceEntityType,
                    sourceEntityId,
                    period.Key);

                if (dedupSource != null)
                {
                    var exists = await _db.QuestProgressEvents.AnyAsync(
                        item => item.UserId == userId
                            && item.QuestDefinitionId == definition.Id
                            && item.PeriodKey == period.Key
                            && item.SourceEntityType == dedupSource.Value.SourceEntityType
                            && item.SourceEntityId == dedupSource.Value.SourceEntityId,
                        cancellationToken);
                    if (exists)
                    {
                        ignoredAsDuplicate = true;
                        continue;
                    }
                }

                if (userQuest.IsCompleted)
                {
                    continue;
                }

                if (dedupSource != null)
                {
                    _db.QuestProgressEvents.Add(new QuestProgressEvent
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        QuestDefinitionId = definition.Id,
                        ActivityType = activityType,
                        PeriodKey = period.Key,
                        SourceEntityType = dedupSource.Value.SourceEntityType,
                        SourceEntityId = dedupSource.Value.SourceEntityId,
                        Increment = increment,
                        CreatedAtUtc = now
                    });
                }

                var completed = userQuest.UpdateProgress(increment);
                updatedQuestIds.Add(userQuest.Id);
                if (completed)
                {
                    completedQuestIds.Add(userQuest.Id);
                }
            }

            return new QuestProgressResult
            {
                UserId = userId,
                ActivityType = activityType,
                PeriodKey = lastPeriodKey,
                UpdatedQuestIds = updatedQuestIds,
                CompletedQuestIds = completedQuestIds,
                IgnoredAsDuplicate = ignoredAsDuplicate
            };
        });
    }

    private async Task<UserQuest> GetOrCreateUserQuestAsync(
        Guid userId,
        QuestDefinition definition,
        QuestPeriod period,
        CancellationToken cancellationToken)
    {
        var userQuest = await _db.UserQuests
            .Include(item => item.QuestDefinition)
            .FirstOrDefaultAsync(
                item => item.UserId == userId
                    && item.QuestDefinitionId == definition.Id
                    && item.PeriodKey == period.Key,
                cancellationToken);

        if (userQuest != null)
        {
            return userQuest;
        }

        userQuest = new UserQuest
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            QuestDefinitionId = definition.Id,
            QuestDefinition = definition,
            AssignedAt = DateTime.UtcNow,
            ExpiresAt = period.EndUtc,
            PeriodKey = period.Key,
            PeriodStartUtc = period.StartUtc,
            PeriodEndUtc = period.EndUtc
        };

        _db.UserQuests.Add(userQuest);
        return userQuest;
    }

    private static QuestPeriod ResolvePeriod(QuestDefinition definition, DateTime occurredAtUtc)
    {
        return ResolvePeriodForDefinition(occurredAtUtc, definition.Category, definition.RefreshIntervalHours);
    }

    internal static QuestPeriod ResolvePeriodForDefinition(
        DateTime occurredAtUtc,
        QuestCategory category,
        int refreshIntervalHours)
    {
        var utc = occurredAtUtc.Kind == DateTimeKind.Utc
            ? occurredAtUtc
            : DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc);

        return category switch
        {
            QuestCategory.Daily => ResolveDailyPeriod(utc),
            QuestCategory.Weekly => ResolveWeeklyPeriod(utc),
            QuestCategory.Achievement => new QuestPeriod(
                "achievement",
                DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),
                DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc)),
            _ => ResolveFixedIntervalPeriod(refreshIntervalHours, utc)
        };
    }

    private static QuestPeriod ResolveDailyPeriod(DateTime utc)
    {
        var start = utc.Date;
        return new QuestPeriod(
            start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            start,
            start.AddDays(1));
    }

    private static QuestPeriod ResolveWeeklyPeriod(DateTime utc)
    {
        var start = utc.Date.AddDays(-(((int)utc.DayOfWeek + 6) % 7));
        var week = ISOWeek.GetWeekOfYear(start);
        return new QuestPeriod(
            $"{ISOWeek.GetYear(start):0000}-W{week:00}",
            start,
            start.AddDays(7));
    }

    private static QuestPeriod ResolveFixedIntervalPeriod(int refreshIntervalHours, DateTime utc)
    {
        var intervalHours = Math.Max(1, refreshIntervalHours);
        var origin = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var elapsedHours = (long)Math.Floor((utc - origin).TotalHours);
        var bucket = elapsedHours / intervalHours;
        var start = origin.AddHours(bucket * intervalHours);
        return new QuestPeriod($"interval-{intervalHours}h-{bucket}", start, start.AddHours(intervalHours));
    }

    private static QuestDedupSource? ResolveDedupSource(
        QuestDeduplicationPolicy policy,
        QuestActivityType activityType,
        string sourceEntityType,
        string sourceEntityId,
        string periodKey)
    {
        return policy switch
        {
            QuestDeduplicationPolicy.None => null,
            QuestDeduplicationPolicy.OncePerPeriod => new QuestDedupSource(
                activityType.ToString(),
                periodKey),
            _ => new QuestDedupSource(
                NormalizeSourceValue(sourceEntityType, activityType.ToString()),
                NormalizeSourceValue(sourceEntityId, periodKey))
        };
    }

    private static string NormalizeSourceValue(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    internal readonly record struct QuestPeriod(string Key, DateTime StartUtc, DateTime EndUtc);

    private readonly record struct QuestDedupSource(string SourceEntityType, string SourceEntityId);
}
