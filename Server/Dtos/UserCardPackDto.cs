using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Dtos;

/// <summary>
/// 用户卡包数据传输对象
/// </summary>
public class UserCardPackDto
{
    /// <summary>
    /// 卡包实例ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 卡包定义ID
    /// </summary>
    public int CardPackDefinitionId { get; set; }

    /// <summary>
    /// 卡包定义信息
    /// </summary>
    public CardPackDefinitionDto? CardPackDefinition { get; set; }

    /// <summary>
    /// 是否已开启
    /// </summary>
    public bool IsOpened { get; set; }

    /// <summary>
    /// 购买时间
    /// </summary>
    public DateTime PurchasedAt { get; set; }

    /// <summary>
    /// 开启时间
    /// </summary>
    public DateTime? OpenedAt { get; set; }

    /// <summary>
    /// 状态描述
    /// </summary>
    public string StatusDescription { get; set; } = string.Empty;

    /// <summary>
    /// 价值描述
    /// </summary>
    public string ValueDescription { get; set; } = string.Empty;

    /// <summary>
    /// 开包获得的卡片列表 (仅在已开启时有值)
    /// </summary>
    public List<UserCardDto>? Cards { get; set; }

    /// <summary>
    /// 从实体转换为DTO
    /// </summary>
    /// <param name="userCardPack">用户卡包实体</param>
    /// <param name="includeCards">是否包含卡片信息</param>
    /// <returns>用户卡包DTO</returns>
    public static UserCardPackDto FromEntity(UserCardPack userCardPack, bool includeCards = false)
    {
        var dto = new UserCardPackDto
        {
            Id = userCardPack.Id,
            UserId = userCardPack.UserId,
            CardPackDefinitionId = userCardPack.CardPackDefinitionId,
            IsOpened = userCardPack.IsOpened,
            PurchasedAt = userCardPack.PurchasedAt,
            OpenedAt = userCardPack.OpenedAt,
            StatusDescription = userCardPack.GetStatusDescription(),
            ValueDescription = userCardPack.GetValueDescription()
        };

        // 包含卡包定义信息
        if (userCardPack.CardPackDefinition != null)
        {
            dto.CardPackDefinition = CardPackDefinitionDto.FromEntity(userCardPack.CardPackDefinition);
        }

        // 包含卡片信息 (仅在已开启且请求包含时)
        if (includeCards && userCardPack.IsOpened && userCardPack.Cards?.Any() == true)
        {
            dto.Cards = userCardPack.Cards.Select(UserCardDto.FromEntity).ToList();
        }

        return dto;
    }
}
