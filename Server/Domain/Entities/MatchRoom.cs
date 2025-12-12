// File: Server/Domain/Entities/MatchRoom.cs
//
// 相关文档:
// - 多人联机方案A设计: 内存房间 + JoinCode/Token
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
using System;

namespace ShadowverseBeyond.Server.Domain.Entities
{
    /// <summary>
    /// 简单的内存房间模型，用于局域网/本地多开测试的两人对战。
    /// 不持久化，进程重启后失效。
    /// </summary>
    public class MatchRoom
    {
        public string GameId { get; init; } = string.Empty;
        public Guid HostPlayerId { get; init; }
        public Guid? GuestPlayerId { get; set; }
        public string JoinCode { get; init; } = string.Empty;
        public string HostToken { get; init; } = string.Empty;
        public string? GuestToken { get; set; }
        public MatchStatus Status { get; set; } = MatchStatus.Waiting;
        public bool HostReady { get; set; }
        public bool GuestReady { get; set; }
        public bool UseAiFallback { get; init; }
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; }
        public string? ClosedReason { get; set; }
    }

    public enum MatchStatus
    {
        Waiting = 0,
        Ready = 1,
        Started = 2,
        Closed = 3
    }
}
