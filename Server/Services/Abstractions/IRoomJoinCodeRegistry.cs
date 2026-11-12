using System;

namespace ShadowverseBeyond.Server.Services.Abstractions
{
    public enum RegisteredRoomKind
    {
        Classic = 0,
        TakeTwo = 1,
        Mirage8P = 2
    }

    public readonly record struct ResolvedJoinCodeTarget(
        RegisteredRoomKind Kind,
        string RoomId,
        string JoinCode);

    public interface IRoomJoinCodeRegistry
    {
        bool TryRegister(string joinCode, RegisteredRoomKind kind, string roomId);

        string AllocateUniqueCode(
            RegisteredRoomKind kind,
            string roomId,
            string alphabet,
            int length = 6);

        bool TryResolve(string? joinCode, out ResolvedJoinCodeTarget target);

        void Release(string? joinCode, string? expectedRoomId = null);
    }
}
