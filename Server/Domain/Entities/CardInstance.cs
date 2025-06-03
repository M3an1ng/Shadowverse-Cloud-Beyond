// File: Server/Domain/Entities/CardInstance.cs
using System;
using System.Collections.Generic;
using Shadowverse.Domain.Interfaces; // For IBoardEntityState
using ShadowverseBeyond.Server.Dtos; // For CardZone enum

namespace ShadowverseBeyond.Server.Domain.Entities
{
    /// <summary>
    /// Represents the state of a specific instance of a card within a game.
    /// This replaces CardState and all BoardEntityState subclasses for unified entity handling.
    /// Can represent cards in any zone (hand, field, deck, graveyard, etc.) and any type (leader, unit, amulet, spell).
    /// </summary>
    public class CardInstance : IBoardEntityState
    {
        /// <summary>
        /// Unique identifier for this card instance within the current game.
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
        /// Active effects on this card instance.
        /// </summary>
        public List<object> ActiveEffects { get; set; } = new List<object>();

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
        string IBoardEntityState.Uid => InstanceId;
        int IBoardEntityState.Health
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
        public void TakeDamage(int amount)
        {
            if (amount < 0) return;
            CurrentHealth -= amount;
            if (CurrentHealth <= 0)
            {
                CurrentHealth = 0;
                // TODO: Trigger leader defeat event
            }
        }

        public void Heal(int amount)
        {
            if (amount < 0 || CurrentHealth <= 0) return;
            CurrentHealth = Math.Min(CurrentHealth + amount, MaxHealth);
        }

        // Amulet-specific methods (only applicable when IsAmulet is true)
        public void TickCountdown()
        {
            if (IsAmulet && Countdown.HasValue)
            {
                Countdown--;
                if (Countdown <= 0)
                {
                    // TODO: Trigger countdown finished event
                }
            }
        }
    }
}