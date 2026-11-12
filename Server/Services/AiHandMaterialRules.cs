using System.Text.Json;
using System.Text.RegularExpressions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

// A bounded current-hand feasibility aid, not a simulator of responses or future generated cards.
internal static class AiHandMaterialRules
{
    internal sealed record Step(int HandIndex,int CardId,string Mode,int Cost,string? DiscardKey,
        long? AbilityDefinitionId,bool BlocksPlayWithoutMaterial,bool ClosedVisibleHandAfterPlay,string Scope);
    internal sealed record Witness(int Step,int DiscardedHandIndex);
    internal sealed record Projection(bool Complete,Witness[] HypotheticalDiscards,int[]? HypotheticalRemainingHandIndexes,
        string Scope="Current known-hand feasibility only. Discard assignments are hypothetical witnesses, not submitted choices or committed effects. Unknown draw/mode/reaction/hand changes stop further material forecasting.");
    private static string Normal(string? s)=>Regex.Replace(s??"","[\\s()]","").ToLowerInvariant();
    private static bool Plain(AbilityConfig c)=>c.IsEnabled&&c.Type==AbilityType.Triggered&&c.Trigger.IsEnabled
        &&c.Trigger.ValidZones.Count==0&&c.AbilityKeywords.All(k=>k.Keyword!=AbilityKeyword.Intromezzo)
        &&c.Conditions.All(x=>!x.IsEnabled)&&c.PreConditions.All(x=>!x.IsEnabled)&&c.Trigger.CountLimit==null
        &&!c.Trigger.LimitSameNameOncePerTransaction&&c.Properties.Count==0&&c.CounterRegistrations.Count==0
        &&c.PlayerCounterRegistrations.Count==0&&c.Auras.All(x=>!x.IsEnabled)&&c.ActivateCost==null&&c.AlternatePlay==null;
    private static bool Deferred(AbilityConfig c)=>!c.IsEnabled || c.Type==AbilityType.Triggered
        &&c.Trigger.IsEnabled&&c.Trigger.EventTypes.Count>0&&c.Trigger.EventTypes.All(e=>e is EventType.TurnStart or EventType.TurnEnd)
        &&c.Auras.All(x=>!x.IsEnabled)&&c.Properties.Count==0&&c.CounterRegistrations.Count==0&&c.PlayerCounterRegistrations.Count==0;
    private static bool Simple(EffectConfig e)=>e.RepeatCount==0&&e.RepeatCountValue==null&&e.RepeatCondition==null
        &&e.ChainedAbilities.Count==0&&e.Outputs?.Count is not >0;
    private static bool OwnPool(TargetConfig t)=>t.Query.Operations.Count==0&&Normal(t.Query.SourcePoolExpression) is
        "context.owner.hand.queryself.type==\"follower\"||self.type==\"spell\"||self.type==\"amulet\"~context.sourcecard";
    private static string? Text(EffectConfig e,string key)=>e.Parameters.GetValueOrDefault(key) is StaticValueConfig s
        ?s.Value is JsonElement {ValueKind:JsonValueKind.String} j?j.GetString():s.Value as string:null;
    private static bool DeferredEmblem(EffectConfig e)
    {
        if(e.Type!=EffectType.AddEmblem||!Simple(e)||e.TargetSelector.Count>0
            ||Text(e,"PlayerScope")!="self"||Text(e,"CardSourceMode")!="definition")return false;
        var raw=e.Parameters.GetValueOrDefault("CardDefinitionId") is StaticValueConfig literal?literal.Value:null;
        var ids=raw is JsonElement value?value:JsonSerializer.SerializeToElement(raw);
        if(ids.ValueKind!=JsonValueKind.Array||ids.GetArrayLength() is <1 or >8)return false;
        foreach(var item in ids.EnumerateArray()){
            if(!item.TryGetInt32(out var id)||id<=0||GameDataProvider.Instance.GetCardDefinition(id)==null)return false;
            var configs=GameDataProvider.Instance.GetAbilityConfigs(id);
            if(configs==null||configs.Count>32||configs.Any(c=>!AiRuntimeAbilityFacts.SafeConfiguration(c)||!Deferred(c)))return false;
        }
        return true;
    }
    // A plain follower changes only the hand and board before the next forecast. Check both
    // Hand and Field registrations on it because playing the card changes its zone.
    internal static bool IsQuietLastFollowerPlay(GameContext context, CardInstance source)
    {
        var game = context.CurrentGameState;
        if (source.CardType != CardType.Follower || source.CurrentZone != CardZone.Hand || source.OwnerPlayerId != context.Owner.PlayerId
            || context.Owner.Hand is not [var last] || !ReferenceEquals(source, last))
            return false;

        bool Quiet(CardInstance card, IAbility runtime)
        {
            if (runtime is not Ability ability) return false;
            var config = ability.Config;
            if (!config.IsEnabled) return true;
            if (card.InstanceId == source.InstanceId)
                return Deferred(config) && config.Trigger.EventTypes.All(type => type == EventType.TurnStart);

            if (AiHandClearEvolution.IsNativeLeaderCounter(card, config)) return true;
            return Deferred(config) || config.Type == AbilityType.Triggered && config.Auras.All(aura => !aura.IsEnabled)
                && !config.Trigger.IsValidZone(card.CurrentZone);
        }

        // Other cards here are already public and stay in their current zones during this play.
        var visible = game.AllField.Concat(game.AllEmblem).Concat(game.AllLeaders).Append(source);
        return visible.All(card => context.BuffManager.GetBuffsForCard(card.InstanceId).All(buff => !buff.IsActive)
            && context.AbilityManager.GetEffectiveAbilitiesForCard(card.InstanceId, context).All(runtime => Quiet(card, runtime)));
    }

    internal static Step[] Read(GameState game,IReadOnlyList<AiPlayCandidates.Candidate> candidates)
    {
        var context=GameServiceProvider.CurrentGameContext;
        if(context==null||!ReferenceEquals(context.CurrentGameState,game))return [];
        var own=context.Owner;var enemy=game.GetOpponentPlayerState(own.PlayerId);
        if(enemy==null)return [];
        bool Registered(IEnumerable<CardInstance> cards,Guid owner,CardZone zone)=>cards.All(c=>c.OwnerPlayerId==owner&&c.CurrentZone==zone);
        if(!Registered(own.Hand,own.PlayerId,CardZone.Hand)||!Registered(own.Field,own.PlayerId,CardZone.Field)
            ||!Registered(enemy.Field,enemy.PlayerId,CardZone.Field)||!Registered(own.Emblem,own.PlayerId,CardZone.Emblem)
            ||!Registered(enemy.Emblem,enemy.PlayerId,CardZone.Emblem)
            ||own.Leader is {} ownLeader&&(ownLeader.OwnerPlayerId!=own.PlayerId||ownLeader.CurrentZone!=CardZone.Field)
            ||enemy.Leader is {} enemyLeader&&(enemyLeader.OwnerPlayerId!=enemy.PlayerId||enemyLeader.CurrentZone!=CardZone.Field))return [];
        // Enemy hand and deck are deliberately absent. Unknown responses are never credited as material supply.
        var visible=own.Hand.Concat(own.Field).Concat(enemy.Field).Concat(own.Emblem).Concat(enemy.Emblem)
            .Concat(new[]{own.Leader,enemy.Leader}.OfType<CardInstance>()).DistinctBy(c=>c.InstanceId).Take(65).ToArray();
        if(visible.Length>64)return [];
        var configs=new Dictionary<Guid,AbilityConfig[]?>();
        foreach(var card in visible){
            try{
                var abilities=context.AbilityManager.GetEffectiveAbilitiesForCard(card.InstanceId,context).Take(65).ToArray();
                configs[card.InstanceId]=abilities.Length>64||abilities.Any(a=>a is not Ability)?null:abilities.Cast<Ability>().Select(a=>a.Config).ToArray();
            }catch(Exception e) when(e is not OperationCanceledException and not OutOfMemoryException){configs[card.InstanceId]=null;}
        }
        var uncertain=visible.Where(c=>configs[c.InstanceId] is not {} a||a.Any(x=>!AiRuntimeAbilityFacts.SafeConfiguration(x)
            ||!Deferred(x)&&!AiSelectedHandLoss.DirectRoot(x))).Select(c=>c.InstanceId).ToHashSet();
        var results=new List<Step>();
        foreach(var candidate in candidates){
            try{
                var actual=own.Hand.SingleOrDefault(c=>c.InstanceId==candidate.Original.InstanceId);
                if(actual==null||actual.IsSilenced||actual.OwnerPlayerId!=own.PlayerId)continue;
                var source=candidate.PlayedCardId==actual.CurrentDefinitionId?configs.GetValueOrDefault(actual.InstanceId)
                    :GameDataProvider.Instance.GetAbilityConfigs(candidate.PlayedCardId)?.ToArray();
                if(source==null||source.Length>64||source.Any(c=>!AiRuntimeAbilityFacts.SafeConfiguration(c)))continue;
                var discards=source.Where(c=>Plain(c)&&AiSelectedHandLoss.DirectRoot(c)).SelectMany(c=>c.Targets
                    .Where(t=>OwnPool(t.Value)&&AiSelectedHandLoss.DiscardsSelection(c,t.Key)).Select(t=>(Config:c,Key:t.Key,Target:t.Value))).ToArray();
                var direct=source.Where(c=>c.IsEnabled&&!Deferred(c)).ToArray();
                var closed=!uncertain.Any(id=>id!=actual.InstanceId)&&direct.All(c=>Plain(c)&&AiSelectedHandLoss.DirectRoot(c)
                    &&c.Effects.Where(e=>e.IsEnabled).All(e=>e.Type==EffectType.Discard&&discards.Length==1
                        &&ReferenceEquals(c,discards[0].Config)&&Simple(e)&&e.Parameters.Count==0
                        ||DeferredEmblem(e)));
                var rule=discards.Length==1?discards[0]:default;
                results.Add(new(candidate.HandIndex,candidate.Original.CardId,candidate.Mode.ToString(),candidate.Cost,
                    discards.Length==1?rule.Key:null,discards.Length==1?rule.Config.Id:null,
                    discards.Length==1&&rule.Target.ShouldBlockPlayerOperationOnInsufficientTargets(),closed,
                    closed?"visible-no-hand-gain-prefix":"unknown-after-play"));
            }catch(Exception e) when(e is not OperationCanceledException and not OutOfMemoryException){/* Missing facts remain unknown. */}
        }
        return results.ToArray();
    }

    internal static AiPlaySequence.Check Validate(AiStrategyPlanner.TurnPlan plan,IReadOnlyList<AiPlayCandidates.Candidate> candidates,
        IReadOnlyList<AiStrategyPlanner.HandLossRule> losses,PlayerStateDto own,IReadOnlyList<AiSelectedHandLoss.Rule>? selected,
        IReadOnlyList<AiPpRefundFacts.Refund>? refunds,IReadOnlyList<Step> materials)
    {
        var initial=AiPlaySequence.Validate(plan,candidates,losses,own,selected,refunds);
        if(!initial.Valid||plan.Plays is not {Length:>0} line)return initial;
        var remaining=Enumerable.Range(0,own.Hand.Count).ToHashSet();var witnesses=new List<Witness>();var known=true;
        AiPlaySequence.Check Fail(int i,string error)=>initial with{Valid=false,Error=$"第{i+1}步的已知手牌素材约束未满足：{error}。未知抽牌或其他能力不预支为现有素材，实际仍须原生核对。",
            HandMaterials=new(true,witnesses.ToArray(),remaining.Order().ToArray())};
        for(var i=0;i<line.Length&&known;i++){
            var play=line[i];var facts=materials.SingleOrDefault(s=>s.HandIndex==play.HandIndex&&s.CardId==play.CardId&&s.Mode==play.Mode&&s.Cost==play.Cost);
            if(facts==null){known=false;break;}
            int? discard=AiSelectedHandLoss.IntendedDiscard(play,own,selected??[]);
            if(facts.DiscardKey!=null){
                var pool=remaining.Where(j=>j!=play.HandIndex&&own.Hand[j].Type is CardType.Follower or CardType.Spell or CardType.Amulet).ToArray();
                if(pool.Length==0&&facts.BlocksPlayWithoutMaterial)return Fail(i,"需要另一张可舍弃的手牌，但此前步骤后已没有素材");
                if(discard.HasValue&&!pool.Contains(discard.Value))return Fail(i,"所选弃牌素材不在该能力已识别的己方其他手牌范围内");
                if(discard==null&&pool.Length>0&&facts.ClosedVisibleHandAfterPlay){
                    var future=line.Skip(i+1).Select(s=>s.HandIndex).ToHashSet();
                    var expendable=pool.Where(j=>!future.Contains(j)).OrderByDescending(j=>losses.Any(l=>l.DiscardedHandIndex==j&&l.WhenPlayedHandIndexes.Contains(play.HandIndex))).ThenBy(j=>j).ToArray();
                    if(expendable.Length==0)return Fail(i,"所有可舍弃素材都还要在后续步骤使用，不能同时保留并舍弃同一实例");
                    discard=expendable[0];witnesses.Add(new(i,discard.Value));
                }
            }
            remaining.Remove(play.HandIndex);if(discard.HasValue)remaining.Remove(discard.Value);
            foreach(var loss in losses.Where(l=>l.WhenPlayedHandIndexes.Contains(play.HandIndex)))remaining.Remove(loss.DiscardedHandIndex);
            known=facts.ClosedVisibleHandAfterPlay;
        }
        return initial with{HandMaterials=new(known,witnesses.ToArray(),known?remaining.Order().ToArray():null)};
    }

    internal static AiStrategyPlanner.PlayAdvice[][] Reorderings(AiStrategyPlanner.TurnPlan plan,IReadOnlyList<AiPlayCandidates.Candidate> candidates,
        IReadOnlyList<AiStrategyPlanner.HandLossRule> losses,PlayerStateDto own,IReadOnlyList<AiSelectedHandLoss.Rule>? selected,
        IReadOnlyList<AiPpRefundFacts.Refund>? refunds,IReadOnlyList<Step> materials)
    {
        if(plan.Plays is not {Length:>1 and <=4} line||line.Any(s=>s==null))return [];
        var results=new List<AiStrategyPlanner.PlayAdvice[]>();
        void Visit(AiStrategyPlanner.PlayAdvice[] prefix,AiStrategyPlanner.PlayAdvice[] rest){
            if(rest.Length==0){if(Validate(plan with{Play=prefix[0],Plays=prefix},candidates,losses,own,selected,refunds,materials).Valid)results.Add(prefix);return;}
            for(var i=0;i<rest.Length;i++)Visit(prefix.Append(rest[i]).ToArray(),rest.Where((_,j)=>i!=j).ToArray());
        }
        Visit([],line);return results.Take(6).ToArray();
    }
}
