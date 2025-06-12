//
// 相关文档:
// - 游戏事件系统: 文档/05-游戏系统/04-事件驱动架构.md
// - 回合管理系统: 文档/05-游戏系统/03-回合管理系统.md
//
using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that the game has officially ended.
    /// This event is triggered when the game reaches a conclusion (victory, defeat, draw, etc.).
    /// </summary>
    public class GameEndedEventDto : GameEventDto
    {
        /// <summary>
        /// The unique identifier of the game that has ended.
        /// </summary>
        public string GameId { get; set; } = string.Empty;

        /// <summary>
        /// The identifier of the winning player (if any).
        /// Can be null in case of a draw or other non-victory endings.
        /// </summary>
        public Guid? WinnerId { get; set; }

        /// <summary>
        /// The identifier of the losing player (if any).
        /// Can be null in case of a draw or other non-defeat endings.
        /// </summary>
        public Guid? LoserId { get; set; }

        /// <summary>
        /// The reason why the game ended.
        /// Examples: "LeaderDefeated", "Surrender", "Timeout", "Draw", "Disconnect"
        /// </summary>
        public string EndReason { get; set; } = "Unknown";

        /// <summary>
        /// Timestamp when the game ended (UTC).
        /// </summary>
        public DateTime GameEndTime { get; set; }

        /// <summary>
        /// Additional details about the game ending (optional).
        /// </summary>
        public string? Details { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GameEndedEventDto"/> class.
        /// </summary>
        /// <param name="gameId">The unique identifier of the game.</param>
        /// <param name="winnerId">The ID of the winning player (can be null).</param>
        /// <param name="loserId">The ID of the losing player (can be null).</param>
        /// <param name="endReason">The reason why the game ended.</param>
        /// <param name="details">Additional details about the game ending.</param>
        public GameEndedEventDto(
            string gameId,
            Guid? winnerId,
            Guid? loserId,
            string endReason = "Unknown",
            string? details = null)
            : base(winnerId ?? loserId ?? Guid.Empty, "GameEnded") // Use winner or loser as source, fallback to Empty
        {
            GameId = gameId;
            WinnerId = winnerId;
            LoserId = loserId;
            EndReason = endReason;
            Details = details;
            GameEndTime = DateTime.UtcNow;
        }
    }
}
