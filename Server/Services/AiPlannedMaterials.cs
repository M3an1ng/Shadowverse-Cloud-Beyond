using System.Security.Cryptography;
using System.Text.Json;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Models;

namespace ShadowverseBeyond.Server.Services;

// A reservation is intent, not proof of future playability. Keep it inside the matching native root.
internal static class AiPlannedMaterials
{
    internal sealed record Reserved(Guid Uid, string CardKey);
    internal sealed record Lease(PlayerActionWrapper Root, Guid Source, int PlayedId, int Turn,
        Reserved[] Cards, HashSet<string> Rules);
    private static string Fingerprint(AbilityConfig config) => Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(AiAbilityFacts.Configuration(config))));

    internal static Lease? Begin(GameState game, PlayerActionWrapper root, GameStateDto before,
        AiStrategyPlanner.TurnPlan? plan)
    {
        if (plan is not {LineConstraintsChecked:true, PlayLine:{Steps.Length:>1} line, PlayBinding:{} binding,
            Play:{Intent:"use", Target:null} advice} || root.ActionType!="PlayCard"
            || root.ActionData is not PlayCardActionDto action || root.Completion.IsCompleted) return null;
        try {
            var own=before.Players.Single(p=>p.PlayerId==root.PlayerId);
            var enemy=before.Players.Single(p=>p.PlayerId!=root.PlayerId);
            if (game.Phase!=GamePhase.Main || before.Phase!=GamePhase.Main || game.CurrentPlayerId!=root.PlayerId || own.TurnNumber!=line.BeforeOwn.TurnNumber
                || binding.PositionKey!=AiStrategyPlanner.PositionKey(own,enemy)
                || action.CardUid!=binding.InstanceId || action.CardUid!=line.Steps[0].Original.InstanceId
                || action.PlayMode?.ToString()!=advice.Mode || action.PaidCost!=advice.Cost) return null;
            var actual=game.FindCardInstance(action.CardUid);
            var context=GameServiceProvider.CurrentGameContext;
            if (actual==null || actual.IsSilenced || actual.OwnerPlayerId!=root.PlayerId
                || context==null || !ReferenceEquals(context.CurrentGameState,game)) return null;
            var expected=binding.PlayedCardId??advice.CardId;
            var configs=(expected==actual.CurrentDefinitionId
                ? game.AbilityManager.GetEffectiveAbilitiesForCard(actual.InstanceId,context).OfType<Ability>().Select(a=>a.Config)
                : GameDataProvider.Instance.GetAbilityConfigs(expected)??[]).Take(65).ToArray();
            if (configs.Length>64) return null;
            var rules=configs.Where(c=>c.Targets.Keys.Any(k=>AiSelectedHandLoss.DiscardsSelection(c,k)))
                .Select(Fingerprint).ToHashSet();
            var cards=line.Steps.Skip(1).Select(s=>new Reserved(s.Original.InstanceId,AiStrategyPlanner.CardKey(s.Original))).ToArray();
            return rules.Count==0 || cards.Any(c=>c.Uid==Guid.Empty || c.Uid==action.CardUid)
                || cards.Select(c=>c.Uid).Distinct().Count()!=cards.Length ? null
                : new(root,action.CardUid,expected,own.TurnNumber,cards,rules);
        } catch (Exception e) when(e is not OperationCanceledException and not OutOfMemoryException) { return null; }
    }

    internal static HashSet<Guid> Read(GameState game, GameStateDto snapshot, PlayerChoiceRequest request,
        Ability? source, Lease? lease)
    {
        if (lease==null) return [];
        try {
            if (lease.Root.Completion.IsCompleted || game.Phase!=GamePhase.Main || snapshot.Phase!=GamePhase.Main || game.CurrentPlayerId!=lease.Root.PlayerId
                || snapshot.GameId!=game.GameId || (!string.IsNullOrEmpty(request.GameId)&&request.GameId!=game.GameId) || request.RequestingPlayerId!=lease.Root.PlayerId
                || request.MinSelectionCount!=1 || request.MaxSelectionCount!=1
                || source?.SourceCard is not {} card || card.InstanceId!=lease.Source
                || card.OwnerPlayerId!=lease.Root.PlayerId || card.CurrentDefinitionId!=lease.PlayedId || card.IsSilenced
                || request.SourceAbilityRuntimeId!=source.RuntimeId
                || !AiSelectedHandLoss.DiscardsSelection(source.Config,request.TargetKey)
                || !lease.Rules.Contains(Fingerprint(source.Config))) return [];
            var own=snapshot.Players.Single(p=>p.PlayerId==request.RequestingPlayerId);
            if (own.TurnNumber!=lease.Turn || game.GetPlayerState(own.PlayerId)?.TurnNumber!=lease.Turn) return [];
            // Do not transfer a reservation to a same-name replacement or a changed/removed future card.
            if (lease.Cards.Any(r=>!own.Hand.Any(c=>c.InstanceId==r.Uid && c.OwnerPlayerId==own.PlayerId && c.Zone==CardZone.Hand
                && AiStrategyPlanner.CardKey(c)==r.CardKey))) return [];
            // Only rank within the native legal pool. If every legal material is reserved, selection still proceeds.
            return lease.Cards.Where(c=>request.SelectableTargetIds.Contains(c.Uid)).Select(c=>c.Uid).ToHashSet();
        } catch (Exception e) when(e is not OperationCanceledException and not OutOfMemoryException) { return []; }
    }
}
