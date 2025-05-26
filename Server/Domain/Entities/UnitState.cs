// File: Server/Domain/Entities/UnitState.cs
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Domain.Entities
{
    /// <summary>
    /// Represents the state of a Unit instance on the board. Inherits common properties from BoardEntityState.
    /// </summary>
    public class UnitState : BoardEntityState
    {
        // Identity & Ownership properties (InstanceId, OwnerPlayerId, DefinitionId) are inherited from BoardEntityState

        // Core Stats (Mutable)
        public int CurrentAttack { get; set; }
        public int CurrentHealth { get; set; }
        public int MaxHealth { get; set; } // Base max health, might change due to effects

        // Combat Status
        public bool CanAttack { get; set; } = false; // Typically false on turn played unless Rush/Storm
        public int AttacksRemainingThisTurn { get; set; } = 0; // Units usually have 1, Rush/Storm may differ
        public bool HasAttackedThisTurn { get; set; } = false; // Simpler than AttacksRemaining if max is usually 1

        // Evolution Status
        public bool IsEvolved { get; set; } = false;
        public bool CanEvolve { get; set; } = false; // Can this unit evolve this turn?

        // Other Statuses & Effects
        public bool IsTargetableByOpponentSpellsOrEffects { get; set; } = true; // For "Cannot be targeted"
        public bool IsTargetableByOpponentAttack { get; set; } = true; // For Ward calculation
        // ActiveEffects list is inherited from BoardEntityState

        // Board Position (Optional, depends on game rules - max 5 units)
        public int BoardPosition { get; set; } // 0 to 4 typically

        // Type Helper (can be derived from DefinitionId later via CardRepository)
        // public bool IsAmulet { get; /* Logic to check CardDefinition type */ }

        // Constructor - requires essential info, passes base info to BoardEntityState constructor
        public UnitState(string instanceId, string ownerPlayerId, int definitionId, int attack, int health, int boardPosition)
            : base(instanceId, ownerPlayerId, definitionId)
        {
            CurrentAttack = attack;
            MaxHealth = health;
            CurrentHealth = health; // Start at max health
            BoardPosition = boardPosition;
            // TODO: Initialize other properties like CanAttack, CanEvolve, IsTargetable etc. based on CardDefinition
            // This logic might belong in a factory or service responsible for creating units.
        }

        // Parameterless constructor for ORM/Serialization
        // Parameterless constructor for ORM/Serialization - calls base constructor
        // Ensure it's used appropriately (e.g., only by ORM/Serializer)
        protected UnitState() : base()
        {
            // Initialize UnitState specific fields if necessary, base fields handled by base()
        }

        // --- Potential methods for state manipulation ---
        public void TakeDamage(int amount) { CurrentHealth -= amount; /* Add logic for lethal, effects etc. */ }
        public void Heal(int amount) { CurrentHealth = System.Math.Min(CurrentHealth + amount, MaxHealth); }
        // Note: ApplyEffect/RemoveEffect methods are inherited from BoardEntityState
        // We use 'object' as a placeholder until EffectState is defined.
        // Consider overriding them here if Unit needs specific effect handling logic.
        // Example override:
        // public override void ApplyEffect(object effect) { base.ApplyEffect(effect); /* Unit specific logic */ }
        public void Evolve(int attackBoost, int healthBoost) { /* Change stats, set IsEvolved, potentially add/remove effects */ }
        /// &lt;summary&gt;
        /// Checks if this unit currently possesses the specified keyword.
        /// This checks the CurrentKeywords list, which should reflect keywords from the definition and active effects.
        /// &lt;/summary&gt;
        /// &lt;param name="keyword"&gt;The keyword to check for (case-sensitive).&lt;/param&gt;
        /// &lt;returns&gt;True if the unit has the keyword, false otherwise.&lt;/returns&gt;
        public bool HasKeyword(string keyword)
        {
            // Basic check against the maintained list.
            // The logic to populate and update CurrentKeywords based on effects needs to be implemented elsewhere.
            return CurrentKeywords.Contains(keyword);
        }
        // public bool HasKeyword(string keyword) { /* Check definition + active effects */ }
    }
}