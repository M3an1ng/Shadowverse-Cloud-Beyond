using System.ComponentModel.DataAnnotations;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// 用户排名实体 - 专门负责用户的排名系统管理
/// 遵循单一职责原则，将排名逻辑从UserEconomy中分离
/// 继承BaseUserEntity以减少代码重复
/// </summary>
public class UserRanking : BaseUserEntity
{


    /// <summary>
    /// 天梯排名等级 (0=青铜, 1=白银, 2=黄金, 3=白金, 4=钻石, 5=大师)
    /// </summary>
    public int Rank { get; set; } = 0;

    /// <summary>
    /// 当前排名积分
    /// </summary>
    public int RankPoints { get; set; } = 0;

    /// <summary>
    /// 当前赛季开始时间
    /// </summary>
    public DateTime SeasonStartAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 赛季最高排名
    /// </summary>
    public int SeasonHighestRank { get; set; } = 0;

    /// <summary>
    /// 总胜场数
    /// </summary>
    public int TotalWins { get; set; } = 0;

    /// <summary>
    /// 总败场数
    /// </summary>
    public int TotalLosses { get; set; } = 0;



    /// <summary>
    /// 处理游戏结果并更新排名
    /// </summary>
    /// <param name="isVictory">是否胜利</param>
    public void ProcessGameResult(bool isVictory)
    {
        if (isVictory)
        {
            TotalWins++;
            RankPoints += CalculateWinPoints();
        }
        else
        {
            TotalLosses++;
            RankPoints = Math.Max(0, RankPoints - CalculateLossPoints());
        }

        CheckRankPromotion();
        UpdateTimestamp();
    }

    /// <summary>
    /// 计算胜利获得的积分
    /// </summary>
    /// <returns>积分数量</returns>
    private int CalculateWinPoints()
    {
        // 基础积分：25分
        // 连胜加成：可以根据连胜次数增加额外积分
        return 25;
    }

    /// <summary>
    /// 计算失败扣除的积分
    /// </summary>
    /// <returns>扣除的积分数量</returns>
    private int CalculateLossPoints()
    {
        // 基础扣分：15分
        // 保护机制：青铜等级不扣分
        return Rank == 0 ? 0 : 15;
    }

    /// <summary>
    /// 检查排名升降级
    /// </summary>
    private void CheckRankPromotion()
    {
        // 升级条件：每个等级需要100积分
        while (RankPoints >= 100 && Rank < 5)
        {
            RankPoints -= 100;
            Rank++;

            // 更新赛季最高排名
            if (Rank > SeasonHighestRank)
            {
                SeasonHighestRank = Rank;
            }
        }

        // 降级条件：积分为0且不是青铜等级
        if (RankPoints == 0 && Rank > 0)
        {
            Rank--;
            RankPoints = 50; // 降级后给予50积分缓冲
        }
    }

    /// <summary>
    /// 获取排名名称
    /// </summary>
    /// <returns>排名名称</returns>
    public string GetRankName()
    {
        return Rank switch
        {
            0 => "青铜",
            1 => "白银",
            2 => "黄金",
            3 => "白金",
            4 => "钻石",
            5 => "大师",
            _ => "未知"
        };
    }

    /// <summary>
    /// 计算胜率
    /// </summary>
    /// <returns>胜率百分比</returns>
    public decimal CalculateWinRate()
    {
        var totalGames = TotalWins + TotalLosses;
        if (totalGames == 0)
        {
            return 0;
        }

        return Math.Round((decimal)TotalWins / totalGames * 100, 2);
    }

    /// <summary>
    /// 重置赛季数据
    /// </summary>
    public void ResetSeason()
    {
        SeasonHighestRank = Rank;
        Rank = Math.Max(0, Rank - 2); // 赛季重置时降低2个等级
        RankPoints = 0;
        SeasonStartAt = DateTime.UtcNow;
        UpdateTimestamp();
    }
}
