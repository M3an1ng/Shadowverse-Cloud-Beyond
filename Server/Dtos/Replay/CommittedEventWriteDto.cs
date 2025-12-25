using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos.Replay
{
    public sealed record CommittedEventWriteDto(
        Guid EventId,
        string EventType,
        DateTime TimestampUtc,
        string? Announcement,
        Guid? SourceId,
        Guid[] TargetIds,
        IReadOnlyList<RecordedClientDtoEnvelopeDto> ClientDtos,
        string? RawEventJson);
}
