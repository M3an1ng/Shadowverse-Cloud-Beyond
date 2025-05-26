// File: Server/Domain/Entities/BoardEntityState.cs
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Domain.Entities
{
    /// <summary>
    /// Abstract base class for any entity that can exist on the game board (Units, Amulets, Leaders).
    /// </summary>
    public abstract class BoardEntityState
    {
        public string InstanceId { get; protected set; } = string.Empty; // Unique ID for this instance
        public string OwnerPlayerId { get; protected set; } = string.Empty; // Player who owns/controls this entity
        public int DefinitionId { get; protected set; } // Link to the CardDefinition

        // Common state applicable to potentially all board entities
        // TODO: Define EffectState properly later
        public List<object> ActiveEffects { get; set; } = new List<object>(); // Placeholder for EffectState

        /// &lt;summary&gt;
        /// List of keywords currently active on this entity, derived from its definition and active effects.
        /// This list should be maintained by the logic that creates the entity and applies/removes effects.
        /// &lt;/summary&gt;
        public List<string> CurrentKeywords { get; set; } = new List<string>();
        // Protected constructor for derived classes
        protected BoardEntityState(string instanceId, string ownerPlayerId, int definitionId)
        {
            InstanceId = instanceId;
            OwnerPlayerId = ownerPlayerId;
            DefinitionId = definitionId;
            // ActiveEffects are initialized by default
        }

        // Parameterless constructor might be needed for some frameworks (e.g., EF Core, Serializers)
        // Mark as protected or ensure non-nullable members are handled if used.
        protected BoardEntityState() { }

        // Potential common methods (example)
        // TODO: Implement actual effect logic with a defined EffectState
        public virtual void ApplyEffect(object effect) { ActiveEffects.Add(effect); /* Update related state? */ }
        public virtual void RemoveEffect(object effect) { ActiveEffects.Remove(effect); /* Revert related state? */ }
        // public bool HasKeyword(string keyword) { /* Check Definition + ActiveEffects */ } - Logic likely involves CardRepository
    }
}