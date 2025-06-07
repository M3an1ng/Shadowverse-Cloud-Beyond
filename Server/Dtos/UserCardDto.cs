using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Dtos;

/// <summary>
/// 用户卡片数据传输对象
/// </summary>
public class UserCardDto
{
    /// <summary>
    /// 用户卡片ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 来源卡包ID
    /// </summary>
    public Guid? CardPackId { get; set; }

    /// <summary>
    /// 卡片定义ID
    /// </summary>
    public int CardDefinitionId { get; set; }

    /// <summary>
    /// 卡片定义信息
    /// </summary>
    public CardDefinitionDto? CardDefinition { get; set; }

    /// <summary>
    /// 拥有数量
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// 获得时间
    /// </summary>
    public DateTime ObtainedAt { get; set; }

    /// <summary>
    /// 来源描述
    /// </summary>
    public string SourceDescription { get; set; } = string.Empty;

    /// <summary>
    /// 是否为重复卡片
    /// </summary>
    public bool IsDuplicate { get; set; }

    /// <summary>
    /// 从实体转换为DTO
    /// </summary>
    /// <param name="userCard">用户卡片实体</param>
    /// <param name="maxCopies">最大副本数</param>
    /// <returns>用户卡片DTO</returns>
    public static UserCardDto FromEntity(UserCard userCard, int maxCopies = 3)
    {
        var dto = new UserCardDto
        {
            Id = userCard.Id,
            UserId = userCard.UserId,
            CardPackId = userCard.CardPackId,
            CardDefinitionId = userCard.CardDefinitionId,
            Count = userCard.Count,
            ObtainedAt = userCard.ObtainedAt,
            SourceDescription = userCard.GetSourceDescription(),
            IsDuplicate = userCard.IsDuplicate(maxCopies)
        };

        // 包含卡片定义信息
        if (userCard.CardDefinition != null)
        {
            // TODO: 修复CardDefinitionDto.FromEntity方法
            // dto.CardDefinition = CardDefinitionDto.FromEntity(userCard.CardDefinition);
        }

        return dto;
    }
}
