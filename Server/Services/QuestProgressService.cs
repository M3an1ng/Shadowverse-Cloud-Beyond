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

                var userQuest = await QuestProgressState.GetOrCreateAsync(
                    _db, userId, definition, now, cancellationToken);
                var periodKey = userQuest.PeriodKey;
                lastPeriodKey = periodKey;

                var dedupSource = ResolveDedupSource(
                    definition.DeduplicationPolicy,
                    activityType,
                    sourceEntityType,
                    sourceEntityId,
                    periodKey);

                if (dedupSource != null)
                {
                    var exists = _db.QuestProgressEvents.Local.Any(item => item.UserId == userId
                            && item.QuestDefinitionId == definition.Id
                            && item.PeriodKey == periodKey
                            && item.SourceEntityType == dedupSource.Value.SourceEntityType
                            && item.SourceEntityId == dedupSource.Value.SourceEntityId)
                        || await _db.QuestProgressEvents.AnyAsync(
                        item => item.UserId == userId
                            && item.QuestDefinitionId == definition.Id
                            && item.PeriodKey == periodKey
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
                        PeriodKey = periodKey,
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
            QuestDeduplicationPolicy.PerSourceEntityPerPeriod => new QuestDedupSource(
                NormalizeSourceValue(sourceEntityType, activityType.ToString()),
                NormalizeSourceValue(sourceEntityId, periodKey)),
            _ => throw new InvalidOperationException($"Unsupported quest deduplication policy: {policy}.")
        };
    }

    private static string NormalizeSourceValue(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private readonly record struct QuestDedupSource(string SourceEntityType, string SourceEntityId);
}
