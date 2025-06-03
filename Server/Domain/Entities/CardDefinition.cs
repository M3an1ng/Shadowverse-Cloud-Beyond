using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using System.Linq; // Required for .Any()
using System.Text.Json; // Keep for CardEffect record
using System.Text.Json.Serialization; // Keep for CardEffect record

namespace ShadowverseBeyond.Server.Domain.Entities; // Added ShadowverseBeyond prefix

// Represents the structure of an effect as stored in JSON
// Consider moving to a shared DTOs location if reused extensively.
public record CardEffect(
    [property: JsonPropertyName("trigger")] string Trigger,
    [property: JsonPropertyName("action")] string Action,
    // Using object for Value to match JSON flexibility, handle type casting carefully upon use.
    [property: JsonPropertyName("value")] object Value
);

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
    public string Type { get; set; } = string.Empty;

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

    /// <summary>
    /// JSON string representation of the card's effects, stored in the database.
    /// </summary>
    public string? EffectsJson { get; set; }

    /// <summary>
    /// Indicates if this card definition represents a base, collectible form (true)
    /// intended for deck building, or a derived form like an evolved state (false).
    /// </summary>
    public bool IsCollectible { get; set; } = true; // Default to true

    /// <summary>
    /// Indicates if this card definition represents the evolved form of a follower (true).
    /// Base forms and non-follower cards should have this as false.
    /// </summary>
    public bool IsEvolved { get; set; } = false; // Default to false

    /// <summary>
    /// The ID of the card this card evolves into. Null if this card cannot evolve or is already evolved.
    /// </summary>
    public int? EvolveId { get; set; } // Changed type to int?
    /// <summary>
    /// Gets or sets the list of card effects. Handled in code, not directly mapped to DB.
    /// Populates EffectsJson property when set.
    /// </summary>
    [NotMapped]
    public List<CardEffect>? Effects
    {
        get => string.IsNullOrEmpty(EffectsJson)
                ? null
                : JsonSerializer.Deserialize<List<CardEffect>>(EffectsJson, _jsonOptions);
        set => EffectsJson = value == null || !value.Any() // Requires System.Linq
                ? null
                : JsonSerializer.Serialize(value, _jsonOptions);
    }

    // Static options for consistent serialization/deserialization settings.
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true // Handles JSON property names like "value" vs C# "Value"
        // Add other required options like converters if 'object' proves problematic.
    };

    /// <summary>
    /// Parameterless constructor required by EF Core.
    /// </summary>
    public CardDefinition() { }

} // Closing brace for the class
