using System.Text.Json;

namespace ShadowverseBeyond.Server.Dtos.Replay;

public sealed record ReplayStateChange(string Operation, string[] Path, JsonElement Value);

public sealed record ReplayHistoryNode(int FormatVersion, string GameId, long Seq, long PreviousSeq,
    Guid NodeId, Guid? EventId, Guid? ParentEventId, Guid? ActionId, Guid? ActorPlayerId,
    string Origin, int? PlayerOperationNo, int TurnNo, Guid? TurnOwnerId,
    string Kind, string EventType, string Stage, DateTime RecordedAtUtc, long StateRevision,
    string SampleKind, JsonElement Data, IReadOnlyList<ReplayStateChange> Changes);

public sealed record ReplayHistoryCheckpoint(int FormatVersion, string GameId, long AtSeq, JsonElement Document);

public sealed record ReplayRecoveryCapabilities(bool ReadOnlyEventRestore, bool LoadPlayableMatch, string ExecutionRecoveryStatus);

public sealed record ReplayHistoryManifest(int FormatVersion, string GameId, string CursorKind, long HeadSeq,
    int EventCount, int CheckpointCount, string Status, string SampleKind, string? WinnerId,
    DateTime StartedAtUtc, DateTime? EndedAtUtc, string? ServerBuild,
    string? HostPlayerId, string? GuestPlayerId, ReplayRecoveryCapabilities Recovery);

public sealed record ReplayHistoryArchive(int FormatVersion, ReplayHistoryManifest Manifest,
    IReadOnlyList<ReplayHistoryCheckpoint> Checkpoints, IReadOnlyList<ReplayHistoryNode> Events, JsonElement? ExecutionRules = null);

public sealed record ReplayHistoryRestore(int FormatVersion, string GameId, long AtSeq, long CheckpointSeq,
    JsonElement Document, ReplayHistoryNode? Node);
