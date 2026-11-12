using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

// Intent consistency only: a card explicitly selected for discard cannot also be a later play.
// This does not assert the discard will resolve or bypass the native target pool and responses.
internal static class AiSelectedHandLoss
{
    internal sealed record Rule(int HandIndex,int CardId,int PlayedCardId,string Mode,int Cost,string Key,
        long AbilityDefinitionId,bool UnambiguousWithoutAbilityId);
    internal static bool DirectRoot(AbilityConfig config)=>config.IsEnabled&&config.Type==AbilityType.Triggered
        &&config.Trigger.IsEnabled&&config.Trigger.Timing==TriggerTiming.On
        &&config.Trigger.RelationType==TriggerRelationType.TargetSelf
        &&config.Trigger.EventTypes.SequenceEqual(new[]{EventType.PlayCard});
    internal static bool DiscardsSelection(AbilityConfig config,string key)
    {
        if(!DirectRoot(config)||config.Conditions.Any(c=>c.IsEnabled)||config.PreConditions.Any(c=>c.IsEnabled)
            ||config.Trigger.ValidZones.Count>0||config.AbilityKeywords.Any(k=>k.Keyword==AbilityKeyword.Intromezzo)
            ||config.Properties.Count>0||config.CounterRegistrations.Count>0||config.PlayerCounterRegistrations.Count>0
            ||config.Trigger.CountLimit!=null||config.Trigger.LimitSameNameOncePerTransaction
            ||config.ActivateCost!=null||config.AlternatePlay!=null||config.Auras.Any(a=>a.IsEnabled)
            ||!AiRuntimeAbilityFacts.SafeConfiguration(config)
            ||!config.Targets.TryGetValue(key,out var target)||!target.IsEnabled
            ||target.PlayerChoiceOptions is not {IsRequired:true,MinCount:1,MaxCount:1})return false;
        // Earlier unknown effects might change the selected instance. Do not infer their result.
        var effect=config.Effects.FirstOrDefault(e=>e.IsEnabled);
        if(effect is not {Type:EffectType.Discard,RepeatCount:0,RepeatCountValue:null,RepeatCondition:null}
            ||effect.ChainedAbilities.Count>0||effect.Parameters.Count>0||effect.Outputs?.Count is >0)return false;
        return effect.TargetSelector.Contains(key)||effect.TargetSelector.Contains("*")
            ||effect.TargetSelector.Count==0&&config.Targets.Count(t=>t.Value.IsEnabled)==1;
    }
    internal static Rule[] Read(GameState game,IReadOnlyList<AiPlayCandidates.Candidate> candidates)
    {
        var context=GameServiceProvider.CurrentGameContext;
        if(context==null||!ReferenceEquals(context.CurrentGameState,game))return [];
        var rules=new List<Rule>();
        foreach(var candidate in candidates){
            var actual=game.FindCardInstance(candidate.Original.InstanceId);
            if(actual==null||actual.IsSilenced||actual.OwnerPlayerId!=context.Owner.PlayerId)continue;
            var configs=(candidate.PlayedCardId==actual.CurrentDefinitionId
                ?context.AbilityManager.GetEffectiveAbilitiesForCard(actual.InstanceId,context).OfType<Ability>().Select(a=>a.Config)
                :GameDataProvider.Instance.GetAbilityConfigs(candidate.PlayedCardId)??[]).Take(65).ToArray();
            if(configs.Length>64)continue;
            foreach(var group in configs.Where(DirectRoot).SelectMany(c=>c.Targets.Where(t=>t.Value.IsEnabled
                &&t.Value.PlayerChoiceOptions is {IsRequired:true,MinCount:1,MaxCount:1}).Select(t=>(Config:c,Key:t.Key))).GroupBy(x=>x.Key)){
                var all=group.All(x=>DiscardsSelection(x.Config,x.Key));
                foreach(var item in group.Where(x=>DiscardsSelection(x.Config,x.Key)))
                    rules.Add(new(candidate.HandIndex,candidate.Original.CardId,candidate.PlayedCardId,candidate.Mode.ToString(),candidate.Cost,item.Key,item.Config.Id,all));
            }
        }
        return rules.Distinct().ToArray();
    }
    internal static int? IntendedDiscard(AiStrategyPlanner.PlayAdvice step,PlayerStateDto own,IReadOnlyList<Rule> rules)
    {
        if(step.Target is not {Side:"own",Zone:"hand"} target||target.Index==step.HandIndex
            ||target.Index<0||target.Index>=own.Hand.Count)return null;
        var card=own.Hand[target.Index];
        if(card.CardId!=target.CardId||card.OwnerPlayerId!=own.PlayerId||card.InstanceId==Guid.Empty)return null;
        return rules.Any(r=>r.HandIndex==step.HandIndex&&r.CardId==step.CardId&&r.Mode==step.Mode&&r.Cost==step.Cost
            &&r.Key==target.Key&&(target.AbilityDefinitionId.HasValue?r.AbilityDefinitionId==target.AbilityDefinitionId:r.UnambiguousWithoutAbilityId))?target.Index:null;
    }
}
