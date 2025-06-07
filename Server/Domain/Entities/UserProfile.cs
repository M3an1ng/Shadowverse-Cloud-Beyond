using System.ComponentModel.DataAnnotations;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// 用户资料实体 - 存储用户的游戏统计和个人信息
/// </summary>
public class UserProfile
{
    /// <summary>
    /// 资料唯一标识符
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 关联的用户ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 总游戏场次
    /// </summary>
    public int TotalGames { get; set; } = 0;

    /// <summary>
    /// 胜利场次
    /// </summary>
    public int Wins { get; set; } = 0;

    /// <summary>
    /// 失败场次
    /// </summary>
    public int Losses { get; set; } = 0;

    /// <summary>
    /// 胜率 (百分比，保留2位小数)
    /// </summary>
    public decimal WinRate { get; set; } = 0;

    /// <summary>
    /// 总游戏时长 (以秒为单位)
    /// </summary>
    public long TotalPlayTime { get; set; } = 0;

    /// <summary>
    /// 最喜欢的卡牌ID (可选)
    /// </summary>
    [StringLength(50)]
    public string? FavoriteCardId { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // 导航属性
    /// <summary>
    /// 关联的用户
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// 计算胜率
    /// </summary>
    public void CalculateWinRate()
    {
        if (TotalGames > 0)
        {
            WinRate = Math.Round((decimal)Wins / TotalGames * 100, 2);
        }
        else
        {
            WinRate = 0;
        }
    }

    /// <summary>
    /// 更新游戏统计
    /// </summary>
    /// <param name="isWin">是否胜利</param>
    /// <param name="gameDuration">游戏时长（秒）</param>
    public void UpdateGameStats(bool isWin, long gameDuration)
    {
        TotalGames++;
        TotalPlayTime += gameDuration;
        
        if (isWin)
        {
            Wins++;
        }
        else
        {
            Losses++;
        }

        CalculateWinRate();
        UpdatedAt = DateTime.UtcNow;
    }
}
