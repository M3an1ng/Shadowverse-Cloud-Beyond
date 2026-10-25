using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Infrastructure.Data;

namespace ShadowverseBeyond.Server.Services;

internal static class QuestProgressState
{
    // 两条进度入口共用周期与待保存记录，提交仍由调用方的事务负责。
    internal static async Task<UserQuest> GetOrCreateAsync(
        ApplicationDbContext db,
        Guid userId,
        QuestDefinition definition,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var period = ResolvePeriod(occurredAtUtc, definition.Category, definition.RefreshIntervalHours);
        var quest = db.UserQuests.Local.FirstOrDefault(item => item.UserId == userId
            && item.QuestDefinitionId == definition.Id && item.PeriodKey == period.Key)
            ?? await db.UserQuests.Include(item => item.QuestDefinition).FirstOrDefaultAsync(
                item => item.UserId == userId && item.QuestDefinitionId == definition.Id
                    && item.PeriodKey == period.Key, cancellationToken);
        if (quest != null)
        {
            return quest;
        }

        quest = new UserQuest
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
        db.UserQuests.Add(quest);
        return quest;
    }

    private static QuestPeriod ResolvePeriod(DateTime occurredAtUtc, QuestCategory category, int refreshIntervalHours)
    {
        var utc = DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc);
        return category switch
        {
            QuestCategory.Daily => new QuestPeriod(
                utc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), utc.Date, utc.Date.AddDays(1)),
            QuestCategory.Weekly => ResolveWeeklyPeriod(utc),
            QuestCategory.Achievement => new QuestPeriod(
                "achievement",
                DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),
                DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc)),
            QuestCategory.Special => ResolveFixedIntervalPeriod(refreshIntervalHours, utc),
            _ => throw new InvalidOperationException($"Unsupported quest category: {category}.")
        };
    }

    private static QuestPeriod ResolveWeeklyPeriod(DateTime utc)
    {
        var start = utc.Date.AddDays(-(((int)utc.DayOfWeek + 6) % 7));
        return new QuestPeriod(
            $"{ISOWeek.GetYear(start):0000}-W{ISOWeek.GetWeekOfYear(start):00}", start, start.AddDays(7));
    }

    private static QuestPeriod ResolveFixedIntervalPeriod(int intervalHours, DateTime utc)
    {
        if (intervalHours <= 0)
        {
            throw new InvalidOperationException($"Quest refresh interval must be positive: {intervalHours}.");
        }

        var origin = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var bucket = (long)Math.Floor((utc - origin).TotalHours) / intervalHours;
        var start = origin.AddHours(bucket * intervalHours);
        return new QuestPeriod($"interval-{intervalHours}h-{bucket}", start, start.AddHours(intervalHours));
    }

    private readonly record struct QuestPeriod(string Key, DateTime StartUtc, DateTime EndUtc);
}
