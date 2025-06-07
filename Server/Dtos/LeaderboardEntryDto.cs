namespace ShadowverseBeyond.Server.Dtos;

/// <summary>
/// 排行榜条目数据传输对象
/// </summary>
public class LeaderboardEntryDto
{
    /// <summary>
    /// 排行榜位置
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 头像URL
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// 排名等级
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
    /// 总游戏场次
    /// </summary>
    public int TotalGames { get; set; }

    /// <summary>
    /// 胜利场次
    /// </summary>
    public int Wins { get; set; }

    /// <summary>
    /// 胜率
    /// </summary>
    public decimal WinRate { get; set; }

    /// <summary>
    /// 总游戏时长 (小时)
    /// </summary>
    public decimal TotalPlayTimeHours { get; set; }
}
