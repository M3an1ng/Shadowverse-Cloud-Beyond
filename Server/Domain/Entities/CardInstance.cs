// File: Server/Domain/Entities/CardInstance.cs
//
// 相关文档:
// - 数据库设计: 文档/04-数据库设计/01-实体关系图.md
// - 卡牌出牌系统: 文档/05-游戏系统/04-卡牌出牌系统.md
// - 战斗系统: 文档/05-游戏系统/02-战斗系统.md
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
//
using System;
using System.Collections.Generic;
using Shadowverse.Domain.Interfaces; // For ICard
using ShadowverseBeyond.Server.Dtos; // For CardZone enum and CardInstanceDto
using ShadowverseBeyond.Server.Domain.Events; // For IGameEvent

namespace ShadowverseBeyond.Server.Domain.Entities
{
    /// <summary>
    /// Represents the state of a specific instance of a card within a game.
    /// This replaces CardState and all BoardEntityState subclasses for unified entity handling.
    /// Can represent cards in any zone (hand, field, deck, graveyard, etc.) and any type (leader, unit, amulet, spell).
    /// </summary>
    public class CardInstance : ICard
    {
        /// <summary>
        /// Unique identifier for this card instance within the current game. (String for flexibility)
        /// </summary>
        public string InstanceId { get; set; } = string.Empty;

        /// <summary>
        /// Identifier linking to the static CardDefinition, providing base stats, text, type, etc.
        /// </summary>
        public int DefinitionId { get; set; }

        /// <summary>
        /// The player who owns this card instance.
        /// </summary>
        public string OwnerPlayerId { get; set; } = string.Empty;

        /// <summary>
        /// The current zone where this card instance is located.
        /// </summary>
        public CardZone CurrentZone { get; set; } = CardZone.Deck;

        /// <summary>
        /// The type of this card (Follower, Spell, Amulet, Leader).
        /// </summary>
        public string CardType { get; set; } = string.Empty;

        /// <summary>
        /// The current play point cost of this card instance.
        /// This can differ from the base cost defined in CardDefinition due to card effects.
        /// </summary>
        public int CurrentCost { get; set; }

        /// <summary>
        /// Current attack value (for units/leaders).
        /// </summary>
        public int CurrentAttack { get; set; }

        /// <summary>
        /// Current health value (for units/leaders).
        /// </summary>
        public int CurrentHealth { get; set; }

        /// <summary>
        /// Maximum health value (for units/leaders).
        /// </summary>
        public int MaxHealth { get; set; }

        /// <summary>
        /// Whether this unit is evolved (for units only).
        /// </summary>
        public bool IsEvolved { get; set; } = false;

        /// <summary>
        /// Countdown value (for amulets only).
        /// </summary>
        public int? Countdown { get; set; }

        /// <summary>
        /// Position on the field (0-4 for field entities, null otherwise).
        /// </summary>
        public int? FieldPosition { get; set; }

        /// <summary>
        /// Whether this card can attack this turn.
        /// </summary>
        public bool CanAttack { get; set; } = false;

        /// <summary>
        /// Whether this card can evolve this turn.
        /// </summary>
        public bool CanEvolve { get; set; } = false;

        /// <summary>
        /// Number of attacks made this turn.
        /// </summary>
        public int AttacksThisTurn { get; set; } = 0;

        /// <summary>
        /// Active effects on this card instance. (Type object for now, consider specific Effect class later)
        /// </summary>
        public List<object> ActiveEffects { get; set; } = new List<object>();

        /// <summary>
        /// 存储与此卡牌实例关联的能力的持久运行时状态（例如计数器）。
        /// Key: AbilityId, Value: Dictionary&lt;string, object&gt; (e.g., {"TriggerCount": 5})
        /// </summary>
        public Dictionary<long, Dictionary<string, object>> AbilityStates { get; private set; } = new();
        /// <summary>
        /// Constructor requiring essential information for a card instance.
        /// </summary>
        public CardInstance(string instanceId, int definitionId, string ownerPlayerId)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            OwnerPlayerId = ownerPlayerId;
        }

        /// <summary>
        /// Parameterless constructor for ORM/Serialization.
        /// </summary>
        protected CardInstance()
        {
            InstanceId = string.Empty;
            OwnerPlayerId = string.Empty;
        }

        // IBoardEntityState implementation
        string ICard.Uid => InstanceId;
        int ICard.Health
        {
            get => CurrentHealth;
            set => CurrentHealth = value;
        }

        // Helper methods for type checking
        public bool IsLeader => CardType.Equals("Leader", StringComparison.OrdinalIgnoreCase);
        public bool IsFollower => CardType.Equals("Follower", StringComparison.OrdinalIgnoreCase);
        public bool IsSpell => CardType.Equals("Spell", StringComparison.OrdinalIgnoreCase);
        public bool IsAmulet => CardType.Equals("Amulet", StringComparison.OrdinalIgnoreCase);

        // Helper methods for zone checking
        public bool IsInHand => CurrentZone == CardZone.Hand;
        public bool IsOnField => CurrentZone == CardZone.Field;
        public bool IsInDeck => CurrentZone == CardZone.Deck;
        public bool IsInGraveyard => CurrentZone == CardZone.Graveyard;

        // Leader-specific methods (only applicable when IsLeader is true)
        /// <summary>
        /// Applies damage to this card instance and returns generated events.
        /// </summary>
        /// <param name="amount">Amount of damage to apply</param>
        /// <param name="source">Source of the damage (optional)</param>
        /// <returns>List of events generated by taking damage</returns>
        public List<IGameEvent> TakeDamage(int amount, CardInstance? source = null)
        {
            var events = new List<IGameEvent>();

            if (amount < 0) return events;

            var actualDamage = Math.Min(amount, CurrentHealth);
            CurrentHealth -= actualDamage;

            // Generate damage event
            events.Add(new DamageEvent(this, actualDamage, source));

            if (CurrentHealth <= 0)
            {
                CurrentHealth = 0;
                // Generate destruction event - this will be handled by the caller
                // who has access to PlayerStateDto
            }

            return events;
        }

        /// <summary>
        /// Applies healing to this card instance and returns generated events.
        /// </summary>
        /// <param name="amount">Amount of healing to apply</param>
        /// <param name="source">Source of the healing (optional)</param>
        /// <returns>List of events generated by healing</returns>
        public List<IGameEvent> Heal(int amount, CardInstance? source = null)
        {
            var events = new List<IGameEvent>();

            if (amount < 0 || CurrentHealth <= 0) return events;

            var oldHealth = CurrentHealth;
            CurrentHealth = Math.Min(CurrentHealth + amount, MaxHealth);
            var actualHealing = CurrentHealth - oldHealth;

            if (actualHealing > 0)
            {
                events.Add(new HealEvent(this, actualHealing, source));
            }

            return events;
        }

        // Amulet-specific methods (only applicable when IsAmulet is true)
        /// <summary>
        /// Decrements countdown and returns generated events.
        /// </summary>
        /// <returns>List of events generated by countdown tick</returns>
        public List<IGameEvent> TickCountdown()
        {
            var events = new List<IGameEvent>();

            if (IsAmulet && Countdown.HasValue)
            {
                Countdown--;
                if (Countdown <= 0)
                {
                    // Generate countdown finished event - this will be handled by the caller
                    // who has access to PlayerStateDto
                }
            }

            return events;
        }

        /// <summary>
        /// Converts this CardInstance domain entity to its corresponding DTO,
        /// representing the current state of the instance.
        /// Note: Properties derived from CardDefinition (like base stats, name, description)
        /// are not populated here as they require fetching the definition.
        /// </summary>
        /// <returns>A CardInstanceDto object representing the current state.</returns>
        public CardInstanceDto ToDto()
        {
            Guid.TryParse(this.InstanceId, out Guid instanceGuid);

            return new CardInstanceDto() // Use parameterless constructor
            {
                // --- Instance State Mapping ---
                InstanceId = instanceGuid,
                CardId = this.DefinitionId, // Assuming DTO's CardId corresponds to DefinitionId
                ControllerPlayerId = this.OwnerPlayerId, // Assuming DTO's Controller is the Owner
                Zone = this.CurrentZone,
                Type = this.CardType, // Map CardType (DTO Type is init, but okay for state representation)

                // Stats reflecting current state
                CurrentAttack = this.CurrentAttack,
                CurrentHealth = this.CurrentHealth,
                MaxHealth = this.MaxHealth, // Current MaxHealth
                IsEvolved = this.IsEvolved,
                CurrentCountdown = this.Countdown,
                CurrentCost = this.CurrentCost, // Added mapping
                FieldPosition = this.FieldPosition, // Added mapping

                // Action-related state
                CanAttack = this.CanAttack,
                CanEvolve = this.CanEvolve, // Map current ability to evolve (DTO CanEvolve is init, okay for state)
                AttacksMadeThisTurn = this.AttacksThisTurn,

                // --- Definition-based properties (Not populated here, rely on DTO defaults) ---
                // Name = ???, // Requires CardDefinition lookup
                // Cost = ???, // Requires CardDefinition lookup
                // Attack = ???, // Requires CardDefinition lookup
                // Health = ???, // Requires CardDefinition lookup
                // Description = ???, // Requires CardDefinition lookup
                // Keywords = ???, // Requires CardDefinition lookup
                // EffectDescriptions = ???, // Requires CardDefinition lookup
                // MaxAttacksPerTurn = ???, // Requires CardDefinition lookup
                // MaxAttack = ???, // Requires CardDefinition lookup
            };
        }
    } // Closing brace for CardInstance class
} // Closing brace for namespace