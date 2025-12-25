using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos.Replay
{
    public sealed record RecordedCommittedEventDto(
        long Seq,
        Guid BundleId,
        int EventIndex,
        Guid EventId,
        string EventType,
        DateTime TimestampUtc,
        int SchemaVersion,
        string? Announcement,
        Guid? SourceId,
        Guid[] TargetIds,
        IReadOnlyList<RecordedClientDtoEnvelopeDto> ClientDtos,
        string? RawEventJson);
}
