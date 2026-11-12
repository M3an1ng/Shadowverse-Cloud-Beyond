using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Hubs;
using ShadowverseBeyond.Server.Hubs.ClientInterfaces;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services
{
    public sealed partial class MirageLobbyService : IMirageLobbyService
    {
        private static readonly string[] PresetAiNames =
        {
            "星界旅人·阿尔法",
            "苍穹统帅·贝塔",
            "幻境猎手·伽马",
            "虚空织者·德尔塔",
            "秘法学者·艾普西隆",
            "圣辉守卫·泽塔",
            "暗影裁决·伊塔",
            "星辰领主·奥米伽"
        };

        private readonly ConcurrentDictionary<string, MirageLobbyRoom> _rooms = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, string> _seatGameToRoom = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _creationGate = new();
        private readonly IRoomJoinCodeRegistry _joinCodeRegistry;
        private readonly IGameService _gameService;
        private readonly IPendingActionService _pendingActionService;
        private readonly IMirageShopService _shopService;
        private readonly IMirageCombatSnapshotBuilder _snapshotBuilder;
        private readonly IMirageCombatSimulator _combatSimulator;
        private readonly IHubContext<GameHub, IGameClient> _hubContext;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IGameDataProvider _gameDataProvider;
        private readonly IMapper _mapper;
        private readonly ILogger<MirageLobbyService> _logger;

        public MirageLobbyService(
            IRoomJoinCodeRegistry joinCodeRegistry,
            IGameService gameService,
            IPendingActionService pendingActionService,
            IMirageShopService shopService,
            IMirageCombatSnapshotBuilder snapshotBuilder,
            IMirageCombatSimulator combatSimulator,
            IHubContext<GameHub, IGameClient> hubContext,
            IServiceScopeFactory scopeFactory,
            IGameDataProvider gameDataProvider,
            IMapper mapper,
            ILogger<MirageLobbyService> logger)
        {
            _joinCodeRegistry = joinCodeRegistry;
            _gameService = gameService;
            _pendingActionService = pendingActionService;
            _shopService = shopService;
            _snapshotBuilder = snapshotBuilder;
            _combatSimulator = combatSimulator;
            _hubContext = hubContext;
            _scopeFactory = scopeFactory;
            _gameDataProvider = gameDataProvider;
            _mapper = mapper;
            _logger = logger;
        }

        public IReadOnlyList<MirageLobbyRoomListItemDto> ListRooms()
        {
            CleanupExpiredRooms();
            var items = new List<MirageLobbyRoomListItemDto>();
            foreach (var room in _rooms.Values)
            {
                lock (room.SyncRoot)
                {
                    if (room.Phase == MirageLobbyPhase.Closed || IsExpiredLocked(room))
                    {
                        continue;
                    }

                    var hostSeat = room.Seats.FirstOrDefault(s => s.IsHost) ?? room.Seats.FirstOrDefault();
                    var humanCount = room.Seats.Count(s => s.Occupancy == MirageSeatOccupancy.Human);
                    var aliveCount = room.Phase == MirageLobbyPhase.Waiting
                        ? MirageLobbyRoom.MaxSeatCount
                        : room.Seats.Count(s => s.Occupancy != MirageSeatOccupancy.Empty && !s.IsEliminated);
                    var canJoin = room.IsPublic
                                  && room.Phase == MirageLobbyPhase.Waiting
                                  && humanCount < MirageLobbyRoom.MaxSeatCount;
                    var canSpectate = room.Phase is MirageLobbyPhase.Recruit
                                      or MirageLobbyPhase.Closing
                                      or MirageLobbyPhase.Simulating
                                      or MirageLobbyPhase.Applying;

                    items.Add(new MirageLobbyRoomListItemDto
                    {
                        RoomId = room.RoomId,
                        IsPublic = room.IsPublic,
                        Phase = room.Phase.ToString(),
                        StatusLabel = FormatPhaseLabel(room.Phase),
                        HostDisplayName = hostSeat?.DisplayName ?? "房主",
                        MaxSeats = MirageLobbyRoom.MaxSeatCount,
                        HumanCount = humanCount,
                        AliveCount = aliveCount,
                        RoundNumber = room.RoundNumber,
                        RecruitTimeoutSeconds = room.RecruitTimeoutSeconds,
                        CanJoin = canJoin,
                        CanSpectate = canSpectate,
                        CreatedAtUtc = room.CreatedAtUtc
                    });
                }
            }

            return items
                .OrderBy(i => i.CanJoin ? 0 : (i.CanSpectate ? 1 : 2))
                .ThenByDescending(i => i.CreatedAtUtc)
                .ToList();
        }

        public MirageResolveCodeResponseDto ResolveJoinCode(string? joinCode)
        {
            CleanupExpiredRooms();
            if (_joinCodeRegistry.TryResolve(joinCode, out var target))
            {
                return new MirageResolveCodeResponseDto
                {
                    Found = true,
                    Kind = target.Kind.ToString(),
                    RoomId = target.RoomId,
                    JoinCode = target.JoinCode
                };
            }

            return new MirageResolveCodeResponseDto { Found = false };
        }

        public async Task<MirageLobbyOperationResult> CreateRoomAsync(
            Guid? hostPlayerId,
            string? hostDisplayName,
            string? hostAvatarUrl,
            bool isPublic,
            int? recruitTimeoutSeconds,
            bool quickStartSoloAi,
            CancellationToken cancellationToken = default)
        {
            CleanupExpiredRooms();

            var effectiveHostId = hostPlayerId.HasValue && hostPlayerId.Value != Guid.Empty
                ? hostPlayerId.Value
                : Guid.NewGuid();
            var (resolvedName, resolvedAvatar) = await ResolveUserIdentityAsync(
                effectiveHostId,
                hostDisplayName,
                hostAvatarUrl,
                "房主",
                cancellationToken);

            var timeout = Math.Clamp(recruitTimeoutSeconds ?? MirageRuleConfig.DefaultRecruitTimeoutSeconds, 15, 300);
            var roomId = Guid.NewGuid().ToString("D");
            string hostToken = GenerateSeatToken();
            MirageLobbyRoom room;

            lock (_creationGate)
            {
                var joinCode = _joinCodeRegistry.AllocateUniqueCode(
                    RegisteredRoomKind.Mirage8P,
                    roomId,
                    RoomJoinCodeRegistry.DefaultAlphanumericAlphabet,
                    6);

                room = new MirageLobbyRoom
                {
                    RoomId = roomId,
                    JoinCode = joinCode,
                    IsPublic = quickStartSoloAi ? false : isPublic,
                    RecruitTimeoutSeconds = timeout,
                    RoomVersion = 1,
                    Phase = MirageLobbyPhase.Waiting,
                    CreatedAtUtc = DateTime.UtcNow,
                    ExpiresAtUtc = DateTime.UtcNow.Add(RoomLifetime.DefaultTtl)
                };

                for (int i = 0; i < MirageLobbyRoom.MaxSeatCount; i++)
                {
                    if (i == 0)
                    {
                        room.Seats.Add(new MirageLobbySeat
                        {
                            SeatIndex = 0,
                            Occupancy = MirageSeatOccupancy.Human,
                            PlayerId = effectiveHostId,
                            SeatToken = hostToken,
                            DisplayName = resolvedName,
                            AvatarUrl = resolvedAvatar,
                            JoinedAtUtc = DateTime.UtcNow,
                            IsHost = true,
                            IsReady = true
                        });
                    }
                    else
                    {
                        room.Seats.Add(new MirageLobbySeat
                        {
                            SeatIndex = i,
                            Occupancy = MirageSeatOccupancy.Empty
                        });
                    }
                }

                if (!_rooms.TryAdd(roomId, room))
                {
                    _joinCodeRegistry.Release(joinCode, roomId);
                    return MirageLobbyOperationResult.Fail(MirageLobbyError.InvalidOperation, "创建幻境房间失败，请重试。");
                }
            }

            _logger.LogInformation("[MirageLobby] Created 8P room {RoomId} (JoinCode={JoinCode}, Host={HostId}, QuickSolo={QuickSolo})",
                roomId, room.JoinCode, effectiveHostId, quickStartSoloAi);

            if (quickStartSoloAi)
            {
                return await StartRoomAsync(roomId, effectiveHostId, hostToken, cancellationToken);
            }

            MirageLobbyRoomDto dto;
            lock (room.SyncRoot)
            {
                dto = BuildRoomDtoLocked(room, effectiveHostId, includeCallerCredentials: true, includeJoinCode: true);
            }
            return MirageLobbyOperationResult.Ok(dto);
        }

        public MirageLobbyOperationResult GetRoom(
            string roomId,
            Guid? callerPlayerId = null,
            string? callerToken = null)
        {
            if (!_rooms.TryGetValue(roomId ?? string.Empty, out var room))
            {
                return MirageLobbyOperationResult.Fail(MirageLobbyError.NotFound, "幻境房间不存在。");
            }

            lock (room.SyncRoot)
            {
                if (IsExpiredLocked(room))
                {
                    CloseRoomInternalLocked(room, "房间已过期。", out var seatsToRetire);
                    RetireSeatGamesOutsideLock(seatsToRetire);
                    return MirageLobbyOperationResult.Fail(MirageLobbyError.Expired, "幻境房间已过期。");
                }

                bool authenticated = false;
                if (callerPlayerId.HasValue && callerPlayerId.Value != Guid.Empty && !string.IsNullOrWhiteSpace(callerToken))
                {
                    var seat = room.Seats.FirstOrDefault(s =>
                        s.Occupancy == MirageSeatOccupancy.Human
                        && s.PlayerId == callerPlayerId.Value
                        && string.Equals(s.SeatToken, callerToken, StringComparison.Ordinal));
                    if (seat != null)
                    {
                        authenticated = true;
                        room.ExpiresAtUtc = DateTime.UtcNow.Add(room.RoomTtl);
                    }
                }

                var dto = BuildRoomDtoLocked(
                    room,
                    authenticated ? callerPlayerId : null,
                    includeCallerCredentials: authenticated,
                    includeJoinCode: authenticated || room.IsPublic);

                if (room.Phase == MirageLobbyPhase.Closed)
                {
                    return MirageLobbyOperationResult.Fail(MirageLobbyError.AlreadyClosed, room.ClosedReason ?? "房间已关闭。", dto);
                }

                return MirageLobbyOperationResult.Ok(dto);
            }
        }

        public async Task<MirageLobbyOperationResult> JoinRoomAsync(
            string roomId,
            string? joinCode,
            Guid? playerId,
            string? existingToken,
            string? displayName,
            string? avatarUrl,
            bool allowPublicWithoutCode,
            CancellationToken cancellationToken = default)
        {
            if (!_rooms.TryGetValue(roomId ?? string.Empty, out var room))
            {
                return MirageLobbyOperationResult.Fail(MirageLobbyError.NotFound, "幻境房间不存在。");
            }

            var candidateId = playerId.HasValue && playerId.Value != Guid.Empty
                ? playerId.Value
                : Guid.NewGuid();
            var (resolvedName, resolvedAvatar) = await ResolveUserIdentityAsync(
                candidateId,
                displayName,
                avatarUrl,
                $"玩家{candidateId.ToString("N")[..4].ToUpperInvariant()}",
                cancellationToken);

            MirageLobbyRoomDto resultDto;
            MirageLobbyRoomDto publicBroadcastDto;
            lock (room.SyncRoot)
            {
                if (IsExpiredLocked(room))
                {
                    CloseRoomInternalLocked(room, "房间已过期。", out var seatsToRetire);
                    RetireSeatGamesOutsideLock(seatsToRetire);
                    return MirageLobbyOperationResult.Fail(MirageLobbyError.Expired, "幻境房间已过期。");
                }

                if (room.Phase == MirageLobbyPhase.Closed)
                {
                    return MirageLobbyOperationResult.Fail(MirageLobbyError.AlreadyClosed, room.ClosedReason ?? "房间已关闭。");
                }

                var existingSeat = room.Seats.FirstOrDefault(s =>
                    s.Occupancy == MirageSeatOccupancy.Human && s.PlayerId == candidateId);
                if (existingSeat != null)
                {
                    if (!string.IsNullOrWhiteSpace(existingToken)
                        && string.Equals(existingSeat.SeatToken, existingToken, StringComparison.Ordinal))
                    {
                        room.ExpiresAtUtc = DateTime.UtcNow.Add(room.RoomTtl);
                        return MirageLobbyOperationResult.Ok(
                            BuildRoomDtoLocked(room, candidateId, includeCallerCredentials: true, includeJoinCode: true));
                    }

                    return MirageLobbyOperationResult.Fail(MirageLobbyError.Forbidden, "该账号已在房间内。");
                }

                var normalizedCode = joinCode?.Trim() ?? string.Empty;
                var codeMatches = !string.IsNullOrWhiteSpace(normalizedCode)
                                  && string.Equals(room.JoinCode, normalizedCode, StringComparison.OrdinalIgnoreCase);
                var publicBypass = allowPublicWithoutCode && room.IsPublic && string.IsNullOrWhiteSpace(normalizedCode);
                if (!codeMatches && !publicBypass)
                {
                    return MirageLobbyOperationResult.Fail(MirageLobbyError.InvalidJoinCode, "邀请码不正确。");
                }

                if (room.Phase != MirageLobbyPhase.Waiting)
                {
                    return MirageLobbyOperationResult.Fail(MirageLobbyError.AlreadyStarted, "对局已开始，无法作为选手加入（可选择观战）。");
                }

                var emptySeat = room.Seats.FirstOrDefault(s => s.Occupancy == MirageSeatOccupancy.Empty);
                if (emptySeat == null)
                {
                    return MirageLobbyOperationResult.Fail(MirageLobbyError.AlreadyFull, "8 人房间已满。");
                }

                emptySeat.Occupancy = MirageSeatOccupancy.Human;
                emptySeat.PlayerId = candidateId;
                emptySeat.SeatToken = GenerateSeatToken();
                emptySeat.DisplayName = resolvedName;
                emptySeat.AvatarUrl = resolvedAvatar;
                emptySeat.JoinedAtUtc = DateTime.UtcNow;
                emptySeat.IsHost = false;
                emptySeat.IsReady = false;

                room.RoomVersion++;
                room.ExpiresAtUtc = DateTime.UtcNow.Add(room.RoomTtl);

                resultDto = BuildRoomDtoLocked(room, candidateId, includeCallerCredentials: true, includeJoinCode: true);
                publicBroadcastDto = BuildRoomDtoLocked(room, null, includeCallerCredentials: false, includeJoinCode: true);
            }

            await BroadcastLobbyUpdatedSafeAsync(room.RoomId, publicBroadcastDto);
            return MirageLobbyOperationResult.Ok(resultDto);
        }

        public async Task<MirageLobbyOperationResult> JoinByCodeAsync(
            string joinCode,
            Guid? playerId,
            string? existingToken,
            string? displayName,
            string? avatarUrl,
            CancellationToken cancellationToken = default)
        {
            var normalized = joinCode?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return MirageLobbyOperationResult.Fail(MirageLobbyError.InvalidJoinCode, "请输入 6 位邀请码。");
            }

            CleanupExpiredRooms();
            if (_joinCodeRegistry.TryResolve(normalized, out var resolved))
            {
                if (resolved.Kind != RegisteredRoomKind.Mirage8P)
                {
                    return MirageLobbyOperationResult.Fail(
                        MirageLobbyError.InvalidJoinCode,
                        $"该邀请码属于{(resolved.Kind == RegisteredRoomKind.TakeTwo ? "双选轮抽" : "经典构筑")}房间，请前往对应模式入口加入。");
                }

                return await JoinRoomAsync(
                    resolved.RoomId,
                    normalized,
                    playerId,
                    existingToken,
                    displayName,
                    avatarUrl,
                    allowPublicWithoutCode: false,
                    cancellationToken);
            }

            return MirageLobbyOperationResult.Fail(MirageLobbyError.NotFound, "未找到该邀请码对应的幻境房间。");
        }

        public async Task<MirageLobbyOperationResult> SetReadyAsync(
            string roomId,
            Guid playerId,
            string token,
            bool ready,
            CancellationToken cancellationToken = default)
        {
            if (!_rooms.TryGetValue(roomId ?? string.Empty, out var room))
            {
                return MirageLobbyOperationResult.Fail(MirageLobbyError.NotFound, "幻境房间不存在。");
            }

            MirageLobbyRoomDto callerDto;
            MirageLobbyRoomDto broadcastDto;
            lock (room.SyncRoot)
            {
                if (!TryAuthorizeHumanSeatLocked(room, playerId, token, out var seat, out var fail))
                {
                    return fail!;
                }

                if (room.Phase != MirageLobbyPhase.Waiting)
                {
                    return MirageLobbyOperationResult.Fail(MirageLobbyError.AlreadyStarted, "房间已开始对局。");
                }

                seat!.IsReady = seat.IsHost ? true : ready;
                room.RoomVersion++;
                room.ExpiresAtUtc = DateTime.UtcNow.Add(room.RoomTtl);

                callerDto = BuildRoomDtoLocked(room, playerId, includeCallerCredentials: true, includeJoinCode: true);
                broadcastDto = BuildRoomDtoLocked(room, null, includeCallerCredentials: false, includeJoinCode: true);
            }

            await BroadcastLobbyUpdatedSafeAsync(room.RoomId, broadcastDto);
            return MirageLobbyOperationResult.Ok(callerDto);
        }

        public async Task<MirageLobbyOperationResult> KickSeatAsync(
            string roomId,
            Guid hostPlayerId,
            string hostToken,
            Guid targetPlayerId,
            CancellationToken cancellationToken = default)
        {
            if (!_rooms.TryGetValue(roomId ?? string.Empty, out var room))
            {
                return MirageLobbyOperationResult.Fail(MirageLobbyError.NotFound, "幻境房间不存在。");
            }

            MirageLobbyRoomDto callerDto;
            MirageLobbyRoomDto broadcastDto;
            lock (room.SyncRoot)
            {
                if (!TryAuthorizeHumanSeatLocked(room, hostPlayerId, hostToken, out var hostSeat, out var fail))
                {
                    return fail!;
                }

                if (!hostSeat!.IsHost)
                {
                    return MirageLobbyOperationResult.Fail(MirageLobbyError.Forbidden, "仅房主可移出访客。");
                }

                if (room.Phase != MirageLobbyPhase.Waiting)
                {
                    return MirageLobbyOperationResult.Fail(MirageLobbyError.AlreadyStarted, "对局开始后无法踢出选手。");
                }

                var targetSeat = room.Seats.FirstOrDefault(s =>
                    s.Occupancy == MirageSeatOccupancy.Human && !s.IsHost && s.PlayerId == targetPlayerId);
                if (targetSeat == null)
                {
                    return MirageLobbyOperationResult.Fail(MirageLobbyError.NotParticipant, "目标玩家不在房间内。");
                }

                ResetSeatToEmptyLocked(targetSeat);
                room.RoomVersion++;
                room.ExpiresAtUtc = DateTime.UtcNow.Add(room.RoomTtl);

                callerDto = BuildRoomDtoLocked(room, hostPlayerId, includeCallerCredentials: true, includeJoinCode: true);
                broadcastDto = BuildRoomDtoLocked(room, null, includeCallerCredentials: false, includeJoinCode: true);
            }

            await BroadcastLobbyUpdatedSafeAsync(room.RoomId, broadcastDto);
            await _hubContext.Clients.Group(SignalRGroupNames.Player(room.RoomId, targetPlayerId))
                .ReceiveError("MIRAGE_ROOM_KICKED:已被房主移出幻境房间。");
            return MirageLobbyOperationResult.Ok(callerDto);
        }

        public async Task<MirageLobbyOperationResult> LeaveRoomAsync(
            string roomId,
            Guid playerId,
            string token,
            CancellationToken cancellationToken = default)
        {
            if (!_rooms.TryGetValue(roomId ?? string.Empty, out var room))
            {
                return MirageLobbyOperationResult.Fail(MirageLobbyError.NotFound, "幻境房间不存在。");
            }

            bool shouldForfeitActiveSeat = false;
            bool roomClosed = false;
            MirageLobbyRoomDto? responseDto = null;
            MirageLobbyRoomDto? broadcastDto = null;
            List<string>? seatsToRetire = null;

            lock (room.SyncRoot)
            {
                if (room.Phase == MirageLobbyPhase.Closed)
                {
                    return MirageLobbyOperationResult.Ok(BuildRoomDtoLocked(room, null, false, false));
                }

                if (!TryAuthorizeHumanSeatLocked(room, playerId, token, out var seat, out var fail))
                {
                    return fail!;
                }

                if (room.Phase == MirageLobbyPhase.Waiting)
                {
                    var wasHost = seat!.IsHost;
                    ResetSeatToEmptyLocked(seat);
                    var remainingHumans = room.Seats
                        .Where(s => s.Occupancy == MirageSeatOccupancy.Human)
                        .OrderBy(s => s.JoinedAtUtc)
                        .ThenBy(s => s.SeatIndex)
                        .ToList();

                    if (remainingHumans.Count == 0)
                    {
                        roomClosed = true;
                        CloseRoomInternalLocked(room, "所有玩家已离开，房间已关闭。", out seatsToRetire);
                    }
                    else
                    {
                        if (wasHost)
                        {
                            // 自动顺延房主给最早入座的真人
                            var nextHost = remainingHumans[0];
                            nextHost.IsHost = true;
                            nextHost.IsReady = true;
                        }
                        room.RoomVersion++;
                        room.ExpiresAtUtc = DateTime.UtcNow.Add(room.RoomTtl);
                        broadcastDto = BuildRoomDtoLocked(room, null, includeCallerCredentials: false, includeJoinCode: true);
                    }

                    responseDto = BuildRoomDtoLocked(room, null, includeCallerCredentials: false, includeJoinCode: !roomClosed);
                }
                else
                {
                    room.HumanGraceDeadlinesUtc.Remove(playerId);
                    seat!.HasLeftPostElimination = true;
                    if (!seat.IsEliminated && room.Phase != MirageLobbyPhase.Finished)
                    {
                        shouldForfeitActiveSeat = true;
                    }
                    responseDto = BuildRoomDtoLocked(room, playerId, includeCallerCredentials: true, includeJoinCode: true);
                }
            }

            RetireSeatGamesOutsideLock(seatsToRetire);

            if (roomClosed)
            {
                await _hubContext.Clients.Group(SignalRGroupNames.MirageRoom(room.RoomId))
                    .ReceiveError("ROOM_CLOSED:所有玩家已离开，幻境房间已关闭。");
                return MirageLobbyOperationResult.Ok(responseDto!);
            }

            if (broadcastDto != null)
            {
                await BroadcastLobbyUpdatedSafeAsync(room.RoomId, broadcastDto);
            }

            if (shouldForfeitActiveSeat)
            {
                await ForfeitSeatAsync(room.RoomId, playerId, "主动退出战局", cancellationToken);
                MirageLobbyRoomDto postForfeitDto;
                lock (room.SyncRoot)
                {
                    postForfeitDto = BuildRoomDtoLocked(
                        room,
                        playerId,
                        includeCallerCredentials: true,
                        includeJoinCode: room.Phase != MirageLobbyPhase.Closed);
                }
                return MirageLobbyOperationResult.Ok(postForfeitDto);
            }

            TryCleanupPureAiRemainder(room);
            lock (room.SyncRoot)
            {
                responseDto = BuildRoomDtoLocked(
                    room,
                    playerId,
                    includeCallerCredentials: true,
                    includeJoinCode: room.Phase != MirageLobbyPhase.Closed);
            }
            return MirageLobbyOperationResult.Ok(responseDto);
        }

        private static bool IsExpiredLocked(MirageLobbyRoom room) => DateTime.UtcNow > room.ExpiresAtUtc;

        private static bool TryAuthorizeHumanSeatLocked(
            MirageLobbyRoom room,
            Guid playerId,
            string? token,
            out MirageLobbySeat? seat,
            out MirageLobbyOperationResult? fail)
        {
            seat = null;
            fail = null;
            if (IsExpiredLocked(room))
            {
                fail = MirageLobbyOperationResult.Fail(MirageLobbyError.Expired, "幻境房间已过期。");
                return false;
            }

            var found = room.Seats.FirstOrDefault(s =>
                s.Occupancy == MirageSeatOccupancy.Human && s.PlayerId == playerId);
            if (found == null)
            {
                fail = MirageLobbyOperationResult.Fail(MirageLobbyError.NotParticipant, "玩家不在该幻境房间内。");
                return false;
            }

            if (string.IsNullOrWhiteSpace(token) || !string.Equals(found.SeatToken, token, StringComparison.Ordinal))
            {
                fail = MirageLobbyOperationResult.Fail(MirageLobbyError.Forbidden, "房间凭证无效。");
                return false;
            }

            seat = found;
            return true;
        }

        private static void ResetSeatToEmptyLocked(MirageLobbySeat seat)
        {
            seat.Occupancy = MirageSeatOccupancy.Empty;
            seat.PlayerId = Guid.Empty;
            seat.SeatToken = string.Empty;
            seat.DisplayName = string.Empty;
            seat.AvatarUrl = string.Empty;
            seat.IsHost = false;
            seat.IsReady = false;
        }

        private void CloseRoomInternalLocked(MirageLobbyRoom room, string reason, out List<string> seatsToRetire)
        {
            seatsToRetire = new List<string>();
            if (room.Phase == MirageLobbyPhase.Closed)
            {
                return;
            }

            room.Phase = MirageLobbyPhase.Closed;
            room.ClosedReason = reason;
            room.RoomVersion++;
            room.ExpiresAtUtc = DateTime.UtcNow.Add(RoomLifetime.ClosedRetention);
            _joinCodeRegistry.Release(room.JoinCode, room.RoomId);

            room.RoundRecruitTimer?.Dispose();
            room.RoundRecruitTimer = null;
            room.ActiveTimerId = Guid.Empty;
            try
            {
                room.RoomLifetimeCts?.Cancel();
                room.RoomLifetimeCts?.Dispose();
            }
            catch
            {
                // ignore
            }
            room.RoomLifetimeCts = null;
            room.EliminatedSeatGhosts.Clear();

            foreach (var seat in room.Seats)
            {
                if (!string.IsNullOrWhiteSpace(seat.SeatGameId))
                {
                    seatsToRetire.Add(seat.SeatGameId!);
                    _seatGameToRoom.TryRemove(seat.SeatGameId!, out _);
                }
            }
        }

        private void RetireSeatGamesOutsideLock(List<string>? seatGameIds)
        {
            if (seatGameIds == null || seatGameIds.Count == 0)
            {
                return;
            }

            foreach (var seatGameId in seatGameIds)
            {
                _gameService.RetireMirageTournamentSeatGame(seatGameId);
            }
        }

        private void CleanupExpiredRooms()
        {
            foreach (var (roomId, room) in _rooms)
            {
                List<string>? seatsToRetire = null;
                bool removeEntry = false;
                lock (room.SyncRoot)
                {
                    if (!IsExpiredLocked(room))
                    {
                        continue;
                    }

                    if (room.Phase == MirageLobbyPhase.Closed)
                    {
                        removeEntry = true;
                    }
                    else
                    {
                        CloseRoomInternalLocked(room, "房间已过期。", out seatsToRetire);
                    }
                }

                RetireSeatGamesOutsideLock(seatsToRetire);
                if (removeEntry)
                {
                    _rooms.TryRemove(roomId, out _);
                }
            }
        }

        private async Task<(string DisplayName, string AvatarUrl)> ResolveUserIdentityAsync(
            Guid playerId,
            string? explicitName,
            string? explicitAvatar,
            string fallbackName,
            CancellationToken cancellationToken)
        {
            var name = explicitName?.Trim() ?? string.Empty;
            var avatar = explicitAvatar?.Trim() ?? string.Empty;
            if (playerId != Guid.Empty)
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetService<ApplicationDbContext>();
                    if (db != null)
                    {
                        var user = await db.Users
                            .AsNoTracking()
                            .Where(u => u.Id == playerId)
                            .Select(u => new { u.Username, u.AvatarUrl })
                            .FirstOrDefaultAsync(cancellationToken);
                        if (user != null)
                        {
                            if (!string.IsNullOrWhiteSpace(user.Username))
                            {
                                name = user.Username.Trim();
                            }
                            if (!string.IsNullOrWhiteSpace(user.AvatarUrl))
                            {
                                avatar = user.AvatarUrl.Trim();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[MirageLobby] Failed to resolve user identity for {PlayerId}", playerId);
                }
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                name = fallbackName;
            }
            if (name.Length > 24)
            {
                name = name[..24];
            }

            return (name, avatar);
        }

        private static MirageLobbyRoomDto BuildRoomDtoLocked(
            MirageLobbyRoom room,
            Guid? callerPlayerId,
            bool includeCallerCredentials,
            bool includeJoinCode)
        {
            var hostSeat = room.Seats.FirstOrDefault(s => s.IsHost) ?? room.Seats[0];
            var humans = room.Seats.Where(s => s.Occupancy == MirageSeatOccupancy.Human).ToList();
            var unreadyGuests = humans.Where(s => !s.IsHost && !s.IsReady).ToList();
            var canHostStart = room.Phase == MirageLobbyPhase.Waiting && unreadyGuests.Count == 0;
            string? startBlockReason = null;
            if (room.Phase == MirageLobbyPhase.Waiting && unreadyGuests.Count > 0)
            {
                startBlockReason = $"仍有 {unreadyGuests.Count} 名已加入访客未准备（可等待准备或将其移出）。";
            }

            var aliveCount = room.Phase == MirageLobbyPhase.Waiting
                ? MirageLobbyRoom.MaxSeatCount
                : room.Seats.Count(s => s.Occupancy != MirageSeatOccupancy.Empty && !s.IsEliminated);

            MirageLobbySeat? callerSeat = callerPlayerId.HasValue
                ? room.Seats.FirstOrDefault(s => s.Occupancy == MirageSeatOccupancy.Human && s.PlayerId == callerPlayerId.Value)
                : null;

            var recommendedSpectate = room.Seats
                .Where(s => s.Occupancy != MirageSeatOccupancy.Empty && !s.IsEliminated)
                .OrderByDescending(s => s.Occupancy == MirageSeatOccupancy.Human)
                .ThenByDescending(s => s.ProjectedCurrentHp)
                .ThenBy(s => s.SeatIndex)
                .Select(s => (Guid?)s.PlayerId)
                .FirstOrDefault();

            return new MirageLobbyRoomDto
            {
                RoomId = room.RoomId,
                RoomVersion = room.RoomVersion,
                JoinCode = includeJoinCode ? room.JoinCode : string.Empty,
                IsPublic = room.IsPublic,
                Phase = room.Phase.ToString(),
                MaxSeats = MirageLobbyRoom.MaxSeatCount,
                HumanCount = humans.Count,
                ReadyHumanCount = humans.Count(s => s.IsHost || s.IsReady),
                AliveCount = aliveCount,
                RoundNumber = room.RoundNumber,
                RecruitTimeoutSeconds = room.RecruitTimeoutSeconds,
                HostPlayerId = hostSeat.PlayerId,
                CanHostStart = canHostStart,
                StartBlockReason = startBlockReason,
                ClosedReason = room.ClosedReason,
                CreatedAtUtc = room.CreatedAtUtc,
                ExpiresAtUtc = room.ExpiresAtUtc,
                CallerSeatIndex = includeCallerCredentials ? callerSeat?.SeatIndex : null,
                CallerPlayerId = includeCallerCredentials ? callerSeat?.PlayerId : null,
                CallerSeatToken = includeCallerCredentials ? callerSeat?.SeatToken : null,
                CallerSeatGameId = includeCallerCredentials ? callerSeat?.SeatGameId : null,
                CallerRole = includeCallerCredentials && callerSeat != null
                    ? (callerSeat.IsHost ? "host" : "guest")
                    : null,
                CallerIsEliminated = callerSeat?.IsEliminated ?? false,
                CallerFinalPlacement = callerSeat?.FinalPlacement,
                RecommendedSpectatePlayerId = recommendedSpectate,
                Seats = room.Seats.Select(s => new MirageLobbySeatDto
                {
                    SeatIndex = s.SeatIndex,
                    Occupancy = s.Occupancy.ToString(),
                    PlayerId = s.Occupancy == MirageSeatOccupancy.Empty ? null : s.PlayerId,
                    DisplayName = s.DisplayName,
                    AvatarUrl = s.AvatarUrl,
                    IsHost = s.IsHost,
                    IsReady = s.IsHost || s.IsReady,
                    IsEliminated = s.IsEliminated,
                    FinalPlacement = s.FinalPlacement
                }).ToList()
            };
        }

        private async Task BroadcastLobbyUpdatedSafeAsync(string roomId, MirageLobbyRoomDto dto)
        {
            try
            {
                await _hubContext.Clients.Group(SignalRGroupNames.MirageRoom(roomId)).ReceiveMirageLobbyUpdated(dto);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[MirageLobby] BroadcastLobbyUpdated failed for room {RoomId}", roomId);
            }
        }

        private static string FormatPhaseLabel(MirageLobbyPhase phase) => phase switch
        {
            MirageLobbyPhase.Waiting => "等待中",
            MirageLobbyPhase.Recruit => "招募中",
            MirageLobbyPhase.Closing or MirageLobbyPhase.Simulating or MirageLobbyPhase.Applying => "交战中",
            MirageLobbyPhase.Finished => "已完赛",
            MirageLobbyPhase.Closed => "已关闭",
            _ => "未知"
        };

        private static string GenerateSeatToken()
        {
            Span<byte> tokenBytes = stackalloc byte[16];
            RandomNumberGenerator.Fill(tokenBytes);
            return Convert.ToBase64String(tokenBytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }
    }
}
