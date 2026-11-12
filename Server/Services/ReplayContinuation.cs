using System.Text.Json;
using System.Text.Json.Nodes;
using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Dtos.Replay;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

public sealed record ReplayResumeRequest(int? TurnNo = null,Guid? PlayerId = null,int? AfterStep = null,long? AtSeq = null);

public sealed class ReplayContinuation(SqliteReplayHistory history,IServiceProvider services,IGameService games,IMatchService matches,IMapper mapper)
{
    public async Task<object> PointsAsync(string gameId,CancellationToken cancellationToken)
    {
        var manifest = await history.ManifestAsync(gameId,cancellationToken);
        if (!manifest.Recovery.LoadPlayableMatch) return new { manifest,Points = Array.Empty<object>() };
        var completed = (await history.IndexAsync(gameId,"operation.completed",null,manifest.HeadSeq,cancellationToken))
            .Where(n => n.Stage == "completed" && !(manifest.Status == "closed" && n.Seq == manifest.HeadSeq - 1)).ToArray();
        var turns = (await history.IndexAsync(gameId,"TurnStart",null,manifest.HeadSeq,cancellationToken)).Where(n => n.Stage == "committed").ToArray();
        var initial = await history.RestoreAsync(gameId,0,cancellationToken);
        var initialTurnNo = initial.Document.GetProperty("players").EnumerateObject().Sum(p => p.Value.GetProperty("turnNumber").GetInt32());
        return new { manifest,InitialSeq = 0,InitialTurnNo = initialTurnNo,Operations = completed.Select(n => new { n.ActorPlayerId,n.PlayerOperationNo,n.Origin,n.TurnNo,AtSeq = n.Seq }),
            Turns = turns.Select(n => new { n.TurnNo,n.TurnOwnerId,RequestedSeq = n.Seq,AtSeq = completed.SingleOrDefault(c => c.ActionId == n.ActionId)?.Seq }) };
    }

    public async Task<object> ResumeAsync(string gameId,ReplayResumeRequest request,CancellationToken cancellationToken)
    {
        var manifest = await history.ManifestAsync(gameId,cancellationToken);
        if (!manifest.Recovery.LoadPlayableMatch) throw new NotSupportedException("recording_has_no_execution_checkpoints");
        var selectors = (request.TurnNo != null ? 1 : 0) + (request.AfterStep != null ? 1 : 0) + (request.AtSeq != null ? 1 : 0);
        if (selectors != 1 || request.PlayerId != null && request.AfterStep == null) throw new ArgumentException("choose_turn_or_player_step_or_exact_sequence");
        var completed = await history.IndexAsync(gameId,"operation.completed",null,manifest.HeadSeq,cancellationToken);
        long atSeq;
        if (request.TurnNo is { } turn)
        {
            if (turn < 1) throw new ArgumentException("turn_must_be_positive");
            var initial = await history.RestoreAsync(gameId,0,cancellationToken);
            var initialTurnNo = initial.Document.GetProperty("players").EnumerateObject().Sum(p => p.Value.GetProperty("turnNumber").GetInt32());
            if (initialTurnNo == turn) atSeq = 0;
            else {
            var start = (await history.IndexAsync(gameId,"TurnStart",null,manifest.HeadSeq,cancellationToken)).SingleOrDefault(n => n.Stage == "committed" && n.TurnNo == turn)
                ?? throw new ArgumentException("recorded_turn_not_found");
            atSeq = completed.SingleOrDefault(n => n.ActionId == start.ActionId && n.Stage == "completed")?.Seq
                ?? throw new NotSupportedException("turn_start_action_not_completed");
            }
        }
        else if (request.AfterStep is { } step)
        {
            if (step < 0 || request.PlayerId == null) throw new ArgumentException("player_and_nonnegative_step_required");
            if (request.PlayerId.Value.ToString() != manifest.HostPlayerId && request.PlayerId.Value.ToString() != manifest.GuestPlayerId)
                throw new ArgumentException("recorded_player_not_found");
            atSeq = step == 0 ? 0 : completed.SingleOrDefault(n => n.ActorPlayerId == request.PlayerId && n.PlayerOperationNo == step && n.Stage == "completed")?.Seq
                ?? throw new ArgumentException("completed_player_step_not_found");
        }
        else atSeq = request.AtSeq!.Value;
        if (atSeq != 0 && !completed.Any(n => n.Seq == atSeq && n.Stage == "completed")) throw new NotSupportedException("resume_requires_idle_operation_boundary");
        var restored = await history.RestoreAsync(gameId,atSeq,cancellationToken);
        if (!restored.Document.TryGetProperty("execution",out var execution) || execution.GetProperty("atSeq").GetInt64() != atSeq)
            throw new NotSupportedException("execution_checkpoint_missing_for_position");
        var rules = await history.ExecutionRulesAsync(gameId,cancellationToken) ?? throw new NotSupportedException("execution_rules_not_recorded");
        ShadowverseBeyond.Server.Dtos.Enums.GameMode? expectedMode = null;
        if (execution.TryGetProperty("mode", out var execModeProp))
        {
            expectedMode = execModeProp.ValueKind == JsonValueKind.Number
                ? (ShadowverseBeyond.Server.Dtos.Enums.GameMode)execModeProp.GetInt32()
                : Enum.TryParse<ShadowverseBeyond.Server.Dtos.Enums.GameMode>(execModeProp.GetString(), true, out var parsedMode)
                    ? parsedMode
                    : null;
        }
        await ReplayExecutionRules.ValidateAsync(rules,services,cancellationToken,expectedMode);
        var ids = ReplayExecutionState.CreateBranchIds(execution);
        var branchState = JsonSerializer.SerializeToElement(ReplayExecutionState.Remap(JsonNode.Parse(execution.GetRawText())!,ids));
        var newId = Guid.NewGuid().ToString();
        var origin = new ReplayBranchOrigin(gameId,atSeq);
        GameState game;
        try {
        game = await ReplayExecutionState.RestoreAsync(branchState,newId,origin,services);
        using (var context = new GameContext(services,mapper,services.GetRequiredService<IGameContextAccessor>(),game,game.CurrentPlayerId!.Value))
        {
            var expected = ReplayStateChanges.View(JsonNode.Parse(restored.Document.GetRawText())!.AsObject());
            expected = ReplayExecutionState.Remap(expected,ids).AsObject();
            expected["gameId"] = newId;expected["turnStartedAtUtc"] = null;expected["turnEndsAtUtc"] = null;
            var actual = JsonSerializer.SerializeToNode(GameStateProjection.Create(game,context),SqliteReplayFrames.Json);
            if (!JsonNode.DeepEquals(expected,actual)) throw new InvalidDataException("resumed_runtime_view_disagrees_with_recorded_position: " +
                string.Join(", ",ReplayStateChanges.Between(expected,actual!.AsObject()).Take(8).Select(c => string.Join('/',c.Path) + "=" + c.Value.GetRawText())));
        }
        }
        catch {
            await services.GetRequiredService<IEventHistoryService>().ClearHistoryForGameAsync(newId);
            throw;
        }
        var room = ((MatchService)matches).CreateResumedRoom(game);
        await ((GameService)games).InstallResumedGameAsync(game);
        return new { Room = room,Origin = origin,GameId = newId,SampleKind = "resumed",BusinessPolicy = "no_settlement_rewards_or_statistics",
            TimerPolicy = "fresh_turn_timeout",RandomPolicy = "new_random_future_historical_outcomes_preserved" };
    }
}
