using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Dtos;

/// <summary>
/// 用户排名数据传输对象
/// </summary>
public class UserRankDto
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 排名等级 (0=青铜, 1=白银, 2=黄金, 3=白金, 4=钻石, 5=大师)
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// 排名名称
    /// </summary>
    public string RankName { get; set; } = string.Empty;

    /// <summary>
    /// 排名积分
    /// </summary>
    public int RankPoints { get; set; }

    /// <summary>
    /// 当前金币数量
    /// </summary>
    public int Coins { get; set; }

    /// <summary>
    /// 总胜场数
    /// </summary>
    public int TotalWins { get; set; }

    /// <summary>
    /// 连续登录天数
    /// </summary>
    public int DailyLoginStreak { get; set; }

    /// <summary>
    /// 最后一次领取每日奖励的时间
    /// </summary>
    public DateTime LastDailyReward { get; set; }

    /// <summary>
    /// 是否可以领取每日奖励
    /// </summary>
    public bool CanClaimDailyReward { get; set; }

    /// <summary>
    /// 升级到下一等级所需积分
    /// </summary>
    public int PointsToNextRank { get; set; }

    /// <summary>
    /// 从实体转换为DTO
    /// </summary>
    /// <param name="userEconomy">用户经济实体</param>
    /// <param name="ranking">用户排名实体（可选）</param>
    /// <param name="dailyReward">用户每日奖励实体（可选）</param>
    /// <returns>用户排名DTO</returns>
    public static UserRankDto FromEntities(UserEconomy userEconomy, UserRanking? ranking, UserDailyReward? dailyReward)
    {
        var pointsToNext = (ranking?.Rank ?? 0) >= 5 ? 0 : (100 - (ranking?.RankPoints ?? 0));

        return new UserRankDto
        {
            UserId = userEconomy.UserId,
            Rank = ranking?.Rank ?? 0,
            RankName = ranking?.GetRankName() ?? "青铜",
            RankPoints = ranking?.RankPoints ?? 0,
            Coins = userEconomy.Coins,
            TotalWins = ranking?.TotalWins ?? 0,
            DailyLoginStreak = dailyReward?.ConsecutiveDays ?? 0,
            LastDailyReward = dailyReward?.LastClaimedAt ?? DateTime.MinValue,
            CanClaimDailyReward = dailyReward?.CanClaimDailyReward() ?? true,
            PointsToNextRank = pointsToNext
        };
    }
}
