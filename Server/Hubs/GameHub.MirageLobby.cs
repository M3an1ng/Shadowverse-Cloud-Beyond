using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Hubs
{
    public partial class GameHub
    {
        internal sealed record DisconnectTimeoutActionPayload(string Reason, long ExpectedEpoch);

        private static readonly ConcurrentDictionary<(Guid GameId, Guid PlayerId), long> _disconnectEpochs = new();

        private static long BumpDisconnectEpoch(Guid gameId, Guid playerId)
        {
            return _disconnectEpochs.AddOrUpdate(
                (gameId, playerId),
                1L,
                static (_, prev) => prev + 1L);
        }

        internal static bool IsCurrentDisconnectEpoch(Guid gameId, Guid playerId, long expectedEpoch)
        {
            return _disconnectEpochs.TryGetValue((gameId, playerId), out var current) && current == expectedEpoch;
        }

        internal static void ReleaseDisconnectTrackingForGame(string gameId)
        {
            if (!Guid.TryParse(gameId, out var parsedGameId))
            {
                return;
            }

            foreach (var key in _disconnectEpochs.Keys)
            {
                if (key.GameId == parsedGameId)
                {
                    _disconnectEpochs.TryRemove(key, out _);
                }
            }

            var keyPrefix = $"{DisconnectCachePrefix}_{parsedGameId}_";
            foreach (var entry in _disconnectTimeouts)
            {
                if (entry.Key.StartsWith(keyPrefix, StringComparison.OrdinalIgnoreCase)
                    && _disconnectTimeouts.TryRemove(entry.Key, out var cts))
                {
                    try
                    {
                        cts.Cancel();
                        cts.Dispose();
                    }
                    catch
                    {
                        // Ignore disposal race
                    }
                }
            }
        }

        private async Task<bool> TryHandleMirageLobbyOnConnectedAsync(HttpContext httpContext)
        {
            if (_mirageLobbyService == null)
            {
                return false;
            }

            var explicitMirageRoomId = httpContext.Request.Query["mirageRoomId"].ToString().Trim();
            var roomIdQuery = httpContext.Request.Query["roomId"].ToString().Trim();
            var lobbyOnlyQuery = httpContext.Request.Query["lobbyOnly"].ToString().Trim();

            string targetMirageRoomId = string.Empty;
            if (!string.IsNullOrWhiteSpace(explicitMirageRoomId))
            {
                targetMirageRoomId = explicitMirageRoomId;
            }
            else if (!string.IsNullOrWhiteSpace(roomIdQuery) && _mirageLobbyService.GetRoom(roomIdQuery).Room != null)
            {
                targetMirageRoomId = roomIdQuery;
            }

            if (string.IsNullOrWhiteSpace(targetMirageRoomId))
            {
                return false;
            }

            var spectateQuery = httpContext.Request.Query["spectate"].ToString();
            var isSpectateFlag = string.Equals(spectateQuery, "1", StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(spectateQuery, "true", StringComparison.OrdinalIgnoreCase);
            var isLobbyOnly = string.Equals(lobbyOnlyQuery, "1", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(lobbyOnlyQuery, "true", StringComparison.OrdinalIgnoreCase);
            var playerIdStr = httpContext.Request.Query["playerId"].ToString();
            var token = httpContext.Request.Query["token"].ToString();
            var targetPlayerStr = httpContext.Request.Query["targetPlayerId"].ToString();
            Guid? preferredTargetPlayerId = Guid.TryParse(targetPlayerStr, out var parsedTarget) && parsedTarget != Guid.Empty
                ? parsedTarget
                : null;

            Context.Items["MirageRoomId"] = targetMirageRoomId;
            Context.Items["RoomId"] = targetMirageRoomId;

            if (isLobbyOnly)
            {
                string? lobbyErr = null;
                MirageLobbyRoomDto? lobbyRoomDto = null;
                if (!Guid.TryParse(playerIdStr, out var lobbyPlayerId)
                    || !_mirageLobbyService.TryValidateParticipant(
                        targetMirageRoomId,
                        lobbyPlayerId,
                        token,
                        out lobbyRoomDto,
                        out _,
                        out _,
                        out lobbyErr))
                {
                    await Clients.Caller.ReceiveError(lobbyErr ?? "幻境房间等待室验证失败。");
                    Context.Abort();
                    return true;
                }

                Context.Items["IsReadOnlySpectator"] = true;
                Context.Items["PlayerId"] = lobbyPlayerId.ToString("D");
                await Groups.AddToGroupAsync(Context.ConnectionId, SignalRGroupNames.MirageRoom(targetMirageRoomId));
                await Groups.AddToGroupAsync(Context.ConnectionId, SignalRGroupNames.Player(targetMirageRoomId, lobbyPlayerId));
                if (lobbyRoomDto != null)
                {
                    await Clients.Caller.ReceiveMirageLobbyUpdated(lobbyRoomDto);
                }
                await base.OnConnectedAsync();
                return true;
            }

            if (isSpectateFlag)
            {
                if (!_mirageLobbyService.TryResolveSpectateTarget(
                        targetMirageRoomId,
                        preferredTargetPlayerId,
                        out var resolvedTargetPlayerId,
                        out var resolvedSeatGameId,
                        out var roomVersion,
                        out var standings,
                        out var specErr))
                {
                    await Clients.Caller.ReceiveError(specErr ?? "当前无法观战该幻境房间。");
                    Context.Abort();
                    return true;
                }

                var specName = NormalizeChatDisplayName(
                    httpContext.Request.Query["spectatorName"].ToString(),
                    BuildSpectatorFallbackDisplayName());
                Context.Items["IsReadOnlySpectator"] = true;
                Context.Items["GameId"] = null;
                Context.Items["ViewingSeatGameId"] = resolvedSeatGameId;
                Context.Items["ViewingPlayerId"] = resolvedTargetPlayerId;
                Context.Items["ViewEpoch"] = 1;
                Context.Items["ParticipantType"] = ChatSenderType.Spectator;
                Context.Items["ParticipantId"] = NormalizeChatParticipantId(
                    httpContext.Request.Query["spectatorUserId"].ToString(),
                    Context.ConnectionId);
                Context.Items["DisplayName"] = specName;
                Context.Items["AvatarUrl"] = NormalizeChatAvatarUrl(httpContext.Request.Query["spectatorAvatar"].ToString());

                _mirageLobbyService.RegisterSpectatorConnection(targetMirageRoomId, Context.ConnectionId);
                await Groups.AddToGroupAsync(Context.ConnectionId, SignalRGroupNames.MirageRoom(targetMirageRoomId));
                await Groups.AddToGroupAsync(Context.ConnectionId, SignalRGroupNames.Game(resolvedSeatGameId));

                await SendMirageSeatStateToCallerAsync(
                    httpContext.RequestServices,
                    resolvedSeatGameId,
                    resolvedTargetPlayerId,
                    viewEpoch: 1,
                    roomVersion: roomVersion);
                if (standings != null)
                {
                    await Clients.Caller.ReceiveMirageStandings(standings);
                }
                await SendRecentChatHistoryToCallerAsync(targetMirageRoomId);
                await base.OnConnectedAsync();
                return true;
            }

            // Authenticated tournament participant connection
            if (!Guid.TryParse(playerIdStr, out var participantGuid) || string.IsNullOrWhiteSpace(token))
            {
                await Clients.Caller.ReceiveError("幻境房间连接参数无效。");
                Context.Abort();
                return true;
            }

            if (!_mirageLobbyService.TryValidateParticipant(
                    targetMirageRoomId,
                    participantGuid,
                    token,
                    out var participantRoomDto,
                    out var participantSeatGameId,
                    out var isEliminated,
                    out var authErr)
                || string.IsNullOrWhiteSpace(participantSeatGameId)
                || !Guid.TryParse(participantSeatGameId, out var seatGameGuid))
            {
                await Clients.Caller.ReceiveError(authErr ?? "幻境房间验证失败或尚未开局。");
                Context.Abort();
                return true;
            }

            var displayName = await ResolveParticipantDisplayNameAsync(participantGuid, "玩家");
            Context.Items["PlayerId"] = participantGuid.ToString("D");
            Context.Items["ParticipantType"] = ChatSenderType.Player;
            Context.Items["ParticipantId"] = participantGuid.ToString("D");
            Context.Items["DisplayName"] = displayName;

            await Groups.AddToGroupAsync(Context.ConnectionId, SignalRGroupNames.MirageRoom(targetMirageRoomId));

            // Eliminated participant reconnecting or staying to spectate
            if (isEliminated && preferredTargetPlayerId.HasValue && preferredTargetPlayerId.Value != participantGuid)
            {
                if (_mirageLobbyService.TryResolveSpectateTarget(
                        targetMirageRoomId,
                        preferredTargetPlayerId,
                        out var targetPid,
                        out var targetSeatGid,
                        out var rVer,
                        out var specStandings,
                        out _))
                {
                    Context.Items["IsReadOnlySpectator"] = true;
                    Context.Items["GameId"] = null;
                    Context.Items["ViewingSeatGameId"] = targetSeatGid;
                    Context.Items["ViewingPlayerId"] = targetPid;
                    Context.Items["ViewEpoch"] = 1;

                    _mirageLobbyService.RegisterSpectatorConnection(targetMirageRoomId, Context.ConnectionId);
                    await Groups.AddToGroupAsync(Context.ConnectionId, SignalRGroupNames.Game(targetSeatGid));
                    await SendMirageSeatStateToCallerAsync(httpContext.RequestServices, targetSeatGid, targetPid, 1, rVer);
                    if (specStandings != null)
                    {
                        await Clients.Caller.ReceiveMirageStandings(specStandings);
                    }
                    await SendRecentChatHistoryToCallerAsync(targetMirageRoomId);
                    await base.OnConnectedAsync();
                    return true;
                }
            }

            // Active participant (or eliminated participant viewing own final board before clicking Stay & Spectate)
            Context.Items["IsReadOnlySpectator"] = isEliminated;
            Context.Items["GameId"] = isEliminated ? null : participantSeatGameId;
            Context.Items["ViewingSeatGameId"] = participantSeatGameId;
            Context.Items["ViewingPlayerId"] = participantGuid;
            Context.Items["ViewEpoch"] = 1;

            await Groups.AddToGroupAsync(Context.ConnectionId, SignalRGroupNames.Game(participantSeatGameId));
            if (!isEliminated)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, SignalRGroupNames.Player(participantSeatGameId, participantGuid));
                BumpDisconnectEpoch(seatGameGuid, participantGuid);
                _playerConnections.Register(seatGameGuid, participantGuid, Context.ConnectionId);
                CancelDisconnectTimeout(seatGameGuid, participantGuid);
                _mirageLobbyService.NotifySeatConnected(targetMirageRoomId, participantGuid);
                _gameService.MarkPlayerReconnected(participantSeatGameId, participantGuid);
            }
            else
            {
                _mirageLobbyService.RegisterSpectatorConnection(targetMirageRoomId, Context.ConnectionId);
            }

            await SendMirageSeatStateToCallerAsync(
                httpContext.RequestServices,
                participantSeatGameId,
                participantGuid,
                viewEpoch: 1,
                roomVersion: participantRoomDto?.RoomVersion);

            var currentStandings = _mirageLobbyService.GetStandingsSnapshot(targetMirageRoomId);
            if (currentStandings != null)
            {
                await Clients.Caller.ReceiveMirageStandings(currentStandings);
            }
            await SendRecentChatHistoryToCallerAsync(targetMirageRoomId);

            if (!isEliminated)
            {
                await _gameService.RefreshHandPlayabilityAsync(participantSeatGameId, participantGuid, Context.ConnectionAborted);
                await _gameService.RefreshActPlayabilityAsync(participantSeatGameId, participantGuid, Context.ConnectionAborted);
            }

            await base.OnConnectedAsync();
            return true;
        }

        public async Task SwitchMirageSpectateTarget(Guid targetPlayerId, int? clientViewEpoch = null)
        {
            if (_mirageLobbyService == null)
            {
                await Clients.Caller.ReceiveError("幻境大厅服务未启用。");
                return;
            }

            if (!Context.Items.TryGetValue("MirageRoomId", out var roomObj)
                || roomObj is not string mirageRoomId
                || string.IsNullOrWhiteSpace(mirageRoomId))
            {
                await Clients.Caller.ReceiveError("当前连接不属于 8 人幻境房间。");
                return;
            }

            // If an active player is not eliminated yet, disallow switching away from their own seat
            if (Context.Items.TryGetValue("GameId", out var activeGameObj)
                && activeGameObj is string activeSeatGameId
                && !string.IsNullOrWhiteSpace(activeSeatGameId))
            {
                var selfGame = _gameService.GetGameState(activeSeatGameId);
                if (selfGame is { IsGameOver: false })
                {
                    await Clients.Caller.ReceiveError("您仍在参赛中，淘汰后方可切换观战其他选手视角。");
                    return;
                }

                // Transition eliminated participant in-place into read-only spectator mode
                if (Context.Items.TryGetValue("PlayerId", out var pidObj)
                    && pidObj is string pidStr
                    && Guid.TryParse(pidStr, out var selfPid))
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, SignalRGroupNames.Player(activeSeatGameId, selfPid));
                    if (Guid.TryParse(activeSeatGameId, out var selfSeatGuid))
                    {
                        BumpDisconnectEpoch(selfSeatGuid, selfPid);
                        _playerConnections.Unregister(selfSeatGuid, selfPid, Context.ConnectionId);
                        CancelDisconnectTimeout(selfSeatGuid, selfPid);
                    }
                }
                Context.Items["GameId"] = null;
                Context.Items["IsReadOnlySpectator"] = true;
                _mirageLobbyService.RegisterSpectatorConnection(mirageRoomId, Context.ConnectionId);
            }

            if (!_mirageLobbyService.TryResolveSpectateTarget(
                    mirageRoomId,
                    targetPlayerId,
                    out var resolvedTargetPlayerId,
                    out var resolvedSeatGameId,
                    out var roomVersion,
                    out var standings,
                    out var error))
            {
                await Clients.Caller.ReceiveError(error ?? "无法切换至该观战目标。");
                return;
            }

            if (Context.Items.TryGetValue("ViewingSeatGameId", out var oldSeatObj)
                && oldSeatObj is string oldSeatGameId
                && !string.IsNullOrWhiteSpace(oldSeatGameId)
                && !string.Equals(oldSeatGameId, resolvedSeatGameId, StringComparison.OrdinalIgnoreCase))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, SignalRGroupNames.Game(oldSeatGameId));
            }

            var currentEpoch = Context.Items.TryGetValue("ViewEpoch", out var epObj) && epObj is int prevEp ? prevEp : 1;
            var nextEpoch = Math.Max(currentEpoch + 1, clientViewEpoch ?? (currentEpoch + 1));

            Context.Items["ViewingSeatGameId"] = resolvedSeatGameId;
            Context.Items["ViewingPlayerId"] = resolvedTargetPlayerId;
            Context.Items["ViewEpoch"] = nextEpoch;

            await Groups.AddToGroupAsync(Context.ConnectionId, SignalRGroupNames.Game(resolvedSeatGameId));

            var httpContext = Context.GetHttpContext();
            if (httpContext != null)
            {
                await SendMirageSeatStateToCallerAsync(
                    httpContext.RequestServices,
                    resolvedSeatGameId,
                    resolvedTargetPlayerId,
                    nextEpoch,
                    roomVersion);
            }

            if (standings != null)
            {
                await Clients.Caller.ReceiveMirageStandings(standings);
            }
        }

        private async Task SendMirageSeatStateToCallerAsync(
            IServiceProvider requestServices,
            string seatGameId,
            Guid viewingPlayerId,
            int viewEpoch,
            long? roomVersion)
        {
            if (_gameService.GetGameState(seatGameId) == null)
            {
                await Clients.Caller.ReceiveError("目标席位对局状态不可用。");
                return;
            }

            GameStateDto? dto = null;
            var read = new MatchStateRead((state, sp) =>
            {
                var acc = sp.GetRequiredService<IGameContextAccessor>();
                using var ctx = new GameContext(sp, _mapper, acc, state, viewingPlayerId, Context.ConnectionAborted);
                dto = GameStateProjection.Create(state, ctx);
                return Task.FromResult(System.Text.Json.JsonSerializer.SerializeToElement(true));
            })
            {
                CancellationToken = Context.ConnectionAborted
            };

            if (await _gameService.EnqueuePlayerActionAsync(seatGameId, read))
            {
                var completion = await read.Completion.WaitAsync(Context.ConnectionAborted);
                if (completion.Status != PlayerActionCompletionStatus.Completed)
                {
                    dto = null;
                }
            }

            if (dto == null)
            {
                await Clients.Caller.ReceiveError("目标席位对局状态同步失败。");
                return;
            }

            dto.ViewingPlayerId = viewingPlayerId;
            dto.ViewEpoch = viewEpoch;
            dto.RoomVersion = roomVersion;

            await Clients.Caller.ReceiveGameState(dto);
            await SendMatchHistoryToCallerAsync(seatGameId);
        }
    }
}
