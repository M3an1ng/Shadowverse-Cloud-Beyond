using System;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;

namespace ShadowverseBeyond.Server.Services.Abstractions
{
    /// <summary>
    /// Manages the game state's need to pause and wait for asynchronous player input.
    /// This service is central to handling abilities that require choices.
    /// </summary>
    public interface IPendingActionService
    {
        /// <summary>
        /// Creates a pending choice, sends the request to the player, and returns a task that completes when the player responds.
        /// </summary>
        /// <param name="request">The details of the choice being requested.</param>
        /// <param name="cancellationToken">A token to cancel the wait operation.</param>
        /// <returns>A task that resolves to a PlayerChoiceResult containing the player's decision.</returns>
        Task<PlayerChoiceResult> WaitForPlayerChoiceAsync(string gameId, PlayerChoiceRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// Completes a pending choice with the data submitted by the player.
        /// </summary>
        /// <param name="choiceId">The ID of the pending choice to complete.</param>
        /// <param name="dto">The data submitted by the player.</param>
        void CompletePlayerChoice(Guid choiceId, SubmitTargetChoiceActionDto dto);

        /// <summary>
        /// Retrieves the context for a currently pending choice, if one exists for the specified player.
        /// This is used to handle interruptions like turn timeouts.
        /// </summary>
        /// <param name="gameId">The ID of the game.</param>
        /// <param name="playerId">The ID of the player.</param>
        /// <returns>The pending choice context, or null if none exists.</returns>
        PendingChoiceContext? GetPendingChoiceForPlayer(string gameId, Guid playerId);

        /// <summary>
        /// Forces a pending choice to resolve with a specific outcome, typically used for timeouts.
        /// </summary>
        /// <param name="choiceId">The ID of the pending choice to force-resolve.</param>
        /// <param name="forcedResult">The result to force upon the choice.</param>
        void ForceResolveChoice(Guid choiceId, PlayerChoiceResult forcedResult);
    }

    /// <summary>
    /// Represents the outcome of a player's choice.
    /// </summary>
    public class PlayerChoiceResult
    {
        public bool WasCancelled { get; set; }
        public List<Guid> ChosenTargetIds { get; set; } = new();
        public static PlayerChoiceResult Cancelled => new() { WasCancelled = true };
    }

    /// <summary>
    /// Stores the context of a choice that is pending player input.
    /// </summary>
    public class PendingChoiceContext
    {
        public PlayerChoiceRequest Request { get; }
        public TaskCompletionSource<PlayerChoiceResult> Tcs { get; }
        public CancellationToken CancellationToken { get; }

        public PendingChoiceContext(PlayerChoiceRequest request, TaskCompletionSource<PlayerChoiceResult> tcs, CancellationToken cancellationToken)
        {
            Request = request;
            Tcs = tcs;
            CancellationToken = cancellationToken;
        }
    }
}