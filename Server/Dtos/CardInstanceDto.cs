using System;
using System.Collections.Generic;
using System.Text.Json.Serialization; // Add for JsonPropertyName

using Shadowverse.Domain.Interfaces; // Correct namespace

namespace ShadowverseBeyond.Server.Dtos
{
    /// <summary>
    /// Represents the zone a card instance currently resides in.
    /// </summary>
    public enum CardZone
    {
        Deck,
        Hand,
        Field,
        Graveyard,
        Extra,
        Leader,
        Playing // Temporary zone during card resolution
    }

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
        public string Type { get; init; } = string.Empty;

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
        /// Number of attacks made by this follower instance this turn.
        /// Relevant primarily for followers on the field.
        /// </summary>
        public int AttacksMadeThisTurn { get; set; }

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
        /// List of keyword identifiers associated with the card (e.g., "Ward", "Rush").
        /// </summary>
        public List<string> Keywords { get; set; } = new List<string>(); // Changed init to set for field state modification

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
        public int? FieldPosition { get; set; }

        /// <summary>
        /// The Player ID of the player who currently controls this instance on the field.
        /// </summary>
        public string ControllerPlayerId { get; set; } = string.Empty;

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
        public CardInstanceDto(CardDefinitionDto definition, Guid instanceId, CardZone initialZone, string controllerPlayerId)
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
            Keywords = new List<string>(definition.Keywords ?? new List<string>()); // Create copy

            // --- Instance State Initialization ---
            // Handle nullable types from definition using ??
            CurrentAttack = definition.Attack ?? 0; // Initial attack = base attack
            CurrentHealth = definition.Health ?? 0; // Initial health = base health
            MaxHealth = definition.Health ?? 0;     // Initial max health = base health
            IsEvolved = false;
            CanAttack = false; // Usually false initially (unless Rush etc.)
            AttacksMadeThisTurn = 0;
            // MaxAttacksPerTurn does not exist in CardDefinitionDto, rely on CardInstanceDto's default initializer (= 1)
            // CurrentCountdown does not exist in CardDefinitionDto, rely on CardInstanceDto's default (null)
            Zone = initialZone;
            ControllerPlayerId = controllerPlayerId;
            MaxAttack = definition.Attack ?? 0; // Initial max attack = base attack
        }
        // --- End Added Constructor ---

        // Potential future additions:
        // public Dictionary<string, object> EffectsData { get; init; } = new Dictionary<string, object>();
        // public bool IsTargetable { get; set; }
        // Explicit implementation for ICard.Uid
        // Note: ICard expects string Uid. We map our Guid InstanceId.
        string ICard.Uid => this.InstanceId.ToString();

        // Explicit implementation for ICard.Health
        // The base Health property setter was changed from init to set to allow this.
        int ICard.Health
        {
            get => this.CurrentHealth; // Map to CurrentHealth for consistency in field state
            set => this.CurrentHealth = value; // Allow setting CurrentHealth via interface
        }
    }
}