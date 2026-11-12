using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Hubs;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services
{
    public sealed partial class MirageLobbyService
    {
        private const int PostEliminationGraceSeconds = 20;

        public async Task<MirageLobbyOperationResult> StartRoomAsync(
            string roomId,
            Guid hostPlayerId,
            string hostToken,
            CancellationToken cancellationToken = default)
        {
            if (!_rooms.TryGetValue(roomId ?? string.Empty, out var room))
            {
                return MirageLobbyOperationResult.Fail(MirageLobbyError.NotFound, "幻境房间不存在。");
            }

            // Ensure Mirage card pool is non-empty before mutating room state
            var probeRule = new MirageRuleConfig();
            if (_shopService.GetPoolUpToTier(probeRule.MaxTavernTier, probeRule.MaxTavernTier).Count == 0)
            {
                return MirageLobbyOperationResult.Fail(MirageLobbyError.InvalidOperation, "幻境专属卡池为空，请先在幻境卡牌工坊录入随从卡牌。");
            }

            List<(int SeatIndex, Guid PlayerId, bool IsAi, string DisplayName, string AvatarUrl, string SeatGameId)> seatSpecs;
            MirageMatchState sharedPoolHolder;
            DateTime round1StartUtc;
            DateTime round1EndsUtc;
            int recruitTimeout;
            CancellationToken roomToken;

            lock (room.SyncRoot)
            {
                if (!TryAuthorizeHumanSeatLocked(room, hostPlayerId, hostToken, out var hostSeat, out var fail))
                {
                    return fail!;
                }

                if (!hostSeat!.IsHost)
                {
                    return MirageLobbyOperationResult.Fail(MirageLobbyError.Forbidden, "仅房主可以开始对局。");
                }

                if (room.Phase == MirageLobbyPhase.Closed)
                {
                    return MirageLobbyOperationResult.Fail(MirageLobbyError.AlreadyClosed, room.ClosedReason ?? "房间已关闭。");
                }

                if (room.Phase != MirageLobbyPhase.Waiting)
                {
                    return MirageLobbyOperationResult.Ok(
                        BuildRoomDtoLocked(room, hostPlayerId, includeCallerCredentials: true, includeJoinCode: true));
                }

                var unreadyGuests = room.Seats
                    .Where(s => s.Occupancy == MirageSeatOccupancy.Human && !s.IsHost && !s.IsReady)
                    .ToList();
                if (unreadyGuests.Count > 0)
                {
                    return MirageLobbyOperationResult.Fail(
                        MirageLobbyError.NotReady,
                        $"仍有 {unreadyGuests.Count} 名已入座真人玩家未准备就绪。");
                }

                // Fill all empty seats with AI opponents
                var usedNames = new HashSet<string>(
                    room.Seats.Where(s => s.Occupancy == MirageSeatOccupancy.Human).Select(s => s.DisplayName),
                    StringComparer.OrdinalIgnoreCase);
                int aiNameCursor = 0;

                foreach (var seat in room.Seats)
                {
                    if (seat.Occupancy == MirageSeatOccupancy.Empty)
                    {
                        string aiName;
                        do
                        {
                            aiName = aiNameCursor < PresetAiNames.Length
                                ? PresetAiNames[aiNameCursor++]
                                : $"幻境AI·{seat.SeatIndex + 1}";
                        } while (!usedNames.Add(aiName));

                        seat.Occupancy = MirageSeatOccupancy.Ai;
                        seat.PlayerId = Guid.NewGuid();
                        seat.SeatToken = string.Empty;
                        seat.DisplayName = aiName;
                        seat.AvatarUrl = string.Empty;
                        seat.IsHost = false;
                        seat.IsReady = true;
                    }

                    var seatGameId = Guid.NewGuid().ToString("D");
                    seat.SeatGameId = seatGameId;
                    seat.IsEliminated = false;
                    seat.EliminatedRound = null;
                    seat.FinalPlacement = null;
                    seat.PendingForfeit = false;
                    seat.ProjectedCurrentHp = GameConstants.Health.PlayerMaxHealth;
                    seat.ProjectedMaxHp = GameConstants.Health.PlayerMaxHealth;
                    seat.ProjectedTavernTier = 1;
                    seat.ProjectedLeaderCardId = GameConstants.Cards.MirageLeaderCardId;
                    seat.ProjectedIsRoundLocked = false;
                    _seatGameToRoom[seatGameId] = room.RoomId;
                }

                recruitTimeout = room.RecruitTimeoutSeconds;
                round1StartUtc = DateTime.UtcNow;
                round1EndsUtc = round1StartUtc.AddSeconds(recruitTimeout);

                sharedPoolHolder = new MirageMatchState
                {
                    TournamentRoomId = room.RoomId,
                    RoundNumber = 1,
                    SharedPoolSyncRoot = room.SharedPoolSyncRoot,
                    SharedCardPool = room.SharedCardPool,
                    InitialCardPoolCaps = room.InitialCardPoolCaps,
                    ReturnedEliminatedPlayerIds = room.ReturnedEliminatedPlayerIds,
                    Rules = new MirageRuleConfig
                    {
                        RecruitTimeoutSeconds = recruitTimeout
                    }
                };
                _shopService.InitializeSharedCardPool(sharedPoolHolder);

                room.RoundNumber = 1;
                room.Phase = MirageLobbyPhase.Recruit;
                room.RoundStartedAtUtc = round1StartUtc;
                room.RoundEndsAtUtc = round1EndsUtc;
                room.RoomLifetimeCts = new CancellationTokenSource();
                roomToken = room.RoomLifetimeCts.Token;
                room.ExpiresAtUtc = DateTime.UtcNow.Add(room.RoomTtl);

                seatSpecs = room.Seats
                    .Select(s => (s.SeatIndex, s.PlayerId, s.Occupancy == MirageSeatOccupancy.Ai, s.DisplayName, s.AvatarUrl, s.SeatGameId!))
                    .ToList();
            }

            // Create all 8 SeatGames outside the room lock
            var createdAll = true;
            foreach (var spec in seatSpecs)
            {
                var created = await _gameService.CreateMirageTournamentSeatGameAsync(
                    spec.SeatGameId,
                    room.RoomId,
                    spec.SeatIndex,
                    spec.PlayerId,
                    spec.IsAi,
                    spec.DisplayName,
                    spec.AvatarUrl,
                    recruitTimeout,
                    round1StartUtc,
                    round1EndsUtc,
                    sharedPoolHolder,
                    cancellationToken);
                if (created == null)
                {
                    createdAll = false;
                    break;
                }
            }

            if (!createdAll)
            {
                List<string> failedSeats;
                lock (room.SyncRoot)
                {
                    CloseRoomInternalLocked(room, "初始化八人幻境对局状态失败。", out failedSeats);
                }
                RetireSeatGamesOutsideLock(failedSeats);
                return MirageLobbyOperationResult.Fail(MirageLobbyError.InvalidOperation, "初始化 8 人幻境战局失败。");
            }

            MirageLobbyRoomDto hostDto;
            MirageLobbyRoomDto publicLobbyDto;
            MirageStandingsSnapshotDto initialStandings;
            List<string> aiSeatGameIds;

            lock (room.SyncRoot)
            {
                RefreshSeatProjectionsFromLiveStatesLocked(room);
                room.CurrentRoundPairings = BuildRoundPairingsLocked(room, 1);
                room.RoomVersion++;
                initialStandings = BuildStandingsSnapshotLocked(room, settledRoundNumber: null);
                room.LatestStandingsSnapshot = initialStandings;

                StartRoomRecruitTimerLocked(room, 1, round1EndsUtc);
                hostDto = BuildRoomDtoLocked(room, hostPlayerId, includeCallerCredentials: true, includeJoinCode: true);
                publicLobbyDto = BuildRoomDtoLocked(room, null, includeCallerCredentials: false, includeJoinCode: true);
                aiSeatGameIds = room.Seats
                    .Where(s => s.Occupancy == MirageSeatOccupancy.Ai && !string.IsNullOrWhiteSpace(s.SeatGameId))
                    .Select(s => s.SeatGameId!)
                    .ToList();
            }

            await BroadcastLobbyUpdatedSafeAsync(room.RoomId, publicLobbyDto);
            await BroadcastStandingsSafeAsync(room.RoomId, initialStandings);

            foreach (var aiSeatGameId in aiSeatGameIds)
            {
                _gameService.TriggerMirageAiRecruitIfNeeded(aiSeatGameId, 1);
            }

            _ = roomToken;
            return MirageLobbyOperationResult.Ok(hostDto);
        }

        public bool TryValidateParticipant(
            string roomId,
            Guid playerId,
            string token,
            out MirageLobbyRoomDto? roomDto,
            out string? seatGameId,
            out bool isEliminated,
            out string? errorMessage)
        {
            roomDto = null;
            seatGameId = null;
            isEliminated = false;
            errorMessage = null;

            if (!_rooms.TryGetValue(roomId ?? string.Empty, out var room))
            {
                errorMessage = "幻境房间不存在。";
                return false;
            }

            lock (room.SyncRoot)
            {
                if (!TryAuthorizeHumanSeatLocked(room, playerId, token, out var seat, out var fail))
                {
                    errorMessage = fail?.Message ?? "身份校验失败。";
                    return false;
                }

                if (room.Phase == MirageLobbyPhase.Closed)
                {
                    errorMessage = room.ClosedReason ?? "房间已关闭。";
                    return false;
                }

                room.ExpiresAtUtc = DateTime.UtcNow.Add(room.RoomTtl);
                seatGameId = seat!.SeatGameId;
                isEliminated = seat.IsEliminated;
                roomDto = BuildRoomDtoLocked(room, playerId, includeCallerCredentials: true, includeJoinCode: true);
                return true;
            }
        }

        public bool TryResolveSpectateTarget(
            string roomId,
            Guid? preferredTargetPlayerId,
            out Guid resolvedTargetPlayerId,
            out string resolvedTargetSeatGameId,
            out long roomVersion,
            out MirageStandingsSnapshotDto? standings,
            out string? errorMessage)
        {
            resolvedTargetPlayerId = Guid.Empty;
            resolvedTargetSeatGameId = string.Empty;
            roomVersion = 0;
            standings = null;
            errorMessage = null;

            if (!_rooms.TryGetValue(roomId ?? string.Empty, out var room))
            {
                errorMessage = "幻境房间不存在。";
                return false;
            }

            lock (room.SyncRoot)
            {
                if (room.Phase == MirageLobbyPhase.Closed || IsExpiredLocked(room))
                {
                    errorMessage = room.ClosedReason ?? "幻境房间已关闭。";
                    return false;
                }

                if (room.Phase == MirageLobbyPhase.Waiting)
                {
                    errorMessage = "幻境房间尚未开始对局，暂不可观战。";
                    return false;
                }

                MirageLobbySeat? targetSeat = null;
                if (preferredTargetPlayerId.HasValue && preferredTargetPlayerId.Value != Guid.Empty)
                {
                    targetSeat = room.Seats.FirstOrDefault(s =>
                        s.Occupancy != MirageSeatOccupancy.Empty
                        && s.PlayerId == preferredTargetPlayerId.Value
                        && !string.IsNullOrWhiteSpace(s.SeatGameId));
                    if (targetSeat is { IsEliminated: true } && room.Phase != MirageLobbyPhase.Finished)
                    {
                        // Fallback if requested target was just eliminated unless caller explicitly views finished winner
                        var aliveFallback = PickDefaultSpectateSeatLocked(room);
                        if (aliveFallback != null)
                        {
                            targetSeat = aliveFallback;
                        }
                    }
                }

                targetSeat ??= PickDefaultSpectateSeatLocked(room)
                    ?? room.Seats.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.SeatGameId));

                if (targetSeat == null || string.IsNullOrWhiteSpace(targetSeat.SeatGameId))
                {
                    errorMessage = "当前没有可观战的席位。";
                    return false;
                }

                resolvedTargetPlayerId = targetSeat.PlayerId;
                resolvedTargetSeatGameId = targetSeat.SeatGameId!;
                roomVersion = room.RoomVersion;
                standings = room.LatestStandingsSnapshot ?? BuildStandingsSnapshotLocked(room, room.LastSettledRoundNumber);
                return true;
            }
        }

        public MirageStandingsSnapshotDto? GetStandingsSnapshot(string roomId)
        {
            if (!_rooms.TryGetValue(roomId ?? string.Empty, out var room))
            {
                return null;
            }

            lock (room.SyncRoot)
            {
                if (room.Phase is MirageLobbyPhase.Waiting or MirageLobbyPhase.Closed)
                {
                    return null;
                }

                return room.LatestStandingsSnapshot ?? BuildStandingsSnapshotLocked(room, room.LastSettledRoundNumber);
            }
        }

        public bool TryGetRoomIdBySeatGameId(string seatGameId, out string roomId)
        {
            if (string.IsNullOrWhiteSpace(seatGameId))
            {
                roomId = string.Empty;
                return false;
            }

            return _seatGameToRoom.TryGetValue(seatGameId, out roomId!);
        }

        public void RefreshExpiration(string roomId, Guid playerId)
        {
            if (!_rooms.TryGetValue(roomId ?? string.Empty, out var room))
            {
                return;
            }

            lock (room.SyncRoot)
            {
                if (room.Phase != MirageLobbyPhase.Closed && !IsExpiredLocked(room))
                {
                    room.ExpiresAtUtc = DateTime.UtcNow.Add(room.RoomTtl);
                }
            }
        }

        public void NotifySeatConnected(string roomId, Guid playerId)
        {
            if (!_rooms.TryGetValue(roomId ?? string.Empty, out var room))
            {
                return;
            }

            lock (room.SyncRoot)
            {
                room.HumanGraceDeadlinesUtc.Remove(playerId);
                if (room.Phase != MirageLobbyPhase.Closed)
                {
                    room.ExpiresAtUtc = DateTime.UtcNow.Add(room.RoomTtl);
                }
            }
        }

        public void NotifySeatDisconnected(string roomId, Guid playerId, TimeSpan gracePeriod)
        {
            if (!_rooms.TryGetValue(roomId ?? string.Empty, out var room))
            {
                return;
            }

            lock (room.SyncRoot)
            {
                var seat = room.Seats.FirstOrDefault(s => s.Occupancy == MirageSeatOccupancy.Human && s.PlayerId == playerId);
                if (seat != null && !seat.HasLeftPostElimination)
                {
                    room.HumanGraceDeadlinesUtc[playerId] = DateTime.UtcNow.Add(gracePeriod);
                }
            }
        }

        public void RegisterSpectatorConnection(string roomId, string connectionId)
        {
            if (!_rooms.TryGetValue(roomId ?? string.Empty, out var room) || string.IsNullOrWhiteSpace(connectionId))
            {
                return;
            }

            lock (room.SyncRoot)
            {
                room.ActiveSpectatorConnectionIds.Add(connectionId);
                if (room.Phase != MirageLobbyPhase.Closed)
                {
                    room.ExpiresAtUtc = DateTime.UtcNow.Add(room.RoomTtl);
                }
            }
        }

        public void UnregisterSpectatorConnection(string roomId, string connectionId)
        {
            if (!_rooms.TryGetValue(roomId ?? string.Empty, out var room) || string.IsNullOrWhiteSpace(connectionId))
            {
                return;
            }

            lock (room.SyncRoot)
            {
                room.ActiveSpectatorConnectionIds.Remove(connectionId);
            }

            TryCleanupPureAiRemainder(room);
        }

        public void NotifySeatLocked(string roomId, Guid playerId, int roundNumber)
        {
            if (!_rooms.TryGetValue(roomId ?? string.Empty, out var room))
            {
                return;
            }

            bool allAliveLocked = false;
            MirageStandingsSnapshotDto? progressStandings = null;

            lock (room.SyncRoot)
            {
                if (room.Phase != MirageLobbyPhase.Recruit || room.RoundNumber != roundNumber)
                {
                    return;
                }

                var seat = room.Seats.FirstOrDefault(s => s.PlayerId == playerId && !s.IsEliminated);
                if (seat == null)
                {
                    return;
                }

                seat.ProjectedIsRoundLocked = true;
                RefreshSingleSeatProjectionLocked(seat);
                room.RoomVersion++;

                var aliveSeats = room.Seats.Where(s => s.Occupancy != MirageSeatOccupancy.Empty && !s.IsEliminated).ToList();
                allAliveLocked = aliveSeats.Count > 0 && aliveSeats.All(s => s.ProjectedIsRoundLocked);
                progressStandings = BuildStandingsSnapshotLocked(room, room.LastSettledRoundNumber);
                room.LatestStandingsSnapshot = progressStandings;
            }

            if (progressStandings != null)
            {
                _ = BroadcastStandingsSafeAsync(room.RoomId, progressStandings);
            }

            if (allAliveLocked)
            {
                using (ExecutionContext.SuppressFlow())
                {
                    _ = Task.Run(() => ExecuteRoundTransitionAsync(room.RoomId, roundNumber));
                }
            }
        }

        public async Task ForfeitSeatAsync(
            string roomId,
            Guid playerId,
            string reason,
            CancellationToken cancellationToken = default,
            long? expectedDisconnectEpoch = null)
        {
            if (!_rooms.TryGetValue(roomId ?? string.Empty, out var room))
            {
                return;
            }

            bool isPreFreezeRecruit = false;
            string? seatGameId = null;
            int assignedPlacement = 0;
            int currentRound = 1;
            int seatIndex = -1;
            string displayName = string.Empty;
            string avatarUrl = string.Empty;
            TaskCompletionSource<bool>? forfeitBarrierTcs = null;
            CancellationToken roomLifetimeToken = CancellationToken.None;

            lock (room.SyncRoot)
            {
                if (room.Phase is MirageLobbyPhase.Closed or MirageLobbyPhase.Finished or MirageLobbyPhase.Waiting)
                {
                    return;
                }

                var seat = room.Seats.FirstOrDefault(s => s.PlayerId == playerId);
                if (seat == null || seat.IsEliminated)
                {
                    return;
                }

                if (expectedDisconnectEpoch.HasValue
                    && Guid.TryParse(seat.SeatGameId, out var sgGuid)
                    && !GameHub.IsCurrentDisconnectEpoch(sgGuid, playerId, expectedDisconnectEpoch.Value))
                {
                    return;
                }

                currentRound = room.RoundNumber;
                seatIndex = seat.SeatIndex;
                seatGameId = seat.SeatGameId;
                displayName = seat.DisplayName;
                avatarUrl = seat.AvatarUrl;
                roomLifetimeToken = room.RoomLifetimeCts?.Token ?? CancellationToken.None;
                room.HumanGraceDeadlinesUtc.Remove(playerId);

                if (room.Phase == MirageLobbyPhase.Recruit)
                {
                    isPreFreezeRecruit = true;
                    var aliveBefore = room.Seats.Count(s => s.Occupancy != MirageSeatOccupancy.Empty && !s.IsEliminated);
                    assignedPlacement = Math.Max(1, aliveBefore);
                    seat.IsEliminated = true;
                    seat.EliminatedRound = currentRound;
                    seat.FinalPlacement = assignedPlacement;
                    seat.ForfeitReason = reason;
                    seat.HasLeftPostElimination = true;
                    seat.ProjectedCurrentHp = 0;
                    seat.ProjectedIsRoundLocked = true;

                    forfeitBarrierTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    room.InFlightRecruitForfeits.Add(forfeitBarrierTcs.Task);
                }
                else
                {
                    // Already frozen/simulating/applying: preserve combat inputs, record monotonic forfeit sequence
                    if (!seat.PendingForfeit)
                    {
                        seat.PendingForfeit = true;
                        seat.PendingForfeitSequence = ++room.NextForfeitSequence;
                    }
                    seat.ForfeitReason = reason;
                    seat.HasLeftPostElimination = true;
                    return;
                }
            }

            if (!isPreFreezeRecruit)
            {
                return;
            }

            bool triggerRoundClose = false;
            bool soleSurvivorChampion = false;
            MirageLobbySeat? championSeat = null;
            MirageStandingsSnapshotDto? updatedStandings = null;

            try
            {
                // Capture current valid warband snapshot at FIFO boundary (bound to room lifetime, NEVER caller HTTP cancellation!)
                MirageCombatSideSnapshot? capturedForfeitSnapshot = null;
                int capturedTier = 1;
                int capturedLeaderDefId = GameConstants.Cards.MirageLeaderCardId;
                try
                {
                    if (!string.IsNullOrWhiteSpace(seatGameId)
                        && _gameService.GetGameState(seatGameId) is { } liveSeatGame
                        && liveSeatGame.Mirage != null)
                    {
                        lock (liveSeatGame.Mirage.RoundSyncLock)
                        {
                            liveSeatGame.Mirage.ClosingRecruitRoundNumber = currentRound;
                        }
                        var pending = _pendingActionService.GetPendingChoiceForPlayer(seatGameId, playerId);
                        if (pending != null)
                        {
                            _pendingActionService.ForceResolveChoice(
                                pending.Request.PendingChoiceId,
                                PendingActionService.CreateTimeoutChoiceResult(pending.Request));
                        }

                        var captureAction = new MatchStateRead((state, _) =>
                        {
                            capturedTier = state.Player1.Mirage?.TavernTier ?? 1;
                            capturedLeaderDefId = state.Player1.Leader?.BaseDefinitionId ?? GameConstants.Cards.MirageLeaderCardId;
                            capturedForfeitSnapshot = _snapshotBuilder.CaptureSideSnapshot(state, playerId);
                            _shopService.ReturnEliminatedPlayerCardsToPool(state, playerId);
                            return Task.FromResult(JsonSerializer.SerializeToElement(true));
                        })
                        {
                            CancellationToken = roomLifetimeToken
                        };
                        if (await _gameService.EnqueuePlayerActionAsync(seatGameId, captureAction))
                        {
                            await captureAction.Completion.WaitAsync(roomLifetimeToken);
                        }

                        var terminateEvent = new MirageTournamentRoundSettledEvent(
                            Guid.NewGuid(),
                            currentRound,
                            0,
                            liveSeatGame.Player1.CurrentHealth,
                            0,
                            "退赛",
                            isCombatTie: false,
                            selfWonCombat: false,
                            selfPermanentGrowthLog: null,
                            MirageTournamentSeatOutcomeKind.Eliminated,
                            assignedPlacement,
                            $"第 {assignedPlacement} 名 · {reason}",
                            preRolledNextShopOffers: null,
                            nextRoundStartedAtUtc: null,
                            nextRoundEndsAtUtc: null,
                            nextRoundTimeoutSeconds: null);
                        var terminateWrapper = new PlayerActionWrapper("MirageTournamentSettle", playerId, terminateEvent)
                        {
                            CancellationToken = roomLifetimeToken
                        };
                        if (await _gameService.EnqueuePlayerActionAsync(seatGameId, terminateWrapper))
                        {
                            await terminateWrapper.Completion.WaitAsync(roomLifetimeToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[MirageLobby] Pre-freeze forfeit cleanup warning for seat {SeatGameId}", seatGameId);
                }

                lock (room.SyncRoot)
                {
                    if (capturedForfeitSnapshot != null)
                    {
                        UpsertEliminatedGhostLocked(
                            room,
                            seatIndex,
                            playerId,
                            displayName,
                            avatarUrl,
                            currentRound,
                            capturedTier,
                            capturedLeaderDefId,
                            capturedForfeitSnapshot);
                    }

                    // Patch current round pairing so the forfeited player's paired opponent faces this ghost instead
                    for (int i = 0; i < room.CurrentRoundPairings.Count; i++)
                    {
                        var p = room.CurrentRoundPairings[i];
                        if (p.SeatIndexA == seatIndex && p.SeatIndexB.HasValue)
                        {
                            room.CurrentRoundPairings[i] = new MirageRoomPairing(p.SeatIndexB.Value, null, seatIndex, p.CombatId);
                        }
                        else if (p.SeatIndexB == seatIndex)
                        {
                            room.CurrentRoundPairings[i] = new MirageRoomPairing(p.SeatIndexA, null, seatIndex, p.CombatId);
                        }
                        else if (p.SeatIndexA == seatIndex && !p.SeatIndexB.HasValue)
                        {
                            room.CurrentRoundPairings.RemoveAt(i);
                            i--;
                        }
                    }

                    var aliveSeats = room.Seats.Where(s => s.Occupancy != MirageSeatOccupancy.Empty && !s.IsEliminated).ToList();
                    if (aliveSeats.Count == 1 && room.Phase != MirageLobbyPhase.Closed)
                    {
                        soleSurvivorChampion = true;
                        championSeat = aliveSeats[0];
                        championSeat.FinalPlacement = 1;
                        room.ChampionPlayerId = championSeat.PlayerId;
                        room.Phase = MirageLobbyPhase.Finished;
                        room.RoundRecruitTimer?.Dispose();
                        room.RoundRecruitTimer = null;
                    }
                    else if (room.Phase == MirageLobbyPhase.Recruit
                             && room.RoundNumber == currentRound
                             && aliveSeats.Count >= 2
                             && aliveSeats.All(s => s.ProjectedIsRoundLocked))
                    {
                        triggerRoundClose = true;
                    }

                    room.RoomVersion++;
                    updatedStandings = BuildStandingsSnapshotLocked(room, room.LastSettledRoundNumber);
                    room.LatestStandingsSnapshot = updatedStandings;
                }
            }
            finally
            {
                if (forfeitBarrierTcs != null)
                {
                    lock (room.SyncRoot)
                    {
                        room.InFlightRecruitForfeits.Remove(forfeitBarrierTcs.Task);
                    }
                    forfeitBarrierTcs.TrySetResult(true);
                }
            }

            await SettleHumanMetaOnceIfEligibleAsync(room.RoomId, playerId, assignedPlacement);

            if (soleSurvivorChampion && championSeat != null && !string.IsNullOrWhiteSpace(championSeat.SeatGameId))
            {
                var champEvent = new MirageTournamentRoundSettledEvent(
                    Guid.NewGuid(),
                    currentRound,
                    0,
                    0,
                    0,
                    "对手退赛",
                    isCombatTie: false,
                    selfWonCombat: true,
                    selfPermanentGrowthLog: null,
                    MirageTournamentSeatOutcomeKind.Champion,
                    1,
                    "第 1 名 · 夺得幻境战棋冠军！",
                    null,
                    null,
                    null,
                    null);
                var champWrapper = new PlayerActionWrapper("MirageTournamentSettle", championSeat.PlayerId, champEvent)
                {
                    CancellationToken = roomLifetimeToken
                };
                if (await _gameService.EnqueuePlayerActionAsync(championSeat.SeatGameId!, champWrapper))
                {
                    await champWrapper.Completion.WaitAsync(roomLifetimeToken);
                }
                if (championSeat.Occupancy == MirageSeatOccupancy.Human)
                {
                    await SettleHumanMetaOnceIfEligibleAsync(room.RoomId, championSeat.PlayerId, 1);
                }
            }

            if (updatedStandings != null)
            {
                await BroadcastStandingsSafeAsync(room.RoomId, updatedStandings);
            }

            if (triggerRoundClose)
            {
                using (ExecutionContext.SuppressFlow())
                {
                    _ = Task.Run(() => ExecuteRoundTransitionAsync(room.RoomId, currentRound));
                }
            }
            else
            {
                TryCleanupPureAiRemainder(room);
            }
        }

        private void StartRoomRecruitTimerLocked(MirageLobbyRoom room, int roundNumber, DateTime endsAtUtc)
        {
            room.RoundRecruitTimer?.Dispose();
            var timerId = Guid.NewGuid();
            room.ActiveTimerId = timerId;
            var delay = endsAtUtc - DateTime.UtcNow;
            if (delay < TimeSpan.Zero)
            {
                delay = TimeSpan.Zero;
            }

            room.RoundRecruitTimer = new Timer(
                _ =>
                {
                    using (ExecutionContext.SuppressFlow())
                    {
                        _ = Task.Run(() => OnRoomRecruitTimerElapsedAsync(room.RoomId, roundNumber, timerId));
                    }
                },
                null,
                delay,
                Timeout.InfiniteTimeSpan);
        }

        private async Task OnRoomRecruitTimerElapsedAsync(string roomId, int roundNumber, Guid expectedTimerId)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                return;
            }

            lock (room.SyncRoot)
            {
                if (room.ActiveTimerId != expectedTimerId
                    || room.Phase != MirageLobbyPhase.Recruit
                    || room.RoundNumber != roundNumber)
                {
                    return;
                }
            }

            await ExecuteRoundTransitionAsync(roomId, roundNumber);
        }

        private async Task ExecuteRoundTransitionAsync(string roomId, int targetRound)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                return;
            }

            List<MirageLobbySeat> activeSeatsSnapshot;
            List<MirageRoomPairing> pairingsSnapshot;
            List<MirageEliminatedGhostEntry> ghostsSnapshot;
            CancellationToken roomToken;
            Task[] inFlightForfeits;

            lock (room.SyncRoot)
            {
                if (room.Phase != MirageLobbyPhase.Recruit || room.RoundNumber != targetRound)
                {
                    return;
                }

                // Seal Recruit phase first so no new pre-freeze forfeits can start; await any in-flight ones next
                room.Phase = MirageLobbyPhase.Closing;
                room.RoundRecruitTimer?.Dispose();
                room.RoundRecruitTimer = null;
                room.ActiveTimerId = Guid.Empty;
                room.ActiveRoundCombatBatchId = Guid.NewGuid();
                roomToken = room.RoomLifetimeCts?.Token ?? CancellationToken.None;
                inFlightForfeits = room.InFlightRecruitForfeits.ToArray();
            }

            if (inFlightForfeits.Length > 0)
            {
                await Task.WhenAll(inFlightForfeits);
            }

            lock (room.SyncRoot)
            {
                if (room.Phase != MirageLobbyPhase.Closing)
                {
                    return;
                }

                activeSeatsSnapshot = room.Seats
                    .Where(s => s.Occupancy != MirageSeatOccupancy.Empty && !s.IsEliminated && !string.IsNullOrWhiteSpace(s.SeatGameId))
                    .ToList();
                pairingsSnapshot = room.CurrentRoundPairings.ToList();
                ghostsSnapshot = room.EliminatedSeatGhosts.ToList();
            }

            if (activeSeatsSnapshot.Count == 0)
            {
                return;
            }

            try
            {
                // =========================================================================
                // STEP 1: CLOSING — Unblock pending choices and freeze all seats via FIFO queue
                // =========================================================================
                var frozenSideBySeatIndex = new Dictionary<int, MirageCombatSideSnapshot>();
                var freezeTasks = activeSeatsSnapshot.Select(async seat =>
                {
                    var seatGameId = seat.SeatGameId!;
                    var liveGame = _gameService.GetGameState(seatGameId);
                    if (liveGame?.Mirage == null)
                    {
                        throw new InvalidOperationException($"Missing live SeatGame {seatGameId} during round {targetRound} freeze.");
                    }

                    bool needsAutoReady;
                    lock (liveGame.Mirage.RoundSyncLock)
                    {
                        liveGame.Mirage.ClosingRecruitRoundNumber = targetRound;
                        needsAutoReady = !liveGame.Mirage.ReadyPlayerIds.Contains(seat.PlayerId);
                    }

                    // Drain any currently suspended choice; chained choices auto-resolve via ClosingRecruitRoundNumber
                    var pending = _pendingActionService.GetPendingChoiceForPlayer(seatGameId, seat.PlayerId);
                    if (pending != null)
                    {
                        _pendingActionService.ForceResolveChoice(
                            pending.Request.PendingChoiceId,
                            PendingActionService.CreateTimeoutChoiceResult(pending.Request));
                    }

                    if (needsAutoReady)
                    {
                        var autoLockAction = new PlayerActionWrapper("MirageSetReady", seat.PlayerId, new { })
                        {
                            Origin = "mirage_room_timeout",
                            CancellationToken = roomToken
                        };
                        if (await _gameService.EnqueuePlayerActionAsync(seatGameId, autoLockAction))
                        {
                            await autoLockAction.Completion.WaitAsync(roomToken);
                        }
                    }

                    var pairCombatId = pairingsSnapshot
                        .FirstOrDefault(p => p.SeatIndexA == seat.SeatIndex || p.SeatIndexB == seat.SeatIndex)?.CombatId
                        ?? Guid.NewGuid();

                    MirageCombatSideSnapshot? captured = null;
                    var freezeBarrier = new MatchStateRead((state, _) =>
                    {
                        lock (state.Mirage!.RoundSyncLock)
                        {
                            state.Phase = GamePhase.AutoCombat;
                            state.Mirage.ActiveCombatId = pairCombatId;
                            state.Mirage.IsNextRoundGateClosed = true;
                        }
                        captured = _snapshotBuilder.CaptureSideSnapshot(state, seat.PlayerId);
                        return Task.FromResult(JsonSerializer.SerializeToElement(true));
                    })
                    {
                        CancellationToken = roomToken
                    };

                    if (!await _gameService.EnqueuePlayerActionAsync(seatGameId, freezeBarrier))
                    {
                        throw new InvalidOperationException($"Failed to enqueue freeze barrier for seat {seat.SeatIndex}.");
                    }

                    var completion = await freezeBarrier.Completion.WaitAsync(roomToken);
                    if (completion.Status != PlayerActionCompletionStatus.Completed || captured == null)
                    {
                        throw new InvalidOperationException($"Freeze barrier failed for seat {seat.SeatIndex}: {completion.Reason}");
                    }

                    return (seat.SeatIndex, Snapshot: captured);
                });

                var frozenResults = await Task.WhenAll(freezeTasks);
                foreach (var item in frozenResults)
                {
                    frozenSideBySeatIndex[item.SeatIndex] = item.Snapshot;
                }

                // =========================================================================
                // STEP 2: SIMULATING — Run 1v1 sandbox combat for each pairing
                // =========================================================================
                lock (room.SyncRoot)
                {
                    if (room.Phase == MirageLobbyPhase.Closed) return;
                    room.Phase = MirageLobbyPhase.Simulating;
                }

                var seatByIdx = activeSeatsSnapshot.ToDictionary(s => s.SeatIndex);
                var seatCombatOutputs = new Dictionary<int, (
                    Guid CombatId,
                    MirageCombatReplayDto Replay,
                    int SelfDamage,
                    int OppDamage,
                    string OppName,
                    bool IsTie,
                    bool SelfWon,
                    List<MiragePermanentGrowthEntry> SelfGrowth,
                    int? OpponentSeatIdx,
                    bool FacedGhost)>();

                foreach (var pairing in pairingsSnapshot)
                {
                    if (!frozenSideBySeatIndex.TryGetValue(pairing.SeatIndexA, out var sideA))
                    {
                        continue;
                    }

                    var seatA = seatByIdx[pairing.SeatIndexA];
                    if (pairing.SeatIndexB.HasValue && frozenSideBySeatIndex.TryGetValue(pairing.SeatIndexB.Value, out var sideB))
                    {
                        var seatB = seatByIdx[pairing.SeatIndexB.Value];
                        var replay = await _combatSimulator.SimulateSidesAsync(sideA, sideB, pairing.CombatId, targetRound, roomToken);
                        var outDto = replay.Outcome;
                        var isTie = outDto.IsTie;
                        var aWon = !isTie && outDto.WinnerPlayerId == seatA.PlayerId;
                        var bWon = !isTie && outDto.WinnerPlayerId == seatB.PlayerId;
                        var dmg = isTie ? 0 : Math.Max(0, outDto.LeaderDamage);

                        seatCombatOutputs[seatA.SeatIndex] = (
                            pairing.CombatId,
                            replay,
                            bWon ? dmg : 0,
                            aWon ? dmg : 0,
                            seatB.DisplayName,
                            isTie,
                            aWon,
                            outDto.PermanentGrowthLog.Where(e => e != null && e.PlayerId == seatA.PlayerId).ToList(),
                            seatB.SeatIndex,
                            false);

                        seatCombatOutputs[seatB.SeatIndex] = (
                            pairing.CombatId,
                            replay,
                            aWon ? dmg : 0,
                            bWon ? dmg : 0,
                            seatA.DisplayName,
                            isTie,
                            bWon,
                            outDto.PermanentGrowthLog.Where(e => e != null && e.PlayerId == seatB.PlayerId).ToList(),
                            seatA.SeatIndex,
                            false);
                    }
                    else
                    {
                        // Odd-player Ghost matchup using room-scoped eliminated snapshot (empty board allowed, never cold-spawn followers!)
                        var ghostEntry = pairing.GhostSourceSeatIndex.HasValue
                            ? ghostsSnapshot.FirstOrDefault(g => g.SourceSeatIndex == pairing.GhostSourceSeatIndex.Value)
                            : ghostsSnapshot.OrderByDescending(g => g.EliminatedRound).FirstOrDefault();
                        var ghostSide = BuildIsolatedRoomGhostCombatSide(ghostEntry, targetRound);
                        var replay = await _combatSimulator.SimulateSidesAsync(sideA, ghostSide, pairing.CombatId, targetRound, roomToken);
                        var outDto = replay.Outcome;
                        var isTie = outDto.IsTie;
                        var aWon = !isTie && outDto.WinnerPlayerId == seatA.PlayerId;
                        var ghostWon = !isTie && outDto.WinnerPlayerId == ghostSide.PlayerId;
                        var dmg = isTie ? 0 : Math.Max(0, outDto.LeaderDamage);
                        var selfGrowthOnly = outDto.PermanentGrowthLog
                            .Where(e => e != null && e.PlayerId == seatA.PlayerId)
                            .ToList();

                        var cleanedReplay = replay with
                        {
                            Outcome = outDto with { PermanentGrowthLog = selfGrowthOnly }
                        };

                        seatCombatOutputs[seatA.SeatIndex] = (
                            pairing.CombatId,
                            cleanedReplay,
                            ghostWon ? dmg : 0,
                            aWon ? dmg : 0,
                            ghostSide.Name ?? "幻境残影",
                            isTie,
                            aWon,
                            selfGrowthOnly,
                            null,
                            true);
                    }
                }

                // =========================================================================
                // STEP 3A: APPLYING STAGE A — Staged pre-validation & exact PostHp calculation
                // =========================================================================
                lock (room.SyncRoot)
                {
                    if (room.Phase == MirageLobbyPhase.Closed) return;
                    room.Phase = MirageLobbyPhase.Applying;
                }

                var stageAResults = new Dictionary<int, MirageStageAEvaluationResult>();
                foreach (var seat in activeSeatsSnapshot)
                {
                    if (!seatCombatOutputs.TryGetValue(seat.SeatIndex, out var combatOut))
                    {
                        throw new InvalidOperationException($"Missing combat simulation output for active seat {seat.SeatIndex} in round {targetRound}.");
                    }

                    MirageStageAEvaluationResult eval = null!;
                    var evalRead = new MatchStateRead((state, sp) =>
                    {
                        var history = sp.GetService<IEventHistoryService>();
                        eval = MirageTournamentRoundSettledEvent.EvaluateStageA(
                            state,
                            sp,
                            _mapper,
                            history,
                            targetRound,
                            combatOut.SelfDamage,
                            combatOut.SelfGrowth,
                            roomToken);
                        return Task.FromResult(JsonSerializer.SerializeToElement(eval.Success));
                    })
                    {
                        CancellationToken = roomToken
                    };

                    if (!await _gameService.EnqueuePlayerActionAsync(seat.SeatGameId!, evalRead))
                    {
                        throw new InvalidOperationException($"Stage A admission failed for seat {seat.SeatIndex}.");
                    }

                    await evalRead.Completion.WaitAsync(roomToken);
                    if (eval == null || !eval.Success)
                    {
                        throw new InvalidOperationException($"Stage A pre-validation failed on seat {seat.SeatIndex}: {eval?.ErrorMessage}");
                    }

                    stageAResults[seat.SeatIndex] = eval;
                }

                // =========================================================================
                // ADJUDICATE ELIMINATIONS, PLACEMENTS, POOL RETURNS & NEXT ROUND OFFERS
                // =========================================================================
                var outcomesBySeatIdx = new Dictionary<int, (
                    MirageTournamentSeatOutcomeKind Kind,
                    int? Placement,
                    string? Reason,
                    List<MirageShopOffer>? NextOffers)>();
                var newlySettledHumans = new List<(Guid PlayerId, int Placement)>();
                int nextRound = targetRound + 1;
                DateTime nextStartUtc = DateTime.UtcNow;
                DateTime nextEndUtc = nextStartUtc.AddSeconds(room.RecruitTimeoutSeconds);
                long broadcastVersion;

                lock (room.SyncRoot)
                {
                    if (room.Phase == MirageLobbyPhase.Closed) return;

                    int totalParticipantsThisRound = activeSeatsSnapshot.Count;
                    var dyingSeats = activeSeatsSnapshot
                        .Where(s => stageAResults[s.SeatIndex].PostCombatHp <= 0 || s.PendingForfeit)
                        .OrderByDescending(s => !s.PendingForfeit) // Normal combat death ranks above mid-round forfeit
                        .ThenByDescending(s => s.PendingForfeit ? s.PendingForfeitSequence : 0L) // Later forfeit ranks above earlier forfeit
                        .ThenByDescending(s => stageAResults[s.SeatIndex].PostCombatHp)
                        .ThenByDescending(s => stageAResults[s.SeatIndex].PreCombatHp)
                        .ThenBy(s => s.SeatIndex)
                        .ToList();

                    // Edge case: all remaining participants died/forfeited simultaneously
                    if (dyingSeats.Count == totalParticipantsThisRound && dyingSeats.Count > 0)
                    {
                        var topSeat = dyingSeats[0];
                        dyingSeats.RemoveAt(0);
                        topSeat.FinalPlacement = 1;
                        topSeat.IsEliminated = false;
                        room.ChampionPlayerId = topSeat.PlayerId;
                        outcomesBySeatIdx[topSeat.SeatIndex] = (
                            MirageTournamentSeatOutcomeKind.Champion,
                            1,
                            "第 1 名 · 夺得幻境战棋冠军！",
                            null);
                        if (topSeat.Occupancy == MirageSeatOccupancy.Human)
                        {
                            newlySettledHumans.Add((topSeat.PlayerId, 1));
                        }
                    }

                    int survivorsCount = totalParticipantsThisRound - dyingSeats.Count;
                    for (int i = 0; i < dyingSeats.Count; i++)
                    {
                        var deadSeat = dyingSeats[i];
                        var placement = survivorsCount + 1 + i;
                        var eval = stageAResults[deadSeat.SeatIndex];
                        var combatOut = seatCombatOutputs[deadSeat.SeatIndex];

                        deadSeat.IsEliminated = true;
                        deadSeat.EliminatedRound = targetRound;
                        deadSeat.FinalPlacement = placement;
                        deadSeat.ProjectedCurrentHp = 0;
                        deadSeat.ProjectedMaxHp = eval.PostCombatMaxHp;
                        deadSeat.ProjectedTavernTier = eval.TavernTier;
                        deadSeat.ProjectedLeaderCardId = eval.LeaderCardDefinitionId;
                        deadSeat.LastCombatDeltaHp = -combatOut.SelfDamage;
                        deadSeat.ConsecutiveWins = 0;

                        var reason = deadSeat.PendingForfeit
                            ? $"第 {placement} 名 · {deadSeat.ForfeitReason ?? "中途退赛"}"
                            : $"第 {placement} 名 · 第 {targetRound} 轮战败淘汰";
                        outcomesBySeatIdx[deadSeat.SeatIndex] = (
                            MirageTournamentSeatOutcomeKind.Eliminated,
                            placement,
                            reason,
                            null);

                        if (frozenSideBySeatIndex.TryGetValue(deadSeat.SeatIndex, out var deadSnap))
                        {
                            UpsertEliminatedGhostLocked(
                                room,
                                deadSeat.SeatIndex,
                                deadSeat.PlayerId,
                                deadSeat.DisplayName,
                                deadSeat.AvatarUrl,
                                targetRound,
                                eval.TavernTier,
                                eval.LeaderCardDefinitionId,
                                deadSnap);
                        }

                        if (_gameService.GetGameState(deadSeat.SeatGameId!) is { } deadGame)
                        {
                            _shopService.ReturnEliminatedPlayerCardsToPool(deadGame, deadSeat.PlayerId);
                        }

                        if (deadSeat.Occupancy == MirageSeatOccupancy.Human)
                        {
                            newlySettledHumans.Add((deadSeat.PlayerId, placement));
                            // Short grace window so eliminated player can click "Stay & Spectate" before pure-AI cleanup
                            if (!deadSeat.HasLeftPostElimination)
                            {
                                room.HumanGraceDeadlinesUtc[deadSeat.PlayerId] = DateTime.UtcNow.AddSeconds(PostEliminationGraceSeconds);
                            }
                        }
                    }

                    var survivingSeats = activeSeatsSnapshot.Where(s => !outcomesBySeatIdx.ContainsKey(s.SeatIndex)).ToList();
                    foreach (var surv in survivingSeats)
                    {
                        var eval = stageAResults[surv.SeatIndex];
                        var combatOut = seatCombatOutputs[surv.SeatIndex];
                        surv.ProjectedCurrentHp = Math.Max(1, eval.PostCombatHp);
                        surv.ProjectedMaxHp = eval.PostCombatMaxHp;
                        surv.ProjectedTavernTier = eval.TavernTier;
                        surv.ProjectedLeaderCardId = eval.LeaderCardDefinitionId;
                        surv.ProjectedKnownWarbandCount = frozenSideBySeatIndex.TryGetValue(surv.SeatIndex, out var fSnap)
                            ? CountFollowersInSnapshot(fSnap)
                            : surv.ProjectedKnownWarbandCount;
                        surv.LastCombatDeltaHp = -combatOut.SelfDamage;
                        surv.ConsecutiveWins = combatOut.SelfWon ? surv.ConsecutiveWins + 1 : 0;
                        if (combatOut.OpponentSeatIdx.HasValue)
                        {
                            surv.LastOpponentSeatIndex = combatOut.OpponentSeatIdx.Value;
                        }
                        if (combatOut.FacedGhost)
                        {
                            surv.LastFacedGhostRound = targetRound;
                        }
                    }

                    if (survivingSeats.Count == 1)
                    {
                        var champ = survivingSeats[0];
                        champ.FinalPlacement = 1;
                        room.ChampionPlayerId = champ.PlayerId;
                        outcomesBySeatIdx[champ.SeatIndex] = (
                            MirageTournamentSeatOutcomeKind.Champion,
                            1,
                            "第 1 名 · 夺得幻境战棋冠军！",
                            null);
                        if (champ.Occupancy == MirageSeatOccupancy.Human)
                        {
                            newlySettledHumans.Add((champ.PlayerId, 1));
                        }
                        room.Phase = MirageLobbyPhase.Finished;
                    }
                    else if (survivingSeats.Count >= 2)
                    {
                        // Pre-roll next round shop offers from the updated shared pool (after eliminated players returned their cards)
                        foreach (var surv in survivingSeats)
                        {
                            var liveSurvGame = _gameService.GetGameState(surv.SeatGameId!)!;
                            var nextOffers = PreRollNextRoundOffersFromSharedPool(liveSurvGame.Player1, liveSurvGame.Mirage!);
                            outcomesBySeatIdx[surv.SeatIndex] = (
                                MirageTournamentSeatOutcomeKind.AdvanceRound,
                                null,
                                null,
                                nextOffers);
                            surv.ProjectedIsRoundLocked = false;
                        }
                    }
                    else
                    {
                        room.Phase = MirageLobbyPhase.Finished;
                    }

                    room.RoomVersion++;
                    broadcastVersion = room.RoomVersion;
                }

                // =========================================================================
                // STEP 3B: DELIVER REPLAYS FIRST, THEN COMMIT STAGE B ON ALL SEATS
                // =========================================================================
                foreach (var seat in activeSeatsSnapshot)
                {
                    var combatOut = seatCombatOutputs[seat.SeatIndex];
                    if (combatOut.Replay != null)
                    {
                        var stampedReplay = combatOut.Replay with
                        {
                            RoomVersion = broadcastVersion,
                            ViewingPlayerId = seat.PlayerId
                        };
                        await _hubContext.Clients.Group(SignalRGroupNames.Game(seat.SeatGameId!))
                            .ReceiveMirageCombatReplay(stampedReplay);
                    }
                }

                var commitTasks = activeSeatsSnapshot.Select(async seat =>
                {
                    var combatOut = seatCombatOutputs[seat.SeatIndex];
                    var decision = outcomesBySeatIdx[seat.SeatIndex];
                    var settleEvent = new MirageTournamentRoundSettledEvent(
                        combatOut.CombatId,
                        targetRound,
                        combatOut.Replay?.Steps.Count ?? 0,
                        combatOut.SelfDamage,
                        combatOut.OppDamage,
                        combatOut.OppName,
                        combatOut.IsTie,
                        combatOut.SelfWon,
                        combatOut.SelfGrowth,
                        decision.Kind,
                        decision.Placement,
                        decision.Reason,
                        decision.NextOffers,
                        nextStartUtc,
                        nextEndUtc,
                        room.RecruitTimeoutSeconds);

                    var wrapper = new PlayerActionWrapper("MirageTournamentSettle", seat.PlayerId, settleEvent)
                    {
                        CancellationToken = roomToken
                    };
                    if (!await _gameService.EnqueuePlayerActionAsync(seat.SeatGameId!, wrapper))
                    {
                        throw new InvalidOperationException($"Stage B admission failed on seat {seat.SeatIndex}.");
                    }

                    var res = await wrapper.Completion.WaitAsync(roomToken);
                    if (res.Status != PlayerActionCompletionStatus.Completed)
                    {
                        throw new InvalidOperationException($"Stage B commit failed on seat {seat.SeatIndex}: {res.Reason}");
                    }
                });

                await Task.WhenAll(commitTasks);

                // =========================================================================
                // STEP 3C & 4: ATOMICALLY DRAIN LATE FORFEITS IN FIFO ORDER & OPEN NEXT ROUND
                // =========================================================================
                MirageStandingsSnapshotDto finalStandings;
                List<(string SeatGameId, Guid PlayerId)> seatsToOpenGate = new();
                MirageLobbySeat? lateSoleSurvivorChampion = null;
                List<string> nextAiSeatGameIds = new();

                while (true)
                {
                    MirageLobbySeat? lateForfeitSeat = null;
                    int latePlacement = 0;
                    string lateReasonText = "中途退赛";

                    lock (room.SyncRoot)
                    {
                        if (room.Phase == MirageLobbyPhase.Closed) return;

                        var aliveNow = room.Seats
                            .Where(s => s.Occupancy != MirageSeatOccupancy.Empty && !s.IsEliminated)
                            .OrderBy(s => s.SeatIndex)
                            .ToList();

                        if (room.Phase != MirageLobbyPhase.Finished && aliveNow.Count >= 2)
                        {
                            lateForfeitSeat = aliveNow
                                .Where(s => s.PendingForfeit)
                                .OrderBy(s => s.PendingForfeitSequence)
                                .ThenBy(s => s.SeatIndex)
                                .FirstOrDefault();

                            if (lateForfeitSeat == null)
                            {
                                // Zero pending forfeits & >= 2 survivors: atomically enter Recruit inside the same lock!
                                room.LastSettledRoundNumber = targetRound;
                                room.ActiveRoundCombatBatchId = null;
                                room.RoundNumber = nextRound;
                                room.Phase = MirageLobbyPhase.Recruit;
                                room.RoundStartedAtUtc = nextStartUtc;
                                room.RoundEndsAtUtc = nextEndUtc;
                                RefreshSeatProjectionsFromLiveStatesLocked(room);
                                room.CurrentRoundPairings = BuildRoundPairingsLocked(room, nextRound);
                                StartRoomRecruitTimerLocked(room, nextRound, nextEndUtc);

                                seatsToOpenGate = aliveNow
                                    .Where(s => !string.IsNullOrWhiteSpace(s.SeatGameId))
                                    .Select(s => (s.SeatGameId!, s.PlayerId))
                                    .ToList();

                                nextAiSeatGameIds = aliveNow
                                    .Where(s => s.Occupancy == MirageSeatOccupancy.Ai && !string.IsNullOrWhiteSpace(s.SeatGameId))
                                    .Select(s => s.SeatGameId!)
                                    .ToList();

                                room.RoomVersion++;
                                finalStandings = BuildStandingsSnapshotLocked(room, settledRoundNumber: targetRound);
                                room.LatestStandingsSnapshot = finalStandings;
                                break;
                            }

                            latePlacement = aliveNow.Count;
                            lateReasonText = lateForfeitSeat.ForfeitReason ?? "中途退赛";
                            lateForfeitSeat.PendingForfeit = false;
                            lateForfeitSeat.IsEliminated = true;
                            lateForfeitSeat.EliminatedRound = targetRound;
                            lateForfeitSeat.FinalPlacement = latePlacement;
                            lateForfeitSeat.ProjectedCurrentHp = 0;
                            lateForfeitSeat.ProjectedIsRoundLocked = true;

                            if (lateForfeitSeat.Occupancy == MirageSeatOccupancy.Human)
                            {
                                newlySettledHumans.Add((lateForfeitSeat.PlayerId, latePlacement));
                            }
                        }
                        else
                        {
                            room.LastSettledRoundNumber = targetRound;
                            room.ActiveRoundCombatBatchId = null;
                            if (room.Phase != MirageLobbyPhase.Finished && aliveNow.Count == 1)
                            {
                                lateSoleSurvivorChampion = aliveNow[0];
                                lateSoleSurvivorChampion.PendingForfeit = false;
                                lateSoleSurvivorChampion.FinalPlacement = 1;
                                room.ChampionPlayerId = lateSoleSurvivorChampion.PlayerId;
                                if (lateSoleSurvivorChampion.Occupancy == MirageSeatOccupancy.Human)
                                {
                                    newlySettledHumans.Add((lateSoleSurvivorChampion.PlayerId, 1));
                                }
                            }
                            room.Phase = MirageLobbyPhase.Finished;
                            room.RoundEndsAtUtc = null;
                            room.CurrentRoundPairings.Clear();
                            RefreshSeatProjectionsFromLiveStatesLocked(room);

                            room.RoomVersion++;
                            finalStandings = BuildStandingsSnapshotLocked(room, settledRoundNumber: targetRound);
                            room.LatestStandingsSnapshot = finalStandings;
                            break;
                        }
                    }

                    MirageCombatSideSnapshot? lateSnap = null;
                    int lateTier = 1;
                    int lateLeaderDefId = GameConstants.Cards.MirageLeaderCardId;
                    if (!string.IsNullOrWhiteSpace(lateForfeitSeat.SeatGameId))
                    {
                        var lateGid = lateForfeitSeat.SeatGameId!;
                        var latePid = lateForfeitSeat.PlayerId;
                        var capRead = new MatchStateRead((state, _) =>
                        {
                            lateTier = state.Player1.Mirage?.TavernTier ?? 1;
                            lateLeaderDefId = state.Player1.Leader?.BaseDefinitionId ?? GameConstants.Cards.MirageLeaderCardId;
                            lateSnap = _snapshotBuilder.CaptureSideSnapshot(state, latePid);
                            _shopService.ReturnEliminatedPlayerCardsToPool(state, latePid);
                            return Task.FromResult(JsonSerializer.SerializeToElement(true));
                        })
                        {
                            CancellationToken = roomToken
                        };

                        if (await _gameService.EnqueuePlayerActionAsync(lateGid, capRead))
                        {
                            await capRead.Completion.WaitAsync(roomToken);
                        }

                        var liveHealth = _gameService.GetGameState(lateGid)?.Player1.CurrentHealth ?? 0;
                        var termEvt = new MirageTournamentRoundSettledEvent(
                            Guid.NewGuid(),
                            nextRound,
                            0,
                            liveHealth,
                            0,
                            "退赛",
                            isCombatTie: false,
                            selfWonCombat: false,
                            selfPermanentGrowthLog: null,
                            MirageTournamentSeatOutcomeKind.Eliminated,
                            latePlacement,
                            $"第 {latePlacement} 名 · {lateReasonText}",
                            null,
                            null,
                            null,
                            null);
                        var termWrap = new PlayerActionWrapper("MirageTournamentSettle", latePid, termEvt)
                        {
                            CancellationToken = roomToken
                        };
                        if (await _gameService.EnqueuePlayerActionAsync(lateGid, termWrap))
                        {
                            await termWrap.Completion.WaitAsync(roomToken);
                        }
                    }

                    if (lateSnap != null)
                    {
                        lock (room.SyncRoot)
                        {
                            UpsertEliminatedGhostLocked(
                                room,
                                lateForfeitSeat.SeatIndex,
                                lateForfeitSeat.PlayerId,
                                lateForfeitSeat.DisplayName,
                                lateForfeitSeat.AvatarUrl,
                                targetRound,
                                lateTier,
                                lateLeaderDefId,
                                lateSnap);
                        }
                    }
                }

                if (lateSoleSurvivorChampion != null && !string.IsNullOrWhiteSpace(lateSoleSurvivorChampion.SeatGameId))
                {
                    var lateChampEvt = new MirageTournamentRoundSettledEvent(
                        Guid.NewGuid(),
                        nextRound,
                        0,
                        0,
                        0,
                        "对手退赛",
                        isCombatTie: false,
                        selfWonCombat: true,
                        selfPermanentGrowthLog: null,
                        MirageTournamentSeatOutcomeKind.Champion,
                        1,
                        "第 1 名 · 夺得幻境战棋冠军！",
                        null,
                        null,
                        null,
                        null);
                    var lateChampWrap = new PlayerActionWrapper("MirageTournamentSettle", lateSoleSurvivorChampion.PlayerId, lateChampEvt)
                    {
                        CancellationToken = roomToken
                    };
                    if (await _gameService.EnqueuePlayerActionAsync(lateSoleSurvivorChampion.SeatGameId!, lateChampWrap))
                    {
                        await lateChampWrap.Completion.WaitAsync(roomToken);
                    }
                }

                // Release per-seat recruit gates via FIFO only AFTER room.Phase == Recruit & room.RoundNumber == nextRound
                if (seatsToOpenGate.Count > 0)
                {
                    var openGateTasks = seatsToOpenGate.Select(async item =>
                    {
                        GameStateDto? unlockedDto = null;
                        var openGateRead = new MatchStateRead((state, sp) =>
                        {
                            lock (state.Mirage!.RoundSyncLock)
                            {
                                state.Mirage.IsNextRoundGateClosed = false;
                            }
                            var acc = sp.GetRequiredService<IGameContextAccessor>();
                            using var ctx = new GameContext(sp, _mapper, acc, state, item.PlayerId, roomToken);
                            unlockedDto = GameStateProjection.Create(state, ctx);
                            return Task.FromResult(JsonSerializer.SerializeToElement(true));
                        })
                        {
                            CancellationToken = roomToken
                        };

                        if (await _gameService.EnqueuePlayerActionAsync(item.SeatGameId, openGateRead))
                        {
                            var completion = await openGateRead.Completion.WaitAsync(roomToken);
                            if (completion.Status == PlayerActionCompletionStatus.Completed && unlockedDto != null)
                            {
                                await _hubContext.Clients.Group(SignalRGroupNames.Game(item.SeatGameId))
                                    .TurnStarted(new TurnStartedEventDto(unlockedDto, item.PlayerId));
                            }
                        }
                    });
                    await Task.WhenAll(openGateTasks);
                }

                await BroadcastStandingsSafeAsync(room.RoomId, finalStandings);

                foreach (var (humanId, placement) in newlySettledHumans)
                {
                    await SettleHumanMetaOnceIfEligibleAsync(room.RoomId, humanId, placement);
                }

                bool canTriggerAi;
                lock (room.SyncRoot)
                {
                    canTriggerAi = room.Phase == MirageLobbyPhase.Recruit && room.RoundNumber == nextRound;
                }

                if (canTriggerAi)
                {
                    foreach (var aiSeatGameId in nextAiSeatGameIds)
                    {
                        _gameService.TriggerMirageAiRecruitIfNeeded(aiSeatGameId, nextRound);
                    }
                }

                TryCleanupPureAiRemainder(room);
            }
            catch (OperationCanceledException) when (roomToken.IsCancellationRequested)
            {
                _logger.LogInformation("[MirageLobby:{RoomId}] Round {Round} transition cancelled.", roomId, targetRound);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MirageLobby:{RoomId}] Fatal error during round {Round} transition; closing room safely to prevent torn state.", roomId, targetRound);
                List<string> seatsToRetire;
                lock (room.SyncRoot)
                {
                    CloseRoomInternalLocked(room, $"幻境赛事结算异常终止：{ex.Message}", out seatsToRetire);
                }
                RetireSeatGamesOutsideLock(seatsToRetire);
                await _hubContext.Clients.Group(SignalRGroupNames.MirageRoom(roomId))
                    .ReceiveError($"ROOM_CLOSED:幻境赛事结算异常终止（{ex.Message}）");
            }
        }

        private List<MirageShopOffer> PreRollNextRoundOffersFromSharedPool(PlayerState player, MirageMatchState mirage)
        {
            if (player.Mirage == null)
            {
                return new List<MirageShopOffer>();
            }

            lock (mirage.SharedPoolSyncRoot)
            {
                var targetSlots = mirage.Rules.GetShopSlotCount(player.Mirage.TavernTier);
                if (player.Mirage.IsShopFrozen)
                {
                    var preserved = player.Mirage.ShopOffers
                        .Where(o => o != null)
                        .Select(o => new MirageShopOffer
                        {
                            OfferId = o.OfferId,
                            CardDefinitionId = o.CardDefinitionId,
                            TavernTier = o.TavernTier,
                            BuyCost = o.BuyCost
                        })
                        .ToList();
                    var missing = Math.Max(0, targetSlots - preserved.Count);
                    if (missing > 0)
                    {
                        preserved.AddRange(_shopService.RollShopOffers(mirage, player.Mirage.TavernTier, missing));
                    }
                    return preserved;
                }

                foreach (var oldOffer in player.Mirage.ShopOffers)
                {
                    if (oldOffer != null)
                    {
                        _shopService.ReturnCopiesToPool(mirage, oldOffer.CardDefinitionId, 1);
                    }
                }
                player.Mirage.ShopOffers.Clear();
                return _shopService.RollShopOffers(mirage, player.Mirage.TavernTier, targetSlots);
            }
        }

        private static List<MirageRoomPairing> BuildRoundPairingsLocked(MirageLobbyRoom room, int roundNumber)
        {
            var alive = room.Seats
                .Where(s => s.Occupancy != MirageSeatOccupancy.Empty && !s.IsEliminated)
                .ToList();
            var pairings = new List<MirageRoomPairing>();
            if (alive.Count < 2)
            {
                return pairings;
            }

            var pool = alive.OrderBy(_ => Random.Shared.Next()).ToList();

            // Odd number of alive players: pick 1 player to fight an eliminated Ghost
            if (pool.Count % 2 == 1)
            {
                var nonRecentGhost = pool.Where(s => s.LastFacedGhostRound != roundNumber - 1).ToList();
                var candidateGroup = nonRecentGhost.Count > 0 ? nonRecentGhost : pool;
                // Prefer bottom half by HP as in standard auto-battler rules
                var orderedByHp = candidateGroup.OrderBy(s => s.ProjectedCurrentHp).ThenBy(_ => Random.Shared.Next()).ToList();
                var bottomHalfCount = Math.Max(1, (orderedByHp.Count + 1) / 2);
                var ghostFighter = orderedByHp[Random.Shared.Next(bottomHalfCount)];
                pool.Remove(ghostFighter);

                var preferredGhost = room.EliminatedSeatGhosts
                    .OrderByDescending(g => g.EliminatedRound)
                    .FirstOrDefault(g => g.SourceSeatIndex != ghostFighter.LastOpponentSeatIndex)
                    ?? room.EliminatedSeatGhosts.OrderByDescending(g => g.EliminatedRound).FirstOrDefault();

                pairings.Add(new MirageRoomPairing(
                    ghostFighter.SeatIndex,
                    null,
                    preferredGhost?.SourceSeatIndex,
                    Guid.NewGuid()));
            }

            // Anti-repeat perfect matching on remaining even pool (2..8 players)
            var matchedPairs = new List<(MirageLobbySeat A, MirageLobbySeat B)>();
            if (pool.Count > 2 && TryFindAntiRepeatPairing(pool, new bool[pool.Count], matchedPairs))
            {
                foreach (var (a, b) in matchedPairs)
                {
                    pairings.Add(new MirageRoomPairing(a.SeatIndex, b.SeatIndex, null, Guid.NewGuid()));
                }
                return pairings;
            }

            // Fallback when graph has no anti-repeat solution (or when only 2 finalists remain)
            for (int i = 0; i + 1 < pool.Count; i += 2)
            {
                pairings.Add(new MirageRoomPairing(pool[i].SeatIndex, pool[i + 1].SeatIndex, null, Guid.NewGuid()));
            }

            return pairings;
        }

        private static bool TryFindAntiRepeatPairing(
            IReadOnlyList<MirageLobbySeat> seats,
            bool[] used,
            List<(MirageLobbySeat A, MirageLobbySeat B)> current)
        {
            int firstUnused = -1;
            for (int i = 0; i < seats.Count; i++)
            {
                if (!used[i])
                {
                    firstUnused = i;
                    break;
                }
            }

            if (firstUnused < 0)
            {
                return true;
            }

            used[firstUnused] = true;
            var seatA = seats[firstUnused];
            for (int j = firstUnused + 1; j < seats.Count; j++)
            {
                if (used[j]) continue;
                var seatB = seats[j];
                if (seatA.LastOpponentSeatIndex == seatB.SeatIndex || seatB.LastOpponentSeatIndex == seatA.SeatIndex)
                {
                    continue;
                }

                used[j] = true;
                current.Add((seatA, seatB));
                if (TryFindAntiRepeatPairing(seats, used, current))
                {
                    return true;
                }
                current.RemoveAt(current.Count - 1);
                used[j] = false;
            }

            used[firstUnused] = false;
            return false;
        }

        private static MirageCombatSideSnapshot BuildIsolatedRoomGhostCombatSide(
            MirageEliminatedGhostEntry? ghostEntry,
            int roundNumber)
        {
            var ghostCombatPlayerId = Guid.NewGuid();
            if (ghostEntry?.FrozenSideSnapshot == null)
            {
                // Empty ghost fallback (leader only, 0 followers — never cold-spawn random followers!)
                var leaderCardId = Guid.NewGuid();
                return new MirageCombatSideSnapshot
                {
                    PlayerId = ghostCombatPlayerId,
                    Name = "幻境残影",
                    Class = CardClass.Neutral,
                    Environment = DeckEnvironment.Open,
                    IsAI = true,
                    TurnNumber = Math.Max(1, roundNumber),
                    TavernTier = 1,
                    LeaderOriginalInstanceId = leaderCardId,
                    ZoneCardIds = new Dictionary<string, List<Guid>>
                    {
                        ["Field"] = new(),
                        ["Hand"] = new(),
                        ["Emblem"] = new(),
                        ["Graveyard"] = new(),
                        ["Extra"] = new()
                    },
                    Cards = new List<MirageCardSnapshot>
                    {
                        new()
                        {
                            OriginalInstanceId = leaderCardId,
                            BaseDefinitionId = GameConstants.Cards.MirageLeaderCardId,
                            CurrentZone = CardZone.Field,
                            IsLeader = true,
                            RuntimeState = new CardInstance(leaderCardId, GameConstants.Cards.MirageLeaderCardId, ghostCombatPlayerId)
                                .CaptureRuntimeStateSnapshot()
                        }
                    }
                };
            }

            var sourceSnap = ghostEntry.FrozenSideSnapshot;
            var serialized = JsonSerializer.SerializeToElement(sourceSnap, ReplayExecutionState.Json);
            var idMap = new Dictionary<Guid, Guid>();
            void MapGuid(Guid id)
            {
                if (id != Guid.Empty && !idMap.ContainsKey(id))
                {
                    idMap[id] = Guid.NewGuid();
                }
            }

            MapGuid(sourceSnap.LeaderOriginalInstanceId);
            foreach (var c in sourceSnap.Cards)
            {
                MapGuid(c.OriginalInstanceId);
                if (c.RuntimeState?.OriginInstanceId is { } oid) MapGuid(oid);
                foreach (var fid in c.FusedCardIds) MapGuid(fid);
            }
            foreach (var a in sourceSnap.Abilities)
            {
                MapGuid(a.OriginalRuntimeId);
                if (a.OriginalSourceCardId.HasValue) MapGuid(a.OriginalSourceCardId.Value);
                if (a.OriginalSourceBuffId.HasValue) MapGuid(a.OriginalSourceBuffId.Value);
            }
            foreach (var b in sourceSnap.Buffs)
            {
                MapGuid(b.OriginalBuffId);
            }

            var node = JsonNode.Parse(serialized.GetRawText());
            var remapped = node != null && idMap.Count > 0
                ? ReplayExecutionState.Remap(node, idMap).Deserialize<MirageCombatSideSnapshot>(ReplayExecutionState.Json) ?? sourceSnap
                : sourceSnap;

            return remapped with
            {
                PlayerId = ghostCombatPlayerId,
                Name = $"{ghostEntry.DisplayName} (残影)",
                IsAI = true,
                Cards = remapped.Cards
                    .Select(c => c with
                    {
                        RuntimeState = c.RuntimeState with { OriginInstanceId = null }
                    })
                    .ToList()
            };
        }

        private static void UpsertEliminatedGhostLocked(
            MirageLobbyRoom room,
            int seatIndex,
            Guid playerId,
            string displayName,
            string avatarUrl,
            int eliminatedRound,
            int tavernTier,
            int leaderCardId,
            MirageCombatSideSnapshot snapshot)
        {
            room.EliminatedSeatGhosts.RemoveAll(g => g.SourceSeatIndex == seatIndex);
            room.EliminatedSeatGhosts.Add(new MirageEliminatedGhostEntry
            {
                SourceSeatIndex = seatIndex,
                SourcePlayerId = playerId,
                DisplayName = displayName,
                AvatarUrl = avatarUrl,
                EliminatedRound = eliminatedRound,
                TavernTier = Math.Max(1, tavernTier),
                LeaderCardDefinitionId = leaderCardId,
                FrozenSideSnapshot = snapshot
            });
        }

        private void RefreshSeatProjectionsFromLiveStatesLocked(MirageLobbyRoom room)
        {
            foreach (var seat in room.Seats)
            {
                RefreshSingleSeatProjectionLocked(seat);
            }
        }

        private void RefreshSingleSeatProjectionLocked(MirageLobbySeat seat)
        {
            if (seat.Occupancy == MirageSeatOccupancy.Empty || string.IsNullOrWhiteSpace(seat.SeatGameId))
            {
                return;
            }

            var state = _gameService.GetGameState(seat.SeatGameId!);
            if (state?.Player1 == null)
            {
                return;
            }

            if (!seat.IsEliminated)
            {
                var maxHp = state.Player1.MaxHealth > 0 ? state.Player1.MaxHealth : GameConstants.Health.PlayerMaxHealth;
                var curHp = Math.Max(0, maxHp - (state.Player1.Leader?.Damage ?? 0));
                seat.ProjectedMaxHp = maxHp;
                seat.ProjectedCurrentHp = curHp;
            }

            seat.ProjectedTavernTier = state.Player1.Mirage?.TavernTier ?? seat.ProjectedTavernTier;
            seat.ProjectedLeaderCardId = state.Player1.Leader?.BaseDefinitionId ?? seat.ProjectedLeaderCardId;
            if (state.Mirage != null)
            {
                lock (state.Mirage.RoundSyncLock)
                {
                    seat.ProjectedIsRoundLocked = state.Mirage.ReadyPlayerIds.Contains(seat.PlayerId);
                }
            }
        }

        private MirageStandingsSnapshotDto BuildStandingsSnapshotLocked(MirageLobbyRoom room, int? settledRoundNumber)
        {
            var populatedSeats = room.Seats
                .Where(s => s.Occupancy != MirageSeatOccupancy.Empty)
                .ToList();

            var ordered = populatedSeats
                .OrderBy(s => s.IsEliminated ? 1 : 0)
                .ThenBy(s => s.IsEliminated ? (s.FinalPlacement ?? 99) : 0)
                .ThenByDescending(s => s.IsEliminated ? 0 : s.ProjectedCurrentHp)
                .ThenByDescending(s => s.ProjectedTavernTier)
                .ThenBy(s => s.SeatIndex)
                .ToList();

            var nextOpponentBySeat = new Dictionary<int, MirageNextOpponentSummaryDto>();
            foreach (var p in room.CurrentRoundPairings)
            {
                var seatA = room.Seats[p.SeatIndexA];
                if (p.SeatIndexB.HasValue)
                {
                    var seatB = room.Seats[p.SeatIndexB.Value];
                    nextOpponentBySeat[seatA.SeatIndex] = BuildNextOpponentDtoLocked(seatB, isGhost: false);
                    nextOpponentBySeat[seatB.SeatIndex] = BuildNextOpponentDtoLocked(seatA, isGhost: false);
                }
                else
                {
                    var ghost = p.GhostSourceSeatIndex.HasValue
                        ? room.EliminatedSeatGhosts.FirstOrDefault(g => g.SourceSeatIndex == p.GhostSourceSeatIndex.Value)
                        : room.EliminatedSeatGhosts.OrderByDescending(g => g.EliminatedRound).FirstOrDefault();
                    nextOpponentBySeat[seatA.SeatIndex] = BuildGhostOpponentDto(ghost);
                }
            }

            var nextOpponentsByPlayerId = new Dictionary<Guid, MirageNextOpponentSummaryDto>();
            var entries = new List<MirageStandingEntryDto>(ordered.Count);

            for (int idx = 0; idx < ordered.Count; idx++)
            {
                var s = ordered[idx];
                var rank = s.FinalPlacement ?? (idx + 1);
                var leaderDef = s.ProjectedLeaderCardId > 0 ? _gameDataProvider.GetCardDefinition(s.ProjectedLeaderCardId) : null;
                nextOpponentBySeat.TryGetValue(s.SeatIndex, out var nextOpp);
                if (nextOpp != null)
                {
                    nextOpponentsByPlayerId[s.PlayerId] = nextOpp;
                }

                entries.Add(new MirageStandingEntryDto
                {
                    Rank = rank,
                    SeatIndex = s.SeatIndex,
                    PlayerId = s.PlayerId,
                    DisplayName = s.DisplayName,
                    AvatarUrl = s.AvatarUrl,
                    IsAi = s.Occupancy == MirageSeatOccupancy.Ai,
                    IsEliminated = s.IsEliminated,
                    EliminatedRound = s.EliminatedRound,
                    FinalPlacement = s.FinalPlacement,
                    CurrentHp = s.IsEliminated ? 0 : s.ProjectedCurrentHp,
                    MaxHp = s.ProjectedMaxHp,
                    TavernTier = s.ProjectedTavernTier,
                    LeaderCardDefinitionId = s.ProjectedLeaderCardId,
                    LeaderName = leaderDef?.Name ?? "幻境统帅",
                    LeaderArtPreview = leaderDef?.ArtPreview,
                    IsReady = s.ProjectedIsRoundLocked,
                    ConsecutiveWins = s.ConsecutiveWins,
                    LastCombatDeltaHp = s.LastCombatDeltaHp,
                    NextOpponentPlayerId = nextOpp?.PlayerId,
                    NextOpponentIsGhost = nextOpp?.IsGhost ?? false
                });
            }

            var aliveSeats = populatedSeats.Where(s => !s.IsEliminated).ToList();
            return new MirageStandingsSnapshotDto
            {
                RoomId = room.RoomId,
                RoomVersion = room.RoomVersion,
                Phase = room.Phase.ToString(),
                RoundNumber = Math.Max(1, room.RoundNumber),
                SettledRoundNumber = settledRoundNumber,
                ActiveCombatId = room.ActiveRoundCombatBatchId,
                ReadyCount = aliveSeats.Count(s => s.ProjectedIsRoundLocked),
                AliveCount = aliveSeats.Count,
                RoundEndsAtUtc = room.RoundEndsAtUtc,
                ChampionPlayerId = room.ChampionPlayerId,
                NextOpponentsByPlayerId = nextOpponentsByPlayerId,
                Entries = entries
            };
        }

        private MirageNextOpponentSummaryDto BuildNextOpponentDtoLocked(MirageLobbySeat oppSeat, bool isGhost)
        {
            var leaderDef = oppSeat.ProjectedLeaderCardId > 0 ? _gameDataProvider.GetCardDefinition(oppSeat.ProjectedLeaderCardId) : null;
            return new MirageNextOpponentSummaryDto
            {
                PlayerId = oppSeat.PlayerId,
                SeatIndex = oppSeat.SeatIndex,
                DisplayName = oppSeat.DisplayName,
                AvatarUrl = oppSeat.AvatarUrl,
                IsGhost = isGhost,
                IsAi = oppSeat.Occupancy == MirageSeatOccupancy.Ai,
                TavernTier = oppSeat.ProjectedTavernTier,
                CurrentHp = oppSeat.IsEliminated ? 0 : oppSeat.ProjectedCurrentHp,
                MaxHp = oppSeat.ProjectedMaxHp,
                LeaderCardDefinitionId = oppSeat.ProjectedLeaderCardId,
                LeaderName = leaderDef?.Name ?? "幻境统帅",
                LeaderArtPreview = leaderDef?.ArtPreview,
                KnownWarbandCount = oppSeat.ProjectedKnownWarbandCount
            };
        }

        private MirageNextOpponentSummaryDto BuildGhostOpponentDto(MirageEliminatedGhostEntry? ghost)
        {
            var leaderDef = ghost != null && ghost.LeaderCardDefinitionId > 0
                ? _gameDataProvider.GetCardDefinition(ghost.LeaderCardDefinitionId)
                : null;
            return new MirageNextOpponentSummaryDto
            {
                PlayerId = ghost?.SourcePlayerId,
                SeatIndex = ghost?.SourceSeatIndex,
                DisplayName = ghost != null ? $"{ghost.DisplayName} (残影)" : "幻境残影",
                AvatarUrl = ghost?.AvatarUrl ?? string.Empty,
                IsGhost = true,
                IsAi = true,
                TavernTier = ghost?.TavernTier ?? 1,
                CurrentHp = 0,
                MaxHp = 20,
                LeaderCardDefinitionId = ghost?.LeaderCardDefinitionId ?? GameConstants.Cards.MirageLeaderCardId,
                LeaderName = leaderDef?.Name ?? "幻境统帅",
                LeaderArtPreview = leaderDef?.ArtPreview,
                KnownWarbandCount = ghost?.FrozenSideSnapshot != null ? CountFollowersInSnapshot(ghost.FrozenSideSnapshot) : 0
            };
        }

        private static MirageLobbySeat? PickDefaultSpectateSeatLocked(MirageLobbyRoom room)
        {
            return room.Seats
                .Where(s => s.Occupancy != MirageSeatOccupancy.Empty && !s.IsEliminated && !string.IsNullOrWhiteSpace(s.SeatGameId))
                .OrderByDescending(s => s.Occupancy == MirageSeatOccupancy.Human)
                .ThenByDescending(s => s.ProjectedCurrentHp)
                .ThenBy(s => s.SeatIndex)
                .FirstOrDefault();
        }

        private static int CountFollowersInSnapshot(MirageCombatSideSnapshot snap)
        {
            if (!snap.ZoneCardIds.TryGetValue("Field", out var fieldIds) || fieldIds.Count == 0)
            {
                return 0;
            }
            var cardMap = snap.Cards.ToDictionary(c => c.OriginalInstanceId);
            return fieldIds.Count(id => cardMap.TryGetValue(id, out var c) && !c.IsLeader);
        }

        private void TryCleanupPureAiRemainder(MirageLobbyRoom room)
        {
            List<string>? seatsToRetire = null;
            lock (room.SyncRoot)
            {
                if (room.Phase is MirageLobbyPhase.Waiting or MirageLobbyPhase.Closed)
                {
                    return;
                }

                var now = DateTime.UtcNow;
                foreach (var expiredKey in room.HumanGraceDeadlinesUtc.Where(kv => kv.Value <= now).Select(kv => kv.Key).ToList())
                {
                    room.HumanGraceDeadlinesUtc.Remove(expiredKey);
                }

                var hasAliveHuman = room.Seats.Any(s => s.Occupancy == MirageSeatOccupancy.Human && !s.IsEliminated);
                var hasGraceHuman = room.HumanGraceDeadlinesUtc.Count > 0;
                var hasSpectator = room.ActiveSpectatorConnectionIds.Count > 0;

                if (hasAliveHuman || hasGraceHuman || hasSpectator)
                {
                    return;
                }

                _logger.LogInformation("[MirageLobby:{RoomId}] No eligible humans, grace sessions, or spectators remain; retiring pure-AI remainder.", room.RoomId);
                CloseRoomInternalLocked(room, "所有真人选手与观战者均已离场，战局已回收。", out seatsToRetire);
            }

            RetireSeatGamesOutsideLock(seatsToRetire);
        }

        private async Task SettleHumanMetaOnceIfEligibleAsync(string roomId, Guid playerId, int placement)
        {
            if (playerId == Guid.Empty || !_rooms.TryGetValue(roomId, out var room))
            {
                return;
            }

            lock (room.SyncRoot)
            {
                var seat = room.Seats.FirstOrDefault(s => s.Occupancy == MirageSeatOccupancy.Human && s.PlayerId == playerId);
                if (seat == null || seat.MetaSettled)
                {
                    return;
                }
                seat.MetaSettled = true;
            }

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetService<ApplicationDbContext>();
                var transactionService = scope.ServiceProvider.GetService<ITransactionService>();
                var economyService = scope.ServiceProvider.GetService<IEconomyService>();
                var questService = scope.ServiceProvider.GetService<IQuestProgressService>();
                if (db == null || transactionService == null)
                {
                    return;
                }

                var settlementGameId = $"mirage8p:{roomId}";
                var isTop4 = placement <= 4;
                const int TournamentMatchCoinReward = 100;

                await transactionService.ExecuteInTransactionAsync(async () =>
                {
                    var isPersistedUser = await db.Users.AnyAsync(u => u.Id == playerId);
                    if (!isPersistedUser)
                    {
                        return;
                    }

                    var alreadySettled = await db.GameSettlements.AnyAsync(
                        s => s.GameId == settlementGameId && s.UserId == playerId);
                    if (alreadySettled)
                    {
                        return;
                    }

                    var now = DateTime.UtcNow;
                    db.GameSettlements.Add(new GameSettlement
                    {
                        Id = Guid.NewGuid(),
                        GameId = settlementGameId,
                        UserId = playerId,
                        Result = isTop4 ? GameResult.Win : GameResult.Loss,
                        WinnerUserId = isTop4 ? playerId : Guid.Empty,
                        SettledAtUtc = now
                    });

                    if (economyService != null)
                    {
                        var coinsGranted = await economyService.AddCoinsAsync(
                            playerId,
                            TournamentMatchCoinReward,
                            isTop4 ? CoinTransactionType.GameWin : CoinTransactionType.GameLoss,
                            description: isTop4
                                ? $"幻境战棋前四奖励（第 {placement} 名）: {roomId}"
                                : $"幻境战棋参与奖励（第 {placement} 名）: {roomId}",
                            relatedEntityId: settlementGameId);
                        if (!coinsGranted)
                        {
                            throw new InvalidOperationException($"Failed to grant Mirage 8P coins for room {roomId}, player {playerId}.");
                        }
                    }

                    if (questService != null)
                    {
                        var sourceKey = $"{roomId}:{playerId:N}";
                        await questService.RecordActivityAsync(playerId, QuestActivityType.PlayMatch, "Mirage8P", sourceKey, 1, now);
                        if (isTop4)
                        {
                            await questService.RecordActivityAsync(playerId, QuestActivityType.WinMatch, "Mirage8P", $"{sourceKey}:win", 1, now);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                lock (room.SyncRoot)
                {
                    var seat = room.Seats.FirstOrDefault(s => s.Occupancy == MirageSeatOccupancy.Human && s.PlayerId == playerId);
                    if (seat != null) seat.MetaSettled = false;
                }
                _logger.LogWarning(ex, "[MirageLobby:{RoomId}] Non-fatal error settling coins/quests for Player {PlayerId}.", roomId, playerId);
            }
        }

        private async Task BroadcastStandingsSafeAsync(string roomId, MirageStandingsSnapshotDto standings)
        {
            try
            {
                await _hubContext.Clients.Group(SignalRGroupNames.MirageRoom(roomId)).ReceiveMirageStandings(standings);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[MirageLobby] BroadcastStandings failed for room {RoomId}", roomId);
            }
        }
    }
}
