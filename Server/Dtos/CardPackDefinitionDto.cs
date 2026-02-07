using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ShadowverseBeyond.Server.Dtos;

/// <summary>
/// 卡包定义数据传输对象
/// </summary>
public class CardPackDefinitionDto
{
    /// <summary>
    /// 卡包定义ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 卡包名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 卡包缩写
    /// </summary>
    public string Abbreviation { get; set; } = string.Empty;

    /// <summary>
    /// 卡包区域编号 (0-9)
    /// </summary>
    public int Region { get; set; }

    /// <summary>
    /// 卡包区域内序号 (0-99)
    /// </summary>
    public int Sequence { get; set; }

    /// <summary>
    /// 三位卡包编号
    /// </summary>
    public string PackCode { get; set; } = string.Empty;

    /// <summary>
    /// 关联的卡牌集合ID
    /// </summary>
    [JsonPropertyName("card_set_definition_id")]
    public int CardSetDefinitionId { get; set; }

    /// <summary>
    /// 关联的卡牌集合名称
    /// </summary>
    [JsonPropertyName("card_set_name")]
    public string CardSetName { get; set; } = string.Empty;

    /// <summary>
    /// 关联的卡牌集合缩写
    /// </summary>
    [JsonPropertyName("card_set_abbr")]
    public string CardSetAbbr { get; set; } = string.Empty;

    /// <summary>
    /// 关联的卡牌集合三位码
    /// </summary>
    [JsonPropertyName("card_set_code")]
    public string CardSetCode { get; set; } = string.Empty;

    /// <summary>
    /// 卡牌集合所属环境列表
    /// </summary>
    [JsonPropertyName("card_set_environment")]
    public List<DeckEnvironment> CardSetEnvironments { get; set; } = new();

    /// <summary>
    /// 卡包描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 卡包价格 (金币)
    /// </summary>
    public int Price { get; set; }

    /// <summary>
    /// 每包卡片数量
    /// </summary>
    public int CardCount { get; set; }

    /// <summary>
    /// 是否激活 (可购买)
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// 保底稀有卡数量
    /// </summary>
    public int GuaranteedRareCount { get; set; }

    /// <summary>
    /// 保底史诗卡数量
    /// </summary>
    public int GuaranteedEpicCount { get; set; }

    /// <summary>
    /// 保底传说卡数量
    /// </summary>
    public int GuaranteedLegendaryCount { get; set; }

    /// <summary>
    /// 卡包类型
    /// </summary>
    public CardPackType PackType { get; set; }

    /// <summary>
    /// 稀有度分布描述
    /// </summary>
    public string RarityDistribution { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 从实体转换为DTO
    /// </summary>
    /// <param name="definition">卡包定义实体</param>
    /// <returns>卡包定义DTO</returns>
    public static CardPackDefinitionDto FromEntity(CardPackDefinition definition)
    {
        return new CardPackDefinitionDto
        {
            Id = definition.Id,
            Name = definition.Name,
            Abbreviation = definition.Abbreviation,
            Region = definition.Region,
            Sequence = definition.Sequence,
            PackCode = definition.PackCode,
            CardSetDefinitionId = definition.CardSetDefinitionId,
            CardSetName = definition.CardSetDefinition?.Name ?? string.Empty,
            CardSetAbbr = definition.CardSetDefinition?.Abbreviation ?? string.Empty,
            CardSetCode = definition.CardSetDefinition?.SetCode ?? string.Empty,
            CardSetEnvironments = definition.CardSetDefinition?.Environments ?? new List<DeckEnvironment>(),
            Description = definition.Description,
            Price = definition.Price,
            CardCount = definition.CardCount,
            IsActive = definition.IsActive,
            GuaranteedRareCount = definition.GuaranteedRareCount,
            GuaranteedEpicCount = definition.GuaranteedEpicCount,
            GuaranteedLegendaryCount = definition.GuaranteedLegendaryCount,
            PackType = definition.GetPackType(),
            RarityDistribution = definition.GetRarityDistribution(),
            CreatedAt = definition.CreatedAt
        };
    }
}
