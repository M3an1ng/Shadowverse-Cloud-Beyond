using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

// A bounded, immutable numeric overlay, not an execution of arbitrary abilities. No live events are fired.
internal static class AiCombatProjection
{
    internal sealed record Omission(Guid Source,string Reason);
    internal enum Pool { Source, OwnerLeader, OpponentLeader, OpponentFollowers, OpponentAll, OwnerFollowers, OwnerAll, EventTarget, EventAttacker, CombatOthers, AttackerOther, DefenderOther, AllFollowers, AllOtherFollowers }
    internal sealed record Operation(EffectType Kind,Pool[] Pools,int Amount,int AttackFactor,bool Split,CardKeyword Keyword,bool AddAttack,int? HealthModifier=null,bool ChangeAttack=true);
    internal sealed record Rule(Guid Source,int Definition,Guid Owner,EventType Event,TriggerTiming Timing,
        TriggerRelationType Relation,int ZonePriority,int Position,int Registration,
        string[] Conditions,Operation[] Operations,Guid RuntimeId,int? Remaining);
    internal sealed record DamageGuard(Guid Source,int Definition,TriggerRelationType Relation,bool OnlyLeaders,bool AlwaysZero);
    internal sealed class Frame
    {
        internal readonly Rule[] Rules;
        internal readonly DamageGuard[] DamageGuards;
        internal readonly bool DamageGuardsComplete;
        internal readonly Omission[] Omissions;
        internal readonly IReadOnlySet<Guid> UnsafeStatCards;
        internal IReadOnlySet<Guid> OrdinaryEvolutionReplacements { get; private set; } = new HashSet<Guid>();
        internal readonly Guid Actor,CatalogRevision;
        internal readonly string GameId,Position;
        internal readonly long Revision;
        internal readonly WeakReference<GameState> Native;
        internal Frame WithoutEvolutionReplacements()
        {
            if (OrdinaryEvolutionReplacements.Count == 0) return this;
            var frame = (Frame)MemberwiseClone();
            frame.OrdinaryEvolutionReplacements = new HashSet<Guid>();
            return frame;
        }

        internal int RuleCount=>Rules.Length+(DamageGuardsComplete?DamageGuards.Count(rule=>rule.AlwaysZero):0);
        internal Frame(Rule[] rules,Omission[] omissions,GameStateDto snapshot,Guid actor,long revision,Guid catalog,IReadOnlySet<Guid> unsafeStatCards,GameState native,DamageGuard[] damageGuards,bool damageGuardsComplete,IReadOnlySet<Guid> replacements)
        {Native=new(native);Rules=rules;DamageGuards=damageGuards;DamageGuardsComplete=damageGuardsComplete;Actor=actor;Revision=revision;CatalogRevision=catalog;GameId=snapshot.GameId;
            Position=AiStrategyPlanner.PositionKey(snapshot.Players.Single(p=>p.PlayerId==actor),snapshot.Players.Single(p=>p.PlayerId!=actor));
            Omissions=omissions;UnsafeStatCards=unsafeStatCards;OrdinaryEvolutionReplacements=replacements;
        }
    }
    // Immutable once returned. Child projections receive a copy; neither native counters nor sibling branches change.
    private sealed record Usage(Frame Frame,Dictionary<Guid,int> Counts);
    private static readonly ConditionalWeakTable<GameStateDto,Usage> Uses=new();
    private static bool Live(Frame frame)=>frame.Native.TryGetTarget(out var game)&&game.StateRevision==frame.Revision;
    internal static GameStateDto CarryUsage(GameStateDto from,GameStateDto to)
    {
        if(!ReferenceEquals(from,to)&&Uses.TryGetValue(from,out var usage))Uses.Add(to,usage);
        return to;
    }
    internal static string UsageKey(GameStateDto state)=>Uses.TryGetValue(state,out var usage)
        ?string.Join(',',usage.Counts.OrderBy(pair=>pair.Key).Select(pair=>$"{pair.Key:N}:{pair.Value}")):"";
    private static int? Remaining(Ability ability)
    {
        var limit=ability.TriggerConfig.CountLimit;
        var bounds=new[]{(Max:limit?.MaxPerTurn,Used:ability.Trigger.CurrentTurnTriggerCount),
            (Max:limit?.MaxPerGame,Used:ability.Trigger.TotalTriggerCount)};
        var limited=bounds.Where(bound=>bound.Max is >0).Select(bound=>(int)Math.Max(0,(long)bound.Max!.Value-bound.Used)).ToArray();
        return limited.Length==0?null:limited.Min();
    }
    private static string Compact(string text)=>Regex.Replace(text,@"[\s()]","").ToLowerInvariant();
    private static string? Literal(EffectConfig effect,string key)=>effect.Parameters.TryGetValue(key,out var value)
        &&value is StaticValueConfig constant?constant.Value?.ToString():null;
    private static string? ExpandedLiteral(EffectConfig effect,string key)=>effect.Parameters.FirstOrDefault(pair=>pair.Key.Equals(key,StringComparison.OrdinalIgnoreCase)).Value is StaticValueConfig value?value.Value?.ToString():null;
    private static bool Integer(string? text,out int value)=>int.TryParse(text,NumberStyles.Integer,CultureInfo.InvariantCulture,out value)&&Math.Abs((long)value)<=1000;
    private static bool Amount(EffectConfig effect,string key,out int constant,out int factor,bool expanded)
    {
        constant=factor=0;if(!effect.Parameters.TryGetValue(key,out var value))return false;
        if(value is StaticValueConfig literal)return Integer(literal.Value?.ToString(),out constant);
        if(value is not FormulaValueConfig formula)return false;
        if(expanded&&Compact(formula.Formula)=="context.sourcecard.currentattack"){factor=1;return true;}
        var match=Regex.Match(Compact(formula.Formula),@"^context\.sourcecard\.currentattack\*([0-9]{1,3})$");
        return match.Success&&int.TryParse(match.Groups[1].Value,out factor)&&factor<=10;
    }
    private static bool Conditions(string text,bool expanded,EventType eventType)
    {
        var compact=Compact(text);
        if(expanded&&(compact is "context.currentgamestate.currentplayer.playerid==context.owner.playerid" or "context.currentgamestate.currentplayer.playerid==context.opponent.playerid"
            ||Regex.IsMatch(compact,@"^context\.triggeringevent\.attacker\.(cardtype|type)==""(follower|leader)""$")
            ||eventType==EventType.Clash&&Regex.IsMatch(compact,@"^context\.triggeringevent\.defender\.(cardtype|type)==""(follower|leader)""$")
            ||compact is "context.triggeringevent.attacker.ownerplayerid==context.owner.playerid" or "context.triggeringevent.attacker.ownerplayerid==context.opponent.playerid"))return true;
        return compact=="true"||compact is "context.triggeringevent.targetcard.cardtype==\"leader\"" or "context.triggeringevent.targetcard.type==\"leader\""
            or "context.triggeringevent.targetcard.cardtype==\"follower\"" or "context.triggeringevent.targetcard.type==\"follower\""
            or "context.triggeringevent.attacker.instanceid==context.sourcecard.instanceid"
            ||compact is "countcontext.opponent.field.queryself.cardtype==\"follower\">0" or "countcontext.opponent.field.queryself.type==\"follower\">0";
    }
    private static bool ConditionApplies(string text,GameStateDto state,Rule rule,CardInstanceDto attacker,CardInstanceDto target)
    {
        var compact=text;if(compact=="true")return true;
        if(compact.StartsWith("context.currentgamestate.currentplayer.playerid==",StringComparison.Ordinal))
            return compact.EndsWith("context.owner.playerid",StringComparison.Ordinal)?state.CurrentPlayerId==rule.Owner:state.CurrentPlayerId!=rule.Owner;
        if(compact.StartsWith("context.triggeringevent.attacker.ownerplayerid==",StringComparison.Ordinal))
            return compact.EndsWith("context.owner.playerid",StringComparison.Ordinal)?attacker.OwnerPlayerId==rule.Owner:attacker.OwnerPlayerId!=rule.Owner;
        if(compact.StartsWith("context.triggeringevent.attacker.cardtype",StringComparison.Ordinal)||compact.StartsWith("context.triggeringevent.attacker.type",StringComparison.Ordinal))
            return compact.EndsWith("\"follower\"",StringComparison.Ordinal)?attacker.Type==CardType.Follower:attacker.Type==CardType.Leader;
        if(compact.StartsWith("count",StringComparison.Ordinal))return state.Players.Single(p=>p.PlayerId!=rule.Owner).Field.Any(c=>c.Type==CardType.Follower);
        if(compact=="context.triggeringevent.attacker.instanceid==context.sourcecard.instanceid")return attacker.InstanceId==rule.Source;
        return compact.EndsWith("\"follower\"",StringComparison.Ordinal)?target.Type==CardType.Follower:target.Type==CardType.Leader;
    }
    private static Pool? ReadPool(TargetConfig target,bool expanded)
    {
        if(target.PlayerChoiceOptions!=null||target.Query.Operations.Count>0)return null;
        var exact=Regex.Replace(target.Query.SourcePoolExpression,@"\s","").ToLowerInvariant();
        if(expanded&&(exact=="((context.owner.field|context.opponent.field).query((self.type==\"follower\")))~(context.sourcecard)"
            ||exact=="((context.owner.field|context.opponent.field).query((self.cardtype==\"follower\")))~(context.sourcecard)"))return Pool.AllOtherFollowers;
        var expression=Compact(target.Query.SourcePoolExpression);
        if(expanded) {
            if(expression=="context.triggeringevent.attacker~context.sourcecard")return Pool.AttackerOther;
            if(expression=="context.triggeringevent.defender~context.sourcecard")return Pool.DefenderOther;
            if(expression is "context.currentgamestate.allfield.queryself.type==\"follower\"" or "context.currentgamestate.allfield.queryself.cardtype==\"follower\"")return Pool.AllFollowers;
        }
        return expression switch {
            "context.sourcecard"=>Pool.Source,"context.owner.leader"=>Pool.OwnerLeader,"context.opponent.leader"=>Pool.OpponentLeader,
            "context.opponent.field.queryself.type==\"follower\"" or "context.opponent.field.queryself.cardtype==\"follower\""=>Pool.OpponentFollowers,
            "context.opponent.field.queryself.type==\"follower\"|context.opponent.leader"=>Pool.OpponentAll,
            "context.owner.field.queryself.type==\"follower\"" or "context.owner.field.queryself.cardtype==\"follower\""=>Pool.OwnerFollowers,
            "context.owner.field.queryself.type==\"follower\"|context.owner.leader" or "context.owner.field.queryself.cardtype==\"follower\"|context.owner.leader"=>Pool.OwnerAll,
            "context.triggeringevent.targetcard" or "context.triggeringevent.defender"=>Pool.EventTarget,
            "context.triggeringevent.attacker"=>Pool.EventAttacker,
            "context.triggeringevent.attacker|context.triggeringevent.defender~context.sourcecard"=>Pool.CombatOthers,_=>null};
    }
    private static Rule? Compile(Ability ability,CardInstanceDto card,int zonePriority,int position,int registration,bool hasBuffs,bool expanded)
    {
        var config=ability.Config;var trigger=ability.TriggerConfig;
        if(config.Type!=AbilityType.Triggered||config.Auras.Any(a=>a.IsEnabled)||config.CounterRegistrations.Count>0||config.PlayerCounterRegistrations.Count>0
            ||!expanded&&trigger.CountLimit!=null||trigger.LimitSameNameOncePerTransaction||!ability.Trigger.IsEnabled
            ||config.AbilityKeywords.Any(k=>k.Keyword is not (AbilityKeyword.None or AbilityKeyword.AttackEffect or AbilityKeyword.FightEffect))
            ||trigger.EventTypes.Count!=1||trigger.EventTypes[0] is not (EventType.Attack or EventType.Clash)
            ||config.PreConditions.Any(c=>c.IsEnabled&&!Conditions(c.Expression,expanded,trigger.EventTypes[0]))
            ||config.Conditions.Any(c=>c.IsEnabled&&!Conditions(c.Expression,expanded,trigger.EventTypes[0]))||config.Conditions.Count>8||ability.Targets.Count>8||config.Effects.Count>16)return null;
        var pools=new Dictionary<string,Pool>(StringComparer.OrdinalIgnoreCase);
        foreach(var target in ability.Targets.Where(t=>t.IsEnabled&&t.Config.IsEnabled)) {
            if(ReadPool(target.Config,expanded) is not { } pool||target.Config.ShouldBlockAbilityExecutionOnInsufficientTargets())return null;
            if(expanded&&trigger.EventTypes[0]!=EventType.Clash&&Compact(target.Config.Query.SourcePoolExpression).Contains("context.triggeringevent.defender",StringComparison.Ordinal))return null;
            pools[target.Key]=pool;
        }
        var operations=new List<Operation>();
        foreach(var effect in config.Effects.Where(e=>e.IsEnabled).OrderBy(e=>e.ExecutionOrder)) {
            if(expanded&&effect.Parameters.Keys.GroupBy(key=>key,StringComparer.OrdinalIgnoreCase).Any(group=>group.Count()>1))return null;
            if(effect.RepeatCount!=0||effect.RepeatCountValue!=null||effect.RepeatCondition!=null||effect.ChainedAbilities.Count>0||effect.Outputs?.Count>0)return null;
            var selectors=effect.TargetSelector;
            bool All(string token)=>new[]{"*","All","ResolvedTargets","ResolvedTargets.*","ResolvedTargets.All"}.Contains(token.Trim(),StringComparer.OrdinalIgnoreCase);
            string Key(string token)=>token.Trim().StartsWith("ResolvedTargets.",StringComparison.OrdinalIgnoreCase)?token.Trim()[16..].Trim():token.Trim();
            var keys=selectors.Select(Key).Where(pools.ContainsKey).ToArray();
            // Match Ability.CreateEffectRequests: an entirely unmatched legacy selector falls back to all pools.
            var all=selectors.Count==0||selectors.Any(All)||keys.Length==0;
            var selected=all?pools.Values.ToArray():keys.Select(key=>pools[key]).Distinct().ToArray();
            if(selected.Length==0)return null;
            var amount=0;var factor=0;var split=false;var addAttack=false;var keyword=CardKeyword.None;int? healthModifier=null;var changeAttack=true;
            if(expanded&&effect.Parameters.Any(pair=>pair.Key.Equals("DurationType",StringComparison.OrdinalIgnoreCase)&&pair.Value is not StaticValueConfig))return null;
            var duration=(expanded?ExpandedLiteral(effect,"DurationType"):Literal(effect,"DurationType"))??"permanent";
            if(!duration.Equals("permanent",StringComparison.OrdinalIgnoreCase)&&!duration.Equals("OwnerTurnEnd",StringComparison.OrdinalIgnoreCase)
                &&!duration.Equals("EndOfTurn",StringComparison.OrdinalIgnoreCase))return null;
            if(effect.Parameters.ContainsKey("RemoveCondition"))return null;
            switch(effect.Type) {
                case EffectType.SetKeyword:
                    if((expanded?effect.Parameters.Keys.Any(key=>!new[]{"Keyword","Operation","DurationType","DurationParameter","CanStack"}.Contains(key,StringComparer.OrdinalIgnoreCase)):effect.Parameters.Keys.Any(k=>k is not ("Keyword" or "Operation" or "DurationType" or "DurationParameter" or "CanStack")))
                        ||!Enum.TryParse(expanded?ExpandedLiteral(effect,"Keyword"):Literal(effect,"Keyword"),true,out keyword)||keyword is not (CardKeyword.Shield or CardKeyword.Bane)
                        ||(expanded?ExpandedLiteral(effect,"Operation"):Literal(effect,"Operation")) is { } operation&&!operation.Equals("add",StringComparison.OrdinalIgnoreCase)&&operation!="0"
                        ||selected.Any(pool=>pool!=Pool.Source))return null;
                    break;
                case EffectType.Damage:
                    if(effect.Parameters.Keys.Any(k=>k is not ("DamageAmount" or "DistributionMode"))||!Amount(effect,"DamageAmount",out amount,out factor,expanded))return null;
                    var mode=Literal(effect,"DistributionMode")??"perTarget";
                    if(mode.Equals("Split",StringComparison.OrdinalIgnoreCase)||mode=="1")split=true;
                    else if(!mode.Equals("perTarget",StringComparison.OrdinalIgnoreCase)&&mode!="0")return null;
                    if(split&&selected.Any(pool=>pool is not (Pool.OpponentAll or Pool.OpponentFollowers or Pool.OpponentLeader)))return null;
                    break;
                case EffectType.SetAttackTimes:
                    if(!expanded||hasBuffs||card.Type!=CardType.Follower||selected.Any(pool=>pool!=Pool.Source)
                        ||effect.Parameters.Keys.Any(key=>!key.Equals("MaxAttacksPerTurn",StringComparison.OrdinalIgnoreCase)))return null;
                    var max=effect.Parameters.FirstOrDefault(pair=>pair.Key.Equals("MaxAttacksPerTurn",StringComparison.OrdinalIgnoreCase)).Value as StaticValueConfig;
                    if(!Integer(max?.Value?.ToString(),out amount)||amount<1||amount>10)return null;
                    break;
                case EffectType.Heal:
                    if(effect.Parameters.Keys.Any(k=>k!="HealAmount")||!Amount(effect,"HealAmount",out amount,out factor,expanded))return null;
                    break;
                case EffectType.ModifyStatus:
                    // Layered existing modifiers require the full buff evaluator. Do not flatten them into a Set.
                    if(hasBuffs&&selected.Contains(Pool.Source)||selected.Any(pool=>pool is not (Pool.Source or Pool.EventTarget or Pool.EventAttacker or Pool.CombatOthers))
                        ||effect.Parameters.Keys.Any(k=>k is not ("AttackModifier" or "HealthModifier" or "ModifierLayer" or "DurationType" or "DurationParameter" or "CanStack")))return null;
                    changeAttack=effect.Parameters.ContainsKey("AttackModifier");
                    if(changeAttack&&!Integer(Literal(effect,"AttackModifier"),out amount))return null;
                    if(effect.Parameters.ContainsKey("HealthModifier")) {
                        if(!Integer(Literal(effect,"HealthModifier"),out var health))return null;
                        healthModifier=health;
                    }
                    if(!changeAttack&&!healthModifier.HasValue)return null;
                    var layer=Literal(effect,"ModifierLayer")??"additive";
                    addAttack=layer.Equals("additive",StringComparison.OrdinalIgnoreCase);
                    if(!addAttack&&!layer.Equals("set",StringComparison.OrdinalIgnoreCase)
                        ||addAttack&&string.Equals(Literal(effect,"CanStack"),"false",StringComparison.OrdinalIgnoreCase))return null;
                    // Other existing modifier layers require the native buff evaluator. Health Set also restores damage.
                    if(!addAttack&&(healthModifier.HasValue||selected.Any(pool=>pool!=Pool.Source)))return null;
                    break;
                case EffectType.Tuck:
                    if(!expanded||trigger.EventTypes[0]!=EventType.Clash||effect.Parameters.Count!=0||card.Type!=CardType.Follower
                        ||selected.Any(pool=>pool is Pool.OwnerLeader or Pool.OpponentLeader or Pool.OpponentAll or Pool.OwnerAll))return null;
                    break;
                case EffectType.Destroy:
                case EffectType.Vanish:
                    if(effect.Parameters.Count!=0||selected.Any(pool=>pool is Pool.OwnerLeader or Pool.OpponentLeader or Pool.OpponentAll or Pool.OwnerAll))return null;
                    break;
                default:return null;
            }
            operations.Add(new(effect.Type,selected,amount,factor,split,keyword,addAttack,healthModifier,changeAttack));
        }
        return operations.Count==0?null:new(card.InstanceId,card.CardId,card.OwnerPlayerId,trigger.EventTypes[0],trigger.Timing,
            trigger.RelationType,zonePriority,position,registration,config.PreConditions.Concat(config.Conditions).Where(c=>c.IsEnabled).Select(c=>Compact(c.Expression)).ToArray(),operations.ToArray(),ability.RuntimeId,Remaining(ability));
    }
    private static DamageGuard ReadDamageGuard(Ability ability,CardInstanceDto card)
    {
        var config=ability.Config;var trigger=ability.TriggerConfig;
        var conditions=config.PreConditions.Concat(config.Conditions).Where(condition=>condition.IsEnabled).ToArray();
        var onlyLeaders=conditions.Any(condition=>Compact(condition.Expression) is "context.triggeringevent.damagetarget.cardtype==\"leader\""
            or "context.triggeringevent.damagetarget.type==\"leader\"");
        var effects=config.Effects.Where(effect=>effect.IsEnabled).ToArray();
        var zero=card.Type==CardType.Follower&&config.Type==AbilityType.Triggered&&trigger.EventTypes.Count==1&&trigger.RelationType==TriggerRelationType.TargetSelf
            &&Remaining(ability)==null&&!trigger.LimitSameNameOncePerTransaction&&conditions.Length==0
            &&config.Properties.Count==0&&config.Auras.All(aura=>!aura.IsEnabled)&&config.AbilityKeywords.Count==0
            &&config.CounterRegistrations.Count==0&&config.PlayerCounterRegistrations.Count==0
            &&config.Targets.All(target=>!target.Value.IsEnabled)&&effects.Length==1;
        if(zero){
            var effect=effects[0];
            zero=effect.Type==EffectType.RewriteEventParams&&effect.RepeatCount==0&&effect.RepeatCountValue==null
                &&effect.RepeatCondition==null&&effect.ChainedAbilities.Count==0&&effect.Outputs?.Count is not >0&&effect.TargetSelector.Count==0
                &&effect.Parameters.Count==2&&effect.Parameters.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count()==2
                &&effect.Parameters.Keys.All(key=>key.Equals("ParamName",StringComparison.OrdinalIgnoreCase)||key.Equals("Value",StringComparison.OrdinalIgnoreCase))
                &&ExpandedLiteral(effect,"ParamName") is { } name&&name.Equals("Damage",StringComparison.OrdinalIgnoreCase)
                &&Integer(ExpandedLiteral(effect,"Value"),out var value)&&value<=0;
        }
        return new(card.InstanceId,card.CardId,trigger.RelationType,onlyLeaders,zero);
    }
    // Only an unconditional, unlimited TargetSelf rewrite is projected. A possibly applicable
    // unknown Before-Damage listener suppresses that claim instead of guessing rewrite order.
    internal static int IncomingDamage(GameStateDto state,CardInstanceDto target,int raw,Guid source)
    {
        var normal=Math.Max(0,raw);
        if(target.Type!=CardType.Follower||!Uses.TryGetValue(state,out var usage)||!Live(usage.Frame)||!usage.Frame.DamageGuardsComplete
            ||usage.Frame.GameId!=state.GameId||usage.Frame.CatalogRevision!=GameDataProvider.Instance.DataRevision)return normal;
        var zero=false;
        foreach(var guard in usage.Frame.DamageGuards){
            if(guard.OnlyLeaders||Find(state,guard.Source) is not {Zone:CardZone.Field} holder||holder.CardId!=guard.Definition)continue;
            var applies=guard.Relation switch{
                TriggerRelationType.Any=>true,TriggerRelationType.Self=>guard.Source==source||guard.Source==target.InstanceId,
                TriggerRelationType.SourceSelf=>guard.Source==source,TriggerRelationType.TargetSelf=>guard.Source==target.InstanceId,
                TriggerRelationType.Other=>guard.Source!=source||guard.Source!=target.InstanceId,
                TriggerRelationType.SourceOther=>guard.Source!=source,TriggerRelationType.TargetOther=>guard.Source!=target.InstanceId,_=>true};
            if(!applies)continue;
            if(!guard.AlwaysZero)return normal;
            zero=true;
        }
        return zero?0:normal;
    }
    internal static Frame? Read(GameContext context,GameStateDto snapshot,Guid actor)=>ReadCore(context,snapshot,actor,AiCombatEvaluation.ExpandedEnabled(context.CurrentGameState,actor));
    internal static Frame? ReadPrevious(GameContext context,GameStateDto snapshot,Guid actor)=>ReadCore(context,snapshot,actor,false);
    private static Frame? ReadCore(GameContext context,GameStateDto snapshot,Guid actor,bool expanded)
    {
        var game=context.CurrentGameState;var revision=game.StateRevision;var catalog=GameDataProvider.Instance.DataRevision;
        if(game.GameId!=snapshot.GameId||game.CurrentPlayerId!=actor||game.Phase!=GamePhase.Main)return null;
        var rules=new List<Rule>();var damageGuards=new List<DamageGuard>();var damageGuardsComplete=true;var omitted=new List<Omission>();var order=0;var unsafeStats=new HashSet<Guid>();
        var replacements = new HashSet<Guid>();
        var beforeEvolutionTargets = new HashSet<Guid>();
        var otherEvolutionInterference = false;
        foreach(var player in snapshot.Players)foreach(var zone in new[]{player.Field,player.Emblem,player.LeaderCard==null?[]:new List<CardInstanceDto>{player.LeaderCard},player.PlayerId==actor?player.Hand:[]})
        for(var index=0;index<zone.Count;index++) {
            var card=zone[index];var actual=game.FindCardInstance(card.InstanceId);
            if(actual==null||actual.CurrentDefinitionId!=card.CardId||actual.CurrentZone!=card.Zone||actual.OwnerPlayerId!=card.OwnerPlayerId)return null;
            if(actual.IsSilenced)continue;
            var buffs=context.BuffManager.GetBuffsForCard(card.InstanceId).Any(buff=>buff.IsActive);
            if(buffs)unsafeStats.Add(card.InstanceId);
            foreach(var current in context.AbilityManager.GetEffectiveAbilitiesForCard(card.InstanceId,context)) {
                if(current is not Ability ability) {omitted.Add(new(card.InstanceId,"unknown-runtime-ability"));if(expanded)damageGuardsComplete=false;otherEvolutionInterference=true;continue;}
                var trigger=ability.TriggerConfig;
                var expectedZone=card.Zone==CardZone.Hand?CardZone.Field:card.Zone;
                if(!ability.Config.IsEnabled||!trigger.IsEnabled||!trigger.IsValidZone(expectedZone))continue;
                if (trigger.Timing == TriggerTiming.Before && trigger.EventTypes.Any(type =>
                    type is EventType.Evolve or EventType.SuperEvolve or EventType.EvolveRequested))
                {
                    if (!buffs && player.PlayerId == actor && card.Zone == CardZone.Field
                        && IsOrdinaryEvolutionReplacement(ability.Config) && replacements.Add(card.InstanceId))
                        continue;
                    else if (trigger.RelationType == TriggerRelationType.TargetSelf)
                        beforeEvolutionTargets.Add(card.InstanceId);
                    else
                        otherEvolutionInterference = true;
                }
                if(expanded&&trigger.Timing==TriggerTiming.Before&&trigger.EventTypes.Contains(EventType.Damage)&&Remaining(ability)!=0){
                    var guard=ReadDamageGuard(ability,card);
                    if(damageGuards.Count<96)damageGuards.Add(guard);
                    else if(damageGuardsComplete){damageGuardsComplete=false;omitted.Add(new(card.InstanceId,"before-damage-budget-exceeded"));}
                    if(!guard.AlwaysZero)omitted.Add(new(card.InstanceId,"before-damage-response-not-projected"));
                }
                if(!trigger.EventTypes.Any(type=>type is EventType.Attack or EventType.Clash))continue;
                if(card.Zone==CardZone.Hand&&trigger.RelationType is not (TriggerRelationType.SourceSelf or TriggerRelationType.Self)) {
                    omitted.Add(new(card.InstanceId,"future-global-trigger-order-not-projected"));continue;
                }
                var compiled=Compile(ability,card,GameConstants.AbilityTrigger.GetZonePriority(expectedZone,player.PlayerId==actor),card.Zone==CardZone.Hand?player.Field.Count:actual.ZoneIndex is >=0?actual.ZoneIndex.Value:actual.IsLeader?int.MaxValue:index,order++,buffs,expanded);
                if(compiled!=null&&rules.Count<96)rules.Add(compiled);else omitted.Add(new(card.InstanceId,"combat-condition-target-or-effects-not-projected"));
            }
        }
        if (otherEvolutionInterference) replacements.Clear();
        else replacements.ExceptWith(beforeEvolutionTargets);
        return game.StateRevision==revision&&GameDataProvider.Instance.DataRevision==catalog
            ? new Frame(rules.ToArray(),omitted.ToArray(),snapshot,actor,revision,catalog,unsafeStats,game,damageGuards.ToArray(),damageGuardsComplete,replacements)
            : null;
    }
    private static bool IsOrdinaryEvolutionReplacement(AbilityConfig ability)
    {
        static bool SingleEffect(EffectConfig effect, EffectType type) =>
            effect.IsEnabled && effect.Type == type && effect.RepeatCount == 0 && effect.RepeatCountValue == null
            && effect.RepeatCondition == null && effect.Parameters.Count == 0 && effect.TargetSelector.Count == 0
            && effect.Outputs is not { Count: > 0 };

        if (ability.Type != AbilityType.Triggered || ability.AbilityKeywords.Count != 1
            || !ability.HasKeyword(AbilityKeyword.EvolveEffect)
            || ability.Trigger.RelationType != TriggerRelationType.TargetSelf
            || !ability.Trigger.EventTypes.SequenceEqual([EventType.Evolve])
            || ability.Trigger.CountLimit != null || ability.Trigger.LimitSameNameOncePerTransaction
            || ability.Targets.Count != 0 || ability.Auras.Count != 0 || ability.PreConditions.Count != 0
            || ability.Conditions is not [ { IsEnabled: true } condition ]
            || Compact(condition.Expression) != "context.triggeringevent.ismanualevolution==true"
            || ability.Effects is not [ var prevent ] || !SingleEffect(prevent, EffectType.PreventEvent)
            || prevent.ChainedAbilities is not [ { IsEnabled: true, Type: AbilityType.Triggered } chain ])
            return false;

        return chain.AbilityKeywords.Count == 0 && chain.Trigger.IsEnabled && chain.Trigger.Timing == TriggerTiming.On
            && chain.Trigger.RelationType == TriggerRelationType.Any && chain.Trigger.EventTypes.SequenceEqual([EventType.EffectCompleted])
            && chain.Trigger.IsValidZone(CardZone.Field) && chain.Trigger.CountLimit == null && !chain.Trigger.LimitSameNameOncePerTransaction
            && chain.PreConditions.Count == 0 && chain.Conditions.Count == 0 && chain.Auras.Count == 0
            && chain.Targets.Count == 1 && chain.Targets.Values.Single() is { IsEnabled: true } target
            && target.GetEffectiveTargetHandlingStrategy() == TargetHandlingStrategy.DoNotIntercept
            && target.Query.SourcePoolExpression?.Trim().Equals("context.SourceCard", StringComparison.OrdinalIgnoreCase) == true
            && target.Query.Operations.Count == 0 && target.PlayerChoiceOptions?.IsRequired != true
            && chain.Effects is [ var super ] && SingleEffect(super, EffectType.SuperEvolve) && super.ChainedAbilities.Count == 0;
    }

    internal static Frame? ForPosition(Frame? frame,GameStateDto snapshot,Guid actor)=>frame!=null&&Live(frame)&&frame.Actor==actor&&frame.GameId==snapshot.GameId
        &&frame.CatalogRevision==GameDataProvider.Instance.DataRevision&&frame.Position==AiStrategyPlanner.PositionKey(snapshot.Players.Single(p=>p.PlayerId==actor),snapshot.Players.Single(p=>p.PlayerId!=actor))?frame:null;
    private static bool Relation(Rule rule,Guid source,Guid target)=>rule.Relation switch {
        TriggerRelationType.Any=>true,TriggerRelationType.SourceSelf=>rule.Source==source,TriggerRelationType.TargetSelf=>rule.Source==target,
        TriggerRelationType.Self=>rule.Source==source||rule.Source==target,TriggerRelationType.Other=>rule.Source!=source||rule.Source!=target,
        TriggerRelationType.SourceOther=>rule.Source!=source,TriggerRelationType.TargetOther=>rule.Source!=target,_=>false};
    private static CardInstanceDto? Find(GameStateDto state,Guid id)=>state.Players.SelectMany(p=>p.Field.Concat(p.Emblem).Concat(p.LeaderCard==null?[]:new[]{p.LeaderCard})).FirstOrDefault(c=>c.InstanceId==id);
    private static void Set(GameStateDto state,CardInstanceDto card) {
        var player=state.Players.Single(p=>p.PlayerId==card.OwnerPlayerId);
        if(player.LeaderCard?.InstanceId==card.InstanceId)player.LeaderCard=card;
        else {var index=player.Field.FindIndex(c=>c.InstanceId==card.InstanceId);if(index>=0)player.Field[index]=card;}
    }
    private static Guid[] Targets(Operation operation,GameStateDto state,Rule rule,Guid attacker,Guid eventTarget)=>operation.Pools.SelectMany(pool=> {
        var own=state.Players.Single(p=>p.PlayerId==rule.Owner);var enemy=state.Players.Single(p=>p.PlayerId!=rule.Owner);
        return pool switch {Pool.Source=>new[]{rule.Source},Pool.OwnerLeader=>own.LeaderCard==null?[]:new[]{own.LeaderCard.InstanceId},
            Pool.OpponentLeader=>enemy.LeaderCard==null?[]:new[]{enemy.LeaderCard.InstanceId},Pool.EventTarget=>new[]{eventTarget},
            Pool.EventAttacker=>new[]{attacker},Pool.CombatOthers=>new[]{attacker,eventTarget}.Where(id=>id!=rule.Source),
            Pool.AttackerOther=>attacker==rule.Source?[]:new[]{attacker},Pool.DefenderOther=>eventTarget==rule.Source?[]:new[]{eventTarget},
            Pool.AllFollowers=>state.Players.SelectMany(player=>player.Field).Where(card=>card.Type==CardType.Follower).Select(card=>card.InstanceId),
            Pool.AllOtherFollowers=>state.Players.SelectMany(player=>player.Field).Where(card=>card.Type==CardType.Follower&&card.InstanceId!=rule.Source).Select(card=>card.InstanceId),
            Pool.OwnerFollowers=>own.Field.Where(c=>c.Type==CardType.Follower).Select(c=>c.InstanceId),
            Pool.OwnerAll=>own.Field.Where(c=>c.Type==CardType.Follower).Select(c=>c.InstanceId).Concat(own.LeaderCard==null?[]:new[]{own.LeaderCard.InstanceId}),
            Pool.OpponentFollowers=>enemy.Field.Where(c=>c.Type==CardType.Follower).Select(c=>c.InstanceId),
            Pool.OpponentAll=>enemy.Field.Where(c=>c.Type==CardType.Follower).Select(c=>c.InstanceId).Concat(enemy.LeaderCard==null?[]:new[]{enemy.LeaderCard.InstanceId}),_=>[]};
    }).Distinct().ToArray();
    private static void Remove(GameStateDto state,CardInstanceDto card,bool destroy) {
        if(card.Type==CardType.Leader)return;
        var player=state.Players.Single(p=>p.PlayerId==card.OwnerPlayerId);
        var removed=player.Field.RemoveAll(c=>c.InstanceId==card.InstanceId)+player.Emblem.RemoveAll(c=>c.InstanceId==card.InstanceId);
        if(removed>0&&destroy){player.GraveyardCount++;player.Counters[PlayerState.GraveyardCounterKey]=player.GraveyardCount;}
    }
    private static void Damage(GameStateDto state,CardInstanceDto card,int raw,Guid source) {
        if(card.InstanceId!=source&&card.Keywords.Contains(CardKeyword.EffectImmune))return;
        var shield=card.Keywords.Contains(CardKeyword.Shield);
        var amount=shield||card.IsSuperEvolved&&card.OwnerPlayerId==state.CurrentPlayerId?0:IncomingDamage(state,card,raw,source);
        var next=card with{CurrentHealth=card.CurrentHealth-amount,Keywords=card.Keywords.Where(k=>k!=CardKeyword.Shield).ToList()};Set(state,next);
        var player=state.Players.Single(p=>p.PlayerId==card.OwnerPlayerId);
        if(card.Type==CardType.Leader&&amount>0){player.BloodPool+=amount;player.Counters[PlayerState.BloodPoolCounterKey]=player.BloodPool;}
        if(card.Type==CardType.Follower&&next.CurrentHealth<=0)Remove(state,next,true);
    }
    internal static GameStateDto BeforeCombat(GameStateDto state,Guid actor,AiTacticalSearch.Move move,Frame? frame,out int applied)
    {
        applied=0;if(frame==null||frame.RuleCount==0||move.Type!="PerformAttack"||move.Target==null||frame.GameId!=state.GameId||frame.Actor!=actor
            ||frame.CatalogRevision!=GameDataProvider.Instance.DataRevision||!Live(frame))return state;
        var projected=state with{Players=state.Players.Select(p=>p with{Field=new(p.Field),Emblem=new(p.Emblem),Counters=new(p.Counters,StringComparer.OrdinalIgnoreCase)}).ToList()};
        var used=Uses.TryGetValue(state,out var prior)&&ReferenceEquals(prior.Frame,frame)?new Dictionary<Guid,int>(prior.Counts):new();
        Uses.Add(projected,new(frame,used));
        var targetId=move.Target.InstanceId;var attackerAtEntry=Find(state,move.Source.InstanceId)??move.Source;
        foreach(var eventType in new[]{EventType.Attack,EventType.Clash}) {
            if(eventType==EventType.Clash&&move.Target.Type==CardType.Leader)continue;
            foreach(var timing in new[]{TriggerTiming.Before,TriggerTiming.On,TriggerTiming.After}) {
                var ordered=frame.Rules.Where(rule=>rule.Event==eventType&&rule.Timing==timing&&Relation(rule,move.Source.InstanceId,targetId))
                    .OrderBy(rule=>rule.Source==move.Source.InstanceId?0:rule.Source==targetId?1:2)
                    .ThenBy(rule=>rule.ZonePriority).ThenBy(rule=>rule.Position).ThenBy(rule=>rule.Registration).ToArray();
                foreach(var rule in ordered) {
                    if(projected.Players.Any(p=>p.LeaderCard is {CurrentHealth:<=0}))return projected;
                    if(Find(projected,rule.Source) is not { } source||source.CardId!=rule.Definition)continue;
                    var target=Find(projected,targetId)??move.Target;
                    if(rule.Conditions.Any(condition=>!ConditionApplies(condition,projected,rule,attackerAtEntry,target)))continue;
                    if(rule.Remaining is int quota){var consumed=used.GetValueOrDefault(rule.RuntimeId);if(consumed>=quota)continue;used[rule.RuntimeId]=consumed+1;}
                    // Targets are resolved once at ability entry, before sequential effects change the field.
                    var targets=rule.Operations.Select(operation=>Targets(operation,projected,rule,move.Source.InstanceId,targetId)).ToArray();
                    for(var i=0;i<rule.Operations.Length;i++) {
                        var operation=rule.Operations[i];var numeric=(long)operation.Amount+(long)operation.AttackFactor*(Find(projected,rule.Source)?.CurrentAttack??source.CurrentAttack);
                        if(numeric<int.MinValue||numeric>int.MaxValue)return state;
                        var amount=(int)numeric;
                        var ids=targets[i];
                        if(operation.Split&&amount>0) {
                            var cards=ids.Select(id=>Find(projected,id)).OfType<CardInstanceDto>().ToArray();
                            var leader=cards.FirstOrDefault(c=>c.Type==CardType.Leader);
                            var followers=projected.Players.SelectMany(p=>p.Field).Where(c=>cards.Any(t=>t.InstanceId==c.InstanceId)&&c.Type!=CardType.Leader).ToArray();
                            var remaining=amount;
                            for(var j=0;j<followers.Length&&remaining>0;j++) {
                                var assigned=j==followers.Length-1&&leader==null?remaining:Math.Min(remaining,Math.Max(0,followers[j].CurrentHealth));
                                if(assigned>0)Damage(projected,followers[j],assigned,rule.Source);remaining-=assigned;
                            }
                            if(leader!=null&&remaining>0)Damage(projected,leader,remaining,rule.Source);
                        } else foreach(var id in ids) {
                            var card=Find(projected,id);if(card==null)continue;
                            if(id!=rule.Source&&card.Keywords.Contains(CardKeyword.EffectImmune))continue;
                            switch(operation.Kind) {
                                case EffectType.SetAttackTimes:Set(projected,card with{MaxAttacksPerTurn=amount});break;
                                case EffectType.SetKeyword:Set(projected,card with{Keywords=card.Keywords.Append(operation.Keyword).Distinct().ToList()});break;
                                case EffectType.ModifyStatus:
                                    if(frame.UnsafeStatCards.Contains(id)){applied=0;return state;}
                                    var attack=operation.ChangeAttack?(long)(operation.AddAttack?card.CurrentAttack:0)+amount:card.CurrentAttack;
                                    var maximum=Math.Max(0,(long)card.MaxHealth+(operation.HealthModifier??0));
                                    var health=maximum-Math.Max(0,(long)card.MaxHealth-card.CurrentHealth);
                                    if(attack>int.MaxValue||attack<int.MinValue||maximum>int.MaxValue||health<int.MinValue){applied=0;return state;}
                                    var updated=card with{CurrentAttack=Math.Max(0,(int)attack),MaxHealth=(int)maximum,CurrentHealth=(int)health};
                                    Set(projected,updated);if(updated.CurrentHealth<=0&&updated.Type!=CardType.Leader)Remove(projected,updated,true);break;
                                case EffectType.Destroy:
                                    if(card.Keywords.Any(k=>k is CardKeyword.DestroyResist or CardKeyword.EarthSigil)||card.IsSuperEvolved&&card.OwnerPlayerId==projected.CurrentPlayerId)break;
                                    Remove(projected,card,true);break;
                                case EffectType.Vanish:
                                    if(!card.Keywords.Contains(CardKeyword.VanishResist))Remove(projected,card,false);break;
                                case EffectType.Tuck:
                                    var holder=projected.Players.Single(player=>player.PlayerId==card.OwnerPlayerId);
                                    if(card.Type==CardType.Follower&&holder.Field.Any(item=>item.InstanceId==id)){
                                        Remove(projected,card,false);holder.DeckCount++;
                                    }
                                    break;
                                case EffectType.Heal:Set(projected,card with{CurrentHealth=(int)Math.Min(card.MaxHealth,(long)card.CurrentHealth+Math.Max(0,amount))});break;
                                case EffectType.Damage:Damage(projected,card,amount,rule.Source);break;
                            }
                        }
                        applied++;
                    }
                }
            }
        }
        return projected;
    }
}
