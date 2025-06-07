using ShadowverseBeyond.Server.Domain.Entities; // For GameState or related entities if needed directly
using ShadowverseBeyond.Server.Dtos; // For GameStateDto
using ShadowverseBeyond.Server.Domain.Events; // Added for IGameEvent

namespace ShadowverseBeyond.Server.Domain.Query
{
    /// <summary>
    /// 为卡牌查询执行提供必要的上下文信息。
    /// </summary>
    public record QueryContext
    {
        /// <summary>
        /// 发起查询的玩家的 ID。
        /// 用于解析 SourcePoolType 中的 "Self" 和 "Opponent"。
        /// </summary>
        public string RequestingPlayerId { get; init; } = string.Empty;

        /// <summary>
        /// 执行查询时相关的游戏状态快照。
        /// 注意：这应该是一个不可变的数据传输对象（DTO）或状态的深拷贝，
        /// 以防止查询逻辑意外修改实时游戏状态。
        /// </summary>
        public GameStateDto CurrentGameState { get; init; } = new();
        /// <summary>
        /// 触发此查询的游戏事件（如果适用）。
        /// 例如，当一个卡牌的 "OnPlay" 触发器执行查询时，这里可以包含 CardPlayedEvent。
        /// </summary>
        public IGameEvent? TriggeringEvent { get; init; }

        // 可以根据需要添加其他上下文信息，例如：
        // - 当前回合数
        // - 当前阶段
        // - 触发查询的卡牌实例 ID (如果适用)
        // public Guid? TriggeringCardInstanceId { get; init; }
    }
}