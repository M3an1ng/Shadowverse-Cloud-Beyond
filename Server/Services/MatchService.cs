// File: Server/Services/MatchService.cs
//
// 相关文档:
// - 多人联机方案A: 内存房间 + JoinCode/Token
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services
{
    /// <summary>
    /// 内存实现的房间服务，专用于局域网/本地多开测试。
    /// 不持久化，进程重启后房间失效。
    /// </summary>
    public partial class MatchService : IMatchService
    {
        public event Action<string>? GameDetached;
        private readonly ConcurrentDictionary<string, MatchRoom> _rooms = new();
        private readonly ConcurrentDictionary<string, string> _gameToRoom = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _creationGate = new();
        private readonly TimeSpan _defaultTtl = RoomLifetime.DefaultTtl;
        private readonly TimeSpan _closedRetention = RoomLifetime.ClosedRetention;
        private readonly ILogger<MatchService> _logger;

        public MatchService(ILogger<MatchService> logger)
        {
            _logger = logger;
        }

        public MatchOperationResult Create(Guid? hostPlayerId = null, bool useAiFallback = false, TimeSpan? ttl = null, DevMatchConfig? devMatchConfig = null, bool isPublic = true, GameMode mode = GameMode.Standard)
        {
            CleanupExpired();

            var roomId = Guid.NewGuid().ToString("D");
            var hostId = hostPlayerId.HasValue && hostPlayerId.Value != Guid.Empty ? hostPlayerId.Value : Guid.NewGuid();
            var roomTtl = ttl.HasValue && ttl.Value > TimeSpan.Zero ? ttl.Value : _defaultTtl;
            MatchRoom room;
            lock (_creationGate)
            {
                room = new MatchRoom
                {
                    RoomId = roomId,
                    HostPlayerId = hostId,
                    JoinCode = GenerateUniqueJoinCode(),
                    HostToken = GenerateToken(),
                    Mode = mode,
                    UseAiFallback = useAiFallback,
                    IsPublic = isPublic,
                    RoomTtl = roomTtl,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.Add(roomTtl),
                    ActiveGameId = null,
                    Round = 0,
                    DevMatchConfig = CloneConfig(devMatchConfig)
                };

                if (!_rooms.TryAdd(roomId, room))
                {
                    return MatchOperationResult.Fail(MatchOperationError.Forbidden, "创建房间失败，请重试。");
                }
            }

            _logger.LogInformation("[MatchService] Created room {RoomId} with joinCode {JoinCode} (Mode={Mode}, IsPublic={IsPublic})", roomId, room.JoinCode, mode, isPublic);
            return MatchOperationResult.Ok(Clone(room));
        }

        public MatchOperationResult TryJoin(string roomId, string? joinCode, Guid? guestPlayerId = null, string? existingGuestToken = null, bool allowPublicWithoutCode = false)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "房间不存在。");
            }

            lock (room)
            {
                if (IsExpired(room))
                {
                    CloseInternal(roomId, "房间已过期");
                    return MatchOperationResult.Fail(MatchOperationError.Expired, "房间已过期。");
                }

                if (room.Status == MatchStatus.Closed)
                {
                    return MatchOperationResult.Fail(MatchOperationError.AlreadyClosed, room.ClosedReason ?? "房间已关闭。");
                }

                var normalizedCode = joinCode?.Trim() ?? string.Empty;
                var codeMatches = !string.IsNullOrWhiteSpace(normalizedCode) &&
                                  string.Equals(room.JoinCode, normalizedCode, StringComparison.OrdinalIgnoreCase);
                var publicBypass = allowPublicWithoutCode && room.IsPublic && string.IsNullOrWhiteSpace(normalizedCode);

                if (!codeMatches && !publicBypass)
                {
                    return MatchOperationResult.Fail(MatchOperationError.InvalidJoinCode, "邀请码不正确。");
                }

                var requestedGuestId = guestPlayerId.HasValue && guestPlayerId.Value != Guid.Empty
                    ? guestPlayerId.Value
                    : (Guid?)null;

                // Idempotent rejoin: requires proving ownership of the existing guest seat token.
                if (room.GuestPlayerId.HasValue &&
                    requestedGuestId.HasValue &&
                    room.GuestPlayerId.Value == requestedGuestId.Value &&
                    !string.IsNullOrWhiteSpace(existingGuestToken) &&
                    string.Equals(room.GuestToken, existingGuestToken, StringComparison.Ordinal))
                {
                    room.ExpiresAt = DateTime.UtcNow.Add(room.RoomTtl);
                    _logger.LogInformation("[MatchService] Guest {GuestId} rejoined room {RoomId} with valid token.", requestedGuestId.Value, roomId);
                    return MatchOperationResult.Ok(Clone(room));
                }

                if (room.Status == MatchStatus.Started)
                {
                    return MatchOperationResult.Fail(MatchOperationError.AlreadyStarted, "房间已开始。");
                }

                if (room.GuestPlayerId.HasValue)
                {
                    return MatchOperationResult.Fail(MatchOperationError.AlreadyFull, "房间已满。");
                }

                var guestId = requestedGuestId ?? Guid.NewGuid();
                if (guestId == room.HostPlayerId)
                {
                    return MatchOperationResult.Fail(MatchOperationError.Forbidden, "玩家已在房间内。");
                }
                room.GuestPlayerId = guestId;
                room.GuestToken = GenerateToken();
                room.GuestReady = false;
                room.GuestDeckSelection = new MatchDeckSelection();
                room.HostWins = 0;
                room.GuestWins = 0;
                room.Draws = 0;
                room.LastWinnerPlayerId = null;
                room.ExpiresAt = DateTime.UtcNow.Add(room.RoomTtl);
                room.Status = MatchStatus.Waiting;

                _logger.LogInformation("[MatchService] Player {GuestId} joined room {RoomId}", guestId, roomId);
                return MatchOperationResult.Ok(Clone(room));
            }
        }

        public MatchOperationResult TryJoinByCode(string joinCode, Guid? guestPlayerId = null, string? existingGuestToken = null)
        {
            var normalizedCode = joinCode?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedCode))
            {
                return MatchOperationResult.Fail(MatchOperationError.InvalidJoinCode, "邀请码不能为空。");
            }

            CleanupExpired();

            MatchRoom? matchedRoom = null;
            foreach (var candidate in _rooms.Values)
            {
                lock (candidate)
                {
                    if (candidate.Status != MatchStatus.Closed &&
                        !IsExpired(candidate) &&
                        string.Equals(candidate.JoinCode, normalizedCode, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedRoom = candidate;
                        break;
                    }
                }
            }

            if (matchedRoom == null)
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "未找到对应该邀请码的房间。");
            }

            return TryJoin(matchedRoom.RoomId, normalizedCode, guestPlayerId, existingGuestToken, allowPublicWithoutCode: false);
        }

        public MatchOperationResult MarkReady(string roomId, Guid playerId, bool ready = true, string? token = null)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "房间不存在。");
            }

            lock (room)
            {
                if (IsExpired(room))
                {
                    CloseInternal(roomId, "房间已过期");
                    return MatchOperationResult.Fail(MatchOperationError.Expired, "房间已过期。");
                }

                if (!ValidateParticipantLocked(room, playerId, token, out var authFail))
                {
                    return authFail!;
                }

                if (room.Status == MatchStatus.Closed)
                {
                    return MatchOperationResult.Fail(MatchOperationError.AlreadyClosed, room.ClosedReason ?? "房间已关闭。");
                }

                if (room.Status == MatchStatus.Started)
                {
                    return MatchOperationResult.Fail(MatchOperationError.AlreadyStarted, "房间已开始。");
                }

                if (ready && !IsMirageRoom(room))
                {
                    if (playerId == room.HostPlayerId)
                    {
                        if (!room.HostDeckSelection.HasSelection)
                        {
                            return MatchOperationResult.Fail(MatchOperationError.MissingDeckSelection, "请先选择卡组后再准备。");
                        }
                        if (!room.GuestPlayerId.HasValue && room.UseAiFallback && !room.AiDeckSelection.HasSelection)
                        {
                            return MatchOperationResult.Fail(MatchOperationError.MissingDeckSelection, "请先选择 AI 卡组后再准备。");
                        }
                    }
                    else if (room.GuestPlayerId.HasValue && playerId == room.GuestPlayerId.Value)
                    {
                        if (!room.GuestDeckSelection.HasSelection)
                        {
                            return MatchOperationResult.Fail(MatchOperationError.MissingDeckSelection, "请先选择卡组后再准备。");
                        }
                    }
                }

                if (playerId == room.HostPlayerId)
                {
                    room.HostReady = ready;
                }
                else if (room.GuestPlayerId.HasValue && playerId == room.GuestPlayerId.Value)
                {
                    room.GuestReady = ready;
                }

                room.ExpiresAt = DateTime.UtcNow.Add(room.RoomTtl);
                if (room.HostReady && (room.GuestReady || (room.UseAiFallback && !room.GuestPlayerId.HasValue)))
                {
                    room.Status = MatchStatus.Ready;
                }
                else
                {
                    room.Status = MatchStatus.Waiting;
                }

                return MatchOperationResult.Ok(Clone(room));
            }
        }

        public MatchOperationResult SubmitDeckSelection(string roomId, Guid playerId, MatchDeckSelection selection, MatchDeckSelection? aiSelection = null, string? token = null)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "房间不存在。");
            }

            lock (room)
            {
                if (IsExpired(room))
                {
                    CloseInternal(roomId, "房间已过期");
                    return MatchOperationResult.Fail(MatchOperationError.Expired, "房间已过期。");
                }

                if (!ValidateParticipantLocked(room, playerId, token, out var authFail))
                {
                    return authFail!;
                }

                if (room.Status == MatchStatus.Closed)
                {
                    return MatchOperationResult.Fail(MatchOperationError.AlreadyClosed, room.ClosedReason ?? "房间已关闭。");
                }

                if (room.Status == MatchStatus.Started)
                {
                    return MatchOperationResult.Fail(MatchOperationError.AlreadyStarted, "房间已开始。");
                }

                if (playerId == room.HostPlayerId)
                {
                    room.HostDeckSelection = CloneSelection(selection);
                    if (aiSelection != null)
                    {
                        room.AiDeckSelection = CloneSelection(aiSelection);
                    }
                }
                else if (room.GuestPlayerId.HasValue && playerId == room.GuestPlayerId.Value)
                {
                    room.GuestDeckSelection = CloneSelection(selection);
                }

                if (room.GuestPlayerId.HasValue &&
                    room.HostDeckSelection.HasSelection &&
                    room.GuestDeckSelection.HasSelection &&
                    !AreDeckFormatsConsistent(room, out _))
                {
                    room.GuestReady = false;
                    room.Status = MatchStatus.Waiting;
                }

                room.ExpiresAt = DateTime.UtcNow.Add(room.RoomTtl);
                return MatchOperationResult.Ok(Clone(room));
            }
        }

        public MatchOperationResult Start(string roomId, Guid playerId, string? token = null)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "房间不存在。");
            }

            lock (room)
            {
                if (IsExpired(room))
                {
                    CloseInternal(roomId, "房间已过期");
                    return MatchOperationResult.Fail(MatchOperationError.Expired, "房间已过期。");
                }

                if (!ValidateParticipantLocked(room, playerId, token, out var authFail))
                {
                    return authFail!;
                }

                if (room.Status == MatchStatus.Closed)
                {
                    return MatchOperationResult.Fail(MatchOperationError.AlreadyClosed, room.ClosedReason ?? "房间已关闭。");
                }

                if (room.Status == MatchStatus.Started)
                {
                    return MatchOperationResult.Fail(MatchOperationError.AlreadyStarted, "房间已开始。");
                }

                var guestReady = room.GuestReady || (room.UseAiFallback && !room.GuestPlayerId.HasValue);
                if (!(room.HostReady && guestReady))
                {
                    return MatchOperationResult.Fail(MatchOperationError.NotReady, "双方未准备完成。");
                }

                if (!AreDecksReady(room))
                {
                    return MatchOperationResult.Fail(MatchOperationError.MissingDeckSelection, "卡组尚未选择完成。");
                }

                if (!AreDeckFormatsConsistent(room, out var mismatchReason))
                {
                    return MatchOperationResult.Fail(MatchOperationError.NotReady, mismatchReason);
                }

                if (!string.IsNullOrWhiteSpace(room.ActiveGameId))
                {
                    DetachGame(room.ActiveGameId);
                }

                var activeGameId = Guid.NewGuid().ToString("D");
                room.ActiveGameId = activeGameId;
                room.Round = Math.Max(1, room.Round + 1);
                _gameToRoom[activeGameId] = room.RoomId;
                room.Status = MatchStatus.Started;
                room.ExpiresAt = DateTime.UtcNow.Add(room.RoomTtl);

                _logger.LogInformation("[MatchService] Room {RoomId} started by {PlayerId}. ActiveGameId={GameId} Round={Round}",
                    roomId,
                    playerId,
                    activeGameId,
                    room.Round);
                return MatchOperationResult.Ok(Clone(room));
            }
        }

        public MatchOperationResult Get(string roomId)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "房间不存在。");
            }

            lock (room)
            {
                if (IsExpired(room))
                {
                    CloseInternal(roomId, "房间已过期");
                    return MatchOperationResult.Fail(MatchOperationError.Expired, "房间已过期。");
                }

                return MatchOperationResult.Ok(Clone(room));
            }
        }

        public IReadOnlyList<MatchRoom> ListCurrentRooms()
        {
            CleanupExpired();

            var rooms = new List<MatchRoom>();
            foreach (var room in _rooms.Values)
            {
                lock (room)
                {
                    if (room.Status != MatchStatus.Closed && !IsExpired(room))
                    {
                        rooms.Add(Clone(room));
                    }
                }
            }

            return rooms.OrderByDescending(room => room.Status == MatchStatus.Started)
                .ThenByDescending(room => room.CreatedAt).ToList();
        }

        public MatchOperationResult ValidateConnection(string roomId, Guid playerId, string token)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "房间不存在。");
            }

            lock (room)
            {
                if (IsExpired(room))
                {
                    CloseInternal(roomId, "房间已过期");
                    return MatchOperationResult.Fail(MatchOperationError.Expired, "房间已过期。");
                }

                var isHost = playerId == room.HostPlayerId && string.Equals(room.HostToken, token, StringComparison.Ordinal);
                var isGuest = room.GuestPlayerId.HasValue && playerId == room.GuestPlayerId.Value &&
                              string.Equals(room.GuestToken, token, StringComparison.Ordinal);

                if (!isHost && !isGuest)
                {
                    return MatchOperationResult.Fail(MatchOperationError.Forbidden, "Token 无效或不属于该房间。");
                }

                if (room.Status == MatchStatus.Closed)
                {
                    return MatchOperationResult.Fail(
                        MatchOperationError.AlreadyClosed,
                        room.ClosedReason ?? "房间已关闭。",
                        Clone(room));
                }

                return MatchOperationResult.Ok(Clone(room));
            }
        }

        public MatchOperationResult RefreshExpiration(string roomId, Guid playerId)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "房间不存在。");
            }

            lock (room)
            {
                if (IsExpired(room))
                {
                    CloseInternal(roomId, "房间已过期");
                    return MatchOperationResult.Fail(MatchOperationError.Expired, "房间已过期。");
                }

                if (room.Status == MatchStatus.Closed)
                {
                    return MatchOperationResult.Fail(MatchOperationError.AlreadyClosed, room.ClosedReason ?? "房间已关闭。", Clone(room));
                }

                if (!IsParticipant(room, playerId))
                {
                    return MatchOperationResult.Fail(MatchOperationError.NotParticipant, "玩家不在房间内。");
                }

                room.ExpiresAt = DateTime.UtcNow.Add(room.RoomTtl);
                return MatchOperationResult.Ok(Clone(room));
            }
        }

        public MatchOperationResult GetByGameId(string gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "对局不存在。");
            }

            if (!TryGetRoomIdByGameId(gameId, out var roomId))
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "房间不存在。");
            }

            return Get(roomId);
        }

        public bool TryGetRoomIdByGameId(string gameId, out string roomId)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                roomId = string.Empty;
                return false;
            }

            return _gameToRoom.TryGetValue(gameId, out roomId);
        }

        public MatchOperationResult Leave(string roomId, Guid playerId, string? hostLeaveReason = null, string? token = null)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "房间不存在。");
            }

            lock (room)
            {
                if (IsExpired(room))
                {
                    CloseInternal(roomId, "房间已过期");
                    return MatchOperationResult.Fail(MatchOperationError.Expired, "房间已过期。");
                }

                if (!ValidateParticipantLocked(room, playerId, token, out var authFail))
                {
                    return authFail!;
                }

                if (room.Status == MatchStatus.Closed)
                {
                    return MatchOperationResult.Fail(MatchOperationError.AlreadyClosed, room.ClosedReason ?? "房间已关闭。", Clone(room));
                }

                if (playerId == room.HostPlayerId)
                {
                    CloseInternal(roomId, hostLeaveReason ?? "房主已退出，房间已解散。");
                    return MatchOperationResult.Ok(Clone(room));
                }

                // Guest departure owns both the released seat and its active-game mapping.
                var activeGameId = room.ActiveGameId;
                room.ActiveGameId = null;
                room.GuestPlayerId = null;
                room.GuestToken = null;
                room.GuestReady = false;
                room.HostReady = false;
                room.GuestDeckSelection = new MatchDeckSelection();
                room.HostWins = 0;
                room.GuestWins = 0;
                room.Draws = 0;
                room.LastWinnerPlayerId = null;
                room.Status = MatchStatus.Waiting;
                room.ExpiresAt = DateTime.UtcNow.Add(room.RoomTtl);
                room.ClosedReason = null;

                if (!string.IsNullOrWhiteSpace(activeGameId))
                {
                    DetachGame(activeGameId);
                }

                _logger.LogInformation("[MatchService] Guest {PlayerId} left room {RoomId}. Seat released for rejoin.", playerId, roomId);
                return MatchOperationResult.Ok(Clone(room));
            }
        }

        public MatchOperationResult KickGuest(string roomId, Guid hostPlayerId, string hostToken, Guid? targetGuestPlayerId = null)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "房间不存在。");
            }

            lock (room)
            {
                if (IsExpired(room))
                {
                    CloseInternal(roomId, "房间已过期");
                    return MatchOperationResult.Fail(MatchOperationError.Expired, "房间已过期。");
                }

                if (room.Status == MatchStatus.Closed)
                {
                    return MatchOperationResult.Fail(MatchOperationError.AlreadyClosed, room.ClosedReason ?? "房间已关闭。", Clone(room));
                }

                if (room.Status == MatchStatus.Started)
                {
                    return MatchOperationResult.Fail(MatchOperationError.AlreadyStarted, "对战进行中无法踢出玩家。");
                }

                if (hostPlayerId != room.HostPlayerId ||
                    string.IsNullOrWhiteSpace(hostToken) ||
                    !string.Equals(room.HostToken, hostToken, StringComparison.Ordinal))
                {
                    return MatchOperationResult.Fail(MatchOperationError.Forbidden, "仅房主可执行踢人操作。");
                }

                if (!room.GuestPlayerId.HasValue)
                {
                    return MatchOperationResult.Fail(MatchOperationError.NotParticipant, "当前房间没有访客。");
                }

                if (targetGuestPlayerId.HasValue &&
                    targetGuestPlayerId.Value != Guid.Empty &&
                    room.GuestPlayerId.Value != targetGuestPlayerId.Value)
                {
                    return MatchOperationResult.Fail(MatchOperationError.NotParticipant, "目标访客已离开或已更换。");
                }

                var kickedGuestId = room.GuestPlayerId.Value;
                var activeGameId = room.ActiveGameId;
                room.ActiveGameId = null;
                room.GuestPlayerId = null;
                room.GuestToken = null;
                room.GuestReady = false;
                room.GuestDeckSelection = new MatchDeckSelection();
                room.HostWins = 0;
                room.GuestWins = 0;
                room.Draws = 0;
                room.LastWinnerPlayerId = null;
                if (room.Status == MatchStatus.Ready && !room.UseAiFallback)
                {
                    room.Status = MatchStatus.Waiting;
                }
                room.ExpiresAt = DateTime.UtcNow.Add(room.RoomTtl);

                if (!string.IsNullOrWhiteSpace(activeGameId))
                {
                    DetachGame(activeGameId);
                }

                _logger.LogInformation("[MatchService] Host {HostId} kicked guest {GuestId} from room {RoomId}.", hostPlayerId, kickedGuestId, roomId);
                return MatchOperationResult.Ok(Clone(room));
            }
        }

        public MatchOperationResult CompleteGame(string gameId, Guid? winnerPlayerId)
        {
            if (string.IsNullOrWhiteSpace(gameId) || !TryGetRoomIdByGameId(gameId, out var roomId))
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "房间不存在。");
            }

            if (!_rooms.TryGetValue(roomId, out var room))
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "房间不存在。");
            }

            lock (room)
            {
                if (room.Status == MatchStatus.Closed || IsExpired(room))
                {
                    return MatchOperationResult.Fail(MatchOperationError.AlreadyClosed, room.ClosedReason ?? "房间已关闭。");
                }

                if (!string.Equals(room.ActiveGameId, gameId, StringComparison.OrdinalIgnoreCase))
                {
                    return MatchOperationResult.Ok(Clone(room));
                }

                if (string.Equals(room.LastCompletedGameId, gameId, StringComparison.OrdinalIgnoreCase))
                {
                    return MatchOperationResult.Ok(Clone(room));
                }

                room.LastCompletedGameId = gameId;

                if (winnerPlayerId.HasValue && winnerPlayerId.Value == room.HostPlayerId)
                {
                    room.HostWins++;
                    room.LastWinnerPlayerId = room.HostPlayerId;
                }
                else if (winnerPlayerId.HasValue && room.GuestPlayerId.HasValue && winnerPlayerId.Value == room.GuestPlayerId.Value)
                {
                    room.GuestWins++;
                    room.LastWinnerPlayerId = room.GuestPlayerId.Value;
                }
                else
                {
                    room.Draws++;
                    room.LastWinnerPlayerId = null;
                }

                var isTakeTwoRoom = room.HostDeckSelection?.GeneratedDraftCardIds != null &&
                                    room.HostDeckSelection.GeneratedDraftCardIds.Length > 0;
                var canRematchInRoom = room.GuestPlayerId.HasValue &&
                                       !room.UseAiFallback &&
                                       !room.IsResumedRoom &&
                                       !isTakeTwoRoom;

                if (canRematchInRoom)
                {
                    room.Status = MatchStatus.Waiting;
                    room.HostReady = false;
                    room.GuestReady = false;
                    room.ExpiresAt = DateTime.UtcNow.Add(room.RoomTtl);
                }

                return MatchOperationResult.Ok(Clone(room));
            }
        }

        public void Close(string roomId, string reason)
        {
            CloseInternal(roomId, reason);
        }

        public void CleanupExpired()
        {
            foreach (var (roomId, room) in _rooms)
            {
                lock (room)
                {
                    if (!IsExpired(room))
                    {
                        continue;
                    }

                    if (room.Status == MatchStatus.Closed)
                    {
                        _rooms.TryRemove(roomId, out _);
                    }
                    else
                    {
                        CloseInternal(roomId, "房间已过期");
                    }
                }
            }
        }

        private void CloseInternal(string roomId, string reason)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                return;
            }

            lock (room)
            {
                if (room.Status == MatchStatus.Closed)
                {
                    return;
                }

                var activeGameId = room.ActiveGameId;
                room.ActiveGameId = null;
                room.Status = MatchStatus.Closed;
                room.ClosedReason = reason;
                room.HostReady = false;
                room.GuestReady = false;
                room.ExpiresAt = DateTime.UtcNow.Add(_closedRetention);
                _logger.LogInformation("[MatchService] Room {RoomId} closed: {Reason}", roomId, reason);

                if (!string.IsNullOrWhiteSpace(activeGameId))
                {
                    DetachGame(activeGameId);
                }
            }
        }

        private void DetachGame(string gameId)
        {
            if (_gameToRoom.TryRemove(gameId, out _))
            {
                GameDetached?.Invoke(gameId);
            }
        }

        private static bool IsExpired(MatchRoom room) => DateTime.UtcNow > room.ExpiresAt;

        private static bool IsParticipant(MatchRoom room, Guid playerId)
        {
            return playerId == room.HostPlayerId || (room.GuestPlayerId.HasValue && room.GuestPlayerId.Value == playerId);
        }

        private static bool ValidateParticipantLocked(MatchRoom room, Guid playerId, string? token, out MatchOperationResult? failResult)
        {
            failResult = null;
            if (!IsParticipant(room, playerId))
            {
                failResult = MatchOperationResult.Fail(MatchOperationError.NotParticipant, "玩家不在房间内。");
                return false;
            }

            if (token != null)
            {
                var isHost = playerId == room.HostPlayerId && string.Equals(room.HostToken, token, StringComparison.Ordinal);
                var isGuest = room.GuestPlayerId.HasValue &&
                              playerId == room.GuestPlayerId.Value &&
                              string.Equals(room.GuestToken, token, StringComparison.Ordinal);
                if (!isHost && !isGuest)
                {
                    failResult = MatchOperationResult.Fail(MatchOperationError.Forbidden, "Token 无效或不属于该房间。");
                    return false;
                }
            }

            return true;
        }

        private static bool IsMirageRoom(MatchRoom room)
        {
            return room.Mode == GameMode.Mirage;
        }

        private static bool AreDecksReady(MatchRoom room)
        {
            if (IsMirageRoom(room))
            {
                return true;
            }

            if (!room.HostDeckSelection.HasSelection)
            {
                return false;
            }

            if (room.GuestPlayerId.HasValue)
            {
                return room.GuestDeckSelection.HasSelection;
            }

            return room.UseAiFallback && room.AiDeckSelection.HasSelection;
        }

        private static bool AreDeckFormatsConsistent(MatchRoom room, out string message)
        {
            message = "双方卡组环境或模式不一致。";
            if (IsMirageRoom(room))
            {
                return true;
            }
            var ignoreDeckFormatConsistency = room.DevMatchConfig?.IgnoreDeckFormatConsistency == true;

            if (!room.HostDeckSelection.HasSelection)
            {
                message = "房主尚未选择卡组。";
                return false;
            }

            var opponentSelection = room.GuestPlayerId.HasValue
                ? room.GuestDeckSelection
                : room.AiDeckSelection;

            if (!opponentSelection.HasSelection)
            {
                message = "对手尚未选择卡组。";
                return false;
            }

            if (ignoreDeckFormatConsistency)
            {
                return true;
            }

            var hostEnvironment = room.HostDeckSelection.Environment ?? DeckEnvironment.Rotation;
            var opponentEnvironment = opponentSelection.Environment ?? DeckEnvironment.Rotation;
            if (hostEnvironment != opponentEnvironment)
            {
                message = "双方卡组环境不一致。";
                return false;
            }

            var hostMode = room.HostDeckSelection.Rule ?? DeckRule.Standard;
            var opponentMode = opponentSelection.Rule ?? DeckRule.Standard;
            if (hostMode != opponentMode)
            {
                message = "双方卡组模式不一致。";
                return false;
            }

            return true;
        }

        private string GenerateUniqueJoinCode()
        {
            for (var attempt = 0; attempt < 32; attempt++)
            {
                var candidate = GenerateJoinCode();
                var exists = _rooms.Values.Any(r =>
                    r.Status != MatchStatus.Closed &&
                    !IsExpired(r) &&
                    string.Equals(r.JoinCode, candidate, StringComparison.OrdinalIgnoreCase));
                if (!exists)
                {
                    return candidate;
                }
            }
            return GenerateJoinCode();
        }

        private static string GenerateJoinCode()
        {
            // 6位大写字母+数字，便于口述
            const string alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
            Span<byte> buffer = stackalloc byte[6];
            RandomNumberGenerator.Fill(buffer);

            char[] chars = new char[6];
            for (int i = 0; i < 6; i++)
            {
                chars[i] = alphabet[buffer[i] % alphabet.Length];
            }
            return new string(chars);
        }

        private static string GenerateToken()
        {
            Span<byte> tokenBytes = stackalloc byte[16];
            RandomNumberGenerator.Fill(tokenBytes);
            return Convert.ToBase64String(tokenBytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        private static MatchRoom Clone(MatchRoom source)
        {
            return new MatchRoom
            {
                RoomId = source.RoomId,
                ActiveGameId = source.ActiveGameId,
                Round = source.Round,
                HostPlayerId = source.HostPlayerId,
                GuestPlayerId = source.GuestPlayerId,
                JoinCode = source.JoinCode,
                HostToken = source.HostToken,
                GuestToken = source.GuestToken,
                Status = source.Status,
                Mode = source.Mode,
                HostReady = source.HostReady,
                GuestReady = source.GuestReady,
                UseAiFallback = source.UseAiFallback,
                IsPublic = source.IsPublic,
                IsResumedRoom = source.IsResumedRoom,
                HostWins = source.HostWins,
                GuestWins = source.GuestWins,
                Draws = source.Draws,
                LastWinnerPlayerId = source.LastWinnerPlayerId,
                LastCompletedGameId = source.LastCompletedGameId,
                RoomTtl = source.RoomTtl,
                CreatedAt = source.CreatedAt,
                ExpiresAt = source.ExpiresAt,
                ClosedReason = source.ClosedReason,
                HostDeckSelection = CloneSelection(source.HostDeckSelection),
                GuestDeckSelection = CloneSelection(source.GuestDeckSelection),
                AiDeckSelection = CloneSelection(source.AiDeckSelection),
                DevMatchConfig = CloneConfig(source.DevMatchConfig)
            };
        }

        private static DevMatchConfig? CloneConfig(DevMatchConfig? source) => source == null
            ? null
            : new DevMatchConfig
            {
                IsLocalAiTraining = source.IsLocalAiTraining,
                BenchmarkHostGoesFirst = source.BenchmarkHostGoesFirst,
                BenchmarkShuffleSeed = source.BenchmarkShuffleSeed,
                IgnoreDeckFormatConsistency = source.IgnoreDeckFormatConsistency,
                AllowIncompleteDecks = source.AllowIncompleteDecks,
                StartingPlayPoints = source.StartingPlayPoints,
                MaxPlayPoints = source.MaxPlayPoints,
                StartingHandSize = source.StartingHandSize,
                StartingTurn = source.StartingTurn,
                StartingHealth = source.StartingHealth,
                PpBoostUses = source.PpBoostUses,
                Seed = source.Seed,
                TurnTimeoutSeconds = source.TurnTimeoutSeconds,
                FirstPlayerMode = source.FirstPlayerMode
            };

        private static MatchDeckSelection CloneSelection(MatchDeckSelection selection)
        {
            return new MatchDeckSelection
            {
                GeneratedDraftCardIds = selection.GeneratedDraftCardIds?.ToArray(),
                DraftEnvironment = selection.DraftEnvironment,
                UserId = selection.UserId,
                GuestId = selection.GuestId,
                DeckId = selection.DeckId,
                DeckCode = selection.DeckCode,
                DeckClass = selection.DeckClass,
                SecondaryClass = selection.SecondaryClass,
                Rule = selection.Rule,
                Environment = selection.Environment
            };
        }
    }
}


