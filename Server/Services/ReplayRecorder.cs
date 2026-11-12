using System.Collections;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Dtos.Replay;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Hubs;
using ShadowverseBeyond.Server.Hubs.ClientInterfaces;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Options;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

public sealed class ReplayRecorder(IServiceScopeFactory scopes, IOptions<ReplayOptions> options,
    IMapper mapper, ILogger<ReplayRecorder> logger, IHubContext<GameHub> hub)
{
    private const int CheckpointInterval = 64;
    private sealed class Recording
    {
        public long Seq;
        public long DurableSeq;
        public long LastCheckpoint;
        public long LastRevision;
        public int TurnNo;
        public bool Closed;
        public string? Failure;
        public string SampleKind = "standard";
        public JsonObject Document = new();
        public JsonElement? ExecutionRules;
        public Dictionary<Guid,int> PlayerSteps { get; } = new();
        public Dictionary<Guid,Guid> Parents { get; } = new();
        public Dictionary<Guid,JsonElement> Choices { get; } = new();
        public List<ReplayHistoryNode> Nodes { get; } = [];
        public List<ReplayHistoryCheckpoint> Checkpoints { get; } = [];
        public List<Task> Observations { get; } = [];
    }
    public sealed class ActionCapture : IDisposable
    {
        internal ActionCapture(ReplayRecorder owner, string gameId, PlayerActionWrapper action, int? step)
        { Owner = owner; GameId = gameId; Action = action; PlayerStep = step; Previous = Current.Value; Current.Value = this; }
        internal ReplayRecorder Owner { get; }
        public string GameId { get; }
        public PlayerActionWrapper Action { get; }
        public int? PlayerStep { get; }
        private ActionCapture? Previous { get; }
        public void Dispose() => Current.Value = Previous;
    }
    private readonly ConcurrentDictionary<string,Recording> recordings = new();
    private readonly ConcurrentDictionary<Guid,ActionCapture> waitingActions = new();
    private static readonly AsyncLocal<ActionCapture?> Current = new();
    private static readonly IReadOnlyDictionary<Type,string> Methods = typeof(IGameClient).GetMethods()
        .Where(m => m.GetParameters().Length == 1 && typeof(GameEventDto).IsAssignableFrom(m.GetParameters()[0].ParameterType))
        .ToDictionary(m => m.GetParameters()[0].ParameterType,m => m.Name);

    public ActionCapture? Begin(GameState game, PlayerActionWrapper action)
    {
        if (!recordings.TryGetValue(game.GameId,out var recording) || recording.Closed || recording.Failure != null
            || action is MatchStateRead || action.ActionType == "CloseReplay") return null;
        if (action.ActionType == "DebugCommand") recording.SampleKind = "debug";
        int? step = null;
        if (action.Origin == "player" && action.ActionType != "DebugCommand")
        { step = recording.PlayerSteps.GetValueOrDefault(action.PlayerId) + 1; recording.PlayerSteps[action.PlayerId] = step.Value; }
        return new ActionCapture(this,game.GameId,action,step);
    }

    public async Task StartAsync(GameState game)
    {
        if (!options.Value.EnableJournalWrites || game.IsMirageTournamentSeat || game.Player1.IsAI || game.Player2.IsAI) return;
        var recording = new Recording { SampleKind = game.ReplayBranch != null ? "resumed" : "standard",TurnNo = game.Player1.TurnNumber + game.Player2.TurnNumber };
        if (!recordings.TryAdd(game.GameId,recording)) throw new InvalidOperationException("duplicate_replay_start");
        try
        {
            recording.Document = Capture(game,recording);
            using (var scope = scopes.CreateScope())
            {
                var execution = ReplayExecutionState.Capture(game,scope.ServiceProvider);
                execution["atSeq"] = 0;
                recording.Document["execution"] = execution;
                recording.ExecutionRules = await ReplayExecutionRules.CaptureAsync(scope.ServiceProvider,CancellationToken.None,game.Mode);
            }
            recording.Checkpoints.Add(new ReplayHistoryCheckpoint(3,game.GameId,0,JsonSerializer.SerializeToElement(recording.Document,SqliteReplayFrames.Json)));
            await FlushAsync(game,recording);
        }
        catch (Exception ex) { Fault(game.GameId,recording,ex); }
    }

    public Task RequestedAsync(GameState game, ActionCapture? action)
    {
        if (action != null) Record(game,"operation","operation.requested","accepted",null,null,
            new { actionType = action.Action.ActionType, actionData = action.Action.ActionData,
                action.Action.CommandId, action.Action.ExpectedStateRevision, action.Action.RequestId, action.Action.Timestamp });
        return Task.CompletedTask;
    }

    public static void Event(GameContext context, IGameEvent gameEvent, string stage, IReadOnlyList<IGameEvent>? generated = null)
    {
        if (SandboxExecutionGuard.IsSuspended || context?.CurrentGameState?.IsCombatSandbox == true) return;
        if (Current.Value is not { } capture) return;
        var owner = capture.Owner;
        if (!owner.recordings.TryGetValue(capture.GameId,out var recording)) return;
        foreach (var child in generated ?? []) recording.Parents[child.EventId] = gameEvent.EventId;
        if (stage == "committed" && gameEvent.EventType == EventType.TurnStart) recording.TurnNo++;
        try
        {
            var parameters = new Dictionary<string,object?>();
            var runtimeOnly = new List<string>();
            foreach (var field in EventParameterCache.GetDescriptor(gameEvent.GetType()).Parameters)
                parameters[field.PropertyName] = Encode(field.Getter(gameEvent),field.PropertyName,runtimeOnly);
            owner.Record(context.CurrentGameState,"domain",gameEvent.EventType.ToString(),stage,gameEvent.EventId,
                recording.Parents.TryGetValue(gameEvent.EventId,out var parent) ? parent : context.CurrentEffect?.Id,
                new { parameters, generatedEventIds = generated?.Select(e => e.EventId).ToArray() ?? [], runtimeOnlyFields = runtimeOnly });
        }
        catch (Exception ex) { owner.Fault(capture.GameId,recording,ex); }
    }

    public static void EffectApplied(GameContext context, Guid effectId, string type, bool success, string? error, IReadOnlyList<IGameEvent> generated)
    {
        if (SandboxExecutionGuard.IsSuspended || context?.CurrentGameState?.IsCombatSandbox == true) return;
        if (Current.Value is not { } capture) return;
        if (!capture.Owner.recordings.TryGetValue(capture.GameId,out var recording)) return;
        foreach (var child in generated) recording.Parents[child.EventId] = effectId;
        capture.Owner.Record(context.CurrentGameState,"effect","effect.applied",success ? "applied" : "partial_or_failed",effectId,
            context.CurrentEffect?.TriggeredEventId, new { effectType = type, success, error, generatedEventIds = generated.Select(e => e.EventId).ToArray() });
    }

    public static void Runtime(GameContext context,string reason)
    {
        if (SandboxExecutionGuard.IsSuspended || context?.CurrentGameState?.IsCombatSandbox == true) return;
        if (Current.Value is { } capture)
            capture.Owner.Record(context.CurrentGameState,"runtime","runtime.updated",reason,null,null,new { reason });
    }

    public static void Message(GameEventDto dto,string audience)
    {
        if (SandboxExecutionGuard.IsSuspended) return;
        if (Current.Value is not { } capture || !capture.Owner.recordings.TryGetValue(capture.GameId,out var recording) || recording.Failure != null) return;
        try
        {
            var method = dto is ErrorOccurredEventDto ? "ReceiveError" : Methods.GetValueOrDefault(dto.GetType()) ?? "ReceiveGameEvent";
            var payload = dto is ErrorOccurredEventDto e ? JsonSerializer.SerializeToNode(e.ErrorMessage) :
                JsonSerializer.SerializeToNode(dto,dto.GetType(),SqliteReplayFrames.Json);
            IReadOnlyList<ReplayStateChange> stateOverrides = [];
            long? stateAtSeq = null;
            if (payload is JsonObject body && body["gameState"] is JsonObject state)
            {
                stateAtSeq = recording.Seq;
                stateOverrides = ReplayStateChanges.Between(ReplayStateChanges.View(recording.Document),state);
                body.Remove("gameState");
            }
            capture.Owner.Marker(recording,capture.GameId,"presentation","message","emitted",null,null,
                new { method, audience, payload, stateAtSeq, stateOverrides });
        }
        catch (Exception ex) { capture.Owner.Fault(capture.GameId,recording,ex); }
    }

    public async Task WaitingAsync(GameState game,JsonElement choice)
    {
        if (recordings.TryGetValue(game.GameId,out var recording) && Current.Value is { } capture)
        {
            var id = choice.GetProperty("pendingChoiceId").GetGuid();
            recording.Choices[id] = choice;
            waitingActions[id] = capture;
            Record(game,"choice","choice.requested","waiting",id,null,new { request = choice });
            await FlushAsync(game,recording);
        }
        using var scope = scopes.CreateScope();
        using var context = Context(scope.ServiceProvider,game);
        await MatchCommandObservation.PublishBoundaryAsync(game,context,hub,"waiting_for_choice",Current.Value?.Action.ActionId,choice);
    }

    public void ChoiceResolved(Guid choiceId,string status,IReadOnlyList<Guid> targets)
    {
        if (!waitingActions.TryRemove(choiceId,out var capture) || !recordings.TryGetValue(capture.GameId,out var recording)) return;
        var previous = Current.Value;
        Current.Value = capture;
        try
        {
            recording.Choices.Remove(choiceId);
            using var scope = scopes.CreateScope();
            var game = scope.ServiceProvider.GetRequiredService<IGameService>().GetGameState(capture.GameId)
                ?? throw new InvalidOperationException("choice_match_released_before_resolution");
            Record(game,"choice","choice.resolved",status,choiceId,null,new { choiceId, status, chosenTargetIds = targets });
        }
        catch (Exception ex) { Fault(capture.GameId,recording,ex); }
        finally { Current.Value = previous; }
    }

    public async Task FinishAsync(GameState game,ActionCapture? capture)
    {
        if (capture is null)
        {
            if (game.Player1.IsAI || game.Player2.IsAI) MatchCommandObservation.Persistence("recording","not_recorded_by_policy");
            else if (!options.Value.EnableJournalWrites) MatchCommandObservation.Persistence("recording","disabled");
            else if (recordings.TryGetValue(game.GameId,out var failed) && failed.Failure != null)
                MatchCommandObservation.Persistence("recording",new { Status = "failed", Error = failed.Failure });
            return;
        }
        if (!recordings.TryGetValue(game.GameId,out var recording)) return;
        var completion = await capture.Action.Completion;
        Record(game,"runtime","runtime.finalized","settled",null,null,new { reason = "queue_boundary_and_timer_finalization" });
        try
        {
            using var scope = scopes.CreateScope();
            var execution = ReplayExecutionState.Capture(game,scope.ServiceProvider);
            execution["atSeq"] = recording.Seq + 1;
            var changes = ReplayStateChanges.Between(recording.Document["execution"]!.AsObject(),execution)
                .Select(c => c with { Path = new[] { "execution" }.Concat(c.Path).ToArray() }).ToArray();
            recording.Document["execution"] = execution;
            Add(recording,game.GameId,"operation","operation.completed",completion.Status.ToString().ToLowerInvariant(),null,null,
                new { status = completion.Status.ToString().ToLowerInvariant(), completion.Reason },changes,game.StateRevision);
        }
        catch (Exception ex) { Fault(game.GameId,recording,ex); }
        if (game.IsGameOver)
            Marker(recording,game.GameId,"lifecycle","recording.closed","closed",null,null,new { winnerId = game.WinnerId, reason = game.GameMessage });
        await FlushAsync(game,recording);
        recording.Closed = game.IsGameOver;
    }

    public async Task CloseAsync(GameState game)
    {
        if (recordings.TryGetValue(game.GameId,out var recording) && !recording.Closed)
        {
            Record(game,"lifecycle","recording.interrupted","interrupted",null,null,new { reason = "room_released", winnerId = game.WinnerId });
            await FlushAsync(game,recording);
        }
        Release(game.GameId);
    }
    public bool IsOpen(string id) => recordings.TryGetValue(id,out var recording) && !recording.Closed;
    public void Release(string id)
    {
        recordings.TryRemove(id,out _);
        foreach (var item in waitingActions.Where(p => p.Value.GameId == id)) waitingActions.TryRemove(item.Key,out _);
    }

    private GameContext Context(IServiceProvider provider,GameState game) => new(provider,mapper,
        provider.GetRequiredService<IGameContextAccessor>(),game,game.CurrentPlayerId ?? game.Player1.PlayerId);
    private JsonObject Capture(GameState game,Recording recording)
    {
        using var scope = scopes.CreateScope();
        using var context = Context(scope.ServiceProvider,game);
        return ReplayStateChanges.Capture(context,recording.Choices.Values);
    }
    private void Record(GameState game,string kind,string type,string stage,Guid? eventId,Guid? parent,object data)
    {
        if (!recordings.TryGetValue(game.GameId,out var recording) || recording.Failure != null || recording.Closed) return;
        try
        {
            var next = Capture(game,recording);
            var execution = recording.Document["execution"];
            recording.Document.Remove("execution");
            var changes = ReplayStateChanges.Between(recording.Document,next);
            next["execution"] = execution;
            recording.Document = next;
            Add(recording,game.GameId,kind,type,stage,eventId,parent,data,changes,game.StateRevision);
        }
        catch (Exception ex) { Fault(game.GameId,recording,ex); }
    }
    private void Marker(Recording recording,string id,string kind,string type,string stage,Guid? eventId,Guid? parent,object data)
    {
        if (recording.Failure != null) return;
        try { Add(recording,id,kind,type,stage,eventId,parent,data,[],recording.LastRevision); }
        catch (Exception ex) { Fault(id,recording,ex); }
    }
    private void Add(Recording recording,string id,string kind,string type,string stage,Guid? eventId,Guid? parent,object data,IReadOnlyList<ReplayStateChange> changes,long revision)
    {
        var action = Current.Value;
        var player = recording.Document["game"]!["currentPlayerId"]!.GetValue<string>();
        var node = new ReplayHistoryNode(3,id,++recording.Seq,recording.Seq-1,Guid.NewGuid(),eventId,parent,action?.Action.ActionId,
            action?.Action.PlayerId,action?.Action.ActionType == "DebugCommand" ? "debug" : action?.Action.Origin ?? "system",action?.PlayerStep,
            recording.TurnNo,Guid.TryParse(player,out var owner) ? owner : null,kind,type,stage,DateTime.UtcNow,revision,
            recording.SampleKind,JsonSerializer.SerializeToElement(data,SqliteReplayFrames.Json),changes);
        recording.Nodes.Add(node);
        recording.LastRevision = revision;
        if (recording.Seq-recording.LastCheckpoint >= CheckpointInterval)
        {
            recording.Checkpoints.Add(new ReplayHistoryCheckpoint(3,id,recording.Seq,JsonSerializer.SerializeToElement(recording.Document,SqliteReplayFrames.Json)));
            recording.LastCheckpoint = recording.Seq;
        }
        if (MatchCommandObservation.HasEventSubscribers(id))
        {
            using var scope = scopes.CreateScope();
            var game = scope.ServiceProvider.GetRequiredService<IGameService>().GetGameState(id)
                ?? throw new InvalidOperationException("event_observation_match_missing");
            using var context = Context(scope.ServiceProvider,game);
            recording.Observations.Add(MatchCommandObservation.PublishEventStateAsync(game,context,hub,node,recording.Choices.Values.ToArray()));
        }
    }
    private async Task FlushAsync(GameState game,Recording recording)
    {
        if (recording.Failure != null) { MatchCommandObservation.Persistence("recording",new { Status = "failed", Error = recording.Failure }); return; }
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<SqliteReplayHistory>().AppendAsync(game.GameId,game.Player1.PlayerId,game.Player2.PlayerId,
                recording.DurableSeq,recording.Nodes.ToArray(),recording.Checkpoints.ToArray(),CancellationToken.None,recording.ExecutionRules);
            recording.DurableSeq = recording.Seq;
            recording.Nodes.Clear(); recording.Checkpoints.Clear();
            recording.ExecutionRules = null;
            await Task.WhenAll(recording.Observations); recording.Observations.Clear();
            MatchCommandObservation.Persistence("recording",new { Status = "saved", FormatVersion = 3, HeadSeq = recording.DurableSeq });
        }
        catch (Exception ex) { Fault(game.GameId,recording,ex); }
    }
    private void Fault(string id,Recording recording,Exception error)
    {
        recording.Failure = error.Message;
        logger.LogError(error,"Event history recording failed for {GameId}; no complete history will be claimed.",id);
        MatchCommandObservation.Persistence("recording",new { Status = "failed", Error = error.Message });
    }
    private static object? Encode(object? value,string path,List<string> runtimeOnly)
    {
        if (value is null || value is string || value is bool || value is int || value is long || value is double || value is float || value is decimal || value is DateTime) return value;
        if (value is Enum) return value.ToString();
        if (value is Guid guid) return guid;
        if (value is CardInstance card) return new { card.InstanceId, card.OwnerPlayerId };
        if (value is PlayerState player) return new { player.PlayerId };
        if (value is CardInstanceSnapshot || value is CardDefinitionSnapshot || value is ShadowverseBeyond.Server.Domain.Abilities.AbilitySnapshot)
            return JsonSerializer.SerializeToElement(value,value.GetType(),SqliteReplayFrames.Json);
        if (value is ShadowverseBeyond.Server.Domain.Buffs.Buff buff) return new { buff.Id, buff.DefinitionId, buff.TargetCardInstanceId,
            buff.SourceAbilityId, buff.DurationType, buff.DurationParameter, StatModifications = buff.GetStatModifications().ToArray(), GrantedKeywords = buff.GetGrantedKeywords().ToArray() };
        if (value is IDictionary dictionary)
        {
            var encoded = new Dictionary<string,object?>();
            foreach (DictionaryEntry entry in dictionary) encoded[entry.Key.ToString()!] = Encode(entry.Value,path+"."+entry.Key,runtimeOnly);
            return encoded;
        }
        if (value is IEnumerable sequence) return sequence.Cast<object?>().Select((item,i) => Encode(item,path+"["+i+"]",runtimeOnly)).ToArray();
        runtimeOnly.Add(path+":"+value.GetType().Name);
        return new { runtimeType = value.GetType().Name, status = "runtime_only_not_required_by_readonly_state_reducer" };
    }
}
