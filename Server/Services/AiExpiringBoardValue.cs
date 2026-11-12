using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Services;

// Board-value heuristic only. We DO NOT remove these cards from the projected field,
// change combat legality/damage, or use their disappearance as a proof of survival/lethal.
internal static class AiExpiringBoardValue
{
    private static string Normal(string text)=>Regex.Replace(text,"[\\s()]","").ToLowerInvariant();
    private static string? Text(EffectConfig effect,string key)=>effect.Parameters.GetValueOrDefault(key) is StaticValueConfig s
        ?s.Value is JsonElement {ValueKind:JsonValueKind.String} j?j.GetString():s.Value as string:null;
    private static bool Simple(EffectConfig effect)=>effect.IsEnabled&&effect.RepeatCount==0&&effect.RepeatCountValue==null
        &&effect.RepeatCondition==null&&effect.ChainedAbilities.Count==0&&effect.Outputs?.Count is not >0;
    private static bool OwnTurnCondition(string expression)=>Normal(expression) is "true"
        or "context.triggeringevent.endingplayer.playerid==context.owner.playerid"
        or "context.triggeringevent.source!=nullandcontext.triggeringevent.source.instanceid==context.owner.playerid";
    internal static bool SelfVanish(AbilityConfig config)
    {
        var trigger=config.Trigger;var effects=config.Effects.Where(e=>e.IsEnabled).ToArray();
        if(!config.IsEnabled||config.Type!=AbilityType.Triggered||!trigger.IsEnabled||trigger.Timing!=TriggerTiming.On
            ||trigger.EventTypes.Count!=1||trigger.EventTypes[0]!=EventType.TurnEnd||trigger.RelationType!=TriggerRelationType.Any
            ||trigger.CountLimit!=null||trigger.LimitSameNameOncePerTransaction||config.AbilityKeywords.Count>0
            ||config.PreConditions.Any(c=>c.IsEnabled)||config.Conditions.Any(c=>c.IsEnabled&&!OwnTurnCondition(c.Expression))
            ||config.Auras.Any(a=>a.IsEnabled)||config.Properties.Count>0||config.CounterRegistrations.Count>0||config.PlayerCounterRegistrations.Count>0||effects.Length!=1||effects[0].Type!=EffectType.Vanish
            ||!Simple(effects[0])||effects[0].Parameters.Count>0)return false;
        var targets=config.Targets.Where(t=>t.Value.IsEnabled).ToArray();
        return targets.Length==1&&targets[0].Value.PlayerChoiceOptions==null&&targets[0].Value.Query.Operations.Count==0
            &&Normal(targets[0].Value.Query.SourcePoolExpression)=="context.sourcecard"
            &&(effects[0].TargetSelector.Count==0||effects[0].TargetSelector.SequenceEqual(new[]{targets[0].Key}));
    }
    internal static bool LeaveAsVanish(AbilityConfig config)=>config.Trigger.RelationType==TriggerRelationType.TargetSelf
        &&config.Trigger.Timing==TriggerTiming.Before&&config.Trigger.EventTypes.SequenceEqual(new[]{EventType.LeaveField})
        &&config.Effects.Where(e=>e.IsEnabled).ToArray() is {Length:1} effects&&Simple(effects[0])
        &&effects[0].Type==EffectType.RewriteEventParams&&Text(effects[0],"ParamName")=="RequestedKind"&&Text(effects[0],"Value")=="Vanish";
    private static bool Changing(EffectConfig effect,int depth=0)=>depth>8
        ||effect.IsEnabled&&(effect.Type is EffectType.SetKeyword or EffectType.GrantAbility or EffectType.Transform or EffectType.RewriteEventParams
            ||effect.Type.ToString() is "RemoveAbility" or "Silence" or "VanishResist"
            ||effect.ChainedAbilities.Any(a=>a.IsEnabled&&a.Effects.Any(e=>Changing(e,depth+1))));
    internal static HashSet<Guid> Read(GameContext context,PlayerStateDto player)
    {
        var answer=new HashSet<Guid>();
        if(context.ServiceProvider.GetService<IConfiguration>()?.GetValue("AiStrategy:ExpiringBoardValue",true)==false
            ||context.CurrentGameState.CurrentPlayerId!=player.PlayerId||context.CurrentGameState.IsGameOver)return answer;
        var visible=context.CurrentGameState.AllField.Concat(context.CurrentGameState.AllEmblem).Concat(context.CurrentGameState.AllLeaders)
            .Where(c=>!c.IsSilenced).Select(c=>(Card:c,Abilities:context.AbilityManager.GetEffectiveAbilitiesForCard(c.InstanceId,context).ToArray())).ToArray();
        foreach(var view in player.Field){
            var row=visible.FirstOrDefault(r=>r.Card.InstanceId==view.InstanceId);
            // Restrict to plain, non-protective followers. A disappearing Ward would require a
            // separate threat projection; arbitrary keyword/protection effects are not replayed here.
            if(row.Card==null||row.Card.OwnerPlayerId!=player.PlayerId||row.Card.CurrentDefinitionId!=view.CardId
                ||view.Type!=ShadowverseBeyond.Server.Dtos.Enums.CardType.Follower
                ||view.Keywords.Any(k=>k is not (CardKeyword.Storm or CardKeyword.Rush or CardKeyword.FreeEvolve or CardKeyword.BanEvolve or CardKeyword.CannotAttackLeader))
                ||row.Abilities.Any(a=>a is not Ability))continue;
            var abilities=row.Abilities.OfType<Ability>().Where(a=>a.Config.IsEnabled&&a.TriggerConfig.IsEnabled&&a.TriggerConfig.IsValidZone(row.Card.CurrentZone)).ToArray();
            if(!abilities.Any(a=>SelfVanish(a.Config)))continue;
            var uncertain=false;
            foreach(var other in visible){
                if(other.Abilities.Any(a=>a is not Ability)){uncertain=true;break;}
                foreach(var ability in other.Abilities.OfType<Ability>().Where(a=>a.Config.IsEnabled&&a.TriggerConfig.IsEnabled&&a.TriggerConfig.IsValidZone(other.Card.CurrentZone))){
                    if(ability.Config.Auras.Any(a=>a.IsEnabled)){uncertain=true;break;}
                    if(other.Card.InstanceId==row.Card.InstanceId&&(SelfVanish(ability.Config)||LeaveAsVanish(ability.Config)))continue;
                    if(other.Card.InstanceId!=row.Card.InstanceId&&ability.TriggerConfig.RelationType==TriggerRelationType.TargetSelf
                        &&ability.TriggerConfig.EventTypes.All(t=>t is EventType.LeaveField or EventType.Vanish))continue;
                    if(ability.TriggerConfig.EventTypes.Any(t=>t is EventType.Attack or EventType.AttackDeclared or EventType.Clash
                        or EventType.TurnEnd or EventType.LeaveField or EventType.Vanish or EventType.Evolve or EventType.SuperEvolve)
                        &&ability.Config.Effects.Any(e=>Changing(e))){uncertain=true;break;}
                }
                if(uncertain)break;
            }
            if(!uncertain)answer.Add(view.InstanceId);
        }
        return answer;
    }
}
