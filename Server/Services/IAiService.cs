// Server/Services/IAiService.cs
using ShadowverseBeyond.Server.Dtos; // For GameStateDto
using ShadowverseBeyond.Server.Dtos.SignalR.Actions; // For Action DTOs
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Services
{
    /// <summary>
    /// Defines the contract for the AI decision-making service.
    /// </summary>
    public interface IAiService
    {
        /// <summary>
        /// Decides which cards the AI should replace during the mulligan phase.
        /// </summary>
        /// <param name="gameState">The current state of the game.</param>
        /// <param name="aiPlayerId">The ID of the AI player.</param>
        /// <returns>A list of card UIDs to be replaced.</returns>
        Task<List<string>> DecideMulliganAsync(GameStateDto gameState, string aiPlayerId);

        /// <summary>
        /// Decides the next card the AI should play. (V0.1: At most one card per turn).
        /// </summary>
        /// <param name="gameState">The current state of the game.</param>
        /// <param name="aiPlayerId">The ID of the AI player.</param>
        /// <returns>A PlayCardActionDto representing the card to play, or null if no card should be played.</returns>
        Task<PlayCardActionDto?> DecidePlayCardAsync(GameStateDto gameState, string aiPlayerId);

        /// <summary>
        /// Decides which unit the AI should evolve. (V0.1: At most one evolution per turn).
        /// </summary>
        /// <param name="gameState">The current state of the game.</param>
        /// <param name="aiPlayerId">The ID of the AI player.</param>
        /// <returns>An EvolveActionDto representing the unit to evolve, or null if no evolution should occur.</returns>
        Task<EvolveActionDto?> DecideEvolveAsync(GameStateDto gameState, string aiPlayerId);

        /// <summary>
        /// Decides all attack actions the AI should perform this turn.
        /// </summary>
        /// <param name="gameState">The current state of the game.</param>
        /// <param name="aiPlayerId">The ID of the AI player.</param>
        /// <returns>A list of AttackActionDto representing the attacks to perform.</returns>
        Task<List<AttackActionDto>> DecideAttacksAsync(GameStateDto gameState, string aiPlayerId);

        /// <summary>
        /// Determines if the given player ID belongs to an AI player.
        /// </summary>
        /// <param name="playerId">The player ID to check.</param>
        /// <returns>True if the player is an AI, false otherwise.</returns>
        Task<bool> IsAiPlayer(string playerId); // Added method signature

        /// <summary>
        /// 请求 AI 服务开始处理指定 AI 玩家回合的行动。
        /// 这通常在游戏状态表明轮到 AI 行动时被调用。
        /// 实现应异步处理回合执行。
        /// </summary>
        /// <param name="gameId">游戏 ID。</param>
        /// <param name="aiPlayerId">轮到其行动的 AI 玩家 ID。</param>
        /// <returns>表示异步操作启动的任务。</returns>
        Task RequestAiActionAsync(string gameId, string aiPlayerId);
    }
}