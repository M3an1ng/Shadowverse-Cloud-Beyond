namespace ShadowverseBeyond.Server.Dtos.Enums
{
    /// <summary>
    /// Represents the overall status of a game instance.
    /// </summary>
    public enum GameStatus
    {
        /// <summary>
        /// The game is currently being initialized (e.g., players joining, decks loading).
        /// </summary>
        Starting,

        /// <summary>
        /// The game is actively being played.
        /// </summary>
        InProgress,

        /// <summary>
        /// The game has completed normally through win/loss conditions.
        /// </summary>
        Finished,

        /// <summary>
        /// The game ended prematurely due to errors, disconnects, or other issues.
        /// </summary>
        Aborted
    }
}