using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    public sealed class ConditionTraceEntryDto
    {
        public string Expression { get; init; } = string.Empty;
        public string Trace { get; init; } = string.Empty;
    }

    public sealed class ConditionTraceFailedDto
    {
        public string GameId { get; init; } = string.Empty;
        public Guid AbilityRuntimeId { get; init; }
        public string AbilityName { get; init; } = string.Empty;
        public string CardName { get; init; } = string.Empty;
        public string CardId { get; init; } = string.Empty;
        public bool Success { get; init; }
        public string TraceKind { get; init; } = "Condition";
        public int? TargetCount { get; init; }
        public string EffectType { get; init; } = string.Empty;
        public string ConditionId { get; init; } = string.Empty;
        public string Expression { get; init; } = string.Empty;
        public string Trace { get; init; } = string.Empty;
        public IReadOnlyList<ConditionTraceEntryDto> Conditions { get; init; } = Array.Empty<ConditionTraceEntryDto>();
        public string Message { get; init; } = string.Empty;
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    }
}
