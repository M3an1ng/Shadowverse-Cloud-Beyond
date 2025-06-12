//
// 相关文档:
// - 游戏事件系统: 文档/05-游戏系统/04-事件驱动架构.md
// - 回合管理系统: 文档/05-游戏系统/03-回合管理系统.md
//
using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that the game has officially started.
    /// This event is triggered after both players complete their mulligan phase.
    /// </summary>
    public class GameStartedEventDto : GameEventDto
    {
        /// <summary>
        /// The unique identifier of the game that has started.
        /// </summary>
        public string GameId { get; set; } = string.Empty;

        /// <summary>
        /// The identifier of the player who will take the first turn.
        /// </summary>
        public Guid FirstPlayerId { get; set; }

        /// <summary>
        /// The identifier of the second player.
        /// </summary>
        public Guid SecondPlayerId { get; set; }

        /// <summary>
        /// Timestamp when the game started (UTC).
        /// </summary>
        public DateTime GameStartTime { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GameStartedEventDto"/> class.
        /// </summary>
        /// <param name="gameId">The unique identifier of the game.</param>
        /// <param name="firstPlayerId">The ID of the player taking the first turn.</param>
        /// <param name="secondPlayerId">The ID of the second player.</param>
        public GameStartedEventDto(string gameId, Guid firstPlayerId, Guid secondPlayerId)
            : base(firstPlayerId, "GameStarted") // Use first player as source
        {
            GameId = gameId;
            FirstPlayerId = firstPlayerId;
            SecondPlayerId = secondPlayerId;
            GameStartTime = DateTime.UtcNow;
        }
    }
}
