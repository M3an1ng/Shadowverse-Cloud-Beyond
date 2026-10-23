using System.ComponentModel.DataAnnotations;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// 用户任务实体 - 表示用户的任务进度和完成状态（参考炉石传说任务机制）
/// </summary>
public class UserQuest
{
    /// <summary>
    /// 用户任务唯一标识符
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 关联的用户ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 任务定义ID
    /// </summary>
    public int QuestDefinitionId { get; set; }

    /// <summary>
    /// 当前进度
    /// </summary>
    public int CurrentProgress { get; set; } = 0;

    /// <summary>
    /// 是否已完成
    /// </summary>
    public bool IsCompleted { get; set; } = false;

    /// <summary>
    /// 是否已领取奖励
    /// </summary>
    public bool IsRewardClaimed { get; set; } = false;

    /// <summary>
    /// 分配时间
    /// </summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 完成时间 (可选)
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 奖励领取时间 (可选)
    /// </summary>
    public DateTime? RewardClaimedAt { get; set; }

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// 任务周期键，如 2026-07-03、2026-W27 或 achievement
    /// </summary>
    [StringLength(32)]
    public string PeriodKey { get; set; } = "legacy";

    /// <summary>
    /// 任务周期开始时间
    /// </summary>
    public DateTime PeriodStartUtc { get; set; } = DateTime.UtcNow.Date;

    /// <summary>
    /// 任务周期结束时间
    /// </summary>
    public DateTime PeriodEndUtc { get; set; } = DateTime.UtcNow.Date.AddDays(1);

    // 导航属性
    /// <summary>
    /// 关联的用户
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// 任务定义
    /// </summary>
    public QuestDefinition QuestDefinition { get; set; } = null!;

    /// <summary>
    /// 更新进度
    /// </summary>
    /// <param name="increment">增加的进度值</param>
    /// <returns>是否完成了任务</returns>
    public bool UpdateProgress(int increment = 1)
    {
        if (IsCompleted)
        {
            return false; // 已完成的任务不再更新进度
        }

        CurrentProgress += increment;

        // 检查是否达到目标
        if (CurrentProgress >= QuestDefinition.TargetValue)
        {
            Complete();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 设置进度 (用于特殊类型的任务，如排名任务)
    /// </summary>
    /// <param name="progress">新的进度值</param>
    /// <returns>是否完成了任务</returns>
    public bool SetProgress(int progress)
    {
        if (IsCompleted)
        {
            return false; // 已完成的任务不再更新进度
        }

        CurrentProgress = progress;

        // 检查是否达到目标
        if (CurrentProgress >= QuestDefinition.TargetValue)
        {
            Complete();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 完成任务
    /// </summary>
    private void Complete()
    {
        IsCompleted = true;
        CompletedAt = DateTime.UtcNow;
        CurrentProgress = QuestDefinition.TargetValue; // 确保进度值正确
    }

    /// <summary>
    /// 领取奖励
    /// </summary>
    /// <returns>是否成功领取</returns>
    public bool ClaimReward()
    {
        if (!IsCompleted || IsRewardClaimed)
        {
            return false;
        }

        IsRewardClaimed = true;
        RewardClaimedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// 检查是否过期
    /// </summary>
    /// <returns>是否过期</returns>
    public bool IsExpired()
    {
        return DateTime.UtcNow > ExpiresAt;
    }

    /// <summary>
    /// 检查是否接近完成 (进度超过80%)
    /// </summary>
    /// <returns>是否接近完成</returns>
    public bool IsNearCompletion()
    {
        if (IsCompleted)
        {
            return true;
        }

        return GetCompletionPercentage() >= 80;
    }

    /// <summary>
    /// 获取完成百分比
    /// </summary>
    /// <returns>完成百分比 (0-100)</returns>
    public decimal GetCompletionPercentage()
    {
        if (QuestDefinition.TargetValue == 0)
        {
            return IsCompleted ? 100 : 0;
        }

        var percentage = (decimal)CurrentProgress / QuestDefinition.TargetValue * 100;
        return Math.Min(100, Math.Max(0, percentage));
    }



    /// <summary>
    /// 获取剩余进度
    /// </summary>
    /// <returns>剩余进度</returns>
    public int GetRemainingProgress()
    {
        if (IsCompleted)
        {
            return 0;
        }

        return Math.Max(0, QuestDefinition.TargetValue - CurrentProgress);
    }

    /// <summary>
    /// 获取任务状态描述
    /// </summary>
    /// <returns>状态描述</returns>
    public string GetStatusDescription()
    {
        if (IsExpired())
        {
            return "已过期";
        }
        else if (IsCompleted)
        {
            if (IsRewardClaimed)
            {
                return $"已完成并领取奖励 ({CompletedAt:yyyy-MM-dd})";
            }
            else
            {
                return $"已完成，待领取奖励 ({CompletedAt:yyyy-MM-dd})";
            }
        }
        else
        {
            var percentage = GetCompletionPercentage();
            return $"进行中 ({percentage:F1}%)";
        }
    }

    /// <summary>
    /// 重置任务进度 (管理员功能)
    /// </summary>
    public void Reset()
    {
        CurrentProgress = 0;
        IsCompleted = false;
        IsRewardClaimed = false;
        CompletedAt = null;
        RewardClaimedAt = null;
    }
}
