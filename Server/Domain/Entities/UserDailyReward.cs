using System.ComponentModel.DataAnnotations;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// 用户每日奖励实体 - 专门负责每日奖励和登录奖励系统
/// 遵循单一职责原则，将每日奖励逻辑从UserEconomy中分离
/// 继承BaseUserEntity以减少代码重复
/// </summary>
public class UserDailyReward : BaseUserEntity
{


    /// <summary>
    /// 最后一次领取每日奖励的时间
    /// </summary>
    public DateTime LastClaimedAt { get; set; } = DateTime.MinValue;

    /// <summary>
    /// 连续登录天数
    /// </summary>
    public int ConsecutiveDays { get; set; } = 0;

    /// <summary>
    /// 总登录天数
    /// </summary>
    public int TotalLoginDays { get; set; } = 0;

    /// <summary>
    /// 最长连续登录记录
    /// </summary>
    public int LongestStreak { get; set; } = 0;

    /// <summary>
    /// 本月已领取奖励天数
    /// </summary>
    public int MonthlyClaimedDays { get; set; } = 0;

    /// <summary>
    /// 当前月份 (用于重置月度统计)
    /// </summary>
    public int CurrentMonth { get; set; } = DateTime.UtcNow.Month;



    /// <summary>
    /// 检查是否可以领取每日奖励
    /// </summary>
    /// <returns>是否可以领取</returns>
    public bool CanClaimDailyReward()
    {
        return DateTime.UtcNow.Date > LastClaimedAt.Date;
    }

    /// <summary>
    /// 更新登录连击数
    /// </summary>
    public void UpdateLoginStreak()
    {
        var now = DateTime.UtcNow;
        var daysSinceLastClaim = (now.Date - LastClaimedAt.Date).Days;

        if (daysSinceLastClaim == 1)
        {
            // 连续登录
            ConsecutiveDays++;
        }
        else if (daysSinceLastClaim > 1)
        {
            // 中断了连续登录
            ConsecutiveDays = 1;
        }
        // daysSinceLastClaim == 0 表示今天已经领取过，不更新

        // 更新最长连击记录
        if (ConsecutiveDays > LongestStreak)
        {
            LongestStreak = ConsecutiveDays;
        }

        // 更新总登录天数
        if (daysSinceLastClaim >= 1)
        {
            TotalLoginDays++;
        }

        // 检查月份是否变更，重置月度统计
        if (now.Month != CurrentMonth)
        {
            MonthlyClaimedDays = 0;
            CurrentMonth = now.Month;
        }

        MonthlyClaimedDays++;
        UpdateTimestamp();
    }

    /// <summary>
    /// 计算每日奖励金币数量
    /// </summary>
    /// <returns>奖励金币数量</returns>
    public int CalculateRewardAmount()
    {
        const int baseReward = 50;

        // 连续登录奖励：每连续一天额外获得10金币，最多150额外金币
        var streakBonus = Math.Min(ConsecutiveDays * 10, 150);

        // 月度奖励：本月登录天数越多，额外奖励越多
        var monthlyBonus = CalculateMonthlyBonus();

        return baseReward + streakBonus + monthlyBonus;
    }

    /// <summary>
    /// 计算月度奖励加成
    /// </summary>
    /// <returns>月度奖励金币</returns>
    private int CalculateMonthlyBonus()
    {
        return MonthlyClaimedDays switch
        {
            >= 25 => 100, // 本月登录25天以上，额外100金币
            >= 20 => 75,  // 本月登录20天以上，额外75金币
            >= 15 => 50,  // 本月登录15天以上，额外50金币
            >= 10 => 25,  // 本月登录10天以上，额外25金币
            _ => 0
        };
    }

    /// <summary>
    /// 领取每日奖励
    /// </summary>
    /// <returns>获得的金币数量，如果无法领取则返回0</returns>
    public int ClaimDailyReward()
    {
        if (!CanClaimDailyReward())
        {
            return 0;
        }

        UpdateLoginStreak();
        LastClaimedAt = DateTime.UtcNow;

        return CalculateRewardAmount();
    }

    /// <summary>
    /// 预览下次奖励金币数量
    /// </summary>
    /// <returns>下次奖励金币数量</returns>
    public int PreviewNextReward()
    {
        const int baseReward = 50;
        var nextStreakBonus = Math.Min((ConsecutiveDays + 1) * 10, 150);
        var monthlyBonus = CalculateMonthlyBonus();

        return baseReward + nextStreakBonus + monthlyBonus;
    }

    /// <summary>
    /// 重置连击数 (用于测试或管理员操作)
    /// </summary>
    public void ResetStreak()
    {
        ConsecutiveDays = 0;
        UpdateTimestamp();
    }

    /// <summary>
    /// 获取奖励状态描述
    /// </summary>
    /// <returns>状态描述</returns>
    public string GetRewardStatusDescription()
    {
        if (CanClaimDailyReward())
        {
            var rewardAmount = CalculateRewardAmount();
            return $"可领取每日奖励：{rewardAmount}金币 (连续{ConsecutiveDays}天)";
        }
        else
        {
            var nextReward = PreviewNextReward();
            return $"今日已领取，明日奖励：{nextReward}金币";
        }
    }
}
