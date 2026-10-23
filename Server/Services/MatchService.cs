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
    public class MatchService : IMatchService
    {
        public event Action<string>? GameDetached;
        private readonly ConcurrentDictionary<string, MatchRoom> _rooms = new();
        private readonly ConcurrentDictionary<string, string> _gameToRoom = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, object> _locks = new();
        private readonly TimeSpan _defaultTtl = TimeSpan.FromMinutes(30);
        private readonly TimeSpan _closedRetention = TimeSpan.FromMinutes(2);
        private readonly ILogger<MatchService> _logger;

        public MatchService(ILogger<MatchService> logger)
        {
            _logger = logger;
        }

        public MatchOperationResult Create(Guid? hostPlayerId = null, bool useAiFallback = false, TimeSpan? ttl = null)
        {
            CleanupExpired();

            var roomId = Guid.NewGuid().ToString("D");
            var hostId = hostPlayerId ?? Guid.NewGuid();
            var roomTtl = ttl.HasValue && ttl.Value > TimeSpan.Zero ? ttl.Value : _defaultTtl;
            var room = new MatchRoom
            {
                RoomId = roomId,
                HostPlayerId = hostId,
                JoinCode = GenerateJoinCode(),
                HostToken = GenerateToken(),
                UseAiFallback = useAiFallback,
                RoomTtl = roomTtl,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(roomTtl),
                ActiveGameId = null,
                Round = 0
            };

            if (!_rooms.TryAdd(roomId, room))
            {
                return MatchOperationResult.Fail(MatchOperationError.Forbidden, "创建房间失败，请重试。");
            }

            _logger.LogInformation("[MatchService] Created room {RoomId} with joinCode {JoinCode}", roomId, room.JoinCode);
            return MatchOperationResult.Ok(room);
        }

        public MatchOperationResult TryJoin(string roomId, string joinCode, Guid? guestPlayerId = null)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "房间不存在。");
            }

            lock (GetLock(roomId))
            {
                if (IsExpired(room))
                {
                    CloseInternal(roomId, "房间已过期");
                    return MatchOperationResult.Fail(MatchOperationError.Expired, "房间已过期。");
                }

                if (!string.Equals(room.JoinCode, joinCode, StringComparison.OrdinalIgnoreCase))
                {
                    return MatchOperationResult.Fail(MatchOperationError.InvalidJoinCode, "邀请码不正确。");
                }

                if (room.Status == MatchStatus.Closed)
                {
                    return MatchOperationResult.Fail(MatchOperationError.AlreadyClosed, room.ClosedReason ?? "房间已关闭。");
                }

                var requestedGuestId = guestPlayerId.HasValue && guestPlayerId.Value != Guid.Empty
                    ? guestPlayerId.Value
                    : (Guid?)null;

                // Idempotent rejoin: if the same guest joins again, return the existing seat instead of conflict.
                if (room.GuestPlayerId.HasValue &&
                    requestedGuestId.HasValue &&
                    room.GuestPlayerId.Value == requestedGuestId.Value)
                {
                    room.GuestToken ??= GenerateToken();
                    room.ExpiresAt = DateTime.UtcNow.Add(GetRoomTtl(room));
                    _logger.LogInformation("[MatchService] Guest {GuestId} rejoined room {RoomId}.", requestedGuestId.Value, roomId);
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
                room.ExpiresAt = DateTime.UtcNow.Add(GetRoomTtl(room));
                room.Status = MatchStatus.Waiting;

                _logger.LogInformation("[MatchService] Player {GuestId} joined room {RoomId}", guestId, roomId);
                return MatchOperationResult.Ok(Clone(room));
            }
        }

        public MatchOperationResult MarkReady(string roomId, Guid playerId)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "房间不存在。");
            }

            lock (GetLock(roomId))
            {
                if (IsExpired(room))
                {
                    CloseInternal(roomId, "房间已过期");
                    return MatchOperationResult.Fail(MatchOperationError.Expired, "房间已过期。");
                }

                if (!IsParticipant(room, playerId))
                {
                    return MatchOperationResult.Fail(MatchOperationError.NotParticipant, "玩家不在房间内。");
                }

                if (room.Status == MatchStatus.Started)
                {
                    return MatchOperationResult.Fail(MatchOperationError.AlreadyStarted, "房间已开始。");
                }

                if (playerId == room.HostPlayerId)
                {
                    room.HostReady = true;
                }
                else if (room.GuestPlayerId.HasValue && playerId == room.GuestPlayerId.Value)
                {
                    room.GuestReady = true;
                }

                if (room.HostReady && (room.GuestReady || (room.UseAiFallback && !room.GuestPlayerId.HasValue)))
                {
                    room.Status = MatchStatus.Ready;
                }

                return MatchOperationResult.Ok(Clone(room));
            }
        }

        public MatchOperationResult SubmitDeckSelection(string roomId, Guid playerId, MatchDeckSelection selection, MatchDeckSelection? aiSelection = null)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "房间不存在。");
            }

            lock (GetLock(roomId))
            {
                if (IsExpired(room))
                {
                    CloseInternal(roomId, "房间已过期");
                    return MatchOperationResult.Fail(MatchOperationError.Expired, "房间已过期。");
                }

                if (!IsParticipant(room, playerId))
                {
                    return MatchOperationResult.Fail(MatchOperationError.NotParticipant, "玩家不在房间内。");
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
                    room.HostDeckSelection = selection;
                    if (aiSelection != null)
                    {
                        room.AiDeckSelection = aiSelection;
                    }
                }
                else if (room.GuestPlayerId.HasValue && playerId == room.GuestPlayerId.Value)
                {
                    room.GuestDeckSelection = selection;
                }

                return MatchOperationResult.Ok(Clone(room));
            }
        }

        public MatchOperationResult Start(string roomId, Guid playerId)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "房间不存在。");
            }

            lock (GetLock(roomId))
            {
                if (IsExpired(room))
                {
                    CloseInternal(roomId, "房间已过期");
                    return MatchOperationResult.Fail(MatchOperationError.Expired, "房间已过期。");
                }

                if (!IsParticipant(room, playerId))
                {
                    return MatchOperationResult.Fail(MatchOperationError.NotParticipant, "玩家不在房间内。");
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
                room.ExpiresAt = DateTime.UtcNow.Add(GetRoomTtl(room));

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

            if (IsExpired(room))
            {
                CloseInternal(roomId, "房间已过期");
                return MatchOperationResult.Fail(MatchOperationError.Expired, "房间已过期。");
            }

            return MatchOperationResult.Ok(Clone(room));
        }

        public IReadOnlyList<MatchRoom> ListCurrentRooms()
        {
            CleanupExpired();

            return _rooms.Values
                .Where(room => room.Status != MatchStatus.Closed && !IsExpired(room))
                .OrderByDescending(room => room.Status == MatchStatus.Started)
                .ThenByDescending(room => room.CreatedAt)
                .Select(Clone)
                .ToList();
        }

        public MatchOperationResult ValidateConnection(string roomId, Guid playerId, string token)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "房间不存在。");
            }

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

        public MatchOperationResult RefreshExpiration(string roomId, Guid playerId)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "房间不存在。");
            }

            lock (GetLock(roomId))
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

                room.ExpiresAt = DateTime.UtcNow.Add(GetRoomTtl(room));
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

        public MatchOperationResult Leave(string roomId, Guid playerId, string? hostLeaveReason = null)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "房间不存在。");
            }

            lock (GetLock(roomId))
            {
                if (IsExpired(room))
                {
                    CloseInternal(roomId, "房间已过期");
                    return MatchOperationResult.Fail(MatchOperationError.Expired, "房间已过期。");
                }

                if (!IsParticipant(room, playerId))
                {
                    return MatchOperationResult.Fail(MatchOperationError.NotParticipant, "玩家不在房间内。");
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

                // Guest leaves: keep room alive and release guest seat for future joins.
                if (!string.IsNullOrWhiteSpace(room.ActiveGameId))
                {
                    DetachGame(room.ActiveGameId);
                }

                room.ActiveGameId = null;
                room.GuestPlayerId = null;
                room.GuestToken = null;
                room.GuestReady = false;
                room.HostReady = false;
                room.GuestDeckSelection = new MatchDeckSelection();
                room.Status = MatchStatus.Waiting;
                room.ExpiresAt = DateTime.UtcNow.Add(GetRoomTtl(room));
                room.ClosedReason = null;

                _logger.LogInformation("[MatchService] Guest {PlayerId} left room {RoomId}. Seat released for rejoin.", playerId, roomId);
                return MatchOperationResult.Ok(Clone(room));
            }
        }

        public void Close(string roomId, string reason)
        {
            CloseInternal(roomId, reason);
        }

        public void CleanupExpired()
        {
            foreach (var kvp in _rooms)
            {
                var room = kvp.Value;
                if (IsExpired(room))
                {
                    if (room.Status == MatchStatus.Closed)
                    {
                        _rooms.TryRemove(kvp.Key, out _);
                        _locks.TryRemove(kvp.Key, out _);
                        if (!string.IsNullOrWhiteSpace(room.ActiveGameId))
                        {
                            DetachGame(room.ActiveGameId);
                        }
                    }
                    else
                    {
                        CloseInternal(kvp.Key, "房间已过期");
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

            lock (GetLock(roomId))
            {
                if (!string.IsNullOrWhiteSpace(room.ActiveGameId))
                {
                    DetachGame(room.ActiveGameId);
                }

                room.Status = MatchStatus.Closed;
                room.ClosedReason = reason;
                room.HostReady = false;
                room.GuestReady = false;
                room.ExpiresAt = DateTime.UtcNow.Add(_closedRetention);
                _logger.LogInformation("[MatchService] Room {RoomId} closed: {Reason}", roomId, reason);
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

        private TimeSpan GetRoomTtl(MatchRoom room)
        {
            return room.RoomTtl > TimeSpan.Zero ? room.RoomTtl : _defaultTtl;
        }

        private object GetLock(string roomId) => _locks.GetOrAdd(roomId, _ => new object());

        private static bool IsParticipant(MatchRoom room, Guid playerId)
        {
            return playerId == room.HostPlayerId || (room.GuestPlayerId.HasValue && room.GuestPlayerId.Value == playerId);
        }

        private static bool AreDecksReady(MatchRoom room)
        {
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
                HostReady = source.HostReady,
                GuestReady = source.GuestReady,
                UseAiFallback = source.UseAiFallback,
                RoomTtl = source.RoomTtl,
                CreatedAt = source.CreatedAt,
                ExpiresAt = source.ExpiresAt,
                ClosedReason = source.ClosedReason,
                HostDeckSelection = CloneSelection(source.HostDeckSelection),
                GuestDeckSelection = CloneSelection(source.GuestDeckSelection),
                AiDeckSelection = CloneSelection(source.AiDeckSelection),
                DevMatchConfig = source.DevMatchConfig == null
                    ? null
                    : new DevMatchConfig
                    {
                        IgnoreDeckFormatConsistency = source.DevMatchConfig.IgnoreDeckFormatConsistency,
                        AllowIncompleteDecks = source.DevMatchConfig.AllowIncompleteDecks,
                        StartingPlayPoints = source.DevMatchConfig.StartingPlayPoints,
                        MaxPlayPoints = source.DevMatchConfig.MaxPlayPoints,
                        StartingHandSize = source.DevMatchConfig.StartingHandSize,
                        StartingTurn = source.DevMatchConfig.StartingTurn,
                        StartingHealth = source.DevMatchConfig.StartingHealth,
                        PpBoostUses = source.DevMatchConfig.PpBoostUses,
                        Seed = source.DevMatchConfig.Seed
                    }
            };
        }

        private static MatchDeckSelection CloneSelection(MatchDeckSelection selection)
        {
            return new MatchDeckSelection
            {
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


