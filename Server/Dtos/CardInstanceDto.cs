using System;
using System.Collections.Generic;
using System.Text.Json.Serialization; // Add for JsonPropertyName

using Shadowverse.Domain.Interfaces; // Correct namespace
using ShadowverseBeyond.Server.Domain.Constants; // For CardKeyword enum
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Dtos
{

    /// <summary>
    /// Data Transfer Object representing a specific instance of a card in the game.
    /// This DTO is used for communication between server and client (e.g., via SignalR).
    /// </summary>
    public record CardInstanceDto
    : ICard // Implement the interface
    {
        /// <summary>
        /// Unique identifier for this specific card instance.
        /// </summary>
        public Guid InstanceId { get; init; }

        /// <summary>
        /// Identifier linking to the base card definition (CardDefinition.Id).
        /// </summary>
        [JsonPropertyName("id")] // Map CardId to "id" in JSON
        public int CardId { get; init; }

        /// <summary>
        /// Name of the card.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Play point cost of the card.
        /// </summary>
        public int Cost { get; init; }

        /// <summary>
        /// Type of the card (e.g., "Follower", "Spell", "Amulet").
        /// Consider using an enum in the future if types become fixed.
        /// </summary>
        public CardType Type { get; init; }

        /// <summary>
        /// Rarity of the card.
        /// </summary>
        public CardRarity Rarity { get; init; }

        /// <summary>
        /// Class of the card.
        /// </summary>
        public CardClass Class { get; init; }

        /// <summary>
        /// Tribe of the card.
        /// </summary>
        public List<string> Tribe { get; init; } = new();

        /// <summary>
        /// Base attack value (from CardDefinition).
        /// </summary>
        public int Attack { get; init; }

        /// <summary>
        /// Base health value (from CardDefinition).
        /// </summary>
        public int Health { get; set; } // Changed init to set for IBoardEntityState

        /// <summary>
        /// Current attack value of the instance (including buffs/debuffs).
        /// Relevant primarily for followers on the field.
        /// </summary>
        public int CurrentAttack { get; set; }

        /// <summary>
        /// Current health value of the instance (including damage/healing).
        /// Relevant primarily for followers on the field.
        /// </summary>
        public int CurrentHealth { get; set; }

        /// <summary>
        /// Maximum health of the instance (including buffs).
        /// Relevant primarily for followers on the field.
        /// </summary>
        public int MaxHealth { get; set; }

        /// <summary>
        /// Card description text.
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Indicates if the card has an evolution form.
        /// TODO: 移除该属性，所有从者卡默认都可以进化
        /// </summary>
        public bool CanEvolve { get; init; }

        /// <summary>
        /// Indicates if the card instance is currently evolved.
        /// Relevant primarily for followers on the field.
        /// </summary>
        public bool IsEvolved { get; set; }

        /// <summary>
        /// Indicates if the follower instance can currently attack.
        /// Affected by summon sickness, effects, and turn progression.
        /// Relevant primarily for followers on the field.
        /// </summary>
        public bool CanAttack { get; set; }

        /// <summary>
        /// Indicates whether this card has an Act (Activated) ability.
        /// </summary>
        public bool HasActAbility { get; set; }

        /// <summary>
        /// The owner's turn number when this card last used Act. Null if never used.
        /// </summary>
        public int? ActLastUsedTurnNumber { get; set; }

        /// <summary>
        /// Number of attacks made by this follower instance this turn.
        /// Relevant primarily for followers on the field.
        /// </summary>
        public int AttacksThisTurn { get; set; }

        /// <summary>
        /// How many turns this instance has been on the field. 0 means it was just played.
        /// </summary>
        public int FieldTurn { get; set; }

        /// <summary>
        /// Maximum attack of the instance (including buffs).
        /// Useful for effects that reference original/max stats after buffs.
        /// Relevant primarily for followers on the field.
        /// </summary>
        public int? MaxAttack { get; set; } // Made nullable to match UnitStateDto
        /// <summary>
        /// The current zone this card instance resides in.
        /// </summary>
        public CardZone Zone { get; set; }

        /// <summary>
        /// List of keyword identifiers associated with the card (e.g., Ward, Rush).
        /// Updated to use CardKeyword enum for type safety
        /// </summary>
        public List<CardKeyword> Keywords { get; set; } = new List<CardKeyword>(); // Changed init to set for field state modification

        /// <summary>
        /// List of effect descriptions for display purposes.
        /// Actual effect logic is handled server-side.
        /// </summary>
        public List<string> EffectDescriptions { get; init; } = new List<string>();

        /// <summary>
        /// Current countdown value for amulets. Null if not applicable.
        /// Relevant primarily for amulets on the field.
        /// </summary>
        public int? CurrentCountdown { get; set; }

        /// <summary>
        /// Maximum number of times this follower can attack per turn. Defaults to 1.
        /// Relevant primarily for followers on the field.
        /// </summary>
        public int MaxAttacksPerTurn { get; set; } = 1;
        /// <summary>
        /// The current play point cost of this card instance, potentially modified by effects.
        /// </summary>
        public int CurrentCost { get; set; }

        /// <summary>
        /// The position of the card on the field (0-4), if applicable. Null otherwise.
        /// </summary>
        public int? ZoneIndex { get; set; }

        /// <summary>
        /// The Player ID of the player who currently controls this instance on the field.
        /// </summary>
        public Guid OwnerPlayerId { get; set; } = Guid.Empty;

        /// <summary>
        /// Holds the full card definition of the evolved state, if this card can evolve.
        /// Null if the card cannot evolve.
        /// </summary>
        public CardDefinitionDto? EvolvedState { get; set; }

        // --- Added Constructor ---
        /// <summary>
        /// <summary>
        /// Default constructor for object initialization syntax.
        /// </summary>
        public CardInstanceDto() { }
        /// Creates a new card instance based on a card definition.
        /// </summary>
        /// <param name="definition">The base card definition.</param>
        /// <param name="instanceId">The unique ID for this instance.</param>
        /// <param name="initialZone">The initial zone for this instance.</param>
        /// <param name="controllerPlayerId">The ID of the player controlling this instance.</param>
        public CardInstanceDto(CardDefinitionDto definition, Guid instanceId, CardZone initialZone, Guid ownerPlayerId)
        {
            // --- Base Property Mapping ---
            InstanceId = instanceId;
            // CardId in CardDefinitionDto is now int, assign directly.
            CardId = definition.CardId;
            Name = definition.Name;
            Cost = definition.Cost;
            Type = definition.Type;
            // Handle nullable types from definition using ??
            Attack = definition.Attack ?? 0;
            Health = definition.Health ?? 0; // Base health
            Description = definition.Description ?? string.Empty;
            CanEvolve = definition.CanEvolve ?? false;
            // EffectDescriptions does not exist in CardDefinitionDto, rely on CardInstanceDto's default initializer
            // Convert string keywords to CardKeyword enum
            Keywords = new List<CardKeyword>();
            if (definition.Keywords != null)
            {
                foreach (var keywordStr in definition.Keywords)
                {
                    if (Enum.TryParse<CardKeyword>(keywordStr, true, out var keywordEnum) && keywordEnum != CardKeyword.None)
                    {
                        Keywords.Add(keywordEnum);
                    }
                }
            }

            // --- Instance State Initialization ---
            // Handle nullable types from definition using ??
            CurrentAttack = definition.Attack ?? 0; // Initial attack = base attack
            CurrentHealth = definition.Health ?? 0; // Initial health = base health
            MaxHealth = definition.Health ?? 0;     // Initial max health = base health
            IsEvolved = false;
            CanAttack = false; // Usually false initially (unless Rush etc.)
            AttacksThisTurn = 0;
            // MaxAttacksPerTurn does not exist in CardDefinitionDto, rely on CardInstanceDto's default initializer (= 1)
            // CurrentCountdown does not exist in CardDefinitionDto, rely on CardInstanceDto's default (null)
            Zone = initialZone;
            OwnerPlayerId = ownerPlayerId;
            MaxAttack = definition.Attack ?? 0; // Initial max attack = base attack
        }
        // --- End Added Constructor ---

        // Potential future additions:
        // public Dictionary<string, object> EffectsData { get; init; } = new Dictionary<string, object>();
        // public bool IsTargetable { get; set; }
        // Explicit implementation for ICard.Uid
        // Note: ICard expects Guid Uid. We map our Guid InstanceId.
        Guid ICard.Uid => InstanceId;

        // Explicit implementation for ICard.Health
        // The base Health property setter was changed from init to set to allow this.
        int ICard.Health
        {
            get => this.CurrentHealth; // Map to CurrentHealth for consistency in field state
            set => this.CurrentHealth = value; // Allow setting CurrentHealth via interface
        }
    }
}
