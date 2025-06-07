namespace ShadowverseBeyond.Server.Dtos.Enums
{
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
}