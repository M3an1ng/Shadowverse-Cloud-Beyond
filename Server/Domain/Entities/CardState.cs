// File: Server/Domain/Entities/CardState.cs
using System.Collections.Generic; // If using Modifications dictionary

namespace ShadowverseBeyond.Server.Domain.Entities
{
    /// <summary>
    /// Represents the state of a specific instance of a card within a game.
    /// This distinguishes it from the static CardDefinition. Each copy of a card
    /// in hand, deck, etc., will have its own CardState instance.
    /// </summary>
    public class CardState
    {
        /// <summary>
        /// Unique identifier for this card instance within the current game.
        /// Example format: "GameID_PlayerID_CardDefID_InstanceNum" or similar. Generated when added to deck/hand.
        /// </summary>
        public string InstanceId { get; set; } = string.Empty;

        /// <summary>
        /// Identifier linking to the static CardDefinition, providing base stats, text, type, etc.
        /// </summary>
        public int DefinitionId { get; set; }

        /// <summary>
        /// The current play point cost of this card instance.
        /// This can differ from the base cost defined in CardDefinition due to card effects.
        /// Should be initialized with the base cost.
        /// </summary>
        public int CurrentCost { get; set; }

        // Optional: Add properties for temporary modifications if needed
        // E.g., public int TemporaryAttackBonus { get; set; } = 0;
        // E.g., public bool GainedRushWhileInHand { get; set; } = false;
        // Alternatively, a more flexible modification system:
        // public Dictionary<string, object> Modifications { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Constructor requiring essential information for a card instance.
        /// </summary>
        /// <param name="instanceId">The unique identifier for this instance.</param>
        /// <param name="definitionId">The ID of the base card definition.</param>
        /// <param name="initialCost">The initial cost (usually the base cost from definition).</param>
        public CardState(string instanceId, int definitionId, int initialCost)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            CurrentCost = initialCost;
        }

        /// <summary>
        /// Parameterless constructor for ORM/Serialization.
        /// </summary>
        private CardState()
        {
            InstanceId = string.Empty;
            // Ensure non-nullable value types have defaults if needed,
            // though DefinitionId and CurrentCost should ideally always be set.
        }
    }
}