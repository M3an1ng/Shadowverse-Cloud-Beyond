using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Mappings;

namespace ShadowverseBeyond.Server.Hubs;

public sealed record AgentActionRequest(string Id, string Command, JsonElement Payload, long? ExpectedStateRevision, string? RequestId = null);
public sealed record AgentActionAdmission(string? Id, Guid? ActionId, string Status, string? Reason, string? Code, string? OriginalCommandId = null);
public sealed record MirageBuyOfferActionDto(Guid OfferId);
public sealed record MirageSellCardActionDto(Guid CardUid);
public sealed record MirageReorderFieldActionDto(List<Guid> OrderedCardUids);
public sealed record MirageRedeemTicketActionDto(Guid TicketId, int ChosenCardDefinitionId);

public partial class GameHub
{
    private AgentActionRequest? _agentRequest;
    private PlayerActionWrapper? _submittedAction;
    private Guid? _duplicateActionId;
    private static readonly JsonSerializerOptions AgentJson = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };

    private bool IsSpectatorOrReadOnlyContext()
    {
        if (Context.Items.TryGetValue("IsReadOnlySpectator", out var ro) && ro is true)
        {
            return true;
        }
        return Context.Items.TryGetValue("ParticipantType", out var kind) && kind is ChatSenderType.Spectator;
    }

    public object SubscribeActionReceipts()
    {
        if (IsSpectatorOrReadOnlyContext())
        {
            throw new HubException("forbidden: spectators cannot subscribe to action receipts.");
        }
        var gameId = GetGameIdForReplay() ?? throw new HubException("missing_match: no active match.");
        var game = _gameService.GetGameState(gameId) ?? throw new HubException("missing_match: game state is unavailable.");
        return MatchCommandObservation.Subscribe(game, Context.ConnectionId);
    }

    public void UnsubscribeActionReceipts() => MatchCommandObservation.Unsubscribe(Context.ConnectionId);

    public object SubscribeEventStateReceipts()
    {
        if (IsSpectatorOrReadOnlyContext())
        {
            throw new HubException("forbidden: spectators cannot subscribe to event state receipts.");
        }
        var gameId = GetGameIdForReplay() ?? throw new HubException("missing_match");
        var game = _gameService.GetGameState(gameId) ?? throw new HubException("missing_match");
        return MatchCommandObservation.SubscribeEvents(game, Context.ConnectionId);
    }

    public void UnsubscribeEventStateReceipts()
    {
        if (IsSpectatorOrReadOnlyContext())
        {
            return;
        }
        MatchCommandObservation.UnsubscribeEvents(GetGameIdForReplay() ?? throw new HubException("missing_match"), Context.ConnectionId);
    }

    public async Task<JsonElement> RequestSpectatorGameState()
    {
        if (!Context.Items.TryGetValue("ParticipantType", out var kind) || kind is not ChatSenderType.Spectator)
            throw new HubException("spectator_required");
        var gameId = GetGameIdForReplay() ?? throw new HubException("missing_match");
        var snapshot = await _gameService.ReadSnapshotAsync(gameId, Context.ConnectionAborted);
        return snapshot.GetProperty("State").Clone();
    }

    private async Task<bool> EnqueueHubActionAsync(string gameId, PlayerActionWrapper action)
    {
        action.CommandId = _agentRequest?.Id;
        action.ExpectedStateRevision = _agentRequest?.ExpectedStateRevision;
        _submittedAction = action;
        return await _gameService.EnqueuePlayerActionAsync(gameId, action);
    }

    /// <summary>Use the existing player methods, including their seat checks and request admission.</summary>
    public async Task<object> ExecuteAgentAction(AgentActionRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Id))
            return new AgentActionAdmission(request?.Id, null, "rejected", "id is required", "invalid_input");
        try { return await DispatchAgentActionAsync(request); }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        { return new AgentActionAdmission(request.Id, null, "rejected", ex.Message, "invalid_input"); }
    }

    private async Task<object> DispatchAgentActionAsync(AgentActionRequest request)
    {
        _agentRequest = request;
        T Payload<T>() => request.Payload.Deserialize<T>(AgentJson) ?? throw new ArgumentException("payload is required");
        switch (request.Command)
        {
            case "action.play-card": await PlayCard(Payload<PlayCardActionDto>()); break;
            case "action.attack": await PerformAttack(Payload<AttackActionDto>()); break;
            case "action.evolve": await PerformEvolve(Payload<EvolveActionDto>()); break;
            case "action.super-evolve": await PerformSuperEvolve(Payload<SuperEvolveActionDto>()); break;
            case "action.act-ability": await RequestAct(Payload<ActActionDto>()); break;
            case "action.fusion": await RequestFusion(Payload<FusionActionDto>()); break;
            case "action.pp-boost": await PerformPpBoost(); break;
            case "action.mulligan": await SubmitMulligan(Payload<List<string>>()); break;
            case "action.end-turn": await EndTurn(request.RequestId ?? request.Id); break;
            case "action.mirage-set-ready": await MirageSetReady(request.RequestId ?? request.Id); break;
            case "action.mirage-refresh-shop": await MirageRefreshShop(request.RequestId ?? request.Id); break;
            case "action.mirage-upgrade-tier": await MirageUpgradeTier(request.RequestId ?? request.Id); break;
            case "action.mirage-toggle-freeze": await MirageToggleFreeze(request.RequestId ?? request.Id); break;
            case "action.mirage-buy-offer": await MirageBuyOffer(Payload<MirageBuyOfferActionDto>().OfferId, request.RequestId ?? request.Id); break;
            case "action.mirage-sell-card": await MirageSellCard(Payload<MirageSellCardActionDto>().CardUid, request.RequestId ?? request.Id); break;
            case "action.mirage-reorder-field": await MirageReorderField(Payload<MirageReorderFieldActionDto>().OrderedCardUids, request.RequestId ?? request.Id); break;
            case "action.mirage-redeem-ticket":
                {
                    var redeemPayload = Payload<MirageRedeemTicketActionDto>();
                    await MirageRedeemTicket(redeemPayload.TicketId, redeemPayload.ChosenCardDefinitionId, request.RequestId ?? request.Id);
                    break;
                }
            case "action.mirage-select-leader":
                {
                    int leaderId = 0;
                    if (request.Payload.ValueKind == JsonValueKind.Object)
                    {
                        if (request.Payload.TryGetProperty("cardDefinitionId", out var p1) && p1.TryGetInt32(out var v1)) leaderId = v1;
                        else if (request.Payload.TryGetProperty("CardDefinitionId", out var p2) && p2.TryGetInt32(out var v2)) leaderId = v2;
                        else if (request.Payload.TryGetProperty("leaderCardDefinitionId", out var p3) && p3.TryGetInt32(out var v3)) leaderId = v3;
                        else if (request.Payload.TryGetProperty("LeaderCardDefinitionId", out var p4) && p4.TryGetInt32(out var v4)) leaderId = v4;
                    }
                    else if (request.Payload.ValueKind == JsonValueKind.Number && request.Payload.TryGetInt32(out var direct))
                    {
                        leaderId = direct;
                    }
                    if (leaderId <= 0) throw new ArgumentException("cardDefinitionId is required");
                    await MirageSelectLeader(leaderId, request.RequestId ?? request.Id);
                    break;
                }
            case "action.mirage-commit-combat": await MirageCommitCombatResult(Payload<MirageCommitCombatActionDto>(), request.RequestId ?? request.Id); break;
            case "action.surrender": await Surrender(request.RequestId ?? request.Id); break;
            case "debug.event.emit":
                await ExecuteDebugCommand(new DebugCommandDto
                {
                    Command = "event.emit", Args = new() { ["json"] = request.Payload.GetRawText() }
                });
                break;
            default: return new AgentActionAdmission(request.Id, null, "rejected", request.Command, "unknown_command");
        }
        if (_submittedAction == null) return new AgentActionAdmission(request.Id, null, "rejected", "The formal entry point did not admit an action.", "not_admitted");
        var completion = _submittedAction.Completion.IsCompletedSuccessfully ? _submittedAction.Completion.Result : null;
        var status = _duplicateActionId.HasValue ? completion == null ? "already_queued" : "already_completed" :
            completion is { Status: not PlayerActionCompletionStatus.Completed } ? completion.Status.ToString().ToLowerInvariant() : "queued";
        return new AgentActionAdmission(request.Id, _submittedAction.ActionId, status, completion?.Reason, _submittedAction.RejectionCode, _submittedAction.CommandId);
    }
}
