// File: Server/Services/Abstractions/IMatchService.cs
//
// 相关文档:
// - 多人联机方案A设计: 内存房间 + JoinCode/Token
using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services.Abstractions
{
    public interface IMatchService
    {
        event Action<string>? GameDetached;
        MatchOperationResult Create(Guid? hostPlayerId = null, bool useAiFallback = false, TimeSpan? ttl = null, DevMatchConfig? devMatchConfig = null, bool isPublic = true, GameMode mode = GameMode.Standard);
        MatchOperationResult TryJoin(string roomId, string? joinCode, Guid? guestPlayerId = null, string? existingGuestToken = null, bool allowPublicWithoutCode = false);
        MatchOperationResult TryJoinByCode(string joinCode, Guid? guestPlayerId = null, string? existingGuestToken = null);
        MatchOperationResult MarkReady(string roomId, Guid playerId, bool ready = true, string? token = null);
        MatchOperationResult SubmitDeckSelection(string roomId, Guid playerId, MatchDeckSelection selection, MatchDeckSelection? aiSelection = null, string? token = null);
        MatchOperationResult Start(string roomId, Guid playerId, string? token = null);
        MatchOperationResult Get(string roomId);
        IReadOnlyList<MatchRoom> ListCurrentRooms();
        MatchOperationResult ValidateConnection(string roomId, Guid playerId, string token);
        MatchOperationResult RefreshExpiration(string roomId, Guid playerId);
        MatchOperationResult GetByGameId(string gameId);
        bool TryGetRoomIdByGameId(string gameId, out string roomId);
        MatchOperationResult Leave(string roomId, Guid playerId, string? hostLeaveReason = null, string? token = null);
        MatchOperationResult KickGuest(string roomId, Guid hostPlayerId, string hostToken, Guid? targetGuestPlayerId = null);
        MatchOperationResult CompleteGame(string gameId, Guid? winnerPlayerId);
        void Close(string roomId, string reason);
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
        Forbidden = 9,
        MissingDeckSelection = 10
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
