using System.ComponentModel.DataAnnotations;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// 金币交易记录实体 - 记录所有金币的收入和支出
/// </summary>
public class CoinTransaction
{
    /// <summary>
    /// 交易记录唯一标识符
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 关联的用户ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 金币数量 (正数=收入，负数=支出)
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    /// 交易类型
    /// </summary>
    public CoinTransactionType Type { get; set; }

    /// <summary>
    /// 交易描述 (可选)
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// 关联的实体ID (如游戏ID、卡包ID、成就ID等，可选)
    /// </summary>
    [StringLength(100)]
    public string? RelatedEntityId { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 导航属性
    /// <summary>
    /// 关联的用户
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// 获取交易类型的显示名称
    /// </summary>
    /// <returns>交易类型显示名称</returns>
    public string GetTypeDisplayName()
    {
        return Type switch
        {
            CoinTransactionType.GameWin => "游戏胜利",
            CoinTransactionType.GameLoss => "游戏参与",
            CoinTransactionType.QuestCompletion => "任务奖励",
            CoinTransactionType.DailyLoginBonus => "每日登录奖励",
            CoinTransactionType.CardPackPurchase => "购买卡包",
            CoinTransactionType.SeasonReward => "赛季奖励",
            CoinTransactionType.AdminGrant => "管理员发放",
            CoinTransactionType.Other => "其他",
            _ => "未知"
        };
    }

    /// <summary>
    /// 检查是否为收入交易
    /// </summary>
    /// <returns>是否为收入</returns>
    public bool IsIncome()
    {
        return Amount > 0;
    }

    /// <summary>
    /// 检查是否为支出交易
    /// </summary>
    /// <returns>是否为支出</returns>
    public bool IsExpense()
    {
        return Amount < 0;
    }
}

/// <summary>
/// 金币交易类型枚举
/// </summary>
public enum CoinTransactionType
{
    /// <summary>
    /// 游戏胜利奖励
    /// </summary>
    GameWin = 1,

    /// <summary>
    /// 游戏失败参与奖励
    /// </summary>
    GameLoss = 2,

    /// <summary>
    /// 任务完成奖励 (统一所有任务类型)
    /// </summary>
    QuestCompletion = 3,

    /// <summary>
    /// 每日登录奖励
    /// </summary>
    DailyLoginBonus = 4,

    /// <summary>
    /// 购买卡包
    /// </summary>
    CardPackPurchase = 5,

    /// <summary>
    /// 赛季奖励
    /// </summary>
    SeasonReward = 6,

    /// <summary>
    /// 管理员发放
    /// </summary>
    AdminGrant = 7,

    /// <summary>
    /// 其他
    /// </summary>
    Other = 99
}
