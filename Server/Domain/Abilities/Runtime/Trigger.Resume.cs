using System.Text.Json;
using ShadowverseBeyond.Server.Services;

namespace ShadowverseBeyond.Server.Domain.Abilities.Runtime;

public partial class Trigger
{
    internal JsonElement CaptureResumeState() => JsonSerializer.SerializeToElement(new
        { LastTriggeredTime, TotalTriggerCount, CurrentTurnTriggerCount, TriggeredThisEvent, IsEnabled }, ReplayExecutionState.Json);

    internal void RestoreResumeState(JsonElement state)
    {
        LastTriggeredTime = state.GetProperty("lastTriggeredTime").Deserialize<DateTime?>();
        TotalTriggerCount = state.GetProperty("totalTriggerCount").GetInt32();
        CurrentTurnTriggerCount = state.GetProperty("currentTurnTriggerCount").GetInt32();
        TriggeredThisEvent = state.GetProperty("triggeredThisEvent").GetBoolean();
        IsEnabled = state.GetProperty("isEnabled").GetBoolean();
    }
}
