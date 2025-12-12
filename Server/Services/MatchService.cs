// File: Server/Services/MatchService.cs
//
// 相关文档:
// - 多人联机方案A: 内存房间 + JoinCode/Token
using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services
{
    /// <summary>
    /// 内存实现的房间服务，专用于局域网/本地多开测试。
    /// 不持久化，进程重启后房间失效。
    /// </summary>
    public class MatchService : IMatchService
    {
        private readonly ConcurrentDictionary<string, MatchRoom> _rooms = new();
        private readonly ConcurrentDictionary<string, object> _locks = new();
        private readonly TimeSpan _defaultTtl = TimeSpan.FromMinutes(30);
        private readonly ILogger<MatchService> _logger;

        public MatchService(ILogger<MatchService> logger)
        {
            _logger = logger;
        }

        public MatchOperationResult Create(Guid? hostPlayerId = null, bool useAiFallback = false, TimeSpan? ttl = null)
        {
            CleanupExpired();

            var gameId = Guid.NewGuid().ToString();
            var hostId = hostPlayerId ?? Guid.NewGuid();
            var room = new MatchRoom
            {
                GameId = gameId,
                HostPlayerId = hostId,
                JoinCode = GenerateJoinCode(),
                HostToken = GenerateToken(),
                UseAiFallback = useAiFallback,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(ttl ?? _defaultTtl)
            };

            if (!_rooms.TryAdd(gameId, room))
            {
                return MatchOperationResult.Fail(MatchOperationError.Forbidden, "创建房间失败，请重试。");
            }

            _logger.LogInformation("[MatchService] Created room {GameId} with joinCode {JoinCode}", gameId, room.JoinCode);
            return MatchOperationResult.Ok(room);
        }

        public MatchOperationResult TryJoin(string gameId, string joinCode)
        {
            if (!_rooms.TryGetValue(gameId, out var room))
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "房间不存在。");
            }

            lock (GetLock(gameId))
            {
                if (IsExpired(room))
                {
                    CloseInternal(gameId, "房间已过期");
                    return MatchOperationResult.Fail(MatchOperationError.Expired, "房间已过期。");
                }

                if (!string.Equals(room.JoinCode, joinCode, StringComparison.OrdinalIgnoreCase))
                {
                    return MatchOperationResult.Fail(MatchOperationError.InvalidJoinCode, "邀请码不正确。");
                }

                if (room.Status == MatchStatus.Started)
                {
                    return MatchOperationResult.Fail(MatchOperationError.AlreadyStarted, "房间已开始。");
                }

                if (room.Status == MatchStatus.Closed)
                {
                    return MatchOperationResult.Fail(MatchOperationError.AlreadyClosed, room.ClosedReason ?? "房间已关闭。");
                }

                if (room.GuestPlayerId.HasValue)
                {
                    return MatchOperationResult.Fail(MatchOperationError.AlreadyFull, "房间已满。");
                }

                var guestId = Guid.NewGuid();
                room.GuestPlayerId = guestId;
                room.GuestToken = GenerateToken();
                room.ExpiresAt = DateTime.UtcNow.Add(_defaultTtl);
                room.Status = MatchStatus.Waiting;

                _logger.LogInformation("[MatchService] Player {GuestId} joined room {GameId}", guestId, gameId);
                return MatchOperationResult.Ok(Clone(room));
            }
        }

        public MatchOperationResult MarkReady(string gameId, Guid playerId)
        {
            if (!_rooms.TryGetValue(gameId, out var room))
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "房间不存在。");
            }

            lock (GetLock(gameId))
            {
                if (IsExpired(room))
                {
                    CloseInternal(gameId, "房间已过期");
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

        public MatchOperationResult Start(string gameId, Guid playerId)
        {
            if (!_rooms.TryGetValue(gameId, out var room))
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "房间不存在。");
            }

            lock (GetLock(gameId))
            {
                if (IsExpired(room))
                {
                    CloseInternal(gameId, "房间已过期");
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

                room.Status = MatchStatus.Started;
                room.ExpiresAt = DateTime.UtcNow.Add(_defaultTtl);

                _logger.LogInformation("[MatchService] Room {GameId} started by {PlayerId}", gameId, playerId);
                return MatchOperationResult.Ok(Clone(room));
            }
        }

        public MatchOperationResult Get(string gameId)
        {
            if (!_rooms.TryGetValue(gameId, out var room))
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "房间不存在。");
            }

            if (IsExpired(room))
            {
                CloseInternal(gameId, "房间已过期");
                return MatchOperationResult.Fail(MatchOperationError.Expired, "房间已过期。");
            }

            return MatchOperationResult.Ok(Clone(room));
        }

        public MatchOperationResult ValidateConnection(string gameId, Guid playerId, string token)
        {
            if (!_rooms.TryGetValue(gameId, out var room))
            {
                return MatchOperationResult.Fail(MatchOperationError.NotFound, "房间不存在。");
            }

            if (IsExpired(room))
            {
                CloseInternal(gameId, "房间已过期");
                return MatchOperationResult.Fail(MatchOperationError.Expired, "房间已过期。");
            }

            var isHost = playerId == room.HostPlayerId && string.Equals(room.HostToken, token, StringComparison.Ordinal);
            var isGuest = room.GuestPlayerId.HasValue && playerId == room.GuestPlayerId.Value &&
                          string.Equals(room.GuestToken, token, StringComparison.Ordinal);

            if (!isHost && !isGuest)
            {
                return MatchOperationResult.Fail(MatchOperationError.Forbidden, "Token 无效或不属于该房间。");
            }

            return MatchOperationResult.Ok(Clone(room));
        }

        public void Close(string gameId, string reason)
        {
            CloseInternal(gameId, reason);
        }

        public void CleanupExpired()
        {
            foreach (var kvp in _rooms)
            {
                var room = kvp.Value;
                if (IsExpired(room))
                {
                    CloseInternal(kvp.Key, "房间已过期");
                }
            }
        }

        private void CloseInternal(string gameId, string reason)
        {
            if (!_rooms.TryGetValue(gameId, out var room))
            {
                return;
            }

            lock (GetLock(gameId))
            {
                room.Status = MatchStatus.Closed;
                room.ClosedReason = reason;
                room.HostReady = false;
                room.GuestReady = false;
                _rooms.TryRemove(gameId, out _);
                _logger.LogInformation("[MatchService] Room {GameId} closed: {Reason}", gameId, reason);
            }
        }

        private static bool IsExpired(MatchRoom room) => DateTime.UtcNow > room.ExpiresAt;

        private object GetLock(string gameId) => _locks.GetOrAdd(gameId, _ => new object());

        private static bool IsParticipant(MatchRoom room, Guid playerId)
        {
            return playerId == room.HostPlayerId || (room.GuestPlayerId.HasValue && room.GuestPlayerId.Value == playerId);
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
                GameId = source.GameId,
                HostPlayerId = source.HostPlayerId,
                GuestPlayerId = source.GuestPlayerId,
                JoinCode = source.JoinCode,
                HostToken = source.HostToken,
                GuestToken = source.GuestToken,
                Status = source.Status,
                HostReady = source.HostReady,
                GuestReady = source.GuestReady,
                UseAiFallback = source.UseAiFallback,
                CreatedAt = source.CreatedAt,
                ExpiresAt = source.ExpiresAt,
                ClosedReason = source.ClosedReason
            };
        }
    }
}
