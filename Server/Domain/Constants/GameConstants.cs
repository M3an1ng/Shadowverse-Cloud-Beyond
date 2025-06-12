using System;

namespace ShadowverseBeyond.Server.Domain.Constants
{
    /// <summary>
    /// Contains all game-related constants used throughout the Shadowverse Beyond application.
    /// This class centralizes magic numbers and configuration values to improve maintainability.
    /// </summary>
    public static class GameConstants
    {
        /// <summary>
        /// Field and board related constants
        /// </summary>
        public static class Field
        {
            /// <summary>
            /// Maximum number of units that can be on a player's field at once
            /// </summary>
            public const int MaxFieldSize = 5;
        }

        /// <summary>
        /// Hand and deck related constants
        /// </summary>
        public static class Hand
        {
            /// <summary>
            /// Maximum number of cards a player can have in hand
            /// </summary>
            public const int MaxHandSize = 9;

            /// <summary>
            /// Initial number of cards drawn at game start for first player
            /// </summary>
            public const int InitialHandSizeFirstPlayer = 3;

            /// <summary>
            /// Initial number of cards drawn at game start for second player
            /// </summary>
            public const int InitialHandSizeSecondPlayer = 4;

            /// <summary>
            /// Cards drawn per turn (normal)
            /// </summary>
            public const int CardsPerTurn = 1;
        }

        /// <summary>
        /// Play Points (PP) related constants
        /// </summary>
        public static class PlayPoints
        {
            /// <summary>
            /// Maximum play points a player can have
            /// </summary>
            public const int MaxPlayPoints = 10;

            /// <summary>
            /// Initial play points at game start
            /// </summary>
            public const int InitialPlayPoints = 0;

            /// <summary>
            /// Play points gained per turn (normal)
            /// </summary>
            public const int PlayPointsPerTurn = 1;
        }

        /// <summary>
        /// Evolution related constants
        /// </summary>
        public static class Evolution
        {
            /// <summary>
            /// Evolution points for first player
            /// </summary>
            public const int FirstPlayerEvolutionPoints = 2;

            /// <summary>
            /// Evolution points for second player
            /// </summary>
            public const int SecondPlayerEvolutionPoints = 3;

            /// <summary>
            /// Turn when first player can start evolving
            /// </summary>
            public const int FirstPlayerEvolutionStartTurn = 5;

            /// <summary>
            /// Turn when second player can start evolving
            /// </summary>
            public const int SecondPlayerEvolutionStartTurn = 4;

            /// <summary>
            /// Cost in evolution points to evolve a unit
            /// </summary>
            public const int EvolutionPointCost = 1;
        }

        /// <summary>
        /// Game timing and turn related constants
        /// </summary>
        public static class Timing
        {
            /// <summary>
            /// Default turn timeout in seconds
            /// </summary>
            public const int DefaultTurnTimeoutSeconds = 90;

            /// <summary>
            /// Maximum number of turns before forced draw
            /// </summary>
            public const int MaxTurnsBeforeDraw = 50;
        }

        /// <summary>
        /// Health and damage related constants
        /// </summary>
        public static class Health
        {
            /// <summary>
            /// Maximum health a player can have
            /// </summary>
            public const int PlayerMaxHealth = 20;
        }

        /// <summary>
        /// Card limits and restrictions
        /// </summary>
        public static class Cards
        {
            /// <summary>
            /// Maximum number of copies of a single card in a deck
            /// </summary>
            public const int MaxCopiesPerCard = 3;

            /// <summary>
            /// Standard deck size
            /// </summary>
            public const int StandardDeckSize = 40;

            /// <summary>
            /// Minimum deck size
            /// </summary>
            public const int MinDeckSize = 40;

            /// <summary>
            /// Maximum deck size
            /// </summary>
            public const int MaxDeckSize = 40;

            /// <summary>
            /// Leader card ID used for player leaders
            /// </summary>
            public const int LeaderCardId = 0;
        }

        /// <summary>
        /// AI behavior constants
        /// </summary>
        public static class AI
        {
            /// <summary>
            /// Default AI thinking time in milliseconds
            /// </summary>
            public const int DefaultThinkingTimeMs = 2000;

            /// <summary>
            /// Maximum AI thinking time in milliseconds
            /// </summary>
            public const int MaxThinkingTimeMs = 5000;
        }

        /// <summary>
        /// Network and connection constants
        /// </summary>
        public static class Network
        {
            /// <summary>
            /// Default connection timeout in seconds
            /// </summary>
            public const int DefaultConnectionTimeoutSeconds = 30;

            /// <summary>
            /// Maximum reconnection attempts
            /// </summary>
            public const int MaxReconnectionAttempts = 3;
        }

        /// <summary>
        /// Game setup and initialization constants
        /// </summary>
        public static class Game
        {
            /// <summary>
            /// Maximum number of players per game
            /// </summary>
            public const int MaxPlayersPerGame = 2;

            /// <summary>
            /// Minimum number of players to start a game
            /// </summary>
            public const int MinPlayersToStart = 2;

            /// <summary>
            /// Default test deck size for development
            /// </summary>
            public const int TestDeckSize = 20;
        }
    }
}

