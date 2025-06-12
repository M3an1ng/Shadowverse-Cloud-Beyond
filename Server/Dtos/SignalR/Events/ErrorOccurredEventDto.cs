using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating an error occurred, either due to an invalid client action
    /// or an unexpected server issue.
    /// </summary>
    public class ErrorOccurredEventDto : GameEventDto
    {

        /// <summary>
        /// A user-friendly message describing the error.
        /// </summary>
        public string ErrorMessage { get; set; } = "An unspecified error occurred."; // Use this existing property

        /// <summary>
        /// Optional: An error code for programmatic handling on the client.
        /// </summary>
        public string? ErrorCode { get; set; }


        // Consider adding details about the failed action if relevant,
        // but keep it minimal to avoid leaking too much internal state.
        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorOccurredEventDto"/> class.
        /// </summary>
        /// <param name="playerId">Identifier of the player primarily associated with the error (e.g., who caused it, or empty/null for global errors).</param>
        /// <param name="errorMessage">The user-friendly error message.</param>
        /// <param name="errorCode">Optional error code for client-side handling.</param>
        public ErrorOccurredEventDto(Guid playerId, string errorMessage, string? errorCode = null)
            : base(playerId, "ErrorOccurred") // Set base PlayerId and Type
        {
            this.ErrorMessage = errorMessage;
            this.ErrorCode = errorCode;
        }

        // Consider adding details about the failed action if relevant,
        // but keep it minimal to avoid leaking too much internal state.
    }
}