// File: Server/Dtos/GameStateDto.cs
//
// 相关文档:
// - 状态管理: 文档/03-前端架构/02-状态管理.md
// - API设计: 文档/06-API文档/01-端点总览.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
//
using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;
namespace ShadowverseBeyond.Server.Dtos // Assuming a base DTO namespace
{
    /// <summary>
    /// Data Transfer Object representing the entire state of the game at a specific point in time.
    /// This is typically sent to clients upon connection or after major state changes.
    /// </summary>
    public record GameStateDto
    {
        public string GameId { get; init; } = string.Empty; // Identifier for the game session
        public Guid CurrentPlayerId { get; set; } = Guid.Empty; // Whose turn it is
        // 【架构重构】删除全局TurnNumber，前端应从当前玩家的PlayerStateDto.TurnNumber获取回合数
        public GamePhase Phase { get; set; } = GamePhase.Initialization; // Changed type to GamePhase Enum and updated default

        public Guid FirstPlayerId { get; set; } = Guid.Empty; // ID of the player who went first
        public GameStatus GameStatus { get; set; } = GameStatus.Starting; // Current status of the game (e.g., Starting, InProgress, Finished)
        public List<PlayerStateDto> Players { get; set; } = new(); // State for both players

        public string? GameMessage { get; set; } // Optional message for the UI (e.g., "Player 1's Turn")
        public bool IsGameOver { get; set; } // Flag indicating game end
        public string? WinnerId { get; set; } // ID of the winner if game is over

        // Consider adding other global state if needed, e.g., pending actions, history log snippet

        /// <summary>
        /// Finds a card instance by its instance ID across all zones and players.
        /// </summary>
        /// <param name="instanceId">The instance ID to search for.</param>
        /// <returns>The card instance DTO if found, null otherwise.</returns>
        public CardInstanceDto? FindCardInstanceById(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return null;

            // Search in all players' zones
            foreach (var player in Players)
            {
                var cardDto = FindCardDtoInPlayer(player, instanceId);
                if (cardDto != null) return cardDto;
            }

            return null;
        }

        private CardInstanceDto? FindCardDtoInPlayer(PlayerStateDto player, string instanceId)
        {
            // Search in hand
            var handCardDto = player.Hand.FirstOrDefault(c => c.InstanceId.ToString() == instanceId);
            if (handCardDto != null) return handCardDto;

            // Search in field
            var fieldCardDto = player.Field.FirstOrDefault(c => c.InstanceId.ToString() == instanceId);
            if (fieldCardDto != null) return fieldCardDto;

            // Search in deck
            var deckCardDto = player.Deck.FirstOrDefault(c => c.InstanceId.ToString() == instanceId);
            if (deckCardDto != null) return deckCardDto;

            // Search in graveyard
            var graveyardCardDto = player.Graveyard.FirstOrDefault(c => c.InstanceId.ToString() == instanceId);
            if (graveyardCardDto != null) return graveyardCardDto;

            // Search leader card
            if (player.LeaderCard?.InstanceId.ToString() == instanceId)
                return player.LeaderCard;

            return null;
        }

        // Removed ConvertToCardInstance method as DTOs should not create domain entities.
        // If conversion is needed, it should happen in a service layer or via AutoMapper profiles
        // that have access to the necessary context (like GameState or ICardFactory).

        /// <summary>
        /// Gets a player state by player ID.
        /// </summary>
        /// <param name="playerId">The player ID to search for.</param>
        /// <returns>The player state if found, null otherwise.</returns>
        public PlayerStateDto? GetPlayerState(Guid playerId)
        {
            return Players.FirstOrDefault(p => p.PlayerId == playerId);
        }

        /// <summary>
        /// Gets the first player state (Player1).
        /// </summary>
        public PlayerStateDto? Player1 => Players.Count > 0 ? Players[0] : null;

        /// <summary>
        /// Gets the second player state (Player2).
        /// </summary>
        public PlayerStateDto? Player2 => Players.Count > 1 ? Players[1] : null;
    }
}