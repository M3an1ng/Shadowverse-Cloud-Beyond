//
// 相关文档:
// - 实体关系图: 文档/04-数据库设计/01-实体关系图.md
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
//
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Metadata;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// Represents a card definition in the game, mapped to the database.
/// </summary>
[DslExposable("原始卡牌", Description = "卡牌的原型，包含卡牌在无附加效果情况下的基础属性和定义")]
public class CardDefinition
{
    /// <summary>
    /// Unique identifier for the card (e.g., "F001"). Primary Key.
    /// </summary>
    [Key]
    [DslExposable("卡牌数据ID", Description = "卡牌数据的唯一标识符")]
    public int Id { get; set; } // Changed type to int, removed initializer

    /// <summary>
    /// Display name of the card.
    /// </summary>
    [DslExposable("名称", Description = "卡牌名称")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of the card (e.g., "Follower", "Spell", "Amulet").
    /// </summary>
    [DslExposable("类型", Description = "卡牌类型")]
    public CardType Type { get; set; }

    /// <summary>
    /// 卡牌稀有度
    /// </summary>
    [DslExposable("稀有度", Description = "卡牌稀有度")]
    public CardRarity Rarity { get; set; } = CardRarity.Bronze; // Default to Bronze

    /// <summary>
    /// 卡牌所属卡包
    /// </summary>
    [DslExposable("卡包", Description = "卡牌所属卡包")]
    public string CardSet { get; set; } = "特殊"; // Default to Legacy

    /// <summary>
    /// 关联的卡牌集合定义ID
    /// </summary>
    public int? CardSetDefinitionId { get; set; }


    /// <summary>
    /// Play point cost of the card.
    /// </summary>
    [DslExposable("原始费用", Description = "卡牌的基础费用")]
    public int Cost { get; set; }

    /// <summary>
    /// Attack value (nullable for non-followers).
    /// </summary>
    [DslExposable("原始攻击力", Description = "卡牌的基础攻击力")]
    public int? Attack { get; set; }

    /// <summary>
    /// Health value (nullable for non-followers).
    /// </summary>
    [DslExposable("原始生命值", Description = "卡牌的基础生命值")]
    public int? Health { get; set; }

    /// <summary>
    /// Countdown value (nullable for non-countdown cards).
    /// </summary>
    [DslExposable("原始倒数", Description = "卡牌的基础倒数")]
    public int? Countdown { get; set; }

    /// <summary>
    /// List of keywords associated with the card (e.g., "Ward", "Storm").
    /// EF Core typically maps List<string> to a suitable format or requires configuration.
    /// Storing as JSON or delimited string might be alternative options if needed.
    /// </summary>
    [DslExposable("原始关键字", Description = "卡牌的基础关键字")]
    public List<string>? Keywords { get; set; }

    /// <summary>
    /// Flavor text or description of the card's effects.
    /// </summary>
    [DslExposable("卡牌描述", Description = "卡牌的描述")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Optional preview art image path.
    /// </summary>
    public string? ArtPreview { get; set; }

    /// <summary>
    /// Optional detail art image path.
    /// </summary>
    public string? ArtDetail { get; set; }

    // Removed EffectsJson property - effects should be managed through the ability system

    /// <summary>
    /// Indicates if this card definition represents a base, collectible form (true)
    /// intended for deck building, or a derived form like an evolved state (false).
    /// </summary>
    [DslExposable("是否可收集", Description = "不可收集代表该卡牌为衍生物")]
    public bool IsCollectible { get; set; } = true; // Default to true

    // Removed IsEvolved property - evolution is handled through the evolution system

    /// <summary>
    /// List of ability IDs associated with this card.
    /// A card can have multiple abilities.
    /// </summary>
    public List<long> AbilityIds { get; set; } = new List<long>();

    /// <summary>
    /// 
    /// </summary>
    [DslExposable("原始卡牌种类", Description = "卡牌所属的种类")]
    public List<string>? Tribe { get; set; }

    /// <summary>
    /// 卡牌职业
    /// </summary>
    [DslExposable("职业", Description = "卡牌所属职业")]
    public CardClass Class { get; set; } = CardClass.Neutral; // Default to Neutral

    /// <summary>
    /// 关联的卡牌集合定义
    /// </summary>
    public CardSetDefinition? CardSetDefinition { get; set; }

    // Removed Effects property - effects are now managed through the ability system

    /// <summary>
    /// Parameterless constructor required by EF Core.
    /// </summary>
    public CardDefinition() { }

} // Closing brace for the class
