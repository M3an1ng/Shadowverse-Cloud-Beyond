using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

// A single plain immediate attacker can be an answer to one visible lethal body.
// This is not a future-hand generator, a full choice search, or a general survival proof.
internal static class AiGeneratedDefenseModes
{
    internal sealed record Option(int Index,int CardId,int Cost,bool SimpleImmediateFollower,bool Affordable,bool RemovesVisibleAttacker);
    internal sealed record Facts(int VisibleDamage,int OwnHealth,int AttackerCardId,bool ExistingAnswer,int? PreferredIndex,Option[] Options,
        bool Preflight,int ParentSelfDamage,int[] PendingTokenIds,int CurrentOwnHealth,int AvailablePP,
        string Scope="One visible enemy follower, static parent self-damage/cost and plain immediate attacker only. PendingTokenIds are earlier proposed choices in this same native effect invocation, NOT generated or played cards. No hidden cards, unknown responses or full survival/turn-end guarantee. PreferredIndex is this request's option index.");
    private sealed record Position(Guid Event,int Effect,int Execution,bool Preflight,AbilityConfig Parent);
    private sealed class Pending { internal Guid Event,Source;internal int Effect,Turn;internal long Revision;internal List<int> Tokens=[]; }
    private static readonly ConditionalWeakTable<PlayerChoiceRequest,Position> Positions=new();
    private static readonly ConditionalWeakTable<GameState,Pending> PendingChoices=new();
    // Native request metadata only; does not change any event, option, cost or engine decision.
    internal static void Bind(PlayerChoiceRequest request,Effect effect,Ability parent,bool preflight)
    {
        Positions.Add(request,new(effect.TriggeredEventId,effect.EffectIndex,effect.ExecutionIndex,preflight,parent.Config));
        // Generic model context is independent of the optional defensive heuristic.
        AiDecisionFlowFacts.Bind(request,effect,parent,preflight);
    }
    internal static void Remember(GameState? game,PlayerChoiceRequest request,IReadOnlyList<Guid> chosen)
    {
        AiDecisionFlowFacts.Remember(game,request,chosen);
        if(game==null||chosen.Count!=1||!Positions.TryGetValue(request,out var position)||!position.Preflight
            ||!PendingChoices.TryGetValue(game,out var pending)||pending.Event!=position.Event||pending.Effect!=position.Effect
            ||pending.Source!=request.OptionSourceCard?.InstanceId||pending.Revision!=game.StateRevision||pending.Tokens.Count!=position.Execution
            ||!request.DecisionOptions.TryGetValue(chosen[0],out var config)||AiGeneratedTokenTactics.Token(config) is not int token)return;
        pending.Tokens.Add(token);
    }
    private static string Compact(string value)=>Regex.Replace(value,@"[\s()]","").ToLowerInvariant();
    private static string? Literal(ShadowverseBeyond.Server.Domain.Abilities.Effects.EffectConfig effect,string key)=>
        effect.Parameters.FirstOrDefault(pair=>pair.Key.Equals(key,StringComparison.OrdinalIgnoreCase)).Value is StaticValueConfig value?value.Value?.ToString():null;
    private static bool PlainEffect(ShadowverseBeyond.Server.Domain.Abilities.Effects.EffectConfig effect)=>effect.RepeatCount==0
        &&effect.RepeatCountValue==null&&effect.RepeatCondition==null&&effect.ChainedAbilities.Count==0&&effect.Outputs?.Count is not >0;
    private static bool HarmlessLifeRule(AbilityConfig config)
    {
        if(config.Type!=AbilityType.Triggered||config.Auras.Any(aura=>aura.IsEnabled)||config.Properties.Count>0
            ||config.CounterRegistrations.Count>0||config.PlayerCounterRegistrations.Count>0||config.AbilityKeywords.Count>0)return false;
        var trigger=config.Trigger;var effects=config.Effects.Where(effect=>effect.IsEnabled).ToArray();
        if(effects.Length!=1||!PlainEffect(effects[0])||trigger.EventTypes.Count!=1)return false;
        var effect=effects[0];var targets=config.Targets.Values.Where(target=>target.IsEnabled).ToArray();
        if(trigger.EventTypes[0]==EventType.TurnEnd&&effect.Type==EffectType.Vanish&&effect.Parameters.Count==0&&effect.TargetSelector.Count==0
            &&targets.Length==1&&targets[0].Query.Operations.Count==0&&targets[0].PlayerChoiceOptions==null
            &&Compact(targets[0].Query.SourcePoolExpression)=="context.sourcecard")return true;
        return trigger.EventTypes[0]==EventType.LeaveField&&trigger.Timing==TriggerTiming.Before&&trigger.RelationType==TriggerRelationType.TargetSelf
            &&targets.Length==0&&effect.Type==EffectType.RewriteEventParams&&effect.Parameters.Count==2&&effect.TargetSelector.Count==0
            &&Literal(effect,"ParamName")=="RequestedKind"&&Literal(effect,"Value")=="Vanish";
    }
    private static bool SimpleRules(IEnumerable<AbilityConfig>? configs)=>configs!=null&&configs.Where(config=>config.IsEnabled).All(HarmlessLifeRule);
    private static bool SimpleKeywords(IEnumerable<CardKeyword> keywords)=>keywords.All(key=>key is CardKeyword.Storm or CardKeyword.Rush)
        &&keywords.Any(key=>key is CardKeyword.Storm or CardKeyword.Rush);
    private static bool ResourceOnly(AbilityConfig config,int depth=0)
    {
        if(depth>6||config.Auras.Any(aura=>aura.IsEnabled)||config.Properties.Count>0
            ||config.CounterRegistrations.Count>0||config.PlayerCounterRegistrations.Count>0)return false;
        return config.Effects.Where(effect=>effect.IsEnabled).All(effect=>
            effect.Type is EffectType.None or EffectType.Draw or EffectType.GetToken or EffectType.AddDeck
            &&effect.ChainedAbilities.Count<=8&&effect.ChainedAbilities.All(chain=>ResourceOnly(chain,depth+1)));
    }
    private static bool FutureResponsesHarmless(GameContext context,Guid source)
    {
        foreach(var card in context.CurrentGameState.AllField.Concat(context.CurrentGameState.AllEmblem).Concat(context.CurrentGameState.AllLeaders))
        foreach(var ability in context.AbilityManager.GetEffectiveAbilitiesForCard(card.InstanceId,context).OfType<Ability>()){
            if(card.InstanceId==source||!ability.Config.IsEnabled||!ability.Trigger.IsEnabled||!ability.TriggerConfig.IsValidZone(card.CurrentZone))continue;
            if(!ability.TriggerConfig.EventTypes.Any(type=>type is EventType.TurnEnd or EventType.TurnStart or EventType.LeaveField))continue;
            // Existing resource emblems may draw/refill. Do not silently assume a board-affecting future response.
            if(!ResourceOnly(ability.Config))return false;
        }
        return true;
    }
    private static bool Kills(CardInstanceDto attacker,CardInstanceDto target)=>!target.Keywords.Contains(CardKeyword.Shield)
        &&!attacker.Keywords.Any(key=>key is CardKeyword.Shackle or CardKeyword.CannotAttackFollower)
        &&attacker.CurrentAttack>=target.CurrentHealth;
    private static bool Same(IReadOnlyCollection<CardInstance> native,IReadOnlyCollection<CardInstanceDto> projected)=>
        native.Count==projected.Count&&native.All(card=>projected.SingleOrDefault(value=>value.InstanceId==card.InstanceId) is {} value
            &&value.CardId==card.CurrentDefinitionId&&value.CurrentCost==card.CurrentCost&&value.CurrentAttack==card.CurrentAttack
            &&value.CurrentHealth==card.CurrentHealth&&value.CanAttack==card.CanAttack
            &&value.Keywords.OrderBy(key=>key).SequenceEqual(card.Keywords.OrderBy(key=>key)));
    internal static Facts? Read(GameContext? context,GameState game,GameStateDto snapshot,PlayerChoiceRequest request,CardInstance? source)
    {
        var actor=request.RequestingPlayerId;
        if(context==null||!ReferenceEquals(context.CurrentGameState,game)||game.CurrentPlayerId!=actor||game.IsGameOver||game.Phase!=GamePhase.Main
            ||game.LegacyAiPlayerId==actor||source==null||source.OwnerPlayerId!=actor||request.TargetKey!="decision"
            ||request.MinSelectionCount!=1||request.MaxSelectionCount!=1||request.SelectableTargetIds.Count is <2 or >12
            ||request.SelectableTargetIds.Any(id=>id==Guid.Empty)||request.SelectableTargetIds.Distinct().Count()!=request.SelectableTargetIds.Count)return null;
        var own=snapshot.Players.SingleOrDefault(player=>player.PlayerId==actor);var enemy=snapshot.Players.SingleOrDefault(player=>player.PlayerId!=actor);
        if(own?.LeaderCard is not {CurrentHealth:>0} ownLeader||enemy==null||snapshot.GameId!=game.GameId)return null;
        var nativeOwn=game.GetPlayerState(actor);var nativeEnemy=game.GetPlayerState(enemy.PlayerId);
        if(nativeOwn==null||nativeEnemy==null||nativeOwn.CurrentPlayPoints!=own.CurrentPP||nativeOwn.IsPpBoostArmed!=own.IsPpBoostArmed
            ||nativeOwn.Leader.CurrentHealth!=ownLeader.CurrentHealth||!Same(nativeOwn.Hand,own.Hand)
            ||!Same(nativeOwn.Field,own.Field)||!Same(nativeEnemy.Field,enemy.Field))return null;
        if(!Positions.TryGetValue(request,out var position))return null;
        var preflight=position.Preflight;
        var parentDamage=0;var budget=own.CurrentPP+(own.IsPpBoostArmed?GameConstants.PlayPoints.PpBoostAmount:0);
        var handCount=own.Hand.Count;var pendingIds=Array.Empty<int>();
        if(preflight){
            if(source.CurrentZone!=CardZone.Hand||position!.Event==Guid.Empty||position.Execution is <0 or >4)return null;
            var configs=context.AbilityManager.GetEffectiveAbilitiesForCard(source.InstanceId,context).ToArray();
            if(configs.Any(ability=>ability is not Ability))return null;
            var recipes=AiGeneratedTokenTactics.Read(GameDataProvider.Instance,source.CurrentDefinitionId,configs.Cast<Ability>().Select(ability=>ability.Config));
            var recipe=recipes.FirstOrDefault();
            if(recipe==null||position.Execution>=recipe.Count||!configs.Cast<Ability>().Any(ability=>ReferenceEquals(ability.Config,position.Parent))
                ||ownLeader.Keywords.Count>0)return null;
            parentDamage=recipe.SelfDamage;budget-=source.CurrentCost;handCount+=position.Execution-1;
            var pending=PendingChoices.GetOrCreateValue(game);
            if(position.Execution==0){pending.Event=position.Event;pending.Effect=position.Effect;pending.Source=source.InstanceId;
                pending.Turn=nativeOwn.TurnNumber;pending.Revision=game.StateRevision;pending.Tokens.Clear();}
            if(pending.Event!=position.Event||pending.Effect!=position.Effect||pending.Source!=source.InstanceId
                ||pending.Turn!=nativeOwn.TurnNumber||pending.Revision!=game.StateRevision||pending.Tokens.Count!=position.Execution)return null;
            pendingIds=pending.Tokens.ToArray();
        }
        var health=ownLeader.CurrentHealth-parentDamage;if(health<=0||budget<0)return null;
        var damage=AiTacticalSearch.EstimateBoardDamage(enemy.Field,own.Field,ownLeader);
        if(damage<health||enemy.Field.Count(card=>card.Type==CardType.Follower&&card.CurrentHealth>0)!=1)return null;
        var target=enemy.Field.Single(card=>card.Type==CardType.Follower&&card.CurrentHealth>0);
        if(target.Keywords.Any(key=>key is CardKeyword.Ambush or CardKeyword.Pressure)
            ||AiGeneratedTokenTactics.HasInterference(context,source)||!FutureResponsesHarmless(context,source.InstanceId))return null;
        var options=new List<Option>();
        var fieldSpace=game.GetPlayerState(actor)!.FieldOccupancyCount<GameConstants.Field.MaxFieldSize;
        for(var index=0;index<request.SelectableTargetIds.Count;index++){
            if(!request.DecisionOptions.TryGetValue(request.SelectableTargetIds[index],out var branch)||AiGeneratedTokenTactics.Token(branch) is not int id)return null;
            var definition=GameDataProvider.Instance.GetCardDefinition(id);if(definition==null)return null;
            var keys=(definition.Keywords??[]).Select(text=>Enum.TryParse<CardKeyword>(text,true,out var key)?key:CardKeyword.None).ToArray();
            var simple=definition is {Type:CardType.Follower,Cost:>=0 and <=1000,Attack:>0 and <=1000,Health:>0 and <=1000}
                &&SimpleKeywords(keys)&&SimpleRules(GameDataProvider.Instance.GetAbilityConfigs(id));
            var affordable=definition.Cost<=budget&&handCount<GameConstants.Hand.MaxHandSize&&fieldSpace;
            var answer=simple&&affordable&&!target.Keywords.Contains(CardKeyword.Shield)&&definition.Attack>=target.CurrentHealth;
            options.Add(new(index,id,definition.Cost,simple,affordable,answer));
        }
        var existing=own.Field.Any(card=>card.Type==CardType.Follower&&card.CanAttack&&Kills(card,target)
            &&AiService.LegalAttackTargets(card,enemy).Any(candidate=>candidate.InstanceId==target.InstanceId));
        if(!existing&&fieldSpace)foreach(var card in own.Hand){
            if(card.Type!=CardType.Follower||card.CurrentCost>budget||!SimpleKeywords(card.Keywords)||!Kills(card,target))continue;
            var actual=game.FindCardInstance(card.InstanceId);
            if(actual==null||context.BuffManager.GetBuffsForCard(card.InstanceId).Count>0)continue;
            var runtime=context.AbilityManager.GetEffectiveAbilitiesForCard(card.InstanceId,context).ToArray();
            if(runtime.All(ability=>ability is Ability)&&SimpleRules(runtime.Cast<Ability>().Select(ability=>ability.Config))){existing=true;break;}
        }
        existing|=fieldSpace&&!target.Keywords.Contains(CardKeyword.Shield)&&pendingIds.Select((id,index)=>(id,index)).Any(prior=>
            own.Hand.Count-1+prior.index<GameConstants.Hand.MaxHandSize&&options.Any(option=>option.CardId==prior.id
                &&option.SimpleImmediateFollower&&option.Cost<=budget&&GameDataProvider.Instance.GetCardDefinition(prior.id)?.Attack>=target.CurrentHealth));
        var preferred=existing?null:options.Where(option=>option.RemovesVisibleAttacker).OrderBy(option=>option.Cost).ThenBy(option=>option.Index).FirstOrDefault();
        return new(damage,health,target.CardId,existing,preferred?.Index,options.ToArray(),preflight,parentDamage,pendingIds,ownLeader.CurrentHealth,budget);
    }
}
