// File: Server/Services/Abstractions/IMatchService.cs
//
// 相关文档:
// - 多人联机方案A设计: 内存房间 + JoinCode/Token
using System;
using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Services.Abstractions
{
    public interface IMatchService
    {
        MatchOperationResult Create(Guid? hostPlayerId = null, bool useAiFallback = false, TimeSpan? ttl = null);
        MatchOperationResult TryJoin(string gameId, string joinCode);
        MatchOperationResult MarkReady(string gameId, Guid playerId);
        MatchOperationResult Start(string gameId, Guid playerId);
        MatchOperationResult Get(string gameId);
        MatchOperationResult ValidateConnection(string gameId, Guid playerId, string token);
        void Close(string gameId, string reason);
        void CleanupExpired();
    }

    public enum MatchOperationError
    {
        None = 0,
        NotFound = 1,
        InvalidJoinCode = 2,
        AlreadyFull = 3,
        AlreadyStarted = 4,
        AlreadyClosed = 5,
        Expired = 6,
        NotParticipant = 7,
        NotReady = 8,
        Forbidden = 9
    }

    public class MatchOperationResult
    {
        public bool Success { get; init; }
        public MatchOperationError Error { get; init; } = MatchOperationError.None;
        public string? Message { get; init; }
        public MatchRoom? Room { get; init; }

        public static MatchOperationResult Ok(MatchRoom room) => new MatchOperationResult { Success = true, Room = room };

        public static MatchOperationResult Fail(MatchOperationError error, string? message = null, MatchRoom? room = null) =>
            new MatchOperationResult
            {
                Success = false,
                Error = error,
                Message = message,
                Room = room
            };
    }
}
