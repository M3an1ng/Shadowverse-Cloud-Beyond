using System.Text.Json;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Services;

namespace ShadowverseBeyond.Server.Domain.Abilities;

public partial class Ability
{
    internal JsonElement CaptureResumeState() => JsonSerializer.SerializeToElement(new
    {
        RuntimeId, SourceBuffId, SourceCardId, Config, SummaryRootAbilityId, SequenceScopePathPrefix,
        QueuedEffectStateVersion, LastQueuedEffectInvalidationReason, TotalTriggerCount, LastTriggerTime,
        DerivedDefinitionId, Trigger = Trigger.CaptureResumeState(), SequenceCursors = _sequenceCursorsByScopePath
    }, ReplayExecutionState.Json);

    internal void RestoreResumeState(JsonElement state)
    {
        SummaryRootAbilityId = state.GetProperty("summaryRootAbilityId").GetGuid();
        SequenceScopePathPrefix = state.GetProperty("sequenceScopePathPrefix").GetString()!;
        QueuedEffectStateVersion = state.GetProperty("queuedEffectStateVersion").GetInt64();
        LastQueuedEffectInvalidationReason = state.GetProperty("lastQueuedEffectInvalidationReason").GetString();
        TotalTriggerCount = state.GetProperty("totalTriggerCount").GetInt32();
        LastTriggerTime = state.GetProperty("lastTriggerTime").Deserialize<DateTime?>();
        _derivedDefinitionId = state.GetProperty("derivedDefinitionId").Deserialize<long?>();
        Trigger.RestoreResumeState(state.GetProperty("trigger"));
        _sequenceCursorsByScopePath.Clear();
        foreach (var pair in state.GetProperty("sequenceCursors").EnumerateObject())
            _sequenceCursorsByScopePath.Add(pair.Name, pair.Value.GetInt32());
    }

    internal void RebindSourceCard(CardInstance? sourceCard)
    {
        SourceCard = sourceCard;
    }
}
