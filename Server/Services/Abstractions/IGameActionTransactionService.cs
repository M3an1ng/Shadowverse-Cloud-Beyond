using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // Added for PlayerChoiceRequest

namespace ShadowverseBeyond.Server.Services.Abstractions
{
    /// <summary>
    /// Represents the outcome of a processed game action transaction.
    /// </summary>
    public class TransactionResult
    {
        public bool IsSuccess { get; protected set; }
        public string? ErrorMessage { get; protected set; }
        public virtual bool CanContinue => IsSuccess;
        public bool ShouldStop => !CanContinue;

        public static TransactionResult Success() => new TransactionResult { IsSuccess = true };
        public static TransactionResult Failure(string message) => new TransactionResult { IsSuccess = false, ErrorMessage = message };
    }

    /// <summary>
    /// Represents the outcome of a transaction that may be paused for player input.
    /// </summary>
    public class PausableTransactionResult : TransactionResult
    {
        public bool IsCancelled { get; private set; }
        public IReadOnlyList<IGameEvent> ProcessedEvents { get; private set; } = new List<IGameEvent>();
        public override bool CanContinue => IsSuccess && !IsCancelled;

        public static PausableTransactionResult Success(IReadOnlyList<IGameEvent> processedEvents) => new PausableTransactionResult { IsSuccess = true, ProcessedEvents = processedEvents };
        public new static PausableTransactionResult Failure(string message) => new PausableTransactionResult { IsSuccess = false, ErrorMessage = message };
        public static PausableTransactionResult Cancelled() => new PausableTransactionResult { IsSuccess = true, IsCancelled = true };
    }


    /// <summary>
    /// Defines the contract for a service that processes player actions as atomic,
    /// cancellable transactions. This service is the cornerstone of the new game
    /// action handling architecture, replacing the monolithic logic in GameService.
    /// </summary>
    public interface IGameActionTransactionService
    {
        /// <summary>
        /// Processes a collection of initial events as a single, atomic, and potentially pausable transaction.
        /// This method orchestrates the entire three-phase process:
        /// 1. Analyze and resolve primary abilities (cancellable part).
        /// 2. Analyze and resolve chained abilities (committed part).
        /// 3. Commit events and handle subsequent effects.
        /// </summary>
        /// <param name="gameContext">The context for the current game state.</param>
        /// <param name="initialEvents">The initial set of game events generated from a player action.</param>
        /// <returns>A result indicating the final state of the transaction (Success, Failure, or Paused).</returns>
        Task<PausableTransactionResult> ProcessTransactionAsync(GameContext gameContext, IEnumerable<IGameEvent> initialEvents);

        /// <summary>
        /// Cancels the currently pending player action, if one exists and is cancellable.
        /// </summary>
        /// <param name="gameContext">The context for the current game state.</param>
        /// <returns>A result indicating whether the cancellation was successful.</returns>
        Task<PausableTransactionResult> CancelPendingActionAsync(GameContext gameContext);
    }
}
