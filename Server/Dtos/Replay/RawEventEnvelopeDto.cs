using System;
using System.Text.Json.Nodes;

namespace ShadowverseBeyond.Server.Dtos.Replay
{
    public sealed record RawEventEnvelopeDto(
        int SchemaVersion,
        string EventType,
        string? EventId,
        DateTime TimestampUtc,
        string? SourceId,
        string[] TargetIds,
        JsonObject Payload);
}
