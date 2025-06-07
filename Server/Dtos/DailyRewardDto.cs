namespace ShadowverseBeyond.Server.Dtos;

/// <summary>
/// 每日奖励数据传输对象
/// </summary>
public class DailyRewardDto
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 奖励金币数量
    /// </summary>
    public int CoinReward { get; set; }

    /// <summary>
    /// 连续登录天数
    /// </summary>
    public int LoginStreak { get; set; }

    /// <summary>
    /// 是否为连续登录奖励
    /// </summary>
    public bool IsStreakReward { get; set; }

    /// <summary>
    /// 奖励描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 领取时间
    /// </summary>
    public DateTime ClaimedAt { get; set; }

    /// <summary>
    /// 下次可领取时间
    /// </summary>
    public DateTime NextClaimTime { get; set; }

    /// <summary>
    /// 创建每日奖励DTO
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="coinReward">金币奖励</param>
    /// <param name="loginStreak">连续登录天数</param>
    /// <returns>每日奖励DTO</returns>
    public static DailyRewardDto Create(Guid userId, int coinReward, int loginStreak)
    {
        var now = DateTime.UtcNow;
        var isStreakReward = loginStreak >= 7;
        
        return new DailyRewardDto
        {
            UserId = userId,
            CoinReward = coinReward,
            LoginStreak = loginStreak,
            IsStreakReward = isStreakReward,
            Description = GetRewardDescription(coinReward, loginStreak, isStreakReward),
            ClaimedAt = now,
            NextClaimTime = now.Date.AddDays(1)
        };
    }

    /// <summary>
    /// 获取奖励描述
    /// </summary>
    /// <param name="coinReward">金币奖励</param>
    /// <param name="loginStreak">连续登录天数</param>
    /// <param name="isStreakReward">是否为连续登录奖励</param>
    /// <returns>奖励描述</returns>
    private static string GetRewardDescription(int coinReward, int loginStreak, bool isStreakReward)
    {
        if (isStreakReward)
        {
            return $"连续登录{loginStreak}天奖励！获得{coinReward}金币";
        }
        else
        {
            return $"每日登录奖励！获得{coinReward}金币 (连续{loginStreak}天)";
        }
    }
}
