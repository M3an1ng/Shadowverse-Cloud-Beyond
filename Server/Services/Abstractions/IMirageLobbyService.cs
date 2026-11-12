using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Services.Abstractions
{
    public enum MirageLobbyError
    {
        None = 0,
        NotFound = 1,
        InvalidJoinCode = 2,
        AlreadyFull = 3,
        AlreadyStarted = 4,
        AlreadyClosed = 5,
        Expired = 6,
        NotParticipant = 7,
        Forbidden = 8,
        NotReady = 9,
        InvalidOperation = 10
    }

    public sealed record MirageLobbyOperationResult(
        bool Success,
        MirageLobbyError Error = MirageLobbyError.None,
        string? Message = null,
        MirageLobbyRoomDto? Room = null)
    {
        public static MirageLobbyOperationResult Ok(MirageLobbyRoomDto room) =>
            new(true, MirageLobbyError.None, null, room);

        public static MirageLobbyOperationResult Fail(MirageLobbyError error, string message, MirageLobbyRoomDto? room = null) =>
            new(false, error, message, room);
    }

    public interface IMirageLobbyService
    {
        IReadOnlyList<MirageLobbyRoomListItemDto> ListRooms();

        MirageResolveCodeResponseDto ResolveJoinCode(string? joinCode);

        Task<MirageLobbyOperationResult> CreateRoomAsync(
            Guid? hostPlayerId,
            string? hostDisplayName,
            string? hostAvatarUrl,
            bool isPublic,
            int? recruitTimeoutSeconds,
            bool quickStartSoloAi,
            CancellationToken cancellationToken = default);

        MirageLobbyOperationResult GetRoom(
            string roomId,
            Guid? callerPlayerId = null,
            string? callerToken = null);

        Task<MirageLobbyOperationResult> JoinRoomAsync(
            string roomId,
            string? joinCode,
            Guid? playerId,
            string? existingToken,
            string? displayName,
            string? avatarUrl,
            bool allowPublicWithoutCode,
            CancellationToken cancellationToken = default);

        Task<MirageLobbyOperationResult> JoinByCodeAsync(
            string joinCode,
            Guid? playerId,
            string? existingToken,
            string? displayName,
            string? avatarUrl,
            CancellationToken cancellationToken = default);

        Task<MirageLobbyOperationResult> SetReadyAsync(
            string roomId,
            Guid playerId,
            string token,
            bool ready,
            CancellationToken cancellationToken = default);

        Task<MirageLobbyOperationResult> KickSeatAsync(
            string roomId,
            Guid hostPlayerId,
            string hostToken,
            Guid targetPlayerId,
            CancellationToken cancellationToken = default);

        Task<MirageLobbyOperationResult> LeaveRoomAsync(
            string roomId,
            Guid playerId,
            string token,
            CancellationToken cancellationToken = default);

        Task<MirageLobbyOperationResult> StartRoomAsync(
            string roomId,
            Guid hostPlayerId,
            string hostToken,
            CancellationToken cancellationToken = default);

        // Runtime & Hub integration hooks
        bool TryValidateParticipant(
            string roomId,
            Guid playerId,
            string token,
            out MirageLobbyRoomDto? roomDto,
            out string? seatGameId,
            out bool isEliminated,
            out string? errorMessage);

        bool TryResolveSpectateTarget(
            string roomId,
            Guid? preferredTargetPlayerId,
            out Guid resolvedTargetPlayerId,
            out string resolvedTargetSeatGameId,
            out long roomVersion,
            out MirageStandingsSnapshotDto? standings,
            out string? errorMessage);

        MirageStandingsSnapshotDto? GetStandingsSnapshot(string roomId);

        bool TryGetRoomIdBySeatGameId(string seatGameId, out string roomId);

        void RefreshExpiration(string roomId, Guid playerId);

        void NotifySeatLocked(string roomId, Guid playerId, int roundNumber);

        void NotifySeatConnected(string roomId, Guid playerId);

        void NotifySeatDisconnected(string roomId, Guid playerId, TimeSpan gracePeriod);

        void RegisterSpectatorConnection(string roomId, string connectionId);

        void UnregisterSpectatorConnection(string roomId, string connectionId);

        Task ForfeitSeatAsync(
            string roomId,
            Guid playerId,
            string reason,
            CancellationToken cancellationToken = default,
            long? expectedDisconnectEpoch = null);
    }
}
