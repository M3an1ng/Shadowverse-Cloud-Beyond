using System.Text.Json;
using System.Text.Json.Nodes;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.Replay;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Mappings;

namespace ShadowverseBeyond.Server.Services;

// Arrays of instances become ordered ids, so a draw doesn't rewrite every card in the deck.
public static class ReplayStateChanges
{
    private static readonly string[] Zones = ["hand", "deck", "field", "graveyard", "extra", "emblem"];

    public static JsonObject Capture(GameContext context, IEnumerable<JsonElement> choices)
    {
        var game = context.CurrentGameState;
        var view = JsonSerializer.SerializeToNode(GameStateProjection.Create(game, context), SqliteReplayFrames.Json)!.AsObject();
        var players = new JsonObject();
        var cards = new JsonObject();
        var zones = new JsonObject();
        var fusions = new JsonObject();
        var order = new JsonArray();
        string Register(JsonObject card)
        {
            var id = card["instanceId"]!.GetValue<string>();
            var fused = card["fusedCards"]!.AsArray();
            var children = new JsonArray();
            foreach (var child in fused) children.Add(Register(child!.DeepClone().AsObject()));
            card.Remove("fusedCards");
            cards[id] = card;
            fusions[id] = children;
            return id;
        }
        foreach (var entry in view["players"]!.AsArray())
        {
            var player = entry!.DeepClone().AsObject();
            var id = player["playerId"]!.GetValue<string>();
            order.Add(id);
            var areas = new JsonObject();
            foreach (var zone in Zones)
            {
                var ids = new JsonArray();
                foreach (var card in player[zone]!.AsArray()) ids.Add(Register(card!.DeepClone().AsObject()));
                player.Remove(zone);
                areas[zone] = ids;
            }
            areas["leaderCard"] = player["leaderCard"] is JsonObject leader ? JsonValue.Create(Register(leader.DeepClone().AsObject())) : null;
            player.Remove("leaderCard");
            players[id] = player;
            zones[id] = areas;
        }
        foreach (var card in game.GetRegisteredCardInstances())
            if (!cards.ContainsKey(card.InstanceId.ToString()))
                Register(JsonSerializer.SerializeToNode(CardInstanceProjection.Create(card, context), SqliteReplayFrames.Json)!.AsObject());
        view.Remove("players");
        view.Remove("player1");
        view.Remove("player2");
        var pending = new JsonObject();
        foreach (var choice in choices) pending[choice.GetProperty("pendingChoiceId").GetString()!] = JsonNode.Parse(choice.GetRawText());
        return new JsonObject { ["game"] = view, ["playerOrder"] = order, ["players"] = players,
            ["cards"] = cards, ["zones"] = zones, ["fusions"] = fusions, ["pendingChoices"] = pending };
    }

    public static IReadOnlyList<ReplayStateChange> Between(JsonObject previous, JsonObject next)
    {
        var changes = new List<ReplayStateChange>();
        void Visit(JsonNode? before, JsonNode? after, string[] path)
        {
            if (JsonNode.DeepEquals(before, after)) return;
            if (before is JsonObject oldObject && after is JsonObject newObject)
            {
                foreach (var key in oldObject.Select(p => p.Key).Union(newObject.Select(p => p.Key)))
                {
                    var childPath = path.Append(key).ToArray();
                    if (!newObject.ContainsKey(key)) changes.Add(new ReplayStateChange("remove", childPath, JsonSerializer.SerializeToElement<object?>(null)));
                    else if (!oldObject.ContainsKey(key)) changes.Add(new ReplayStateChange("set", childPath, JsonSerializer.SerializeToElement(newObject[key], SqliteReplayFrames.Json)));
                    else Visit(oldObject[key], newObject[key], childPath);
                }
                return;
            }
            changes.Add(new ReplayStateChange("set", path, JsonSerializer.SerializeToElement(after, SqliteReplayFrames.Json)));
        }
        Visit(previous, next, []);
        return changes;
    }

    public static void Apply(JsonObject document, IEnumerable<ReplayStateChange> changes)
    {
        foreach (var change in changes)
        {
            if (change.Path.Length == 0) throw new InvalidDataException("replay_change_requires_property_path");
            var parent = document;
            foreach (var key in change.Path.SkipLast(1))
                parent = parent[key] as JsonObject ?? throw new InvalidDataException("replay_change_parent_missing: " + string.Join('/', change.Path));
            var property = change.Path[^1];
            if (change.Operation == "remove")
            {
                if (!parent.Remove(property)) throw new InvalidDataException("replay_change_remove_missing");
            }
            else if (change.Operation == "set") parent[property] = JsonNode.Parse(change.Value.GetRawText());
            else throw new InvalidDataException("unsupported_replay_change: " + change.Operation);
        }
    }

    public static JsonObject View(JsonObject document)
    {
        var view = document["game"]!.DeepClone().AsObject();
        JsonObject Card(string id, HashSet<string> ancestors)
        {
            if (!ancestors.Add(id)) throw new InvalidDataException("replay_fusion_cycle");
            var card = document["cards"]![id]?.DeepClone().AsObject() ?? throw new InvalidDataException("replay_card_missing: " + id);
            var children = new JsonArray();
            foreach (var child in document["fusions"]![id]!.AsArray()) children.Add(Card(child!.GetValue<string>(), new HashSet<string>(ancestors)));
            card["fusedCards"] = children;
            return card;
        }
        var players = new JsonArray();
        foreach (var playerId in document["playerOrder"]!.AsArray())
        {
            var id = playerId!.GetValue<string>();
            var player = document["players"]![id]!.DeepClone().AsObject();
            foreach (var zone in Zones)
            {
                var cards = new JsonArray();
                foreach (var card in document["zones"]![id]![zone]!.AsArray()) cards.Add(Card(card!.GetValue<string>(), []));
                player[zone] = cards;
            }
            player["leaderCard"] = document["zones"]![id]!["leaderCard"] is { } leader ? Card(leader.GetValue<string>(), []) : null;
            players.Add(player);
        }
        view["players"] = players;
        view["player1"] = players.Count > 0 ? players[0]!.DeepClone() : null;
        view["player2"] = players.Count > 1 ? players[1]!.DeepClone() : null;
        return view;
    }
}
