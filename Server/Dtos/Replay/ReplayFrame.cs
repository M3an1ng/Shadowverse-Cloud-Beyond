using System.Text.Json;

namespace ShadowverseBeyond.Server.Dtos.Replay;

public sealed record ReplayMessage(string Method, JsonElement Payload, string Audience);

public sealed record ReplayFrame(
    int FormatVersion, string GameId, long FrameNo, Guid CaptureId, Guid? ActionId,
    string Boundary, string ActionStatus, string? Reason, DateTime RecordedAtUtc,
    long StateRevision, string SampleKind, JsonElement State,
    IReadOnlyList<JsonElement> PendingChoices, IReadOnlyList<ReplayMessage> Messages);

public sealed record ReplayManifest(int FormatVersion, string GameId, long HeadFrame, int FrameCount,
    string Status, string SampleKind, string? WinnerId, DateTime StartedAtUtc, DateTime? EndedAtUtc,
    string? ServerBuild, string? HostPlayerId, string? GuestPlayerId);

public sealed record ReplayArchive(int FormatVersion, ReplayManifest Manifest, IReadOnlyList<ReplayFrame> Frames);
