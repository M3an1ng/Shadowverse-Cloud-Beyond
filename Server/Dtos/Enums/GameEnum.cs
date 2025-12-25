using System.ComponentModel;

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
    /// Represents the mode used to play a card.
    /// </summary>
    public enum PlayMode
    {
        /// <summary>
        /// Play the card at its base cost.
        /// </summary>
        Base,

        /// <summary>
        /// Play the card using its Enhance cost.
        /// </summary>
        Enhance
    }


    /// <summary>
    /// Represents the type of a card.
    /// </summary>
    public enum CardType
    {
        /// <summary>
        /// Represents a Leader card.
        /// </summary>
        [Description("主战者")]
        Leader,

        /// <summary>
        /// Represents a follower card that occupies the field.
        /// </summary>
        [Description("随从")]
        Follower,

        /// <summary>
        /// Represents a spell card with an immediate effect.
        /// </summary>
        [Description("法术")]
        Spell,

        /// <summary>
        /// Represents an amulet card that occupies the field and provides effects.
        /// </summary>
        [Description("护符")]
        Amulet
    }

    /// <summary>
    /// Represents the class of a card.
    /// </summary>
    public enum CardClass
    {
        [Description("中立")]
        Neutral = 0,
        [Description("妖精")]
        Forestcraft = 1,
        [Description("皇家护卫")]
        Swordcraft = 2,
        [Description("巫师")]
        Runecraft = 3,
        [Description("龙族")]
        Dragoncraft = 4,
        [Description("死灵法师")]
        Shadowcraft = 5,
        [Description("吸血鬼")]
        Bloodcraft = 6,
        [Description("主教")]
        Havencraft = 7,
        [Description("复仇者")]
        Portalcraft = 8,
    }

    /// <summary>
    /// Represents the deck environment (format) that constrains the card pool.
    /// </summary>
    public enum DeckEnvironment
    {
        [Description("默认环境")]
        Open = 0
    }

    /// <summary>
    /// Represents the rarity of a card.
    /// </summary>
    public enum CardRarity
    {
        [Description("特殊")]
        Special,
        [Description("青铜")]
        Bronze,
        [Description("白银")]
        Silver,
        [Description("黄金")]
        Gold,
        [Description("传说")]
        Legend,
    }

    /// <summary>
    /// Represents the zone a card instance currently resides in.
    /// </summary>
    public enum CardZone
    {
        [Description("战场")]
        Field = 1,
        [Description("手牌")]
        Hand = 2,
        [Description("卡组")]
        Deck = 3,
        [Description("墓地")]
        Graveyard = 4,
        [Description("额外区")]
        Extra = 5,

        // Transient or non-prioritized zones
        [Description("打出中")]
        Playing = 6, // Temporary zone during card resolution
        [Description("纹章")]
        Emblem = 7,
        [Description("移出游戏")]
        Removed = 8,
        [Description("无")]
        None = 100
    }
}
