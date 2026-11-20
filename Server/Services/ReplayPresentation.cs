using System.Text.Json;
using ShadowverseBeyond.Server.Dtos.Replay;

namespace ShadowverseBeyond.Server.Services;

public sealed record ReplayVisual(string Method, Dictionary<string, JsonElement> Payload)
{
    // Only the public battle effects used by the spectator view belong in a playback packet.
    private static readonly Dictionary<string, string[]> Fields = new()
    {
        ["DamageDealt"] = ["targetUid", "damageAmount"],
        ["HealApplied"] = ["targetUid", "healAmount"],
        ["HealthChanged"] = ["cardInstanceId", "newHealth"],
        ["StatChange"] = ["targetEntityUid", "newAttack", "newCurrentHealth", "newMaxHealth", "newCost"],
        ["AttackDeclared"] = ["attackerUid", "targetUid"],
        ["KeywordRemoved"] = ["cardInstanceId", "keyword"],
        ["CardPlayed"] = ["playedCardInstance", "sourcePlayerId", "paidCost", "playMode"],
        ["CardMoved"] = ["cardUid", "playerId", "fromZone", "toZone", "moveReason", "movedCard"],
        ["UnitEvolved"] = ["evolvedUnitState", "sourcePlayerId"],
        ["AbilityTriggered"] = ["sourceCardUid", "sourcePlayerId", "abilityKeywords", "triggerEventType", "abilityParameters"],
        ["CardRevealed"] = ["revealedCard", "revealingPlayerId", "handIndex"],
        ["Fusion"] = ["sourcePlayerId", "fusedCardInstanceIds"],
        ["TurnStarted"] = ["sourcePlayerId"]
    };

    public static ReplayVisual? FromMessage(string? method, JsonElement payload, string? audience)
    {
        if (audience == null || !(audience.StartsWith("Group (All): Game_", StringComparison.Ordinal) ||
            audience.StartsWith("Group: Game_", StringComparison.Ordinal)) || method == null ||
            !Fields.TryGetValue(method, out var fields) || payload.ValueKind != JsonValueKind.Object) return null;
        var visible = new Dictionary<string, JsonElement>();
        foreach (var field in fields)
        {
            if (payload.TryGetProperty(field, out var value) ||
                payload.TryGetProperty(char.ToUpperInvariant(field[0]) + field[1..], out value))
                visible[field] = value;
        }
        return new(method, visible);
    }
}

// The engine commits state first and broadcasts the spectator messages in a batch afterwards.
// New recordings identify the source event. Older ones preserve broadcast order within each action.
internal sealed class ReplayPresentation(int firstStep)
{
    private readonly Dictionary<Guid, int> eventSteps = new();
    private readonly Dictionary<string, Queue<int>> legacySteps = new();
    private readonly List<(int Index, ReplayVisual Visual)> messages = [];
    private int lastMessageStep = firstStep;

    public void Track(ReplayHistoryNode node, int step)
    {
        if (node.EventId is not Guid id || node.Kind != "domain" || node.Stage != "committed") return;
        eventSteps[id] = step;
        var method = node.EventType switch
        {
            "Damage" => "DamageDealt", "Heal" => "HealApplied", "Attack" => "AttackDeclared",
            "PlayCard" => "CardPlayed", "TurnStart" => "TurnStarted", "Evolve" or "SuperEvolve" => "UnitEvolved",
            "CardFused" => "Fusion", "StatChanged" => "StatChange",
            "HealthChanged" or "KeywordRemoved" or "CardMoved" or "CardRevealed" or "AbilityTriggered" => node.EventType,
            _ => null
        };
        if (method == null) return;
        if (!legacySteps.TryGetValue(method, out var slots)) legacySteps[method] = slots = new();
        slots.Enqueue(step);
    }

    public void ReadMessage(ReplayHistoryNode node)
    {
        if (node.Kind != "presentation" || node.EventType != "message" || node.Stage != "emitted") return;
        var data = node.Data;
        var visual = ReplayVisual.FromMessage(data.GetProperty("method").GetString(),
            data.GetProperty("payload"), data.GetProperty("audience").GetString());
        if (visual == null) return;
        if (legacySteps.TryGetValue(visual.Method, out var slots) && slots.TryDequeue(out var legacy))
            lastMessageStep = legacy;
        if (node.ParentEventId is Guid id && eventSteps.TryGetValue(id, out var exact))
            lastMessageStep = exact;
        messages.Add((lastMessageStep, visual));
    }

    public void Attach(List<ReplayPlayback.Step> steps)
    {
        foreach (var (index, visual) in messages)
            if (index < steps.Count) steps[index].Effects.Add(visual);
    }
}
