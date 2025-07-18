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


    /// <summary>
    /// Represents the type of a card.
    /// </summary>
    public enum CardType
    {
        /// <summary>
        /// Represents a Leader card.
        /// </summary>
        Leader,

        /// <summary>
        /// Represents a follower card that occupies the field.
        /// </summary>
        Follower,

        /// <summary>
        /// Represents a spell card with an immediate effect.
        /// </summary>
        Spell,

        /// <summary>
        /// Represents an amulet card that occupies the field and provides effects.
        /// </summary>
        Amulet
    }

    /// <summary>
    /// Represents the type of a card.
    /// </summary>
    public enum CardRarity
    {
        /// <summary>
        /// Represents a Leader card.
        /// </summary>
        Leader,

        /// <summary>
        /// Represents a follower card that occupies the field.
        /// </summary>
        Follower,

        /// <summary>
        /// Represents a spell card with an immediate effect.
        /// </summary>
        Spell,

        /// <summary>
        /// Represents an amulet card that occupies the field and provides effects.
        /// </summary>
        Amulet
    }

    /// <summary>
    /// Represents the zone a card instance currently resides in.
    /// </summary>
    public enum CardZone
    {
        None = 0,
        Deck,
        Hand,
        Field,
        Graveyard,
        Extra,
        Leader,
        Playing // Temporary zone during card resolution
    }
}