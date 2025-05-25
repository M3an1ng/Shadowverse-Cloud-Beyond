// File: ShadowverseBeyond.Server.Exceptions.GameLogicException.cs
using System;

namespace ShadowverseBeyond.Server.Exceptions
{
    /// <summary>
    /// Represents errors that occur during game logic validation or execution
    /// (e.g., insufficient PP, invalid target, action out of turn).
    /// These exceptions are typically caught and translated into user-friendly
    /// error messages sent back to the specific client via SignalR.
    /// </summary>
    public class GameLogicException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GameLogicException"/> class.
        /// </summary>
        public GameLogicException() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="GameLogicException"/> class
        /// with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public GameLogicException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="GameLogicException"/> class
        /// with a specified error message and a reference to the inner exception that
        /// is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception,
        /// or a null reference if no inner exception is specified.</param>
        public GameLogicException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}