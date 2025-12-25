using System;
using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Dtos.Replay
{
    public sealed record RecordedSnapshotDto(
        string GameId,
        long AtSeq,
        DateTime CreatedAtUtc,
        int SchemaVersion,
        string Compression,
        string PayloadHash,
        GameStateDto Snapshot);
}
