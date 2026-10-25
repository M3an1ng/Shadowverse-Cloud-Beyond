using System;
using System.Threading.Tasks;
using System.Threading;
using System.Text.Json.Serialization;

namespace ShadowverseBeyond.Server.Models
{
    /// <summary>
    /// Wraps a player action request received from the Hub, intended for the action queue.
    /// </summary>
    public class PlayerActionWrapper
    {
        private readonly TaskCompletionSource<PlayerActionCompletion> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

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

        /// <summary>
        /// Completes only after the game loop has accepted, rejected, or cancelled this action.
        /// Queue admission alone is not evidence that the action changed game state.
        /// </summary>
        [JsonIgnore]
        public Task<PlayerActionCompletion> Completion => _completion.Task;

        [JsonIgnore]
        public CancellationToken CancellationToken { get; init; }

        [JsonIgnore]
        public int? ExpectedTurnNumber { get; init; }

        public void MarkCompleted() => _completion.TrySetResult(PlayerActionCompletion.Completed);

        public void MarkRejected(string reason) => _completion.TrySetResult(PlayerActionCompletion.Rejected(reason));

        public void MarkCancelled(string reason) => _completion.TrySetResult(PlayerActionCompletion.Cancelled(reason));

        public void MarkFailed(string reason) => _completion.TrySetResult(new(PlayerActionCompletionStatus.Failed, reason));

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

    public sealed record PlayerActionCompletion(PlayerActionCompletionStatus Status, string? Reason)
    {
        public static PlayerActionCompletion Completed { get; } = new(PlayerActionCompletionStatus.Completed, null);

        public static PlayerActionCompletion Rejected(string reason) => new(PlayerActionCompletionStatus.Rejected, reason);

        public static PlayerActionCompletion Cancelled(string reason) => new(PlayerActionCompletionStatus.Cancelled, reason);
    }

    public enum PlayerActionCompletionStatus
    {
        Completed,
        Rejected,
        Cancelled,
        Failed
    }
}
