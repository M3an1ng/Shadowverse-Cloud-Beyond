using System.ComponentModel.DataAnnotations;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// 卡包定义实体 - 定义不同类型的卡包及其属性
/// </summary>
public class CardPackDefinition
{
    /// <summary>
    /// 卡包定义唯一标识符
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 卡包名称
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 卡包描述
    /// </summary>
    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 卡包价格 (金币)
    /// </summary>
    public int Price { get; set; }

    /// <summary>
    /// 每包卡片数量
    /// </summary>
    public int CardCount { get; set; } = 5;

    /// <summary>
    /// 是否激活 (可购买)
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 保底稀有卡数量
    /// </summary>
    public int GuaranteedRareCount { get; set; } = 0;

    /// <summary>
    /// 保底史诗卡数量
    /// </summary>
    public int GuaranteedEpicCount { get; set; } = 0;

    /// <summary>
    /// 保底传说卡数量
    /// </summary>
    public int GuaranteedLegendaryCount { get; set; } = 0;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 导航属性
    /// <summary>
    /// 用户购买的此类型卡包列表
    /// </summary>
    public List<UserCardPack> UserCardPacks { get; set; } = new();

    /// <summary>
    /// 获取卡包类型
    /// </summary>
    /// <returns>卡包类型</returns>
    public CardPackType GetPackType()
    {
        // 根据价格和保底机制判断卡包类型
        if (GuaranteedLegendaryCount > 0)
            return CardPackType.Legendary;
        else if (GuaranteedEpicCount > 0 || GuaranteedRareCount > 0)
            return CardPackType.Premium;
        else
            return CardPackType.Basic;
    }

    /// <summary>
    /// 获取卡包稀有度分布
    /// </summary>
    /// <returns>稀有度分布描述</returns>
    public string GetRarityDistribution()
    {
        var parts = new List<string>();

        if (GuaranteedLegendaryCount > 0)
            parts.Add($"{GuaranteedLegendaryCount}张传说");
        if (GuaranteedEpicCount > 0)
            parts.Add($"{GuaranteedEpicCount}张史诗");
        if (GuaranteedRareCount > 0)
            parts.Add($"{GuaranteedRareCount}张稀有");

        var remainingCards = CardCount - GuaranteedLegendaryCount - GuaranteedEpicCount - GuaranteedRareCount;
        if (remainingCards > 0)
            parts.Add($"{remainingCards}张随机");

        return parts.Count > 0 ? string.Join(", ", parts) : $"{CardCount}张随机";
    }
}

/// <summary>
/// 卡包类型枚举
/// </summary>
public enum CardPackType
{
    /// <summary>
    /// 基础卡包
    /// </summary>
    Basic = 1,

    /// <summary>
    /// 高级卡包
    /// </summary>
    Premium = 2,

    /// <summary>
    /// 传说卡包
    /// </summary>
    Legendary = 3
}
