using System.ComponentModel.DataAnnotations;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// 用户卡包实体 - 表示用户购买的卡包实例
/// </summary>
public class UserCardPack
{
    /// <summary>
    /// 卡包实例唯一标识符
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 关联的用户ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 卡包定义ID
    /// </summary>
    public int CardPackDefinitionId { get; set; }

    /// <summary>
    /// 是否已开启
    /// </summary>
    public bool IsOpened { get; set; } = false;

    /// <summary>
    /// 购买时间
    /// </summary>
    public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 开启时间 (可选)
    /// </summary>
    public DateTime? OpenedAt { get; set; }

    // 导航属性
    /// <summary>
    /// 关联的用户
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// 卡包定义
    /// </summary>
    public CardPackDefinition CardPackDefinition { get; set; } = null!;

    /// <summary>
    /// 开包获得的卡片列表
    /// </summary>
    public List<UserCard> Cards { get; set; } = new();

    /// <summary>
    /// 开启卡包
    /// </summary>
    /// <param name="cards">获得的卡片列表</param>
    public void Open(List<UserCard> cards)
    {
        if (IsOpened)
        {
            throw new InvalidOperationException("卡包已经开启过了");
        }

        IsOpened = true;
        OpenedAt = DateTime.UtcNow;
        Cards = cards;

        // 设置卡片的来源卡包
        foreach (var card in cards)
        {
            card.CardPackId = Id;
        }
    }

    /// <summary>
    /// 检查是否可以开启
    /// </summary>
    /// <returns>是否可以开启</returns>
    public bool CanOpen()
    {
        return !IsOpened;
    }

    /// <summary>
    /// 获取卡包状态描述
    /// </summary>
    /// <returns>状态描述</returns>
    public string GetStatusDescription()
    {
        if (IsOpened)
        {
            return $"已开启 ({OpenedAt:yyyy-MM-dd HH:mm})";
        }
        else
        {
            return "未开启";
        }
    }

    /// <summary>
    /// 获取卡包价值描述
    /// </summary>
    /// <returns>价值描述</returns>
    public string GetValueDescription()
    {
        if (CardPackDefinition != null)
        {
            return $"{CardPackDefinition.Name} - {CardPackDefinition.Price}金币";
        }
        return "未知卡包";
    }
}
