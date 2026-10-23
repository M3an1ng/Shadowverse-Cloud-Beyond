using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Services.Abstractions;

public interface IQuestProgressService
{
    Task<QuestProgressResult> RecordActivityAsync(
        Guid userId,
        QuestActivityType activityType,
        string sourceEntityType,
        string sourceEntityId,
        int increment = 1,
        DateTime? occurredAtUtc = null,
        CancellationToken cancellationToken = default);
}

public sealed class QuestProgressResult
{
    public Guid UserId { get; init; }
    public QuestActivityType ActivityType { get; init; }
    public string PeriodKey { get; init; } = string.Empty;
    public List<Guid> UpdatedQuestIds { get; init; } = new();
    public List<Guid> CompletedQuestIds { get; init; } = new();
    public bool IgnoredAsDuplicate { get; init; }
}
