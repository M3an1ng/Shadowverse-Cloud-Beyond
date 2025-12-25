using System;

namespace ShadowverseBeyond.Server.Models
{
    /// <summary>
    /// Wraps a player action request received from the Hub, intended for the action queue.
    /// </summary>
    public class PlayerActionWrapper
    {
        /// <summary>
        /// A unique identifier for this specific action instance, used for tracking its completion.
        /// </summary>
        public Guid ActionId { get; } = Guid.NewGuid();

        /// <summary>
        /// Identifies the type of action (e.g., "PlayCard", "Attack", "EndTurn").
        /// Consider using an enum for better type safety in the future.
        /// </summary>
        public string ActionType { get; set; } = string.Empty;

        /// <summary>
        /// The ID of the player initiating the action.
        /// </summary>
        public Guid PlayerId { get; set; } = Guid.Empty;

        /// <summary>
        /// The data/payload associated with the action. This could be a specific DTO
        /// like PlayCardActionDto, AttackActionDto, or null if no data is needed (e.g., EndTurn).
        /// </summary>
        public object? ActionData { get; set; }

        /// <summary>
        /// (Optional) A unique identifier for the request, useful for tracking or idempotency.
        /// </summary>
        public Guid RequestId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// (Optional) The timestamp when the action was received or created.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Parameterless constructor for serialization/deserialization if needed
        public PlayerActionWrapper() { }

        // Constructor for convenience
        public PlayerActionWrapper(string actionType, Guid playerId, object? actionData)
        {
            ActionType = actionType;
            PlayerId = playerId;
            ActionData = actionData;
        }
    }
}