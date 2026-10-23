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
        /// Emblem zone related constants
        /// </summary>
        public static class Emblem
        {
            /// <summary>
            /// Maximum number of emblems a player can have
            /// </summary>
            public const int MaxEmblemSize = 5;
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
            public const int InitialHandSizeFirstPlayer = 4;

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

            /// <summary>
            /// PP+1 boost amount for the second player.
            /// </summary>
            public const int PpBoostAmount = 1;

            /// <summary>
            /// PP+1 uses available per phase (before and after the reset turn).
            /// </summary>
            public const int PpBoostUsesPerPhase = 1;

            /// <summary>
            /// Maximum PP overflow allowed by PP+1.
            /// </summary>
            public const int PpBoostOverflowAmount = 1;

            /// <summary>
            /// The second player's PP+1 availability resets at the start of this turn.
            /// </summary>
            public const int PpBoostResetTurn = 6;
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
            public const int SecondPlayerEvolutionPoints = 2;

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
        /// Super evolution related constants
        /// </summary>
        public static class SuperEvolution
        {
            /// <summary>
            /// Super evolution points for first player
            /// </summary>
            public const int FirstPlayerSuperEvolutionPoints = 2;

            /// <summary>
            /// Super evolution points for second player
            /// </summary>
            public const int SecondPlayerSuperEvolutionPoints = 2;

            /// <summary>
            /// Turn when first player can start super evolving
            /// </summary>
            public const int FirstPlayerSuperEvolutionStartTurn = 7;

            /// <summary>
            /// Turn when second player can start super evolving
            /// </summary>
            public const int SecondPlayerSuperEvolutionStartTurn = 6;

            /// <summary>
            /// Cost in super evolution points to super evolve a unit
            /// </summary>
            public const int SuperEvolutionPointCost = 1;
        }

        /// <summary>
        /// Game timing and turn related constants
        /// </summary>
        public static class Timing
        {
            /// <summary>
            /// Default turn timeout in seconds
            /// </summary>
            public const int DefaultTurnTimeoutSeconds = 180;

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

        public static class Tribes
        {
            public const string MatchAll = "全部";
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
            public const int DefaultConnectionTimeoutSeconds = 180;

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

        /// <summary>
        /// System Ability ID constants
        /// </summary>
        public static class SystemAbilityIds
        {
            // General System Abilities
            public const long PlayCardAbilityId = -1L;
            public const long AttackAbilityId = -2L;
            public const long EvolveAbilityId = -3L;
            public const long EndTurnAbilityId = -4L;
            public const long MulliganAbilityId = -5L;
            public const long StartTurnAbilityId = -6L; // Example for a common system ability
            public const long EarthSigilSystemAbilityId = -7L;

            // Add more system ability IDs as needed, ensuring they are unique.
            // Consider a scheme for ID generation or assignment if many system abilities are anticipated.
            // For example, negative numbers for core system abilities, specific ranges for others.
        }

        /// <summary>
        /// Constants and helpers related to Ability triggering logic
        /// </summary>
        public static class AbilityTrigger
        {
            /// <summary>
            /// Maximum number of Before intercept iterations for a single event.
            /// </summary>
            public const int BeforeInterceptMaxIterations = 8;

            /// <summary>
            /// Maximum recursion depth while processing events in a single transaction.
            /// </summary>
            public const int MaxEventRecursionDepth = 256;

            /// <summary>
            /// Maximum times the same event signature can be processed in a single transaction.
            /// </summary>
            public const int MaxEventSignatureRepeats = 128;

            /// <summary>
            /// Gets the priority value for a given card zone and ownership context.
            /// Lower numbers have higher priority.
            /// </summary>
            /// <param name="zone">The card zone where the ability's source card resides.</param>
            /// <param name="isMyTurn">True if the ability's owner is the active player.</param>
            /// <returns>An integer representing the priority.</returns>
            public static int GetZonePriority(Dtos.Enums.CardZone zone, bool isMyTurn)
            {
                // The priority is defined as: Field > Hand > Deck > Graveyard.
                // Within the same zone, the active player's abilities (isMyTurn = true) have priority.
                return (zone, isMyTurn) switch
                {
                    (Dtos.Enums.CardZone.Hand, true) => 1, // My Hand
                    (Dtos.Enums.CardZone.Emblem, true) => 2,
                    (Dtos.Enums.CardZone.Field, true) => 3, // My Field
                    (Dtos.Enums.CardZone.Hand, false) => 4, // Opponent's Hand
                    (Dtos.Enums.CardZone.Emblem, false) => 5,
                    (Dtos.Enums.CardZone.Field, false) => 6, // Opponent's Field

                    // Deck
                    (Dtos.Enums.CardZone.Deck, true) => 7, // My Deck
                    (Dtos.Enums.CardZone.Deck, false) => 8, // Opponent's Deck

                    // Graveyard
                    (Dtos.Enums.CardZone.Graveyard, true) => 9, // My Graveyard
                    (Dtos.Enums.CardZone.Graveyard, false) => 10, // Opponent's Graveyard

                    // Other zones have the lowest priority
                    _ => 99
                };
            }
        }
    }
}
