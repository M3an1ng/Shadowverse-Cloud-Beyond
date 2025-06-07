using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Dtos;

/// <summary>
/// 用户任务数据传输对象
/// </summary>
public class UserQuestDto
{
    /// <summary>
    /// 用户任务ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 任务定义ID
    /// </summary>
    public int QuestDefinitionId { get; set; }

    /// <summary>
    /// 任务名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 任务描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 任务类型
    /// </summary>
    public QuestType Type { get; set; }

    /// <summary>
    /// 任务类型显示名称
    /// </summary>
    public string TypeDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 任务分类
    /// </summary>
    public QuestCategory Category { get; set; }

    /// <summary>
    /// 任务分类显示名称
    /// </summary>
    public string CategoryDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 目标值
    /// </summary>
    public int TargetValue { get; set; }

    /// <summary>
    /// 当前进度
    /// </summary>
    public int CurrentProgress { get; set; }

    /// <summary>
    /// 是否已完成
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// 是否已领取奖励
    /// </summary>
    public bool IsRewardClaimed { get; set; }

    /// <summary>
    /// 分配时间
    /// </summary>
    public DateTime AssignedAt { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 奖励领取时间
    /// </summary>
    public DateTime? RewardClaimedAt { get; set; }

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// 金币奖励
    /// </summary>
    public int CoinReward { get; set; }

    /// <summary>
    /// 经验奖励
    /// </summary>
    public int ExperienceReward { get; set; }

    /// <summary>
    /// 完成百分比
    /// </summary>
    public decimal CompletionPercentage { get; set; }

    /// <summary>
    /// 进度描述
    /// </summary>
    public string ProgressDescription { get; set; } = string.Empty;

    /// <summary>
    /// 状态描述
    /// </summary>
    public string StatusDescription { get; set; } = string.Empty;

    /// <summary>
    /// 是否接近完成
    /// </summary>
    public bool IsNearCompletion { get; set; }

    /// <summary>
    /// 剩余进度
    /// </summary>
    public int RemainingProgress { get; set; }

    /// <summary>
    /// 是否过期
    /// </summary>
    public bool IsExpired { get; set; }

    /// <summary>
    /// 从实体转换为DTO
    /// </summary>
    /// <param name="userQuest">用户任务实体</param>
    /// <returns>用户任务DTO</returns>
    public static UserQuestDto FromEntity(UserQuest userQuest)
    {
        return new UserQuestDto
        {
            Id = userQuest.Id,
            UserId = userQuest.UserId,
            QuestDefinitionId = userQuest.QuestDefinitionId,
            Name = userQuest.QuestDefinition?.Name ?? string.Empty,
            Description = userQuest.QuestDefinition?.Description ?? string.Empty,
            Type = userQuest.QuestDefinition?.Type ?? QuestType.PlayGames,
            TypeDisplayName = userQuest.QuestDefinition?.GetTypeDisplayName() ?? string.Empty,
            Category = userQuest.QuestDefinition?.Category ?? QuestCategory.Daily,
            CategoryDisplayName = userQuest.QuestDefinition?.GetCategoryDisplayName() ?? string.Empty,
            TargetValue = userQuest.QuestDefinition?.TargetValue ?? 0,
            CurrentProgress = userQuest.CurrentProgress,
            IsCompleted = userQuest.IsCompleted,
            IsRewardClaimed = userQuest.IsRewardClaimed,
            AssignedAt = userQuest.AssignedAt,
            CompletedAt = userQuest.CompletedAt,
            RewardClaimedAt = userQuest.RewardClaimedAt,
            ExpiresAt = userQuest.ExpiresAt,
            CoinReward = userQuest.QuestDefinition?.CoinReward ?? 0,
            ExperienceReward = userQuest.QuestDefinition?.ExperienceReward ?? 0,
            CompletionPercentage = userQuest.GetCompletionPercentage(),
            ProgressDescription = userQuest.QuestDefinition?.GetProgressDescription(userQuest.CurrentProgress) ?? string.Empty,
            StatusDescription = userQuest.GetStatusDescription(),
            IsNearCompletion = userQuest.IsNearCompletion(),
            RemainingProgress = userQuest.GetRemainingProgress(),
            IsExpired = userQuest.IsExpired()
        };
    }
}
