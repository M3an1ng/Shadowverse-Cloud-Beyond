using System.Text.Json;
using System.Text.Json.Nodes;
using ShadowverseBeyond.Server.Dtos.Replay;
using ShadowverseBeyond.Server.Infrastructure.Services;

namespace ShadowverseBeyond.Server.Services;

// The viewer needs board changes, not the execution checkpoints or private command payloads.
public static class ReplayPlayback
{
    public sealed record Step(string Kind, int TurnNo, Guid? Owner, string? CardId, string? TargetId,
        int? Amount, List<ReplayStateChange> Changes)
    {
        public List<ReplayVisual> Effects { get; } = [];
    }
    public sealed record Timeline(JsonObject Opening, IReadOnlyList<Step> Steps);

    private static bool VisiblePath(ReplayStateChange change) => change.Path.Length > 0 &&
        change.Path[0] is "game" or "players" or "playerOrder" or "cards" or "zones" or "fusions";

    public static async Task<Timeline> ReadAsync(SqliteReplayHistory history, string gameId, CancellationToken cancel)
    {
        var manifest = await history.ManifestAsync(gameId, cancel);
        if (manifest.Status != "closed") throw new KeyNotFoundException();
        var initial = await history.RestoreAsync(gameId, 0, cancel);
        var opening = JsonNode.Parse(initial.Document.GetRawText())!.AsObject();
        opening.Remove("execution");
        opening.Remove("pendingChoices");
        var steps = new List<Step>();
        var pending = new List<ReplayStateChange>();
        string? action = null;
        var actionStart = 0;
        var presentation = new ReplayPresentation(actionStart);
        for (long next = 1; next <= manifest.HeadSeq;)
        {
            var nodes = await history.EventsAsync(gameId, next, manifest.HeadSeq, 128, cancel);
            if (nodes.Count == 0) throw new InvalidDataException("replay_event_gap");
            foreach (var node in nodes)
            {
                if (node.EventType == "operation.requested")
                {
                    presentation.Attach(steps);
                    action = node.Data.GetProperty("actionType").GetString();
                    actionStart = steps.Count;
                    presentation = new ReplayPresentation(actionStart);
                }
                var kind = node.Stage == "committed" && node.EventType is
                    ("MulliganSubmitted" or "TurnStart" or "TurnEnd" or "PlayCard" or "Summon" or "Attack" or "Damage" or
                    "Heal" or "Destroy" or "Vanish" or "Evolve" or "SuperEvolve" or "Draw" or "Fusion" or "Act" or
                    "Transform" or "Discard" or "Return" or "CardFused" or "CardRevealed" or "GameEnd") ? node.EventType : null;
                if (node.EventType == "operation.completed" && node.Stage == "completed" && steps.Count == actionStart)
                    kind = action ?? "Action";
                if (kind != null)
                {
                    // Include generated zone moves and resource updates in the action which caused them.
                    // For example, Summon commits before its generated CardMoved places the follower.
                    if (steps.Count > actionStart)
                    {
                        steps[^1].Changes.AddRange(pending);
                        pending.Clear();
                    }
                    pending.AddRange(node.Changes.Where(VisiblePath));
                    node.Data.TryGetProperty("parameters", out var parameters);
                    var card = Card(parameters, "PlayedCard", "Attacker", "EvolvedCard", "SummonedCard", "DamageSource", "HealSource");
                    var target = Card(parameters, "TargetCard", "DamageTarget", "HealTarget");
                    int? amount = null;
                    if (parameters.ValueKind == JsonValueKind.Object &&
                        (parameters.TryGetProperty("ActualDamage", out var n) || parameters.TryGetProperty("HealAmount", out n))
                        && n.TryGetInt32(out var value)) amount = value;
                    steps.Add(new Step(kind, node.TurnNo, node.TurnOwnerId, card, target, amount, pending));
                    pending = [];
                }
                else pending.AddRange(node.Changes.Where(VisiblePath));
                presentation.Track(node, Math.Max(actionStart, steps.Count - 1));
                presentation.ReadMessage(node);
                if (node.EventType == "operation.completed" && steps.Count > 0)
                {
                    steps[^1].Changes.AddRange(pending);
                    pending.Clear();
                }
            }
            next = nodes[^1].Seq + 1;
        }
        if (steps.Count == 0) throw new InvalidDataException("replay_has_no_steps");
        presentation.Attach(steps);
        steps[^1].Changes.AddRange(pending);
        return new Timeline(opening, steps);
    }

    private static string? Card(JsonElement parameters, params string[] names)
    {
        if (parameters.ValueKind != JsonValueKind.Object) return null;
        foreach (var name in names)
            if (parameters.TryGetProperty(name, out var card) && card.ValueKind == JsonValueKind.Object &&
                card.TryGetProperty("instanceId", out var id) && id.ValueKind == JsonValueKind.String) return id.GetString();
        return null;
    }
}
