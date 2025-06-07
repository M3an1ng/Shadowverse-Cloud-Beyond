using System.ComponentModel.DataAnnotations;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// 用户卡片实体 - 表示用户拥有的卡片
/// </summary>
public class UserCard
{
    /// <summary>
    /// 用户卡片唯一标识符
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 关联的用户ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 来源卡包ID (可选)
    /// </summary>
    public Guid? CardPackId { get; set; }

    /// <summary>
    /// 卡片定义ID (关联到现有的CardDefinition)
    /// </summary>
    public int CardDefinitionId { get; set; }

    /// <summary>
    /// 拥有数量
    /// </summary>
    public int Count { get; set; } = 1;

    /// <summary>
    /// 获得时间
    /// </summary>
    public DateTime ObtainedAt { get; set; } = DateTime.UtcNow;

    // 导航属性
    /// <summary>
    /// 关联的用户
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// 来源卡包 (可选)
    /// </summary>
    public UserCardPack? CardPack { get; set; }

    /// <summary>
    /// 卡片定义 (关联到现有的CardDefinition实体)
    /// 注意：这里需要在ApplicationDbContext中配置外键关系
    /// </summary>
    public CardDefinition? CardDefinition { get; set; }

    /// <summary>
    /// 增加卡片数量
    /// </summary>
    /// <param name="amount">增加的数量</param>
    public void AddCount(int amount = 1)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("增加数量必须大于0", nameof(amount));
        }

        Count += amount;
    }

    /// <summary>
    /// 减少卡片数量
    /// </summary>
    /// <param name="amount">减少的数量</param>
    /// <returns>是否成功减少</returns>
    public bool RemoveCount(int amount = 1)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("减少数量必须大于0", nameof(amount));
        }

        if (Count < amount)
        {
            return false; // 数量不足
        }

        Count -= amount;
        return true;
    }

    /// <summary>
    /// 检查是否有足够的数量
    /// </summary>
    /// <param name="requiredAmount">需要的数量</param>
    /// <returns>是否有足够数量</returns>
    public bool HasSufficientCount(int requiredAmount)
    {
        return Count >= requiredAmount;
    }

    /// <summary>
    /// 获取卡片来源描述
    /// </summary>
    /// <returns>来源描述</returns>
    public string GetSourceDescription()
    {
        if (CardPack != null && CardPack.CardPackDefinition != null)
        {
            return $"来自 {CardPack.CardPackDefinition.Name}";
        }
        return "其他来源";
    }

    /// <summary>
    /// 检查是否为重复卡片
    /// </summary>
    /// <param name="maxCopies">最大副本数</param>
    /// <returns>是否为重复卡片</returns>
    public bool IsDuplicate(int maxCopies = 3)
    {
        return Count > maxCopies;
    }
}
