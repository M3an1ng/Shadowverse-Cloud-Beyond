using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using System;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Provides context information for game event processing and ability execution.
    /// Contains the current game state and player information needed for event processing.
    /// </summary>
    public class GameContext
    {
        /// <summary>
        /// The current game state snapshot.
        /// </summary>
        public GameStateDto CurrentGameState { get; }

        /// <summary>
        /// The player who is currently taking their turn.
        /// </summary>
        public PlayerStateDto CurrentPlayer { get; }

        /// <summary>
        /// The opponent of the current player.
        /// </summary>
        public PlayerStateDto OpponentPlayer { get; }

        /// <summary>
        /// The unique identifier of the current game.
        /// </summary>
        public string GameId => CurrentGameState.GameId;

        /// <summary>
        /// Initializes a new instance of the GameContext class.
        /// </summary>
        /// <param name="gameState">The current game state.</param>
        /// <param name="currentPlayer">The player currently taking their turn.</param>
        /// <param name="opponentPlayer">The opponent player.</param>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
        public GameContext(GameStateDto gameState, PlayerStateDto currentPlayer, PlayerStateDto opponentPlayer)
        {
            CurrentGameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
            CurrentPlayer = currentPlayer ?? throw new ArgumentNullException(nameof(currentPlayer));
            OpponentPlayer = opponentPlayer ?? throw new ArgumentNullException(nameof(opponentPlayer));
        }

        /// <summary>
        /// Gets a player by their ID.
        /// </summary>
        /// <param name="playerId">The player ID to search for.</param>
        /// <returns>The player state if found, null otherwise.</returns>
        public PlayerStateDto? GetPlayer(string playerId)
        {
            if (CurrentPlayer.PlayerId == playerId)
                return CurrentPlayer;
            if (OpponentPlayer.PlayerId == playerId)
                return OpponentPlayer;
            return null;
        }

        /// <summary>
        /// Gets the opponent of the specified player.
        /// </summary>
        /// <param name="playerId">The player ID to get the opponent for.</param>
        /// <returns>The opponent player state if found, null otherwise.</returns>
        public PlayerStateDto? GetOpponent(string playerId)
        {
            if (CurrentPlayer.PlayerId == playerId)
                return OpponentPlayer;
            if (OpponentPlayer.PlayerId == playerId)
                return CurrentPlayer;
            return null;
        }

        /// <summary>
        /// Finds a card instance by its instance ID across all zones and players.
        /// </summary>
        /// <param name="instanceId">The instance ID to search for.</param>
        /// <returns>The card instance if found, null otherwise.</returns>
        public CardInstance? FindCardInstance(string instanceId)
        {
            // Search in current player's zones
            var card = FindCardInPlayer(CurrentPlayer, instanceId);
            if (card != null) return card;

            // Search in opponent's zones
            return FindCardInPlayer(OpponentPlayer, instanceId);
        }

        private CardInstance? FindCardInPlayer(PlayerStateDto player, string instanceId)
        {
            // Search in hand
            var handCard = player.Hand.FirstOrDefault(c => c.InstanceId == instanceId);
            if (handCard != null) return ConvertToCardInstance(handCard);

            // Search in field
            var fieldCard = player.Field.FirstOrDefault(c => c.InstanceId == instanceId);
            if (fieldCard != null) return ConvertToCardInstance(fieldCard);

            // Search in deck
            var deckCard = player.Deck.FirstOrDefault(c => c.InstanceId == instanceId);
            if (deckCard != null) return ConvertToCardInstance(deckCard);

            // Search in graveyard
            var graveyardCard = player.Graveyard.FirstOrDefault(c => c.InstanceId == instanceId);
            if (graveyardCard != null) return ConvertToCardInstance(graveyardCard);

            return null;
        }

        private CardInstance ConvertToCardInstance(CardInstanceDto dto)
        {
            return new CardInstance(dto.InstanceId, dto.CardId, dto.ControllerPlayerId)
            {
                CurrentZone = dto.Zone,
                CardType = dto.Type,
                CurrentCost = dto.Cost,
                CurrentAttack = dto.Attack,
                CurrentHealth = dto.CurrentHealth,
                MaxHealth = dto.MaxHealth,
                CanAttack = dto.CanAttack,
                CanEvolve = dto.CanEvolve,
                IsEvolved = dto.IsEvolved,
                AttacksThisTurn = dto.AttacksMadeThisTurn
            };
        }
    }
}
