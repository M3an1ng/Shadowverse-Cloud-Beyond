namespace ShadowverseBeyond.Server.Dtos.Enums
{
    /// <summary>
    /// Represents the distinct phases within a game turn or the overall game flow.
    /// </summary>
    public enum GamePhase
    {
        /// <summary>
        /// Game setup phase before mulligan or first turn actions.
        /// </summary>
        Initialization,

        /// <summary>
        /// Initial hand replacement phase at the start of the game.
        /// </summary>
        Mulligan,

        /// <summary>
        /// Start of a player's turn, includes drawing a card and resolving start-of-turn effects.
        /// </summary>
        TurnStart,

        /// <summary>
        /// The main phase where the active player can play cards, attack with followers, etc.
        /// </summary>
        Main,

        /// <summary>
        /// Optional phase specifically for attack declarations and resolutions, if needed for complex combat logic.
        /// </summary>
        Combat, // Optional, might merge with Main

        /// <summary>
        /// End of the player's turn, resolving end-of-turn effects.
        /// </summary>
        TurnEnd,

        /// <summary>
        /// The game has concluded, determining win/loss conditions.
        /// </summary>
        GameOver
    }
}