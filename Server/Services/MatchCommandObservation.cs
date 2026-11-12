using System.Collections;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Hubs;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Dtos.Replay;

namespace ShadowverseBeyond.Server.Services;

/// <summary>Only explicitly observed matches retain tool evidence; no second mutable game state is owned here.</summary>
public static class MatchCommandObservation
{
    private sealed class Subscription
    {
        public ConcurrentDictionary<string, byte> Connections { get; } = new();
        public ConcurrentDictionary<string, byte> EventConnections { get; } = new();
        public string SampleKind { get; set; } = "unknown";
        public PlayerActionWrapper? ActiveAction;
        public CompletedFrame? LastSnapshot;
    }

    private sealed record CompletedFrame(JsonElement Snapshot);

    public sealed class ActionScope : IDisposable
    {
        internal ActionScope(string gameId, PlayerActionWrapper action)
        {
            GameId = gameId;
            Action = action;
            Previous = Current.Value;
            Current.Value = this;
        }
        private ActionScope? Previous { get; }
        public string GameId { get; }
        public PlayerActionWrapper Action { get; }
        public List<object> Events { get; } = [];
        public Dictionary<string, object?> Persistence { get; } = new()
        {
            ["recording"] = "unknown", ["settlement"] = "not_applicable", ["statistics"] = "unknown"
        };
        public void Dispose() => Current.Value = Previous;
    }

    private static readonly ConcurrentDictionary<string, Subscription> Subscriptions = new();
    private static readonly ConcurrentDictionary<string, byte> DebugMatches = new();
    private static readonly AsyncLocal<ActionScope?> Current = new();
    public static Guid? CurrentActionId => Current.Value?.Action.ActionId;
    public static string? CurrentCommandId => Current.Value?.Action.CommandId;
    public static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static object Subscribe(GameState game, string connectionId)
    {
        var subscription = Subscriptions.GetOrAdd(game.GameId, _ => new Subscription
        {
            SampleKind = game.ReplayBranch != null ? "resumed" : DebugMatches.ContainsKey(game.GameId) ? "debug" :
                game.StateRevision == 0 && !game.Player1.HasCompletedMulligan && !game.Player2.HasCompletedMulligan
                    ? "standard" : "unknown"
        });
        subscription.Connections[connectionId] = 0;
        return new { game.GameId, subscription.SampleKind, game.StateRevision };
    }

    public static void Unsubscribe(string connectionId)
    {
        foreach (var pair in Subscriptions)
        {
            pair.Value.Connections.TryRemove(connectionId, out _);
            pair.Value.EventConnections.TryRemove(connectionId, out _);
            if (pair.Value.Connections.IsEmpty) Subscriptions.TryRemove(pair.Key, out _);
        }
    }

    public static void Release(string gameId)
    {
        Subscriptions.TryRemove(gameId, out _);
        DebugMatches.TryRemove(gameId, out _);
    }

    public static object SubscribeEvents(GameState game, string connectionId)
    {
        Subscribe(game, connectionId);
        Subscriptions[game.GameId].EventConnections[connectionId] = 0;
        return new { game.GameId, Status = "observing_event_states" };
    }

    public static void UnsubscribeEvents(string gameId, string connectionId)
    {
        if (Subscriptions.TryGetValue(gameId, out var subscription)) subscription.EventConnections.TryRemove(connectionId, out _);
    }

    public static bool HasEventSubscribers(string gameId) => Subscriptions.TryGetValue(gameId, out var subscription) && !subscription.EventConnections.IsEmpty;

    public static Task PublishEventStateAsync(GameState game, GameContext context, IHubContext<GameHub> hub, ReplayHistoryNode node, IReadOnlyList<JsonElement> choices)
    {
        if (!Subscriptions.TryGetValue(game.GameId, out var subscription) || subscription.EventConnections.IsEmpty) return Task.CompletedTask;
        var view = JsonSerializer.SerializeToElement(GameStateProjection.Create(game, context), Json);
        return DeliverEventStateAsync(subscription.EventConnections.Keys.ToArray(), hub,
            new { game.GameId, node.Seq, node.ActionId, node.EventId, node.EventType, node.Kind, node.Stage, View = view, PendingChoices = choices });
    }

    private static async Task DeliverEventStateAsync(string[] connections, IHubContext<GameHub> hub, object payload)
    {
        try { await Task.WhenAll(connections.Select(id => hub.Clients.Client(id).SendAsync("AgentEventState", payload))); }
        catch (Exception ex) { Persistence("observation", new { Status = "failed", Error = ex.Message }); }
    }

    public static void Admitted(string gameId, PlayerActionWrapper action)
    {
        if (action.ActionType == "DebugCommand")
        {
            DebugMatches[gameId] = 0;
            if (Subscriptions.TryGetValue(gameId, out var subscription)) subscription.SampleKind = "debug";
        }
    }

    public static ActionScope? Begin(string gameId, PlayerActionWrapper action)
    {
        if (action is MatchStateRead || action.ActionType == "CloseReplay") return null;
        if (!Subscriptions.TryGetValue(gameId, out var subscription)) return null;
        Volatile.Write(ref subscription.ActiveAction, action);
        return new ActionScope(gameId, action);
    }

    public static string SampleKind(string gameId) => Subscriptions.TryGetValue(gameId, out var subscription)
        ? subscription.SampleKind : DebugMatches.ContainsKey(gameId) ? "debug" : "unknown";

    public static Guid? ActiveAction(string gameId) => Subscriptions.TryGetValue(gameId, out var subscription)
        ? Volatile.Read(ref subscription.ActiveAction)?.ActionId : null;

    public static JsonElement? LastSnapshot(string gameId) => Subscriptions.TryGetValue(gameId, out var subscription) ? Volatile.Read(ref subscription.LastSnapshot)?.Snapshot : null;

    public static void RememberSnapshot(string gameId, JsonElement snapshot)
    {
        if (Subscriptions.TryGetValue(gameId, out var subscription)) Volatile.Write(ref subscription.LastSnapshot, new CompletedFrame(snapshot));
    }

    public static void Persistence(string stage, object? outcome)
    {
        if (SandboxExecutionGuard.IsSuspended) return;
        if (Current.Value is { } scope) scope.Persistence[stage] = outcome;
    }

    public static async Task PublishBoundaryAsync(GameState game, GameContext context, IHubContext<GameHub> hub, string boundary, Guid? actionId, JsonElement choice)
    {
        if (SandboxExecutionGuard.IsSuspended || game.IsCombatSandbox) return;
        if (!Subscriptions.TryGetValue(game.GameId, out var subscription)) return;
        try
        {
            var snapshot = JsonSerializer.SerializeToElement(Snapshot(game, context), Json);
            RememberSnapshot(game.GameId, snapshot);
            foreach (var connection in subscription.Connections.Keys)
                await hub.Clients.Client(connection).SendAsync("AgentHistoryBoundary", new { game.GameId, ActionId = actionId, BoundaryKind = boundary,
                    ChoiceId = choice.GetProperty("pendingChoiceId").GetGuid(), PendingChoices = new[] { choice }, Snapshot = snapshot });
        }
        catch (Exception ex)
        {
            Persistence("observation", new { Status = "failed", Error = ex.Message });
            System.Diagnostics.Trace.TraceError("Agent choice observation failed: {0}", ex.Message);
        }
    }

    public static void Trace(GameContext context, IGameEvent gameEvent, string stage, IReadOnlyList<IGameEvent>? generated = null)
    {
        if (SandboxExecutionGuard.IsSuspended || context?.CurrentGameState?.IsCombatSandbox == true) return;
        if (Current.Value is not { } scope) return;
        var missing = new List<string>();
        object? Encode(object? value, string path)
        {
            if (value is null || value is string || value is bool || value is decimal || value is DateTime ||
                value is int || value is long || value is float || value is double) return value;
            if (value is Enum) return value.ToString();
            if (value is Guid id) return id;
            if (value is ShadowverseBeyond.Server.Domain.Buffs.Buff buff)
                return new { buff.Id, buff.TargetCardInstanceId, buff.SourceAbilityId, buff.DefinitionId,
                    buff.DurationType, buff.DurationParameter, StatModifications = buff.GetStatModifications().ToArray(),
                    GrantedKeywords = buff.GetGrantedKeywords().ToArray() };
            if (value is CardInstance card) return new { card.InstanceId, CardRef = context.CurrentGameState.InGameIds.Format(InGameIdKind.Card, card.InstanceId) };
            if (value is PlayerState player) return new { player.PlayerId, PlayerRef = context.CurrentGameState.InGameIds.Format(InGameIdKind.Player, player.PlayerId) };
            if (value is CardInstanceSnapshot || value is CardDefinitionSnapshot) return JsonSerializer.SerializeToElement(value, value.GetType(), Json);
            if (value is IDictionary dictionary)
            {
                var encoded = new Dictionary<string, object?>();
                foreach (DictionaryEntry item in dictionary) encoded[item.Key.ToString()!] = Encode(item.Value, path + "." + item.Key);
                return encoded;
            }
            if (value is IEnumerable enumerable) return enumerable.Cast<object?>().Select((item, i) => Encode(item, path + "[" + i + "]")).ToArray();
            missing.Add(path + ":" + value.GetType().Name);
            return null;
        }
        var parameters = new Dictionary<string, object?>();
        foreach (var parameter in EventParameterCache.GetDescriptor(gameEvent.GetType()).Parameters)
        {
            try { parameters[parameter.PropertyName] = Encode(parameter.Getter(gameEvent), parameter.PropertyName); }
            catch (Exception ex) { missing.Add(parameter.PropertyName + ": " + ex.Message); }
        }
        scope.Events.Add(new
        {
            scope.GameId, scope.Action.ActionId, scope.Action.CommandId, gameEvent.EventId,
            EventType = gameEvent.EventType.ToString(), Stage = stage, Sequence = scope.Events.Count + 1,
            context.CurrentGameState.StateRevision, Parameters = parameters,
            GeneratedEventIds = generated?.Select(e => e.EventId).ToArray() ?? [],
            ParentEventId = (Guid?)null, CausalRelation = "Only GeneratedEventIds are confirmed; adjacency is not causality.",
            UnrepresentedFields = missing
        });
    }

    public static object Snapshot(GameState game, GameContext context) => new
    {
        game.GameId, game.StateRevision, game.ReplayBranch, SampleKind = SampleKind(game.GameId),
        MatchKind = game.Player1.IsAI || game.Player2.IsAI ? "vs-ai" : "pvp",
        ActiveActionId = (Guid?)null,
        State = JsonSerializer.SerializeToElement(GameStateProjection.Create(game, context), Json),
        Players = new[] { game.Player1, game.Player2 }.Select((p, i) => new
        {
            Seat = i == 0 ? "A" : "B", p.PlayerId, PlayerRef = game.InGameIds.Format(InGameIdKind.Player, p.PlayerId), p.IsAI
        }).ToArray(),
        References = game.GetRegisteredCardInstances().Select(c => new
        {
            c.InstanceId, CardRef = game.InGameIds.Format(InGameIdKind.Card, c.InstanceId), c.Name, c.CurrentZone,
            Abilities = c.Abilities.Select(a => new { a.RuntimeId, a.Name }).ToArray()
        }).ToArray()
    };

    public static async Task FinishAsync(ActionScope? scope, GameState? game, IServiceScopeFactory scopeFactory,
        IMapper mapper, IHubContext<GameHub> hub)
    {
        if (scope is null || !Subscriptions.TryGetValue(scope.GameId, out var subscription)) return;
        Volatile.Write(ref subscription.ActiveAction, null);
        object? snapshot = null;
        string? observationError = null;
        if (game != null)
        {
            try
            {
                using var services = scopeFactory.CreateScope();
                using var context = new GameContext(services.ServiceProvider, mapper,
                    services.ServiceProvider.GetRequiredService<Abstractions.IGameContextAccessor>(), game, scope.Action.PlayerId);
                snapshot = Snapshot(game, context);
                RememberSnapshot(game.GameId, JsonSerializer.SerializeToElement(snapshot, Json));
                if (game.IsGameOver)
                {
                    var db = services.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var participants = new[] { game.Player1.PlayerId, game.Player2.PlayerId };
                    var expectedSettlements = game.ReplayBranch != null ? 0 : await db.Users.AsNoTracking().CountAsync(user => participants.Contains(user.Id));
                    var settlements = await db.GameSettlements.AsNoTracking().CountAsync(row => row.GameId == game.GameId);
                    var statistics = await db.GameParticipantStatsFacts.AsNoTracking().CountAsync(row => row.GameId == game.GameId);
                    scope.Persistence["databaseEvidence"] = new { ExpectedSettlements = expectedSettlements, SettlementRows = settlements, StatisticsRows = statistics };
                    if (scope.Persistence["settlement"] is string settlementStatus && settlementStatus == "unknown" && expectedSettlements > 0 && settlements == expectedSettlements)
                        scope.Persistence["settlement"] = new { Status = "confirmed", Rows = settlements };
                    if (scope.Persistence["statistics"] is string statisticsStatus && statisticsStatus == "unknown" && statistics == participants.Length)
                        scope.Persistence["statistics"] = new { Status = "confirmed", Rows = statistics };
                }
            }
            catch (Exception ex) { observationError = ex.Message; }
        }
        var completion = scope.Action.Completion.IsCompletedSuccessfully ? scope.Action.Completion.Result : null;
        var receipt = new
        {
            scope.GameId, scope.Action.ActionId, scope.Action.CommandId, scope.Action.PlayerId, scope.Action.ActionType,
            Status = completion?.Status.ToString().ToLowerInvariant() ?? "outcome_unknown", Reason = completion?.Reason,
            Code = scope.Action.RejectionCode ?? (completion?.Status == PlayerActionCompletionStatus.Rejected ? "rule_rejected" : null),
            BoundaryKind = completion?.Status == PlayerActionCompletionStatus.Completed ? "completed_action" : "partial_action",
            Snapshot = snapshot, ObservationError = observationError, scope.Persistence, scope.Events
        };
        foreach (var connection in subscription.Connections.Keys)
        {
            try { await hub.Clients.Client(connection).SendAsync("AgentActionCompleted", receipt); }
            catch (Exception ex) { System.Diagnostics.Trace.TraceError("Agent receipt delivery failed: {0}", ex.Message); }
        }
    }
}
