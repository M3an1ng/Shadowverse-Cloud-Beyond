//
// 相关文档:
// - 实体关系图: 文档/04-数据库设计/01-实体关系图.md
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
//
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// Represents a card definition in the game, mapped to the database.
/// </summary>
public class CardDefinition
{
    /// <summary>
    /// Unique identifier for the card (e.g., "F001"). Primary Key.
    /// </summary>
    [Key]
    public int Id { get; set; } // Changed type to int, removed initializer

    /// <summary>
    /// Display name of the card.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of the card (e.g., "Follower", "Spell", "Amulet").
    /// </summary>
    public CardType Type { get; set; }

    /// <summary>
    /// 卡牌稀有度
    /// </summary>
    public string Rarity { get; set; } = "Common"; // Default to Common

    /// <summary>
    /// 卡牌所属卡包
    /// </summary>
    public string CardSet { get; set; } = "Basic"; // Default to Basic

    /// <summary>
    /// Play point cost of the card.
    /// </summary>
    public int Cost { get; set; }

    /// <summary>
    /// Attack value (nullable for non-followers).
    /// </summary>
    public int? Attack { get; set; }

    /// <summary>
    /// Health value (nullable for non-followers).
    /// </summary>
    public int? Health { get; set; }

    /// <summary>
    /// List of keywords associated with the card (e.g., "Ward", "Storm").
    /// EF Core typically maps List<string> to a suitable format or requires configuration.
    /// Storing as JSON or delimited string might be alternative options if needed.
    /// </summary>
    public List<string>? Keywords { get; set; }

    /// <summary>
    /// Flavor text or description of the card's effects.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    // Removed EffectsJson property - effects should be managed through the ability system

    /// <summary>
    /// Indicates if this card definition represents a base, collectible form (true)
    /// intended for deck building, or a derived form like an evolved state (false).
    /// </summary>
    public bool IsCollectible { get; set; } = true; // Default to true

    // Removed IsEvolved property - evolution is handled through the evolution system

    /// <summary>
    /// The ID of the card this card evolves into. Null if this card cannot evolve or is already evolved.
    /// </summary>
    public int? EvolveId { get; set; } // Changed type to int?

    /// <summary>
    /// List of ability IDs associated with this card.
    /// A card can have multiple abilities.
    /// </summary>
    public List<long> AbilityIds { get; set; } = new List<long>();

    /// <summary>
    /// 
    /// </summary>
    public List<string>? Tribe { get; set; }

    /// <summary>
    /// 卡牌职业
    /// </summary>
    public string Class { get; set; } = "Neutral"; // Default to Neutral

    // Removed Effects property - effects are now managed through the ability system

    /// <summary>
    /// Parameterless constructor required by EF Core.
    /// </summary>
    public CardDefinition() { }

} // Closing brace for the class
