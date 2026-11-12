using System.Text.Json;
using System.Security.Cryptography;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Models;

namespace ShadowverseBeyond.Server.Services;

public sealed record AiTargetPick(string Side,string Zone,int Index,int CardId);
public sealed record AiTargetChoiceFact(int Turn,int? SourceCardId,long? AbilityDefinitionId,string Key,
    AiStrategyPlanner.TargetAdvice? Advice,string AdviceStatus,string? Reason,AiTargetPick[] Local,AiTargetPick[] Selected,
    string Scope="Submitted selection before effects settle, not committed damage/discard or proof of success. Only own hand and public zones are recorded; missing locations are unknown.");

// One explicit, position-bound target for the current play. No new model request or DSL evaluation.
internal static class AiPlannedTargets
{
    internal sealed record Bound(AiTargetPick At,Guid Uid,string Key);
    internal sealed record Lease(PlayerActionWrapper Root,Guid Source,int PlayedCardId,int Turn,AiStrategyPlanner.TargetAdvice Advice,Bound Target,HashSet<string> Rules){internal Guid? Request;internal Guid? Ability;}
    internal sealed record Attempt(Lease? Lease,string? Reason);
    internal sealed record Selection(Guid? Uid,string Status,string? Reason);
    private static readonly System.Text.RegularExpressions.Regex Key=new("^[A-Za-z_][A-Za-z0-9_]{0,63}$");
    private static string Fingerprint(AbilityConfig config)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(AiAbilityFacts.Configuration(config))));
    private static bool Root(AbilityConfig config)=>config.IsEnabled&&config.Type==AbilityType.Triggered&&config.Trigger.IsEnabled
        &&config.Trigger.Timing==TriggerTiming.On&&config.Trigger.RelationType==TriggerRelationType.TargetSelf
        &&config.Trigger.EventTypes.SequenceEqual(new[]{EventType.PlayCard});
    private static IReadOnlyList<CardInstanceDto>? Zone(PlayerStateDto own,PlayerStateDto enemy,string side,string zone)
    {
        if(side is not ("own" or "enemy"))return null;var p=side=="own"?own:enemy;
        return zone switch{"hand" when side=="own"=>p.Hand,"field"=>p.Field,"leader"=>p.LeaderCard==null?[]:[p.LeaderCard],_=>null};
    }
    private static bool Valid(AiStrategyPlanner.TargetAdvice a)=>a.Key!=null&&Key.IsMatch(a.Key)&&a.Index>=0&&a.CardId>0
        &&a.Side is "own" or "enemy"&&a.Zone is "hand" or "field" or "leader"&&!(a.Side=="enemy"&&a.Zone=="hand")&&a.AbilityDefinitionId is null or >=0;
    internal static Bound? Resolve(AiStrategyPlanner.TargetAdvice advice,PlayerStateDto own,PlayerStateDto enemy)
    {
        if(!Valid(advice))return null;var cards=Zone(own,enemy,advice.Side,advice.Zone);
        if(cards==null||advice.Index>=cards.Count)return null;var card=cards[advice.Index];
        return card.CardId!=advice.CardId||card.InstanceId==Guid.Empty||card.OwnerPlayerId!=(advice.Side=="own"?own.PlayerId:enemy.PlayerId)?null:new(new(advice.Side,advice.Zone,advice.Index,card.CardId),card.InstanceId,AiStrategyPlanner.CardKey(card));
    }
    internal static AiTargetPick? Locate(GameStateDto state,Guid actor,Guid uid)
    {
        var own=state.Players.FirstOrDefault(p=>p.PlayerId==actor);var enemy=state.Players.FirstOrDefault(p=>p.PlayerId!=actor);if(own==null||enemy==null)return null;
        foreach(var side in new[]{"own","enemy"})foreach(var zone in new[]{"hand","field","leader"}){
            var cards=Zone(own,enemy,side,zone);if(cards==null)continue;
            for(var i=0;i<cards.Count;i++)if(cards[i].OwnerPlayerId==(side=="own"?own.PlayerId:enemy.PlayerId)&&(cards[i].InstanceId==uid||zone=="leader"&&(side=="own"?own.PlayerId:enemy.PlayerId)==uid))return new(side,zone,i,cards[i].CardId);
        }
        return null;
    }
    internal static Attempt Begin(GameState game,PlayerActionWrapper root,GameStateDto before,AiStrategyPlanner.TurnPlan? plan)
    {
        if(plan?.Play?.Target is not {} advice)return new(null,null);
        try{
            var own=before.Players.FirstOrDefault(p=>p.PlayerId==root.PlayerId);var enemy=before.Players.FirstOrDefault(p=>p.PlayerId!=root.PlayerId);
            if(own==null||enemy==null||root.ActionType!="PlayCard"||root.ActionData is not PlayCardActionDto action||plan.Play.Intent!="use"
                ||plan.PlayBinding is not {} binding||binding.PositionKey!=AiStrategyPlanner.PositionKey(own,enemy)||action.CardUid!=binding.InstanceId
                ||action.PlayMode?.ToString()!=plan.Play.Mode||action.PaidCost!=plan.Play.Cost)return new(null,"root-or-original-position-mismatch");
            var target=Resolve(advice,own,enemy);if(target==null||target.Uid==action.CardUid)return new(null,"target-not-in-original-visible-position");
            var source=game.FindCardInstance(action.CardUid);if(source==null||source.OwnerPlayerId!=root.PlayerId)return new(null,"source-unavailable");
            var expected=binding.PlayedCardId??plan.Play.CardId;
            var context=GameServiceProvider.CurrentGameContext;
            if(context==null||!ReferenceEquals(context.CurrentGameState,game))return new(null,"native-context-unavailable");
            var configs=expected==source.CurrentDefinitionId?game.AbilityManager.GetEffectiveAbilitiesForCard(source.InstanceId,context).OfType<Ability>().Select(a=>a.Config).ToArray()
                :GameDataProvider.Instance.GetAbilityConfigs(expected)?.ToArray()??[];
            if(configs.Length>64)return new(null,"source-rule-limit");
            var rules=configs.Where(c=>Root(c)&&(!advice.AbilityDefinitionId.HasValue||c.Id==advice.AbilityDefinitionId)
                &&c.Targets.TryGetValue(advice.Key,out var t)&&t.IsEnabled&&t.PlayerChoiceOptions is {IsRequired:true,MinCount:1,MaxCount:1}
                &&AiRuntimeAbilityFacts.SafeConfiguration(c)).Select(Fingerprint).ToHashSet();
            return rules.Count==0?new(null,"no-matching-direct-play-target-rule"):new(new(root,source.InstanceId,expected,own.TurnNumber,advice,target,rules),null);
        }catch(Exception e) when(e is not OperationCanceledException and not OutOfMemoryException){return new(null,"target-binding-unavailable");}
    }
    internal static Selection Select(GameState game,GameStateDto snapshot,PlayerChoiceRequest request,Ability? source,Lease? lease,string? omission)
    {
        if(lease==null)return new(null,omission==null?"none":"rejected",omission);
        try{
            if(lease.Root.Completion.IsCompleted||request.RequestingPlayerId!=lease.Root.PlayerId||game.CurrentPlayerId!=request.RequestingPlayerId
                ||game.GetPlayerState(request.RequestingPlayerId)?.TurnNumber!=lease.Turn||snapshot.GameId!=game.GameId)return new(null,"rejected","root-finished-or-turn-changed");
            if(request.TargetKey!=lease.Advice.Key)return new(null,"other-key",null);
            if(request.MinSelectionCount!=1||request.MaxSelectionCount!=1||source?.SourceCard is not {} card||card.InstanceId!=lease.Source
                ||card.CurrentDefinitionId!=lease.PlayedCardId||card.OwnerPlayerId!=request.RequestingPlayerId||!Root(source.Config)
                ||!lease.Rules.Contains(Fingerprint(source.Config)))return new(null,"rejected","source-mode-or-rule-changed");
            if(!request.SelectableTargetIds.Contains(lease.Target.Uid))return new(null,"rejected","target-not-in-current-legal-pool");
            if(lease.Request.HasValue&&(lease.Request!=request.PendingChoiceId||lease.Ability!=source.RuntimeId))return new(null,"rejected","target-advice-already-used-for-this-root");
            var own=snapshot.Players.Single(p=>p.PlayerId==request.RequestingPlayerId);var enemy=snapshot.Players.Single(p=>p.PlayerId!=request.RequestingPlayerId);
            var cards=Zone(own,enemy,lease.Target.At.Side,lease.Target.At.Zone);
            var actual=cards?.FirstOrDefault(c=>c.InstanceId==lease.Target.Uid);
            if(actual==null||actual.OwnerPlayerId!=(lease.Target.At.Side=="own"?own.PlayerId:enemy.PlayerId)||actual.CardId!=lease.Target.At.CardId||AiStrategyPlanner.CardKey(actual)!=lease.Target.Key)return new(null,"rejected","target-instance-or-state-changed");
            lease.Request=request.PendingChoiceId;lease.Ability=source.RuntimeId;return new(lease.Target.Uid,"selected",null);
        }catch(Exception e) when(e is not OperationCanceledException and not OutOfMemoryException){return new(null,"rejected","target-check-unavailable");}
    }
    internal static object[] Keys(GameState game,AiPlayCandidates.Candidate candidate)
    {
        try{
            var context=GameServiceProvider.CurrentGameContext;var actual=game.FindCardInstance(candidate.Original.InstanceId);
            if(context==null||!ReferenceEquals(context.CurrentGameState,game)||actual==null)return [];
            var configs=candidate.PlayedCardId==actual.CurrentDefinitionId?context.AbilityManager.GetEffectiveAbilitiesForCard(actual.InstanceId,context).OfType<Ability>().Select(a=>a.Config)
                :GameDataProvider.Instance.GetAbilityConfigs(candidate.PlayedCardId)??[];
            return configs.Take(64).Where(Root).SelectMany(c=>c.Targets.Where(t=>t.Value.IsEnabled&&t.Value.PlayerChoiceOptions is {IsRequired:true,MinCount:1,MaxCount:1})
                .Select(t=>(object)new{key=t.Key,abilityDefinitionId=c.Id,selectedHandEffect=AiSelectedHandLoss.DiscardsSelection(c,t.Key)?"discard":null})).Take(8).ToArray();
        }catch(Exception e) when(e is not OperationCanceledException and not OutOfMemoryException){return [];}
    }
    internal static AiStrategyPlanner.TargetAdvice? Rebase(AiStrategyPlanner.TargetAdvice advice,PlayerStateDto beforeOwn,PlayerStateDto beforeEnemy,PlayerStateDto own,PlayerStateDto enemy)
    {
        var old=Resolve(advice,beforeOwn,beforeEnemy);if(old==null)return null;var cards=Zone(own,enemy,advice.Side,advice.Zone);if(cards==null)return null;
        for(var i=0;i<cards.Count;i++)if(cards[i].InstanceId==old.Uid&&cards[i].OwnerPlayerId==(advice.Side=="own"?own.PlayerId:enemy.PlayerId)&&cards[i].CardId==advice.CardId)return advice with{Index=i};return null;
    }
}
