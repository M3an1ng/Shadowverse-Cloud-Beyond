using System.Text.Json;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

public sealed record AiNativeActionCard(string Side,string Zone,int? Index,int CardId,int? CurrentCardCost);
public sealed record AiNativeActionFact(string Kind,string ActorSide,AiNativeActionCard? Actor,AiNativeActionCard[] DeclaredTargets,
    int DeclaredTargetCount,int OmittedTargets,string? PlayMode,bool PlayModeSpecified,int? PaidCostQuote,bool PaidCostSpecified,
    string? CostQuoteOmission,long? RequestedAbilityDefinitionId,bool AbilitySpecified,bool? SameCardAsAbilitySource,
    string Scope="Snapshot of the active native root request when this choice was bound. Quotes and declared targets are request inputs, not completed payment, legal-target proof or successful effects. CurrentCardCost is the actor card's ordinary current cost, not necessarily the selected mode cost. The root actor can differ from the ability source. Locations refer to this binding snapshot; missing data is unknown, not zero.");

internal static class AiNativeActionFacts
{
    internal sealed record Capture(AiNativeActionFact? Fact,string? Omission);
    internal static Capture Read(GameContext? context,Guid viewer,CardInstance abilitySource)
    {
        try{return ReadCore(context,viewer,abilitySource);}
        catch(Exception e) when(e is not OperationCanceledException and not OutOfMemoryException){return new(null,"native-root-unreadable");}
    }
    private static Capture ReadCore(GameContext? context,Guid viewer,CardInstance abilitySource)
    {
        if(context?.TriggeringEvent is not {} root)return new(null,"native-root-unavailable");
        var game=context.CurrentGameState;var own=game.GetPlayerState(viewer);var enemy=game.GetOpponentPlayerState(viewer);
        if(own==null||enemy==null)return new(null,"viewer-unavailable");
        (CardInstance Card,AiNativeActionCard Fact)? Locate(Guid uid)
        {
            if(uid==Guid.Empty)return null;
            foreach(var (player,side) in new[]{(own,"own"),(enemy,"enemy")}){
                if(player.Leader is {} leader&&leader.InstanceId==uid&&leader.OwnerPlayerId==player.PlayerId)
                    return(leader,new(side,"leader",0,leader.CurrentDefinitionId,leader.CurrentCost));
                foreach(var (cards,zone) in new[]{(player.Field,"field"),(player.Emblem,"emblem"),(side=="own"?player.Hand:[],"hand"),(side=="own"?player.Graveyard:[],"graveyard")}){
                    var index=cards.FindIndex(card=>card.InstanceId==uid&&card.OwnerPlayerId==player.PlayerId);if(index<0)continue;
                    var card=cards[index];return(card,new(side,zone,zone=="graveyard"?null:index,card.CurrentDefinitionId,card.CurrentCost));
                }
            }
            return null;
        }
        Guid player;Guid? actor=null,ability=null;List<Guid> targets=[];string kind;PlayMode? mode=null;int? cost=null;
        switch(root){
            case PlayCardRequestedEvent play:
                player=play.PlayerId;actor=play.ActionDto.CardUid;targets=play.ActionDto.TargetUids??[];mode=play.ActionDto.PlayMode;
                cost=play.ActionDto.PaidCost;ability=play.ActionDto.AbilityRuntimeId;kind="PlayCard";break;
            case AttackRequestedEvent attack:
                player=attack.PlayerId;actor=attack.ActionDto.AttackerUid;targets=[attack.ActionDto.TargetUid];kind="PerformAttack";break;
            case EvolveRequestedEvent evolve:
                player=evolve.PlayerId;actor=Guid.TryParse(evolve.ActionDto.UnitUid,out var evolved)?evolved:Guid.Empty;kind="PerformEvolve";break;
            case SuperEvolveRequestedEvent evolve:
                player=evolve.PlayerId;actor=Guid.TryParse(evolve.ActionDto.UnitUid,out var superEvolved)?superEvolved:Guid.Empty;kind="PerformSuperEvolve";break;
            case ActRequestedEvent act:
                player=act.PlayerId;actor=act.ActionDto.CardUid;ability=act.ActionDto.AbilityRuntimeId;kind="Act";break;
            case FusionRequestedEvent fusion:
                player=fusion.PlayerId;actor=fusion.ActionDto.CardUid;ability=fusion.ActionDto.AbilityRuntimeId;kind="Fusion";break;
            case EndTurnRequestedEvent end:player=end.PlayerId;kind="EndTurn";break;
            case PpBoostRequestedEvent boost:player=boost.PlayerId;kind="PerformPpBoost";break;
            default:return new(null,"unsupported-native-root");
        }
        if(player!=own.PlayerId&&player!=enemy.PlayerId)return new(null,"native-root-owner-unavailable");
        var located=actor.HasValue?Locate(actor.Value):null;
        // An enemy play still in a hidden zone must not reveal even its quote, ability or target count.
        if(actor.HasValue&&(located==null||located.Value.Card.OwnerPlayerId!=player))return new(null,"native-root-actor-not-visible");
        var visibleTargets=targets.Take(12).Select(id=>Locate(id)?.Fact).OfType<AiNativeActionCard>().ToArray();long? definition=null;
        if(ability is Guid runtime&&runtime!=Guid.Empty&&located is {} actual
            &&context.AbilityManager.GetAbilityByRuntimeId(runtime) is Ability selected&&selected.SourceCard?.InstanceId==actual.Card.InstanceId
            &&selected.Config.Id>0)definition=selected.Config.Id;
        var fact=new AiNativeActionFact(kind,player==viewer?"own":"enemy",located?.Fact,visibleTargets,targets.Count,targets.Count-visibleTargets.Length,
            mode.HasValue&&Enum.IsDefined(mode.Value)?mode.Value.ToString():null,mode.HasValue,cost is >=0 and <=1000?cost:null,cost.HasValue,
            cost.HasValue&&cost is not (>=0 and <=1000)?"invalid-request-cost-quote":null,definition,ability.HasValue&&ability!=Guid.Empty,
            actor.HasValue?actor.Value==abilitySource.InstanceId:null);
        return JsonSerializer.SerializeToUtf8Bytes(fact).Length<=3072?new(fact,null):new(null,"native-root-metadata-budget");
    }
}
