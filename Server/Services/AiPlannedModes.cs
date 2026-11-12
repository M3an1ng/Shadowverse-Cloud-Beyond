using System.Security.Cryptography;
using System.Text.Json;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Models;

namespace ShadowverseBeyond.Server.Services;

// Explicit branch intent belongs to one native play root. It is not a simulated outcome.
internal static class AiPlannedModes
{
    internal sealed record Option(int Index, string Name, string Text, string Fingerprint);
    internal sealed record Stage(long AbilityDefinitionId, int EffectIndex, int Executions,
        string ParentFingerprint, Option[] Options)
    { internal Guid CatalogRevision {get;init;} }
    internal sealed record Choice(AiStrategyPlanner.ModeAdvice Advice, string ParentFingerprint, string BranchFingerprint);
    internal sealed class Lease(PlayerActionWrapper root, Guid source, int playedId, int turn, string position, Guid revision, Choice[] choices)
    {
        internal readonly PlayerActionWrapper Root = root;
        internal readonly Guid Source = source;
        internal readonly int PlayedId = playedId, Turn = turn;
        internal readonly string Position = position;
        internal readonly Guid CatalogRevision = revision;
        internal readonly Choice[] Choices = choices;
        internal Guid? Invocation;
        internal readonly Dictionary<(long Ability, int Effect, int Execution), Guid> Requests = [];
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal static string? Fingerprint(AbilityConfig config)
    {
        if (!AiRuntimeAbilityFacts.SafeConfiguration(config)) return null;
        var body = AiAbilityFacts.Configuration(config);
        return AiRuntimeAbilityFacts.ContainsPrivateData(body) ? null
            : Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(body, Json))).ToLowerInvariant();
    }
    private static bool Root(AbilityConfig config) => config.IsEnabled && config.Type == AbilityType.Triggered
        && config.Trigger.IsEnabled && config.Trigger.Timing == TriggerTiming.On
        && config.Trigger.RelationType == TriggerRelationType.TargetSelf
        && config.Trigger.EventTypes.SequenceEqual(new[] { EventType.PlayCard });
    private static AbilityConfig[] Configs(GameState game, CardInstance source, int playedId, GameContext context) =>
        (playedId == source.CurrentDefinitionId
            ? game.AbilityManager.GetEffectiveAbilitiesForCard(source.InstanceId, context).OfType<Ability>().Select(a => a.Config)
            : GameDataProvider.Instance.GetAbilityConfigs(playedId) ?? []).Take(65).ToArray();

    internal static Stage[] Read(GameState game, AiPlayCandidates.Candidate candidate)
    {
        try
        {
            var revision = GameDataProvider.Instance.DataRevision;
            if (revision == Guid.Empty) return [];
            var context = GameServiceProvider.CurrentGameContext;
            var source = game.FindCardInstance(candidate.Original.InstanceId);
            if (context == null || !ReferenceEquals(context.CurrentGameState, game) || source == null
                || game.Phase != GamePhase.Main || game.IsGameOver
                || source.OwnerPlayerId != game.CurrentPlayerId
                || source.OwnerPlayerId != candidate.Original.OwnerPlayerId || source.CurrentZone != CardZone.Hand
                || game.GetPlayerState(source.OwnerPlayerId)?.Hand.Any(card => ReferenceEquals(card, source)) != true
                || source.CurrentDefinitionId != candidate.Original.CardId) return [];
            var configs = Configs(game, source, candidate.PlayedCardId, context);
            if (configs.Length > 64) return [];
            var result = new List<Stage>();
            foreach (var config in configs.Where(Root))
            {
                var fingerprint = Fingerprint(config);
                if (fingerprint == null) continue;
                var enabled = config.Effects.Where(effect => effect.IsEnabled).ToArray();
                for (var index = 0; index < enabled.Length; index++)
                {
                    var effect = enabled[index];
                    if (effect.Type != EffectType.Choose || effect.RepeatCountValue != null
                        || effect.RepeatCondition != null || effect.RepeatCount is < 0 or > 7) continue;
                    if (!effect.Parameters.TryGetValue("SelectionCount", out var count)
                        || count is not StaticValueConfig literal) continue;
                    var single = literal.Value is int number ? number == 1
                        : literal.Value is JsonElement json && json.ValueKind == JsonValueKind.Number && json.TryGetInt32(out var parsed) && parsed == 1;
                    if (!single) continue;
                    var branches = AiGeneratedTokenTactics.Branches(effect);
                    if (branches.Length is < 2 or > 12) continue;
                    var options = branches.Select((branch, option) => new Option(option, branch.Name,
                        branch.Description, Fingerprint(branch) ?? "")).ToArray();
                    if (options.Any(option => option.Fingerprint.Length != 64)) continue;
                    result.Add(new(config.Id, index, effect.RepeatCount + 1, fingerprint, options){CatalogRevision=revision});
                    // Whole stage metadata only. No clipped names, hashes or partial option lists.
                    if (result.Count > 8 || JsonSerializer.SerializeToUtf8Bytes(result, Json).Length > 6144) return [];
                }
            }
            if (GameDataProvider.Instance.DataRevision != revision) return [];
            return result.GroupBy(stage => (stage.AbilityDefinitionId, stage.EffectIndex)).Any(group => group.Count() > 1)
                ? [] : result.ToArray();
        }
        catch (Exception error) when (error is not OperationCanceledException and not OutOfMemoryException) { return []; }
    }

    internal static bool Same(AiStrategyPlanner.PlayAdvice? a, AiStrategyPlanner.PlayAdvice? b) =>
        a == null || b == null ? a == b
            : (a with { Choices = null }) == (b with { Choices = null })
                && (a.Choices ?? []).SequenceEqual(b.Choices ?? []);

    internal static Lease? Begin(GameState game, PlayerActionWrapper root, GameStateDto before,
        AiStrategyPlanner.TurnPlan? plan)
    {
        if (plan?.Play is not { Intent: "use", Choices: { Length: > 0 and <= 8 } hints } advice
            || plan.PlayBinding is not { } binding || root.Completion.IsCompleted
            || root.ActionType != "PlayCard" || root.ActionData is not PlayCardActionDto action) return null;
        try
        {
            var own = before.Players.Single(p => p.PlayerId == root.PlayerId);
            var enemy = before.Players.Single(p => p.PlayerId != root.PlayerId);
            if (before.GameId != game.GameId || game.CurrentPlayerId != root.PlayerId || game.Phase != GamePhase.Main
                || game.IsGameOver || game.GetPlayerState(root.PlayerId)?.TurnNumber != own.TurnNumber
                || binding.PositionKey != AiStrategyPlanner.PositionKey(own, enemy) || action.CardUid != binding.InstanceId
                || action.PlayMode?.ToString() != advice.Mode || action.PaidCost != advice.Cost) return null;
            var source = game.FindCardInstance(action.CardUid);
            var playedId = binding.PlayedCardId ?? advice.CardId;
            if (source == null || source.OwnerPlayerId != root.PlayerId || source.CurrentZone != CardZone.Hand
                || !plan.ModeStages.TryGetValue((source.InstanceId, playedId), out var original)) return null;
            var view = own.Hand.Single(c => c.InstanceId == source.InstanceId);
            var current = Read(game, new(view, view, advice.HandIndex,
                action.PlayMode!.Value, advice.Cost, false, null, playedId));
            var choices = new List<Choice>();
            foreach (var hint in hints)
            {
                var stage = original.SingleOrDefault(s => s.AbilityDefinitionId == hint.AbilityDefinitionId && s.EffectIndex == hint.EffectIndex);
                if (stage == null || stage.CatalogRevision == Guid.Empty || stage.CatalogRevision != GameDataProvider.Instance.DataRevision
                    || hint.ExecutionIndex < 0 || hint.ExecutionIndex >= stage.Executions
                    || hint.OptionIndex < 0 || hint.OptionIndex >= stage.Options.Length
                    || !current.Any(s => s.AbilityDefinitionId == stage.AbilityDefinitionId && s.EffectIndex == stage.EffectIndex
                        && s.ParentFingerprint == stage.ParentFingerprint)) return null;
                choices.Add(new(hint, stage.ParentFingerprint, stage.Options[hint.OptionIndex].Fingerprint));
            }
            if (hints.Select(hint => (hint.AbilityDefinitionId, hint.EffectIndex, hint.ExecutionIndex)).Distinct().Count() != hints.Length) return null;
            return new(root, source.InstanceId, playedId, own.TurnNumber, binding.PositionKey, original[0].CatalogRevision, choices.ToArray());
        }
        catch (Exception error) when (error is not OperationCanceledException and not OutOfMemoryException) { return null; }
    }

    internal static List<Guid>? Select(GameState game, GameStateDto snapshot, PlayerChoiceRequest request, Lease? lease)
    {
        if (lease == null || lease.CatalogRevision != GameDataProvider.Instance.DataRevision
            || lease.Root.Completion.IsCompleted || request.TargetKey != "decision"
            || request.RequestingPlayerId != lease.Root.PlayerId || game.CurrentPlayerId != request.RequestingPlayerId
            || game.IsGameOver || game.Phase != GamePhase.Main
            || game.FindCardInstance(lease.Source)?.OwnerPlayerId != request.RequestingPlayerId
            || game.GetPlayerState(request.RequestingPlayerId)?.TurnNumber != lease.Turn
            || request.MinSelectionCount != 1 || request.MaxSelectionCount != 1) return null;
        try
        {
            var own = snapshot.Players.Single(player => player.PlayerId == request.RequestingPlayerId);
            var enemy = snapshot.Players.Single(player => player.PlayerId != request.RequestingPlayerId);
            if (snapshot.GameId != game.GameId || AiStrategyPlanner.PositionKey(own, enemy) != lease.Position) return null;
            var invocation = AiDecisionFlowFacts.Identify(game, request);
            if (invocation == null || invocation.Source != lease.Source || invocation.PlayedId != lease.PlayedId
                || lease.Invocation.HasValue && lease.Invocation != invocation.Event) return null;
            var hint = lease.Choices.SingleOrDefault(c => c.Advice.AbilityDefinitionId == invocation.AbilityDefinitionId
                && c.Advice.EffectIndex == invocation.Effect && c.Advice.ExecutionIndex == invocation.Execution
                && c.ParentFingerprint == invocation.ParentFingerprint);
            if (hint == null) return null;
            var matches = request.SelectableTargetIds.Where(id => request.DecisionOptions.TryGetValue(id, out var branch)
                && Fingerprint(branch) == hint.BranchFingerprint).ToArray();
            if (matches.Length != 1) return null;
            var key = (invocation.AbilityDefinitionId, invocation.Effect, invocation.Execution);
            if (lease.Requests.TryGetValue(key, out var prior) && prior != request.PendingChoiceId) return null;
            lease.Invocation = invocation.Event;
            lease.Requests[key] = request.PendingChoiceId;
            return [matches[0]];
        }
        catch (Exception error) when (error is not OperationCanceledException and not OutOfMemoryException) { return null; }
    }
}
