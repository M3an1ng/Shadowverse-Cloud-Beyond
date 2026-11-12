using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Effects;
using System.Text.Json.Serialization;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Models;

namespace ShadowverseBeyond.Server.Services;

public sealed record AiObservedResources(int? OwnHealth,int? EnemyHealth,int PP,int MaxPP,int EP,int SEP,
    bool EvolutionUnlocked,bool SuperEvolutionUnlocked,bool BoostArmed,int BoostRemaining,
    int OwnHand,int EnemyHand,int OwnField,int EnemyField,int OwnGraveyard,int EnemyGraveyard,string ActiveSide,bool CanEvolve,bool CanSuperEvolve)
{public int? BloodPool{get;init;}public int[]? OwnEmblemIds{get;init;}public int[]? EnemyEmblemIds{get;init;}public int OwnEmblemsOmitted{get;init;}public int EnemyEmblemsOmitted{get;init;}}
public sealed record AiObservedBody(int CardId,string Zone,int? Attack,int? Health);
public sealed record AiObservedHandCard(int Index,int CardId,string Type,int Cost,int? Attack,int? Health,string[] Keywords);
public sealed record AiObservedHandChange(string Change,AiObservedHandCard? Before,AiObservedHandCard? After);
public sealed record AiObservedHandChanges(int BeforeCount,int AfterCount,AiObservedHandChange[] Changes);
public sealed record AiObservedDrawBatch(int? SourceCardId,int Requested,int Resolved)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceEvidence { get; init; }
}
public sealed record AiObservedEndTurnDraws(AiObservedDrawBatch[] Batches,bool DeckExhausted,bool Truncated)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? OwnDeckBefore { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ExhaustedBatchIndex { get; init; }
}
public sealed record AiActionDecisionFact(string Policy, AiStrategyPlanner.ActionOption Local, bool Different);
public sealed record AiCompletedActionFact(int Turn,AiStrategyPlanner.ActionOption Action,AiObservedResources Before,
    AiObservedResources After,AiObservedBody? ActorBefore,AiObservedBody? ActorAfter,
    AiObservedBody? TargetBefore,AiObservedBody? TargetAfter,string Result,AiObservedHandChanges? OwnHandChanges=null,AiObservedChoiceOutcomes? ChoiceTargets=null,
    AiActionDecisionFact? Decision=null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EvolutionSettlement { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AiObservedEndTurnDraws? EndTurnDraws { get; init; }
}

// A transaction may pause for choices. Commit matching and root completion are separate requirements.
internal static class AiActionOutcomes
{
    // Runtime identities only join two endpoints inside the root lease; never serialize them.
    internal sealed record HandSnapshot(Dictionary<Guid,AiObservedHandCard> Cards);
    private static HandSnapshot? Capture(PlayerStateDto player)
    {
        if(player.Hand.Count>GameConstants.Hand.MaxHandSize||player.Hand.Any(card=>card.OwnerPlayerId!=player.PlayerId||card.Zone!=CardZone.Hand||card.InstanceId==Guid.Empty)
            ||player.Hand.Select(card=>card.InstanceId).Distinct().Count()!=player.Hand.Count)return null;
        return new(player.Hand.Select((card,index)=>(card,index)).ToDictionary(row=>row.card.InstanceId,row=>new AiObservedHandCard(
            row.index,row.card.CardId,row.card.Type.ToString(),row.card.CurrentCost,
            row.card.Type==CardType.Follower?row.card.CurrentAttack:null,row.card.Type==CardType.Follower?row.card.CurrentHealth:null,
            row.card.Keywords.Select(key=>key.ToString()).Distinct().Order(StringComparer.Ordinal).ToArray())));
    }
    private static HandSnapshot? Capture(PlayerState player,GameState game)
    {
        var context=GameServiceProvider.CurrentGameContext;
        if(context==null||!ReferenceEquals(context.CurrentGameState,game)||player.Hand.Count>GameConstants.Hand.MaxHandSize
            ||player.Hand.Any(card=>card.OwnerPlayerId!=player.PlayerId||card.CurrentZone!=CardZone.Hand||card.InstanceId==Guid.Empty)
            ||player.Hand.Select(card=>card.InstanceId).Distinct().Count()!=player.Hand.Count)return null;
        return new(player.Hand.Select((card,index)=>(card,index)).ToDictionary(row=>row.card.InstanceId,row=>new AiObservedHandCard(
            row.index,row.card.CurrentDefinitionId,row.card.CardType.ToString(),row.card.GetCurrentCost(context),
            row.card.CardType==CardType.Follower?row.card.GetCurrentAttack(context):null,
            row.card.CardType==CardType.Follower?row.card.GetMaxHealth(context)-row.card.Damage:null,
            row.card.GetKeywords(context).Select(key=>key.ToString()).Distinct().Order(StringComparer.Ordinal).ToArray())));
    }
    private static AiObservedHandChanges? Compare(HandSnapshot? before,HandSnapshot? after)
    {
        if(before==null||after==null)return null;
        var changes=new List<AiObservedHandChange>();
        foreach(var (uid,card) in before.Cards) {
            if(!after.Cards.TryGetValue(uid,out var next))changes.Add(new("departed",card,null));
            else if(card.CardId!=next.CardId||card.Type!=next.Type||card.Cost!=next.Cost||card.Attack!=next.Attack
                ||card.Health!=next.Health||!card.Keywords.SequenceEqual(next.Keywords))changes.Add(new("changed",card,next));
        }
        foreach(var (uid,card) in after.Cards)
            if(!before.Cards.ContainsKey(uid))changes.Add(new("arrived",null,card));
        return new(before.Cards.Count,after.Cards.Count,changes.ToArray());
    }
    internal static IEnumerable<int> CardIds(AiCompletedActionFact fact)=>
        new[]{fact.Action.CardId,fact.Action.TargetCardId,fact.Decision?.Local.CardId,fact.Decision?.Local.TargetCardId}.OfType<int>()
            .Concat((fact.EndTurnDraws?.Batches??[]).Select(draw=>draw.SourceCardId).OfType<int>())
            .Concat((fact.OwnHandChanges?.Changes??[]).SelectMany(change=>new[]{change.Before?.CardId,change.After?.CardId}).OfType<int>())
            .Concat((fact.Before.OwnEmblemIds??[]).Concat(fact.Before.EnemyEmblemIds??[]).Concat(fact.After.OwnEmblemIds??[]).Concat(fact.After.EnemyEmblemIds??[]))
            .Concat((fact.ChoiceTargets?.Requests??[]).SelectMany(q=>q.Targets.SelectMany(t=>new[]{(int?)t.Before.CardId,t.After?.CardId}).Concat(new[]{q.SourceCardId})).OfType<int>());

    internal sealed class Lease(PlayerActionWrapper request,AiStrategyPlanner.ActionOption action,AiObservedResources before,
        AiObservedBody? actor,AiObservedBody? target,Guid? actorUid,Guid? targetUid,int turn,HandSnapshot? handBefore,AiActionDecisionFact? decision)
    {
        internal readonly PlayerActionWrapper Request=request;
        internal readonly AiStrategyPlanner.ActionOption Action=action;
        internal readonly AiObservedResources Before=before;
        internal readonly AiObservedBody? ActorBefore=actor,TargetBefore=target;
        internal readonly Guid? ActorUid=actorUid,TargetUid=targetUid;
        internal readonly int Turn=turn;
        internal readonly HandSnapshot? HandBefore=handBefore;
        internal HandSnapshot? HandAfter;
        internal readonly AiActionDecisionFact? Decision = decision;
        internal readonly AiTargetOutcomes.Window Choices=new();
        internal EndTurnDrawWindow? EndTurnDraws;
        internal bool Matched;
        internal bool OrdinaryEvolutionRightUsed;
        internal bool SelfSuperEvolution;
        internal bool ConvertedEvolution => OrdinaryEvolutionRightUsed && SelfSuperEvolution;
        internal AiObservedResources? After;
        internal AiObservedBody? ActorAfter,TargetAfter;
        internal string Result="not-ended";
        internal bool Completed=>Request.Completion.IsCompletedSuccessfully
            &&Request.Completion.Result.Status==PlayerActionCompletionStatus.Completed;
    }

    internal sealed class EndTurnDrawWindow(Guid actor, int ownDeckBefore, Dictionary<Guid,int> sources,
        Dictionary<Guid,(CardInstance Card,int CardId,long DefinitionId)> abilitySources)
    {
        private const int Limit = 8;
        private readonly List<(Guid EventId,AiObservedDrawBatch Batch)> batches = [];
        private bool deckExhausted, truncated;

        internal void Observe(IReadOnlyList<IGameEvent> events)
        {
            foreach (var item in events)
            {
                if (item is DrawEvent draw && draw.DrawingPlayer?.PlayerId==actor && batches.All(batch=>batch.EventId!=draw.EventId))
                {
                    if (batches.Count>=Limit) truncated=true;
                    else
                    {
                        var batch=new AiObservedDrawBatch(null,draw.Count,draw.DrawnCards.Count);
                        // A terminal draw can stop the transaction before its effect completion.
                        // Only look up a runtime ability captured on an own public card at submission.
                        if (batch.Resolved<batch.Requested && draw.SourceAbilityRuntimeId is Guid runtimeId
                            && abilitySources.TryGetValue(runtimeId,out var source)
                            && draw.SourceAbilityDefinitionId==source.DefinitionId && source.Card.OwnerPlayerId==actor
                            && source.Card.CurrentDefinitionId==source.CardId)
                            batch=batch with {SourceCardId=source.CardId,SourceEvidence="public-source-ability"};
                        batches.Add((draw.EventId,batch));
                    }
                }
                else if (item is DeckEmptyOnDrawEvent && item.Source?.Player?.PlayerId==actor)
                    deckExhausted=true;
            }
            foreach (var completion in events.OfType<EffectCompletedEvent>())
            {
                if (completion.OriginalEffectType!=EffectType.Draw || completion.Source?.Card is not { } card
                    || !sources.TryGetValue(card.InstanceId,out var cardId) || card.CurrentDefinitionId!=cardId)
                    continue;
                foreach (var draw in completion.GeneratedEvents.OfType<DrawEvent>())
                {
                    var index=batches.FindIndex(batch=>batch.EventId==draw.EventId);
                    if (index>=0)
                        batches[index]=(draw.EventId,batches[index].Batch with {SourceCardId=cardId,SourceEvidence=null});
                }
            }
        }

        internal AiObservedEndTurnDraws? Snapshot()
        {
            if (batches.Count==0 && !deckExhausted) return null;
            var ordered=batches.Select(entry=>entry.Batch).ToArray();
            var shortBatches=Enumerable.Range(0,ordered.Length)
                .Where(index=>ordered[index].Resolved<ordered[index].Requested).Take(2).ToArray();
            return new(ordered,deckExhausted,truncated)
            {
                OwnDeckBefore=ownDeckBefore,
                ExhaustedBatchIndex=deckExhausted && !truncated && shortBatches is [var index] ? index : null
            };
        }
    }

    private static EndTurnDrawWindow StartDrawWindow(GameState game,PlayerStateDto own)
    {
        var sources=own.Field.Concat(own.Emblem)
            .Where(card=>card.InstanceId!=Guid.Empty && card.OwnerPlayerId==own.PlayerId
                && card.Zone is CardZone.Field or CardZone.Emblem && card.CardId>0)
            .GroupBy(card=>card.InstanceId).Where(group=>group.Count()==1)
            .ToDictionary(group=>group.Key,group=>group.Single().CardId);
        var abilities=new Dictionary<Guid,(CardInstance,int,long)>();
        foreach (var (id,cardId) in sources)
        {
            var card=game.FindCardInstance(id);
            if (card==null || card.OwnerPlayerId!=own.PlayerId || card.CurrentDefinitionId!=cardId
                || card.CurrentZone is not (CardZone.Field or CardZone.Emblem)) continue;
            foreach (var ability in game.AbilityManager?.GetAbilitiesForCard(id) ?? [])
                if (ability.SourceCardId==id && ability.RuntimeId!=Guid.Empty)
                    abilities.TryAdd(ability.RuntimeId,(card,cardId,ability.DefinitionId));
        }
        return new(own.PlayerId,own.DeckCount,sources,abilities);
    }

    internal static Lease? Begin(GameState game,PlayerActionWrapper request,GameStateDto snapshot,
        AiStrategyPlanner.ActionComparisonFact? comparison)
    {
        var own=snapshot.Players.SingleOrDefault(player=>player.PlayerId==request.PlayerId);
        var enemy=snapshot.Players.SingleOrDefault(player=>player.PlayerId!=request.PlayerId);
        if(own==null||enemy==null)return null;
        Guid? actor=request.ActionData switch {
            PlayCardActionDto play=>play.CardUid,AttackActionDto attack=>attack.AttackerUid,
            EvolveActionDto evo when Guid.TryParse(evo.UnitUid,out var uid)=>uid,
            SuperEvolveActionDto evo when Guid.TryParse(evo.UnitUid,out var uid)=>uid,
            ActActionDto act=>act.CardUid,FusionActionDto fusion=>fusion.CardUid,_=>null};
        var target=request.ActionData is AttackActionDto attackData?(Guid?)attackData.TargetUid:null;
        var actorCard=own.Hand.Concat(own.Field).Concat(own.LeaderCard==null?[]:new[]{own.LeaderCard}).FirstOrDefault(card=>card.InstanceId==actor);
        var targetCard=enemy.Field.Concat(enemy.LeaderCard==null?[]:new[]{enemy.LeaderCard}).FirstOrDefault(card=>card.InstanceId==target);
        var playRequest=request.ActionData as PlayCardActionDto;
        int? Index(List<CardInstanceDto> cards,Guid? uid){var index=cards.FindIndex(card=>card.InstanceId==uid);return index<0?null:index;}
        var action=comparison?.Selected??new(request.ActionType,actorCard?.CardId,targetCard?.CardId,null,0,
            HandIndex:Index(own.Hand,actor),PlayMode:playRequest?.PlayMode?.ToString(),PaidCost:playRequest?.PaidCost,
            FieldIndex:Index(own.Field,actor),TargetFieldIndex:Index(enemy.Field,target));
        if(action.Kind!=request.ActionType)return null;
        var self=game.GetPlayerState(request.PlayerId);var other=game.GetOpponentPlayerState(request.PlayerId);
        if(self==null||other==null)return null;
        var before=new AiObservedResources(own.LeaderCard?.CurrentHealth,enemy.LeaderCard?.CurrentHealth,own.CurrentPP,own.MaxPP,own.CurrentEP,own.CurrentSEP,
            self.IsEvolutionUnlocked,self.IsSuperEvolutionUnlocked,own.IsPpBoostArmed,own.PpBoostUsesAvailable,
            own.Hand.Count,enemy.Hand.Count,own.Field.Count,enemy.Field.Count,self.GraveyardCount,other.GraveyardCount,Side(game,request.PlayerId),own.CanEvolve,own.CanSuperEvolve);
        before=AiPublicResourceContext.Capture(before,own,enemy);
        AiObservedBody? Body(CardInstanceDto? card)=>card==null?null:new(card.CardId,card.Zone.ToString(),card.CurrentAttack,card.CurrentHealth);
        var decision = comparison is { SameSelection: bool same, SelectionPolicy: string policy } && comparison.Turn == self.TurnNumber
            ? new AiActionDecisionFact(policy, comparison.Local, !same) : null;
        return new(request,action,before,Body(actorCard),Body(targetCard),actor,target,self.TurnNumber,Capture(own),decision)
        {
            EndTurnDraws = request.ActionType=="EndTurn" ? StartDrawWindow(game,own) : null
        };
    }

    internal static void Observe(Lease lease,GameState game,IReadOnlyList<IGameEvent> events,Guid? pendingAbility)
    {
        if(lease.Request.Completion.IsCompleted)return;
        lease.Matched|=events.Any(item=>AiCommittedAction.Matches(lease.Action,lease.Request.ActionData,lease.Request.PlayerId,item,pendingAbility));
        if(events.Count==0)return;
        lease.EndTurnDraws?.Observe(events);
        if (lease.Request.ActionType == "PerformEvolve" && lease.Before.CanEvolve && lease.ActorUid is Guid unit)
        {
            // Action requests are not present in the committed event stream. Bind both pieces of
            // evidence to the in-flight ordinary evolution and its exact card instance.
            foreach (var item in events)
            {
                if (item is EvolveStateUpdatedEvent { CanEvolve: false } rights
                    && rights.Source?.Player?.PlayerId == lease.Request.PlayerId)
                    lease.OrdinaryEvolutionRightUsed = true;
                if (lease.OrdinaryEvolutionRightUsed && item is SuperEvolveEvent { IsManualEvolution: false } evolved
                    && evolved.SuperEvolvedCard is { } card && card.InstanceId == unit
                    && card.OwnerPlayerId == lease.Request.PlayerId && evolved.EvolutionSource?.Card?.InstanceId == unit)
                    lease.SelfSuperEvolution = true;
            }
            lease.Matched |= lease.ConvertedEvolution;
        }
        var own=game.GetPlayerState(lease.Request.PlayerId);var enemy=game.GetOpponentPlayerState(lease.Request.PlayerId);
        if(own==null||enemy==null)return;
        lease.After=new(own.Leader?.CurrentHealth,enemy.Leader?.CurrentHealth,own.CurrentPlayPoints,own.MaxPlayPoints,
            own.CurrentEvolutionPoints,own.CurrentSuperEvolutionPoints,own.IsEvolutionUnlocked,own.IsSuperEvolutionUnlocked,
            own.IsPpBoostArmed,own.PpBoostUsesAvailable,own.Hand.Count,enemy.Hand.Count,own.Field.Count,enemy.Field.Count,
            own.GraveyardCount,enemy.GraveyardCount,Side(game,lease.Request.PlayerId),own.CanEvolve,own.CanSuperEvolve);
        lease.After=AiPublicResourceContext.Capture(lease.After,own,enemy);
        AiObservedBody? Body(Guid? uid) {
            var card=uid.HasValue?game.FindCardInstance(uid.Value):null;
            if(card==null||card.CurrentZone is not (CardZone.Field or CardZone.Hand))return null;
            // Ownership can change during the action. Hidden enemy zones must still stay hidden.
            if(card.OwnerPlayerId!=lease.Request.PlayerId&&card.CurrentZone!=CardZone.Field)return null;
            return new(card.CurrentDefinitionId,card.CurrentZone.ToString(),card.CurrentAttack,card.CurrentHealth);
        }
        lease.HandAfter=Capture(own,game);lease.Choices.Observe(lease,game);
        lease.ActorAfter=Body(lease.ActorUid);lease.TargetAfter=Body(lease.TargetUid);
        lease.Result=game.Status==GameStatus.Finished?(game.WinnerId==own.PlayerId?"won":game.WinnerId==enemy.PlayerId?"lost":"draw"):"not-ended";
    }

    internal static AiCompletedActionFact? Finish(Lease lease)=>lease.Completed&&lease.Matched&&lease.After!=null
        ?new(lease.Turn,lease.Action,lease.Before,lease.After,lease.ActorBefore,lease.ActorAfter,lease.TargetBefore,lease.TargetAfter,lease.Result,Compare(lease.HandBefore,lease.HandAfter),lease.Choices.Snapshot(),lease.Decision)
            {
                EvolutionSettlement = lease.ConvertedEvolution ? "super-evolved-via-ordinary-request" : null,
                EndTurnDraws = lease.EndTurnDraws?.Snapshot()
            } : null;
    private static string Side(GameState game,Guid own)=>game.CurrentPlayerId==own?"own":game.CurrentPlayerId==null?"none":"enemy";

    internal static object? ForModel(AiCompletedActionFact[]? facts,bool recentOnly=false)=>Pack(facts,recentOnly,null);
    internal static object? ForModelAtIndex(AiCompletedActionFact[]? facts,int index)=>Pack(facts,false,index);
    internal static object? ForModelWithTargetFocus(AiCompletedActionFact[]? facts,int index,int[] focus)=>Pack(facts,false,index,focus);
    private static object ActionForModel(AiStrategyPlanner.ActionOption action) => new
    {
        action.Kind, action.CardId, action.TargetCardId, action.HandIndex, action.FieldIndex,
        action.TargetFieldIndex, action.PlayMode, action.PaidCost, action.AbilityName
    };

    internal static int[] ModelSampleIndices(AiCompletedActionFact[] facts, bool recentOnly = false, int? focused = null)
    {
        var defaults=(facts.Length<=4?Enumerable.Range(0,facts.Length):recentOnly?Enumerable.Range(facts.Length-4,4):new[]{0,1,facts.Length-2,facts.Length-1}).ToArray();
        if (facts.Length > 4 && !recentOnly)
            defaults = new[] { 0 }.Concat(Enumerable.Range(1, facts.Length - 2)
                .Where(index => facts[index].Decision?.Different == true).TakeLast(2).Reverse())
                .Concat(Enumerable.Range(1,facts.Length-2).Reverse().Where(index=>facts[index].EndTurnDraws is
                    { DeckExhausted: true } or { Batches.Length: >1 }).Take(1))
                .Concat(defaults.Reverse()).Distinct().Take(4).Order().ToArray();
        // Kept neighbors may have omitted events between them. Do not claim they are consecutive native roots.
        return focused is int focus?new[]{focus,focus+1,focus-1,facts.Length-1}.Concat(defaults)
            .Where(index=>index>=0&&index<facts.Length).Distinct().Take(4).Order().ToArray():defaults;
    }

    private static object? Pack(AiCompletedActionFact[]? facts,bool recentOnly,int? requestedIndex,int[]? targetFocus=null)
    {
        if(facts==null)return null;
        if(facts.Length==0)return new {retained=0,included=0,omitted=0,samples=Array.Empty<object>(),scope="No completed root outcomes recorded in this observation window; not proof of no actions."};
        var focused=requestedIndex is int requested&&requested>=0&&requested<facts.Length?(int?)requested:null;
        var indices = ModelSampleIndices(facts, recentOnly, focused);
        object?[] Values(AiObservedResources value)=>[value.OwnHealth,value.EnemyHealth,value.PP,value.MaxPP,value.EP,value.SEP,
            value.EvolutionUnlocked,value.SuperEvolutionUnlocked,value.BoostArmed,value.BoostRemaining,value.OwnHand,value.EnemyHand,
            value.OwnField,value.EnemyField,value.OwnGraveyard,value.EnemyGraveyard,value.ActiveSide,value.CanEvolve,value.CanSuperEvolve,value.BloodPool,value.OwnEmblemIds,value.EnemyEmblemIds,value.OwnEmblemsOmitted,value.EnemyEmblemsOmitted];
        return new {retained=facts.Length,included=indices.Length,omitted=facts.Length-indices.Length,retrievalFocusedIndex=focused,
            selectionScope=focused==null?"Bounded opening/recent excerpts, prioritizing settled differences for historical lessons.":"Matched retained action plus bounded retained neighbors; neighbors may have gaps and are not proof of a continuous combo.",
            ownHandScope="Own hand endpoint differences only; indexes belong to their endpoint. No draw/generation/discard cause, transient-card, complete attached-ability or playability claim. Null means unavailable, empty changes means no differences in observed endpoint fields.",
            columns=new[]{"ownHealth","enemyHealth","pp","maxPP","ep","sep","evolutionUnlocked","superEvolutionUnlocked","boostArmed","boostRemaining",
                "ownHand","enemyHand","ownField","enemyField","ownGraveyard","enemyGraveyard","activeSide","canEvolve","canSuperEvolve","ownBloodPool","ownEmblemIds","enemyEmblemIds","ownEmblemsOmitted","enemyEmblemsOmitted"},
            samples=indices.Select(index=>(index,fact:facts[index])).Select(row=>new {sourceIndex=row.index,row.fact.Turn,action=ActionForModel(row.fact.Action),
                decision=row.fact.Decision is {} decision?new {decision.Policy,decision.Different,local=ActionForModel(decision.Local)}:null,
                before=Values(row.fact.Before),after=Values(row.fact.After),row.fact.ActorBefore,row.fact.ActorAfter,row.fact.TargetBefore,row.fact.TargetAfter,row.fact.Result,row.fact.EvolutionSettlement,row.fact.OwnHandChanges,row.fact.EndTurnDraws,choiceTargets=AiTargetOutcomes.ForModel(row.fact.ChoiceTargets,targetFocus)}),
            endTurnDrawScope="Committed own draw requests in the selected end-turn action. Resolved counts reflect native draw targets, not cards retained in hand. Source IDs require an own public card at submission and a matching completed effect or the short draw's exact native runtime ability. A null source is unverified. ExhaustedBatchIndex is zero-based and present only for a unique short batch with a committed own deck-empty event. OwnDeckBefore is only the actor's deck size before submission; null means it was not recorded.",
            resourceScope="ownBloodPool is the recorded actor counter, null means not collected, never zero by inference. Emblem arrays contain public definition IDs, including duplicate copies; null means unknown, [] with omitted=0 means no emblems. Each side is limited to 32 entries, positive omitted means incomplete. No abilities, cause of addition or continuing activation are inferred from presence.",
            scope="Before is the pre-submission decision snapshot; after is the last committed transaction state inside the matched completed root, including triggered responses. Not causal attribution, counterfactual alternatives or win-rate proof."};
    }
}
