using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using AutoMapper;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Auras;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

// Only idle action boundaries are runnable. An event snapshot cannot stand in for a suspended execution stack.
public static class ReplayExecutionState
{
    public static readonly JsonSerializerOptions Json = CreateJson();
    public static string EngineBuild => typeof(ReplayExecutionState).Module.ModuleVersionId.ToString();
    private static readonly string[] Zones = ["Hand","Deck","Field","Graveyard","Extra","Emblem","InitialDeck"];

    private static JsonSerializerOptions CreateJson()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info => {
            if (info.Type == typeof(CardDefinition))
                info.Properties.Remove(info.Properties.Single(p => p.Name == "cardSetDefinition"));
            if (info.Type == typeof(PlayerState))
            {
                var excluded = Zones.Select(JsonNamingPolicy.CamelCase.ConvertName).Concat(
                    ["leader","counters","counterMap","canEvolve","canSuperEvolve","combo","playedCardCountThisTurn","graveyardCount","bloodPool","rally"])
                    .ToHashSet();
                foreach (var property in info.Properties.Where(p => p.Set == null || excluded.Contains(p.Name)).ToArray()) info.Properties.Remove(property);
            }
        });
        var json = new JsonSerializerOptions(SqliteReplayFrames.Json) { TypeInfoResolver = resolver };
        json.Converters.Add(new ReplayBuffEffectConverter());
        return json;
    }

    public static JsonObject Capture(GameState game, IServiceProvider services)
    {
        var history = services.GetRequiredService<IEventHistoryService>().GetEvents(game.GameId);
        var cards = new Dictionary<Guid,CardInstance>();
        void Register(CardInstance card)
        {
            if (!cards.TryAdd(card.InstanceId,card)) return;
            foreach (var child in card.FusedCards) Register(child);
        }
        foreach (var card in game.GetRegisteredCardInstances().Concat(game.AllCards).Concat(game.Player1.InitialDeck).Concat(game.Player2.InitialDeck)) Register(card);
        foreach (var entry in history)
        {
            if (entry.Source?.Card is { } source) Register(source);
            foreach (var target in entry.Target) if (target.Card is { } card) Register(card);
        }
        var runtimeCards = new JsonObject();
        var registered = game.GetRegisteredCardInstanceIds().ToHashSet();
        foreach (var card in cards.Values)
        {
            runtimeCards[card.InstanceId.ToString()] = JsonSerializer.SerializeToNode(new {
                card.InstanceId,card.OwnerPlayerId,card.BaseDefinitionId,card.CurrentZone,card.ZoneIndex,card.LastMoveReason,
                card.HealthFloorFromBuffRemoval,card.CombatContinuityVersion,
                State = card.CaptureRuntimeStateSnapshot() with { FusedCards = [] },
                FusedCards = card.FusedCards.Select(c => c.InstanceId).ToArray(),
                Registered = registered.Contains(card.InstanceId)
            },Json);
        }
        var players = new JsonObject();
        foreach (var player in new[] { game.Player1,game.Player2 })
        {
            var areas = new JsonObject();
            foreach (var zone in Zones)
                areas[zone] = JsonSerializer.SerializeToNode(((List<CardInstance>)typeof(PlayerState).GetProperty(zone)!.GetValue(player)!).Select(c => c.InstanceId),Json);
            players[player.PlayerId.ToString()] = new JsonObject {
                ["values"] = JsonSerializer.SerializeToNode(player,Json), ["zones"] = areas,
                ["leader"] = JsonValue.Create(player.Leader.InstanceId),
                ["counters"] = JsonSerializer.SerializeToNode(player.CounterMap,Json),
                ["manualEvolutionUsed"] = JsonValue.Create(player.HasUsedManualEvolutionRightThisTurn)
            };
        }
        var abilities = new JsonObject();
        var active = ((AbilityManager)game.AbilityManager).GetActiveAbilities().OfType<Ability>().ToArray();
        for (var i = 0; i < active.Length; i++)
            abilities[active[i].RuntimeId.ToString()] = new JsonObject { ["order"] = i,["state"] = JsonNode.Parse(active[i].CaptureResumeState().GetRawText()) };
        var buffs = cards.Values.SelectMany(c => game.BuffManager.GetBuffsForCard(c.InstanceId)).DistinctBy(b => b.Id);
        var runtimeBuffs = new JsonObject();
        foreach (var buff in buffs) runtimeBuffs[buff.Id.ToString()] = JsonSerializer.SerializeToNode(buff,Json);
        var aliases = new Dictionary<object,JsonObject>(ReferenceEqualityComparer.Instance);
        foreach (var player in new[] { game.Player1,game.Player2 })
            foreach (var zone in Zones)
                aliases[typeof(PlayerState).GetProperty(zone)!.GetValue(player)!] = new JsonObject { ["kind"] = "zone",["player"] = JsonValue.Create(player.PlayerId),["name"] = zone };
        foreach (var card in cards.Values) aliases[card.FusedCards] = new JsonObject { ["kind"] = "fused",["id"] = JsonValue.Create(card.InstanceId) };
        var events = new JsonObject();
        for (var i = 0; i < history.Count; i++) events[history[i].EventId.ToString()] = CaptureHistory(history[i],i,aliases);
        return new JsonObject {
            ["version"] = 1,["engineBuild"] = EngineBuild,
            ["game"] = JsonSerializer.SerializeToNode(new { game.Status,game.Phase,game.Mode,game.Mirage,game.StartedAtUtc,game.TurnTimeoutSeconds,
                game.TurnStartedAtUtc,game.TurnEndsAtUtc,game.StateRevision,game.BuffRevision,game.RefreshCycleId,
                game.CurrentPlayerId,game.FirstPlayerId,game.WinnerId,game.GameMessage,game.ReplayBranch,HostPlayerId = game.Player1.PlayerId,GuestPlayerId = game.Player2.PlayerId },Json),
            ["players"] = players,["cards"] = runtimeCards,["abilities"] = abilities,["buffs"] = runtimeBuffs,
            ["auras"] = JsonNode.Parse(((AuraManager)game.AuraManager).CaptureResumeState().GetRawText()),
            ["references"] = JsonSerializer.SerializeToNode(game.InGameIds.CaptureResumeState(),Json),["history"] = events
        };
    }

    public static async Task<GameState> RestoreAsync(JsonElement state,string newGameId,ReplayBranchOrigin origin,IServiceProvider services)
    {
        if (state.GetProperty("version").GetInt32() != 1 || state.GetProperty("engineBuild").GetString() != EngineBuild)
            throw new NotSupportedException("resume_engine_build_mismatch");
        var players = new Dictionary<Guid,PlayerState>();
        foreach (var entry in state.GetProperty("players").EnumerateObject())
        {
            var player = entry.Value.GetProperty("values").Deserialize<PlayerState>(Json)!;
            foreach (var counter in entry.Value.GetProperty("counters").EnumerateObject()) player.Counters.Set(counter.Name,counter.Value.GetInt32());
            player.RestoreManualEvolutionRight(entry.Value.GetProperty("manualEvolutionUsed").GetBoolean());
            players.Add(player.PlayerId,player);
        }
        var values = state.GetProperty("game");
        var game = new GameState(newGameId,players[values.GetProperty("hostPlayerId").GetGuid()],players[values.GetProperty("guestPlayerId").GetGuid()],values.GetProperty("firstPlayerId").GetGuid()) {
            Status = values.GetProperty("status").Deserialize<ShadowverseBeyond.Server.Dtos.Enums.GameStatus>(Json),
            Phase = values.GetProperty("phase").Deserialize<ShadowverseBeyond.Server.Dtos.Enums.GamePhase>(Json),
            Mode = values.TryGetProperty("mode", out var modeProp) ? modeProp.Deserialize<ShadowverseBeyond.Server.Dtos.Enums.GameMode>(Json) : ShadowverseBeyond.Server.Dtos.Enums.GameMode.Standard,
            Mirage = values.TryGetProperty("mirage", out var mirageProp) ? mirageProp.Deserialize<MirageMatchState>(Json) : null,
            CurrentPlayerId = values.GetProperty("currentPlayerId").Deserialize<Guid?>(),
            WinnerId = values.GetProperty("winnerId").Deserialize<Guid?>(),GameMessage = values.GetProperty("gameMessage").GetString(),
            StartedAtUtc = DateTime.UtcNow,TurnTimeoutSeconds = values.GetProperty("turnTimeoutSeconds").GetInt32(),ReplayBranch = origin,
            AbilityManager = services.GetRequiredService<IAbilityManager>(),BuffManager = services.GetRequiredService<IBuffManager>(),
            AuraManager = ActivatorUtilities.CreateInstance<AuraManager>(services)
        };
        if (game.IsGameOver) throw new ArgumentException("cannot_resume_finished_position");
        game.RestoreRevisions(values.GetProperty("stateRevision").GetInt64(),values.GetProperty("buffRevision").GetInt64(),values.GetProperty("refreshCycleId").GetInt64());
        var cards = new Dictionary<Guid,CardInstance>();
        foreach (var entry in state.GetProperty("cards").EnumerateObject())
        {
            var row = entry.Value;
            var card = new CardInstance(row.GetProperty("instanceId").GetGuid(),row.GetProperty("baseDefinitionId").GetInt32(),row.GetProperty("ownerPlayerId").GetGuid()) {
                CurrentZone = row.GetProperty("currentZone").Deserialize<CardZone>(Json),ZoneIndex = row.GetProperty("zoneIndex").Deserialize<int?>()
            };
            card.RestoreResumeState(row.GetProperty("state").Deserialize<CardInstance.CardRuntimeStateSnapshot>(Json)!,
                row.GetProperty("healthFloorFromBuffRemoval").GetInt32(),row.GetProperty("combatContinuityVersion").GetInt64());
            card.SetLastMoveReason(row.GetProperty("lastMoveReason").Deserialize<CardMoveReason>(Json));
            cards.Add(card.InstanceId,card);
            if (row.GetProperty("registered").GetBoolean()) game.RegisterCardInstance(card);
        }
        foreach (var entry in state.GetProperty("cards").EnumerateObject())
            foreach (var child in entry.Value.GetProperty("fusedCards").EnumerateArray()) cards[Guid.Parse(entry.Name)].AddFusedCard(cards[child.GetGuid()]);
        foreach (var entry in state.GetProperty("players").EnumerateObject())
        {
            var player = players[Guid.Parse(entry.Name)];
            foreach (var zone in Zones)
                typeof(PlayerState).GetProperty(zone)!.SetValue(player,entry.Value.GetProperty("zones").GetProperty(zone).EnumerateArray().Select(id => cards[id.GetGuid()]).ToList());
            player.Leader = cards[entry.Value.GetProperty("leader").GetGuid()];
        }
        foreach (var entry in state.GetProperty("abilities").EnumerateObject().OrderBy(e => e.Value.GetProperty("order").GetInt32()))
        {
            var row = entry.Value.GetProperty("state");
            game.AbilityManager.RegisterAbility(RestoreAbility(row,cards,services));
        }
        foreach (var row in state.GetProperty("buffs").EnumerateObject()) game.BuffManager.RegisterBuffInstance(RestoreBuff(row.Value),game);
        ((AuraManager)game.AuraManager).RestoreResumeState(state.GetProperty("auras"));
        game.InGameIds.RestoreResumeState(state.GetProperty("references").Deserialize<Dictionary<string,Guid>>(Json)!);
        var history = services.GetRequiredService<IEventHistoryService>();
        var historicalEvents = new Dictionary<Guid,ResumedHistoryEvent>();
        foreach (var row in state.GetProperty("history").EnumerateObject().OrderBy(e => e.Value.GetProperty("order").GetInt32()))
            await history.StoreEventAsync(newGameId,RestoreHistory(row.Value,cards,players,services,historicalEvents));
        game.RestoreRevisions(values.GetProperty("stateRevision").GetInt64(),values.GetProperty("buffRevision").GetInt64(),values.GetProperty("refreshCycleId").GetInt64());
        return game;
    }

    internal static Ability RestoreAbility(JsonElement row,Dictionary<Guid,CardInstance> cards,IServiceProvider services)
    {
        var source = row.GetProperty("sourceCardId").Deserialize<Guid?>();
        var ability = new Ability(row.GetProperty("config").Deserialize<AbilityConfig>(Json)!,
            services.GetRequiredService<IConditionFactory>(),services.GetRequiredService<ITargetFactory>(),services.GetRequiredService<TargetSelectionService>(),
            source is { } id && cards.TryGetValue(id, out var sourceCard) ? sourceCard : null,row.GetProperty("sourceBuffId").Deserialize<Guid?>(),row.GetProperty("runtimeId").GetGuid());
        ability.RestoreResumeState(row);
        return ability;
    }

    internal static Buff RestoreBuff(JsonElement row) => new(row.GetProperty("sourceAbilityId").GetGuid(),row.GetProperty("targetCardInstanceId").GetGuid(),
        row.GetProperty("appliedTurn").GetInt32(),row.GetProperty("effects").Deserialize<List<IBuffEffect>>(Json)!,row.GetProperty("definitionId").GetString(),
        row.GetProperty("durationType").Deserialize<DurationType>(Json),row.GetProperty("durationParameter").Deserialize<int?>(),
        row.GetProperty("removalTrigger").Deserialize<ShadowverseBeyond.Server.Domain.Abilities.Triggers.TriggerConfig>(Json),
        row.GetProperty("removalCondition").Deserialize<ConditionConfig>(Json),row.GetProperty("applicabilityCondition").Deserialize<ConditionConfig>(Json)) {
            Id = row.GetProperty("id").GetGuid(),SummaryRootAbilityId = row.GetProperty("summaryRootAbilityId").Deserialize<Guid?>(),
            SourceEffectIndex = row.GetProperty("sourceEffectIndex").Deserialize<int?>(),EffectKey = row.GetProperty("effectKey").GetString(),
            SourceCardInstanceId = row.GetProperty("sourceCardInstanceId").Deserialize<Guid?>(),SourceCardName = row.GetProperty("sourceCardName").GetString(),
            SourceAbilityName = row.GetProperty("sourceAbilityName").GetString(),IsActive = row.GetProperty("isActive").GetBoolean(),
            RemoveAfterUse = row.TryGetProperty("removeAfterUse", out var removeAfterUseProp) && removeAfterUseProp.GetBoolean(),
            IsAuraGenerated = row.TryGetProperty("isAuraGenerated", out var isAuraGeneratedProp) && isAuraGeneratedProp.GetBoolean(),
            PreserveCurrentHealthOnApply = row.GetProperty("preserveCurrentHealthOnApply").GetBoolean()
        };

    public static Dictionary<Guid,Guid> CreateBranchIds(JsonElement execution)
    {
        var players = execution.GetProperty("players").EnumerateObject().Select(p => Guid.Parse(p.Name)).ToHashSet();
        var ids = execution.GetProperty("references").EnumerateObject().Select(p => p.Value.GetGuid())
            .Concat(new[] { "cards","abilities","buffs","history" }.SelectMany(name => execution.GetProperty(name).EnumerateObject().Select(p => Guid.Parse(p.Name))))
            .Concat(execution.GetProperty("auras").EnumerateArray().Select(a => a.GetProperty("auraId").GetGuid()));
        return ids.Where(id => !players.Contains(id)).Distinct().ToDictionary(id => id,_ => Guid.NewGuid());
    }

    public static JsonNode Remap(JsonNode node,IReadOnlyDictionary<Guid,Guid> ids)
    {
        var replacements = ids.Select(p => new { OldD = p.Key.ToString("D"),NewD = p.Value.ToString("D"),OldN = p.Key.ToString("N"),NewN = p.Value.ToString("N") }).ToArray();
        string Text(string value)
        {
            if (Guid.TryParse(value,out var id) && ids.TryGetValue(id,out var replacement)) return replacement.ToString();
            // Derived keys (buff grouping, aura keys and sequence cursors) also embed runtime ids.
            foreach (var pair in replacements)
                value = value.Replace(pair.OldD,pair.NewD,StringComparison.OrdinalIgnoreCase).Replace(pair.OldN,pair.NewN,StringComparison.OrdinalIgnoreCase);
            return value;
        }
        JsonNode? Visit(JsonNode? value) => value switch {
            JsonObject obj => new JsonObject(obj.Select(p => KeyValuePair.Create(Text(p.Key),Visit(p.Value)))),
            JsonArray array => new JsonArray(array.Select(Visit).ToArray()),
            JsonValue scalar when scalar.TryGetValue<string>(out var text) => JsonValue.Create(Text(text)),
            _ => value?.DeepClone()
        };
        return Visit(node)!;
    }

    private static JsonObject CaptureHistory(IGameEvent entry,int order,IReadOnlyDictionary<object,JsonObject> references)
    {
        if (entry is ResumedHistoryEvent restored)
        {
            var row = restored.Capture(order);
            row["parameters"] = new JsonObject(restored.Parameters.Select(p => KeyValuePair.Create(p.Key,CaptureValue(p.Value,references))));
            row["source"] = CaptureValue(entry.Source,references);row["target"] = CaptureValue(entry.Target,references);
            return row;
        }
        var parameters = new JsonObject();
        var aliases = new JsonObject();
        foreach (var field in EventParameterCache.GetDescriptor(entry.GetType()).Parameters)
        {
            try { parameters[field.PropertyName] = CaptureValue(field.Getter(entry),references); }
            catch (JsonException ex) { throw new InvalidDataException($"resume_history_parameter_unserializable: {entry.EventType}.{field.PropertyName}: {ex.Message}",ex); }
            aliases[field.DisplayName] = field.PropertyName;
        }
        return new JsonObject { ["order"] = order,["eventId"] = JsonValue.Create(entry.EventId),["eventType"] = entry.EventType.ToString(),
            ["timestamp"] = JsonValue.Create(entry.Timestamp),["source"] = CaptureValue(entry.Source,references),
            ["target"] = CaptureValue(entry.Target,references),["parameters"] = parameters,["aliases"] = aliases };
    }

    private static JsonNode? CaptureValue(object? value,IReadOnlyDictionary<object,JsonObject> references)
    {
        if (value is null) return null;
        if (references.TryGetValue(value,out var reference)) return reference.DeepClone();
        if (value is CardInstance card) return new JsonObject { ["kind"] = "card",["id"] = JsonValue.Create(card.InstanceId) };
        if (value is PlayerState player) return new JsonObject { ["kind"] = "player",["id"] = JsonValue.Create(player.PlayerId) };
        if (value is Ability ability) return new JsonObject { ["kind"] = "ability",["state"] = JsonNode.Parse(ability.CaptureResumeState().GetRawText()) };
        if (value is Buff buff) return new JsonObject { ["kind"] = "buff",["value"] = JsonSerializer.SerializeToNode(buff,Json) };
        if (value is IGameEvent gameEvent) return new JsonObject { ["kind"] = "event",["value"] = CaptureHistory(gameEvent,0,references) };
        if (value is TargetInfo target) return new JsonObject { ["kind"] = "target",["value"] = CaptureValue((object?)target.Card ?? target.Player,references) };
        if (value is IDictionary dictionary)
        {
            var entries = new JsonObject();
            foreach (DictionaryEntry entry in dictionary) entries[entry.Key.ToString()!] = CaptureValue(entry.Value,references);
            return new JsonObject { ["kind"] = "dictionary",["value"] = entries };
        }
        if (value is IEnumerable sequence && value is not string && value is not JsonNode)
        {
            var enumerable = value.GetType().GetInterfaces().Append(value.GetType()).FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                ?? throw new NotSupportedException("resume_history_collection_has_no_element_type");
            return new JsonObject { ["kind"] = "list",["elementType"] = enumerable.GenericTypeArguments[0].AssemblyQualifiedName,
                ["value"] = new JsonArray(sequence.Cast<object?>().Select(v => CaptureValue(v,references)).ToArray()) };
        }
        return new JsonObject { ["kind"] = "json",["type"] = value.GetType().AssemblyQualifiedName,["value"] = JsonSerializer.SerializeToNode(value,value.GetType(),Json) };
    }

    private static object? RestoreValue(JsonElement value,Dictionary<Guid,CardInstance> cards,Dictionary<Guid,PlayerState> players,IServiceProvider services,Dictionary<Guid,ResumedHistoryEvent> events)
    {
        if (value.ValueKind == JsonValueKind.Null) return null;
        return value.GetProperty("kind").GetString() switch {
            "card" => cards[value.GetProperty("id").GetGuid()],"player" => players[value.GetProperty("id").GetGuid()],
            "zone" => typeof(PlayerState).GetProperty(value.GetProperty("name").GetString()!)!.GetValue(players[value.GetProperty("player").GetGuid()]),
            "fused" => cards[value.GetProperty("id").GetGuid()].FusedCards,
            "ability" => services.GetRequiredService<IAbilityManager>().GetAbilityByRuntimeId(value.GetProperty("state").GetProperty("runtimeId").GetGuid()) ?? RestoreAbility(value.GetProperty("state"),cards,services),
            "buff" => services.GetRequiredService<IBuffManager>().FindBuffInstance(value.GetProperty("value").GetProperty("id").GetGuid()) ?? RestoreBuff(value.GetProperty("value")),
            "event" => RestoreHistory(value.GetProperty("value"),cards,players,services,events),
            "target" => RestoreValue(value.GetProperty("value"),cards,players,services,events) switch { CardInstance c => TargetInfo.ForCard(c),PlayerState p => TargetInfo.ForPlayer(p),_ => throw new InvalidDataException("resume_target_missing") },
            "list" => RestoreList(value,cards,players,services,events),
            "dictionary" => value.GetProperty("value").EnumerateObject().ToDictionary(p => p.Name,p => RestoreValue(p.Value,cards,players,services,events)),
            "json" => value.GetProperty("value").Deserialize(Type.GetType(value.GetProperty("type").GetString()!,throwOnError:true)!,Json),
            _ => throw new InvalidDataException("unsupported_resume_history_value")
        };
    }

    private static Array RestoreList(JsonElement value,Dictionary<Guid,CardInstance> cards,Dictionary<Guid,PlayerState> players,IServiceProvider services,Dictionary<Guid,ResumedHistoryEvent> events)
    {
        var rows = value.GetProperty("value").EnumerateArray().ToArray();
        var array = Array.CreateInstance(Type.GetType(value.GetProperty("elementType").GetString()!,throwOnError:true)!,rows.Length);
        for (var i = 0; i < rows.Length; i++) array.SetValue(RestoreValue(rows[i],cards,players,services,events),i);
        return array;
    }

    private static ResumedHistoryEvent RestoreHistory(JsonElement row,Dictionary<Guid,CardInstance> cards,Dictionary<Guid,PlayerState> players,IServiceProvider services,Dictionary<Guid,ResumedHistoryEvent> events)
    {
        var id = row.GetProperty("eventId").GetGuid();
        if (events.TryGetValue(id,out var existing)) return existing;
        var restored = new ResumedHistoryEvent(row,
            (TargetInfo?)RestoreValue(row.GetProperty("source"),cards,players,services,events),
            ((IEnumerable)RestoreValue(row.GetProperty("target"),cards,players,services,events)!).Cast<TargetInfo>().ToList(),
            row.GetProperty("parameters").EnumerateObject().ToDictionary(p => p.Name,p => RestoreValue(p.Value,cards,players,services,events),StringComparer.OrdinalIgnoreCase));
        events.Add(id,restored);
        return restored;
    }
}

public sealed class ResumedHistoryEvent : GameEventBase
{
    private readonly JsonElement record;
    public IReadOnlyDictionary<string,object?> Parameters { get; }
    public ResumedHistoryEvent(JsonElement row,TargetInfo? source,List<TargetInfo> targets,IReadOnlyDictionary<string,object?> parameters)
        : base(Enum.Parse<EventType>(row.GetProperty("eventType").GetString()!),row.GetProperty("eventId").GetGuid(),row.GetProperty("timestamp").GetDateTime())
    {
        record = row.Clone(); Source = source; Target = targets; Parameters = parameters;
        foreach (var field in EventParameterCache.GetDescriptor(typeof(GameEventBase)).Parameters)
            if (parameters.TryGetValue(field.PropertyName,out var value)) field.Setter!(this,value);
    }

    public bool TryGetParameter(string name,out object? value)
    {
        if (Parameters.TryGetValue(name,out value)) return true;
        foreach (var alias in record.GetProperty("aliases").EnumerateObject())
            if (alias.Name.Equals(name,StringComparison.OrdinalIgnoreCase)) return Parameters.TryGetValue(alias.Value.GetString()!,out value);
        return false;
    }

    internal JsonObject Capture(int order)
    {
        var row = JsonNode.Parse(record.GetRawText())!.AsObject(); row["order"] = order; return row;
    }
}
