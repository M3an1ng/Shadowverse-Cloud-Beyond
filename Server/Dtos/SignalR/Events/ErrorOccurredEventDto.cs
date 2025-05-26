using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating an error occurred, either due to an invalid client action
    /// or an unexpected server issue.
    /// </summary>
    public class ErrorOccurredEventDto : GameEventDto
    {
        public override string Type => "ErrorOccurred";

        /// <summary>
        /// A user-friendly message describing the error.
        /// </summary>
        public string ErrorMessage { get; set; } = "An unspecified error occurred."; // Use this existing property

        /// <summary>
        /// Optional: An error code for programmatic handling on the client.
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Optional: The identifier of the player who triggered the error (if applicable).
        /// Useful for displaying the error message only to the relevant player.
        /// If null or empty, the error might be global or server-side.
        /// </summary>
        public string? PlayerId { get; set; }

        // Consider adding details about the failed action if relevant,
        // but keep it minimal to avoid leaking too much internal state.
    }
}