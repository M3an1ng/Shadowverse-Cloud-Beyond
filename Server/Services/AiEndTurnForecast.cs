using System.Text.Json;
using System.Text.RegularExpressions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Ordering;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

// Public, read-only projection. It is not an end-turn replay or a model of hidden responses.
internal static class AiEndTurnForecast
{
    private static string Compact(string text)=>Regex.Replace(text,@"[\s()]","");
    private static bool OwnerTurnEnd(ConditionConfig condition) => condition.IsEnabled
        && Compact(condition.Expression).Equals("context.triggeringEvent.EndingPlayer.playerId==context.owner.playerId",
            StringComparison.OrdinalIgnoreCase);

    private static bool EmptyHandCondition(ConditionConfig condition) =>
        condition.IsEnabled && Compact(condition.Expression).Equals("countcontext.owner.hand==0",StringComparison.OrdinalIgnoreCase);

    private static bool InvariantConditions(Ability ability)=>ability.Config.Conditions.Where(condition=>condition.IsEnabled).All(condition=> {
        var text=Compact(condition.Expression);
        return text.Equals("true",StringComparison.OrdinalIgnoreCase)
            ||OwnerTurnEnd(condition)
            ||Regex.IsMatch(text,@"^countcontext\.owner\.hand(==|<=|>=|<|>)[0-9]+$",RegexOptions.IgnoreCase);
    });
    private static bool PerTarget(EffectConfig effect) {
        if(!effect.Parameters.TryGetValue("DistributionMode",out var mode))return true;
        if(mode is not StaticValueConfig literal)return false;
        var text=literal.Value?.ToString();return text=="0"||string.Equals(text,"perTarget",StringComparison.OrdinalIgnoreCase);
    }
    private static bool SimpleDamage(Ability ability)=>ability.Config.Effects.Any(effect=>effect.IsEnabled)
        &&ability.Config.Effects.Where(effect=>effect.IsEnabled).All(effect=>effect.Type==EffectType.Damage
            &&effect.RepeatCount==0&&effect.RepeatCountValue==null&&effect.RepeatCondition==null
            &&effect.ChainedAbilities.Count==0&&effect.Outputs?.Count is not >0
            &&effect.Parameters.Keys.All(key=>key.Equals("DamageAmount",StringComparison.OrdinalIgnoreCase)||key.Equals("DistributionMode",StringComparison.OrdinalIgnoreCase))
            &&effect.Parameters.TryGetValue("DamageAmount",out var amount)&&amount is StaticValueConfig&&PerTarget(effect));
    private static bool OptionalRandomFollower(TargetConfig target) {
        if(target.Query.Operations.Count>0||target.PlayerChoiceOptions!=null
            ||target.GetEffectiveTargetHandlingStrategy()!=TargetHandlingStrategy.DoNotIntercept)return false;
        var text=Compact(target.Query.SourcePoolExpression);
        var match=Regex.Match(text,"^context\\.opponent\\.field\\.queryself\\.(type|cardType)==[\"']Follower[\"']\\.takerandom([1-9][0-9]*)$",RegexOptions.IgnoreCase);
        return match.Success&&int.TryParse(match.Groups[2].Value,out var count)&&count<=GameConstants.Field.MaxFieldSize;
    }
    private static bool FixedLeader(TargetConfig target)=>target.Query.Operations.Count==0&&target.PlayerChoiceOptions==null
        &&Compact(target.Query.SourcePoolExpression).Equals("context.opponent.leader",StringComparison.OrdinalIgnoreCase);
    private static bool UsesRandom(TargetConfig target)=>JsonSerializer.Serialize(target.Query).Contains("random",StringComparison.OrdinalIgnoreCase)
        ||JsonSerializer.Serialize(target.Query).Contains("sample",StringComparison.OrdinalIgnoreCase);
    private static bool OpponentOnly(TargetConfig target) {
        if(OptionalRandomFollower(target)||FixedLeader(target))return true;
        if(target.Query.Operations.Count>0||target.PlayerChoiceOptions!=null)return false;
        var text=Compact(target.Query.SourcePoolExpression).ToLowerInvariant();
        const string field="context.opponent.field.queryself.type==\"follower\"";
        return text==field||text==field+"|context.opponent.leader"||text=="context.opponent.leader|"+field;
    }
    private static bool SelfKeywordOnly(Ability ability)=>ability.Config.Targets.Count>0
        &&ability.Config.Targets.Values.Where(target=>target.IsEnabled).All(target=>target.Query.Operations.Count==0&&target.PlayerChoiceOptions==null
            &&Compact(target.Query.SourcePoolExpression).Equals("context.sourceCard",StringComparison.OrdinalIgnoreCase))
        &&ability.Config.Effects.Any(effect=>effect.IsEnabled)
        &&ability.Config.Effects.Where(effect=>effect.IsEnabled).All(effect=>effect.Type==EffectType.SetKeyword&&effect.ChainedAbilities.Count==0
            &&effect.RepeatCount==0&&effect.RepeatCountValue==null&&effect.RepeatCondition==null&&effect.Outputs?.Count is not >0
            &&effect.Parameters.Values.All(value=>value is StaticValueConfig));
    private static readonly HashSet<string> IdleEvents=new(StringComparer.Ordinal) {"PlayCard","Evolve","SuperEvolve","Act","Fusion","TurnStart","CardRevealed","Attack","AttackDeclared","Clash","CombatResolution"};
    private sealed class RefillFollowUps
    {
        internal List<AiTacticalSearch.LaterFieldDraw> Draws { get; } = [];
        internal HashSet<Guid> StatLossSources { get; } = [];
        internal bool HasPostDrawResponse { get; set; }
    }

    private static (CardInstance Card,Ability Ability)? FindPublicInterference(
        GameContext context,Guid actor,Ability current,HashSet<Guid> unsafeAttackers,Guid? refillAbility=null,RefillFollowUps? followUps=null,Guid? verifiedDrawSource=null)
    {
        var refillPrefix = followUps != null;
        (CardInstance Card, Ability Ability)? summonListener = null;
        // Public zones only: inspecting hidden enemy abilities would reveal unavailable information.
        foreach(var card in context.CurrentGameState.AllField.Concat(context.CurrentGameState.AllEmblem).Concat(context.CurrentGameState.AllLeaders))
            foreach(var ability in context.AbilityManager.GetEffectiveAbilitiesForCard(card.InstanceId,context).OfType<Ability>()) {
                if(ability.RuntimeId==current.RuntimeId||ability.RuntimeId==refillAbility||!ability.Config.IsEnabled||!ability.TriggerConfig.IsEnabled||card.IsSilenced
                    ||!ability.TriggerConfig.IsValidZone(card.CurrentZone))continue;
                if(ability.TriggerConfig.EventTypes.Any(type=>type is EventType.Attack or EventType.AttackDeclared or EventType.Clash or EventType.CombatResolution))
                    unsafeAttackers.UnionWith(context.CurrentGameState.GetPlayerState(actor)!.Field.Select(unit=>unit.InstanceId));
                if(ability.Config.Auras.Any(aura=>aura.IsEnabled))return (card,ability);
                if (refillPrefix && card.OwnerPlayerId != actor
                    && ability.TriggerConfig.Timing == TriggerTiming.On
                    && ability.TriggerConfig.EventTypes.SequenceEqual([EventType.TurnEnd])
                    && ability.Config.Conditions.Any(OwnerTurnEnd))
                    continue;
                if (AiHandClearEvolution.IsNativeLeaderCounter(card,ability.Config)) continue;
                if (refillPrefix && ability.TriggerConfig.EventTypes.SequenceEqual([EventType.Summon]))
                {
                    summonListener ??= (card, ability);
                    continue;
                }
                if (card.InstanceId==verifiedDrawSource && TryLaterLeaderEffect(card,ability,out _)) continue;
                if (followUps != null && card.OwnerPlayerId==actor)
                {
                    if (TryLaterLeaderEffect(card,ability,out var draw))
                    {
                        if (draw>0) followUps.Draws.Add(new(card.InstanceId,draw));
                        continue;
                    }
                    if (IsLaterSelfStatLoss(card,ability))
                    {
                        followUps.StatLossSources.Add(card.InstanceId);
                        continue;
                    }
                }
                if (followUps!=null && ability.TriggerConfig.EventTypes.Any(type=>type is EventType.Damage or EventType.Destroy or EventType.Discard))
                    followUps.HasPostDrawResponse=true;
                // With an empty hand, the compiled insert/draw prefix emits no damage, destruction,
                // or discard. These responses still block the later damage projection.
                if(ability.TriggerConfig.EventTypes.All(type=>IdleEvents.Contains(type.ToString())
                        || refillPrefix && (type is EventType.Damage or EventType.Destroy or EventType.Discard))
                    || refillPrefix && card.OwnerPlayerId==actor && IsLaterFieldAction(card,ability)
                    || refillPrefix && card.CurrentZone==CardZone.Field && AiExpiringBoardValue.LeaveAsVanish(ability.Config)
                    || refillAbility.HasValue && IsNightingaleCounter(card.CurrentDefinitionId,ability.Config))continue;
                if(card.OwnerPlayerId==actor&&ability.TriggerConfig.EventTypes.All(type=>type==EventType.TurnEnd)&&InvariantConditions(ability)
                    &&(SelfKeywordOnly(ability)||(SimpleDamage(ability)&&ability.Config.Targets.Values.Where(target=>target.IsEnabled).All(OpponentOnly))))continue;
                return (card,ability);
            }
        // The emblem's insert/draw prefix cannot summon. Keep the later draw total
        // unknown if field actions could trigger a summon listener before those draws.
        return followUps?.Draws.Count>0 ? summonListener : null;
    }
    private static bool IsLaterFieldAction(CardInstance card,Ability ability)
    {
        var config=ability.Config;
        if (card.CurrentZone==CardZone.Field && AiExpiringBoardValue.SelfVanish(config)) return true;
        var trigger=ability.TriggerConfig;
        if (card.CurrentZone!=CardZone.Field || config.Type!=AbilityType.Triggered || config.AbilityKeywords.Count!=0
            || config.PreConditions.Count!=0 || !InvariantConditions(ability) || trigger.Timing!=TriggerTiming.On
            || trigger.RelationType!=TriggerRelationType.Any || !trigger.EventTypes.SequenceEqual([EventType.TurnEnd])
            || config.Targets.Count!=1 || config.Targets.Values.Single() is not {IsEnabled:true} target
            || target.Query.Operations.Count!=0 || target.PlayerChoiceOptions!=null
            || !string.Equals(target.Query.SourcePoolExpression.Trim(),"context.SourceCard",StringComparison.OrdinalIgnoreCase)
            || config.Effects is not [var effect] || !effect.IsEnabled || effect.RepeatCount!=0
            || effect.RepeatCondition!=null || effect.RepeatCountValue!=null || effect.TargetSelector.Count!=0)
            return false;
        // On-TurnEnd effects from the actor's field follow the actor's emblem. Treat these
        // self-actions only as irrelevant to the refill prefix; their later effects remain unknown.
        return effect.Type is EffectType.Tuck or EffectType.Evolve;
    }

    private static bool IsLaterSelfStatLoss(CardInstance card,Ability ability)
    {
        var config=ability.Config;
        var trigger=ability.TriggerConfig;
        if (card.CardType!=CardType.Follower || card.CurrentZone!=CardZone.Field
            || config.Type!=AbilityType.Triggered || config.AbilityKeywords.Count!=0 || config.PreConditions.Count!=0
            || trigger.Timing!=TriggerTiming.On || trigger.RelationType!=TriggerRelationType.Any
            || !trigger.EventTypes.SequenceEqual([EventType.TurnEnd])
            || config.Conditions is not [{IsEnabled:true} condition] || !OwnerTurnEnd(condition)
            || config.Targets.Count!=1 || config.Targets.Values.Single() is not {IsEnabled:true} target
            || target.PlayerChoiceOptions!=null || target.Query.Operations.Count!=0
            || !string.Equals(Compact(target.Query.SourcePoolExpression),"context.sourceCard",StringComparison.OrdinalIgnoreCase)
            || config.Effects is not [{Type:EffectType.ModifyStatus,IsEnabled:true,RepeatCount:0,
                RepeatCondition:null,RepeatCountValue:null} effect] || effect.TargetSelector.Count!=0
            || effect.ChainedAbilities.Count!=0 || effect.Outputs is {Count:>0} || effect.Parameters.Count!=5)
            return false;
        string? Value(string key)=>(effect.Parameters.GetValueOrDefault(key) as StaticValueConfig)?.Value?.ToString();
        // Ice Prison's granted effect only reduces the source follower's stats. A resulting
        // death and its responses can still affect the separate draws from field abilities.
        return int.TryParse(Value("AttackModifier"),out var attack) && attack<0
            && int.TryParse(Value("HealthModifier"),out var health) && health<0
            && string.Equals(Value("DurationType"),"permanent",StringComparison.OrdinalIgnoreCase)
            && Value("DurationParameter")=="0" && string.Equals(Value("ModifierLayer"),"additive",StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryLaterLeaderEffect(CardInstance card,Ability ability,out int draw)
    {
        draw = 0;
        var config = ability.Config;
        var trigger = ability.TriggerConfig;
        if (card.CurrentZone!=CardZone.Field || config.Type!=AbilityType.Triggered || config.AbilityKeywords.Count!=0
            || trigger.Timing!=TriggerTiming.On || trigger.RelationType!=TriggerRelationType.Any
            || !trigger.EventTypes.SequenceEqual([EventType.TurnEnd]) || trigger.CountLimit!=null
            || trigger.LimitSameNameOncePerTransaction || config.PreConditions.Count!=0
            || config.Conditions is not [{IsEnabled:true} condition]
            || !OwnerTurnEnd(condition)
            || config.Targets.Count!=1 || config.Targets.Values.Single() is not {IsEnabled:true} target
            || target.Query.Operations.Count!=0 || target.PlayerChoiceOptions!=null
            || !string.Equals(Compact(target.Query.SourcePoolExpression),"context.owner.leader",StringComparison.OrdinalIgnoreCase)
            || config.Effects is not [var effect] || !effect.IsEnabled || effect.RepeatCount!=0
            || effect.RepeatCondition!=null || effect.RepeatCountValue!=null || effect.TargetSelector.Count!=0
            || effect.ChainedAbilities.Count!=0 || effect.Outputs is {Count:>0})
            return false;
        string? Value(string key) => (effect.Parameters.GetValueOrDefault(key) as StaticValueConfig)?.Value?.ToString();
        if (effect.Type==EffectType.Heal)
            return effect.Parameters.Count==1 && int.TryParse(Value("HealAmount"),out var heal) && heal>0;
        return effect.Type==EffectType.Draw && effect.Parameters.Count==3 && Value("PlayerScope")=="self"
            && string.Equals(Value("UseSearchMode"),"false",StringComparison.OrdinalIgnoreCase)
            && int.TryParse(Value("DrawAmount"),out draw) && draw is >0 and <=GameConstants.Hand.MaxHandSize;
    }

    private static bool IsNightingaleCounter(int cardId,AbilityConfig ability)
    {
        if (cardId != 1473441 || ability.Type != AbilityType.Triggered || ability.Auras.Count != 0
            || ability.AbilityKeywords.Count != 0 || ability.PreConditions.Count != 0
            || ability.Targets.Values.Any(target=>target.Query.Operations.Count!=0 || target.PlayerChoiceOptions!=null
                || !string.Equals(target.Query.SourcePoolExpression.Trim(),"context.SourceCard",StringComparison.OrdinalIgnoreCase))
            || ability.Conditions.Count != 0 || ability.Trigger.Timing != TriggerTiming.On
            || !ability.Trigger.EventTypes.ToHashSet().SetEquals([EventType.AbilityTriggered,EventType.AddCardToHand,EventType.PlayCard,EventType.Choose])
            || ability.Effects is not [var effect] || effect.Type != EffectType.ModifyVariable || !effect.IsEnabled
            || effect.RepeatCount != 0 || effect.RepeatCountValue != null || effect.RepeatCondition != null
            || effect.ChainedAbilities.Count != 0 || effect.TargetSelector.Count != 0 || effect.Outputs is { Count: > 0 }
            || effect.Parameters.Count != 5)
            return false;
        string? Value(string key) => (effect.Parameters.GetValueOrDefault(key) as StaticValueConfig)?.Value?.ToString();
        // This counter reads spent blood pool and only writes back to Nightingale.
        // Reactions to CounterChanged still require their own check.
        return string.Equals(Value("TargetType"),"Card",StringComparison.OrdinalIgnoreCase)
            && Value("CounterName") == "已消费的血池数" && string.Equals(Value("Operation"),"Set",StringComparison.OrdinalIgnoreCase)
            && Value("PlayerScope") == "self" && effect.Parameters.GetValueOrDefault("Amount") is FormulaValueConfig amount
            && Compact(amount.Formula).ToLowerInvariant() ==
                "sumbytypecontext.eventhistory.events,\"counterchanged\".queryself.sourceplayer.playerid==context.owner.playerid"
                + ".queryself.countername==\"bloodpool\".queryself.oldvalue>=self.newvalue.selectself.oldvalue-self.newvalue";
    }

    private static bool TryReadRefill(Ability ability, bool superUnlocked, out int draw)
    {
        draw = 0;
        var config = ability.Config;
        var trigger = ability.TriggerConfig;
        static string? Value(EffectConfig effect, string key) =>
            (effect.Parameters.GetValueOrDefault(key) as StaticValueConfig)?.Value?.ToString();
        static bool Plain(EffectConfig effect) => effect.IsEnabled && effect.RepeatCount == 0
            && effect.RepeatCountValue == null && effect.RepeatCondition == null
            && effect.TargetSelector.Count == 0 && effect.Outputs is not { Count: > 0 };
        static bool Scope(AbilityConfig candidate) => candidate.IsEnabled && candidate.Type == AbilityType.Triggered
            && candidate.Targets.Count == 0 && candidate.Auras.Count == 0 && candidate.PreConditions.Count == 0
            && candidate.AbilityKeywords.Count == 0 && candidate.Trigger.IsEnabled && candidate.Trigger.Timing == TriggerTiming.On
            && candidate.Trigger.RelationType == TriggerRelationType.Any && candidate.Trigger.CountLimit == null
            && !candidate.Trigger.LimitSameNameOncePerTransaction && candidate.Trigger.IsValidZone(CardZone.Emblem);

        if (!Scope(config) || !trigger.EventTypes.SequenceEqual([EventType.TurnEnd])
            || config.Conditions.Where(condition=>condition.IsEnabled).Select(condition=>Compact(condition.Expression).ToLowerInvariant())
                .ToHashSet().SetEquals(["context.triggeringevent.endingplayer.playerid==context.owner.playerid","countcontext.owner.hand==0"]) != true
            || config.Effects is not [var root] || !Plain(root) || root.Type != EffectType.None
            || root.Parameters.Count != 0 || root.ChainedAbilities.Count != 3)
            return false;
        var added = false;
        var early = false;
        var late = false;
        foreach (var (branch,index) in root.ChainedAbilities.Select((branch,index)=>(branch,index)))
        {
            if (!Scope(branch) || !branch.Trigger.EventTypes.SequenceEqual([EventType.EffectCompleted])
                || branch.Effects is not [var effect] || !Plain(effect) || effect.ChainedAbilities.Count != 0
                || !string.Equals(Value(effect,"PlayerScope"),"self",StringComparison.OrdinalIgnoreCase))
                return false;
            if (effect.Type == EffectType.AddDeck && branch.Conditions.Count == 0 && effect.Parameters.Count == 5
                && Value(effect,"CardSourceMode") == "definition" && Value(effect,"Amount") == "2" && Value(effect,"InsertMode") == "random"
                && Value(effect,"CardDefinitionId") is { } ids && Regex.IsMatch(ids, @"^\[\s*1474120\s*\]$"))
            {
                if (index != 0) return false;
                added = true;
                continue;
            }
            if (effect.Type != EffectType.Draw || effect.Parameters.Count != 3
                || !string.Equals(Value(effect,"UseSearchMode"),"false",StringComparison.OrdinalIgnoreCase)
                || branch.Conditions is not [{ IsEnabled: true } condition])
                return false;
            switch (Compact(condition.Expression).ToLowerInvariant(),Value(effect,"DrawAmount"))
            {
                case ("context.owner.issuperevolutionunlocked==false","2") when !early: early = true; break;
                case ("context.owner.issuperevolutionunlocked","4") when !late: late = true; break;
                default: return false;
            }
        }
        if (!added || !early || !late) return false;
        draw = superUnlocked ? 4 : 2;
        return true;
    }

    // These permanent modifiers survive Deck -> Hand without creating a removal or trigger.
    // Runtime abilities still have their own draw-window check.
    private static bool IsPassiveDrawBuff(Buff buff) =>
        buff.DurationType==DurationType.Permanent && !buff.RemoveAfterUse
        && buff.RemovalTrigger==null && buff.RemovalCondition==null
        && buff.Effects.All(effect=>effect switch
        {
            StatBuffEffect stat => stat.Modifications.All(modifier=>modifier.Stat==StatType.Attack
                && modifier.Layer==ModifierLayer.Additive && double.IsFinite(modifier.Value)),
            KeywordBuffEffect keyword => keyword.GrantedKeywords.All(value=>value==CardKeyword.Storm),
            _ => false
        });

    // Verify the gap between the emblem and a single later draw. The broad refill check may
    // admit self-tuck, evolution, or damage reactions that could add cards during this gap.
    private static HashSet<Guid>? DamageBeforeLaterDraw(GameContext context,PlayerState owner,TurnEndEvent ending,
        Ability crest,Guid laterSource)
    {
        var game=context.CurrentGameState;
        var enemy=game.GetOpponentPlayerState(owner.PlayerId);
        if (enemy?.Leader is not { } leader || context.BuffManager.GetBuffsForCard(leader.InstanceId).Any(buff=>buff.IsActive))
            return null;

        var ordered=new List<IAbility>();
        Ability? later=null;
        var visible=game.AllField.Concat(game.AllEmblem).Concat(game.AllLeaders).Concat(owner.Hand);
        foreach (var card in visible)
        {
            if (context.BuffManager.GetBuffsForCard(card.InstanceId).Any(buff=>buff.IsActive
                && (buff.RemovalTrigger!=null || buff.RemovalCondition!=null))) return null;
            foreach (var runtime in context.AbilityManager.GetEffectiveAbilitiesForCard(card.InstanceId,context))
            {
                if (runtime is not Ability ability) return null;
                var config=ability.Config;
                var trigger=ability.TriggerConfig;
                if (!config.IsEnabled || !trigger.IsEnabled || card.IsSilenced
                    || !trigger.IsValidZone(card.CurrentZone) || ability.RuntimeId==crest.RuntimeId) continue;
                if (config.Auras.Any(aura=>aura.IsEnabled)) return null;
                if (AiHandClearEvolution.IsNativeLeaderCounter(card,config) || IsNightingaleCounter(card.CurrentDefinitionId,config)
                    || trigger.EventTypes.All(type=>IdleEvents.Contains(type.ToString()))) continue;
                if (card.OwnerPlayerId!=owner.PlayerId || card.CurrentZone!=CardZone.Field) return null;
                if (card.InstanceId==laterSource && TryLaterLeaderEffect(card,ability,out var draw))
                {
                    if (draw==0) continue;
                    if (later!=null) return null;
                    later=ability;
                    ordered.Add(ability);
                    continue;
                }
                if (card.InstanceId==laterSource || config.Type!=AbilityType.Triggered || config.AbilityKeywords.Count!=0
                    || trigger.Timing!=TriggerTiming.On || !trigger.EventTypes.SequenceEqual([EventType.TurnEnd])
                    || trigger.CountLimit!=null || trigger.LimitSameNameOncePerTransaction
                    || !InvariantConditions(ability) || !SimpleDamage(ability)
                    || !config.Targets.Values.Where(target=>target.IsEnabled).All(OpponentOnly)) return null;
                // The nonempty-hand forecast only projects damage after a clear when exactly one
                // empty-hand condition changes. Other hand tests could hide an earlier lethal.
                if (owner.Hand.Count!=0 && (config.Conditions.Count(EmptyHandCondition)!=1
                    || config.Conditions.Any(condition=>condition.IsEnabled && !EmptyHandCondition(condition)
                        && Compact(condition.Expression).StartsWith("countcontext.owner.hand",StringComparison.OrdinalIgnoreCase))))
                    return null;
                ordered.Add(ability);
            }
        }
        if (later==null || !later.Trigger.IsEnabled) return null;

        // The exact registration tie-breaker is irrelevant when the draw and damage have distinct positions.
        var policy=context.ServiceProvider.GetService<ITriggerOrderingPolicy>() ?? new DefaultTriggerOrderingPolicy();
        var queue=policy.OrderTriggeredAbilities(ordered,ending,context,new Dictionary<Guid,long>());
        var drawKey=queue.Single(entry=>entry.Ability.RuntimeId==later.RuntimeId).Key;
        if (queue.Any(entry=>entry.Ability.RuntimeId!=later.RuntimeId && entry.Key==drawKey)) return null;
        return queue.TakeWhile(entry=>entry.Ability.RuntimeId!=later.RuntimeId).Select(entry=>entry.Ability.RuntimeId).ToHashSet();
    }

    private static async Task<(AiTacticalSearch.PendingRefill? Refill,Guid? Ability,AiRefillBlock? Block,
        HashSet<Guid>? BeforeLaterDraw)> ReadRefillAsync(
        GameContext context,PlayerStateDto player,PlayerState owner,TurnEndEvent ending,HashSet<Guid> unsafeAttackers,bool mayClearHand)
    {
        if (player.Hand.Count != 0 && !mayClearHand) return (null,null,null,null);
        var crests = owner.Emblem.Where(card=>!card.IsSilenced && card.CurrentDefinitionId==AiEternalEngine.Emblem).Take(2).ToArray();
        if (crests.Length == 0) return (null,null,null,null);
        if (crests is not [var crest]) return (null,null,new("multiple-refill-emblems"),null);
        if (context.BuffManager.GetBuffsForCard(crest.InstanceId).Any(buff=>buff.IsActive))
            return (null,null,new("modified-refill-emblem"),null);
        var effective = context.AbilityManager.GetEffectiveAbilitiesForCard(crest.InstanceId,context).ToArray();
        if (effective is not [Ability ability] || !TryReadRefill(ability,owner.IsSuperEvolutionUnlocked,out var draw)
            || GameDataProvider.Instance.GetCardDefinition(AiEternalEngine.Reiss) == null)
            return (null,null,new("unrecognized-refill"),null);
        var evaluation = context with { SourceCard=crest,SourceAbility=ability,TriggeringEvent=ending };
        if (!await ability.CanTrigger(ending,evaluation)) return (null,null,new("refill-trigger-unavailable"),null);
        var followUps=new RefillFollowUps();
        if (FindPublicInterference(context,player.PlayerId,ability,unsafeAttackers,ability.RuntimeId,followUps) is { } response)
            return (null,null,new("public-reaction",response.Card.CurrentDefinitionId,response.Card.CurrentZone.ToString(),
                response.Ability.TriggerConfig.EventTypes.Select(type=>type.ToString()).ToArray()),null);

        var laterDraws=followUps.Draws;
        var laterUnknown=followUps.StatLossSources.Count>0 && followUps.HasPostDrawResponse;
        var requested = draw + laterDraws.Sum(effect=>effect.DrawCount);
        if (requested > GameConstants.Hand.MaxHandSize)
            return (null,null,new("combined-refill-overdraw"),null);

        // The refill resolves before field damage. Check potential responses without reading or using draw order.
        bool QuietWhenDrawn(int cardId,AbilityConfig config) => !config.IsEnabled || !config.Trigger.IsEnabled
            || (!config.Trigger.IsValidZone(CardZone.Deck) && !config.Trigger.IsValidZone(CardZone.Hand))
            || (!config.Auras.Any(aura=>aura.IsEnabled)
                && (IsNightingaleCounter(cardId,config) || config.Trigger.EventTypes.All(type=>
                    IdleEvents.Contains(type.ToString()) || type == EventType.Discard)));
        if ((GameDataProvider.Instance.GetAbilityConfigs(AiEternalEngine.Reiss) ?? []).Any(config=>!QuietWhenDrawn(AiEternalEngine.Reiss,config))
            || owner.Deck.Any(card=>context.BuffManager.GetBuffsForCard(card.InstanceId).Any(buff=>buff.IsActive&&!IsPassiveDrawBuff(buff))
                || context.AbilityManager.GetEffectiveAbilitiesForCard(card.InstanceId,context)
                    .Any(runtime=>runtime is not Ability entry || !QuietWhenDrawn(card.CurrentDefinitionId,entry.Config))))
            return (null,null,new("possible-draw-pile-reaction"),null);
        var available = player.DeckCount + AiTacticalSearch.PendingRefill.AddedCards;
        if (available < draw)
            return (new(crest.InstanceId,draw,[]),null,new("deck-exhaustion"),null);
        if (laterUnknown)
            return (new AiTacticalSearch.PendingRefill(crest.InstanceId,draw,[]){LaterDrawsKnown=false},ability.RuntimeId,
                new("possible-post-draw-stat-loss-response"),null);
        if (available < requested)
        {
            if (laterDraws is [var later] && DamageBeforeLaterDraw(context,owner,ending,ability,later.SourceId) is { } before)
                return (new(crest.InstanceId,draw,[later]),ability.RuntimeId,new("combined-refill-deck-exhaustion"),before);
            return (null,null,new("combined-refill-deck-exhaustion"),null);
        }
        return (new(crest.InstanceId,draw,laterDraws.ToArray()),ability.RuntimeId,null,null);
    }

    internal static Task<AiTacticalSearch.EndTurnForecast> ReadAsync(GameContext context,PlayerStateDto player) =>
        ReadForPolicyAsync(context,player,projectRefill:true);

    internal static async Task<AiTacticalSearch.EndTurnForecast> ReadForPolicyAsync(
        GameContext context,PlayerStateDto player,bool projectRefill)
    {
        var damage=new List<AiTacticalSearch.PendingDamage>();var omissions=new List<AiTacticalSearch.ForecastOmission>();
        var unsafeAttackers=new HashSet<Guid>();
        var handClears = await AiHandClearEvolution.ReadAsync(context,player);
        var emptyHandDamage = new List<AiTacticalSearch.PendingDamage>();
        var query=context.ServiceProvider.GetRequiredService<IQueryExecutor>();
        var conditions=context.ServiceProvider.GetRequiredService<IConditionEvaluator>();
        var owner=context.CurrentGameState.GetPlayerState(player.PlayerId)!;var ending=new TurnEndEvent(owner);
        Guid? lastHandPlay = projectRefill && owner.IsSuperEvolutionUnlocked && player.DeckCount < 2
            && owner.Emblem.Any(card => card.CurrentDefinitionId == AiEternalEngine.Emblem && !card.IsSilenced)
            && player.Hand is [var last] && owner.Hand is [var nativeLast] && last.InstanceId == nativeLast.InstanceId
            && AiHandMaterialRules.IsQuietLastFollowerPlay(context, nativeLast) ? last.InstanceId : null;
        var (refill,refillAbility,refillBlock,beforeLaterDraw) = projectRefill
            ? await ReadRefillAsync(context,player,owner,ending,unsafeAttackers,handClears.Length>0 || lastHandPlay.HasValue)
            : (null,null,null,null);
        var verifiedDrawSource=beforeLaterDraw!=null ? refill!.LaterDraws.Single().SourceId : (Guid?)null;
        foreach(var view in player.Field.Concat(player.Emblem)) {
            var card=context.CurrentGameState.FindCardInstance(view.InstanceId);if(card==null)continue;
            foreach(var ability in context.AbilityManager.GetEffectiveAbilitiesForCard(card.InstanceId,context).OfType<Ability>()) {
                if(ability.TriggerConfig.EventTypes.Any(type=>type is EventType.Attack or EventType.AttackDeclared or EventType.Clash or EventType.CombatResolution))
                    unsafeAttackers.UnionWith(player.Field.Select(unit=>unit.InstanceId));
                if(!ability.Config.IsEnabled||!ability.TriggerConfig.IsEnabled||!ability.TriggerConfig.EventTypes.Contains(EventType.TurnEnd))continue;
                if (ability.RuntimeId == refillAbility) continue;
                void Omit(string reason)=>omissions.Add(new(card.InstanceId,reason));
                if(ability.TriggerConfig.CountLimit!=null||ability.TriggerConfig.LimitSameNameOncePerTransaction
                    ||ability.AbilityKeywords.Any(keyword=>keyword.Keyword!=AbilityKeyword.None)||!InvariantConditions(ability)||!SimpleDamage(ability)) {Omit("unsupported-keyword-condition-count-or-effects");continue;}
                var enabled=ability.Targets.Where(target=>target.IsEnabled&&target.Config.IsEnabled).OrderBy(target=>target.Config.Order??0).ToArray();
                if(enabled.Any(target=>target.Config.PlayerChoiceOptions?.IsRequired==true)) {Omit("player-choice-required");continue;}
                var partial=enabled.Any(target=>UsesRandom(target.Config));
                if(enabled.Any(target=>target.Config.Query.Operations.Count>0)
                    ||(partial&&enabled.Any(target=>!OptionalRandomFollower(target.Config)&&!FixedLeader(target.Config)))) {Omit("unsupported-target-query");continue;}
                var evaluation=context with {SourceCard=card,SourceAbility=ability,TriggeringEvent=ending};
                if (!await ability.CanTrigger(ending,evaluation)) continue;
                var unmet = ability.Config.Conditions.Where(condition=>condition.IsEnabled&&!conditions.Evaluate(condition,evaluation)).ToArray();
                var afterClear = handClears.Length > 0 && unmet.Length == 1
                    && EmptyHandCondition(unmet[0])
                    && ability.Config.Conditions.Count(condition=>condition.IsEnabled
                        && Compact(condition.Expression).StartsWith("countcontext.owner.hand",StringComparison.OrdinalIgnoreCase))==1;
                if (unmet.Length > 0 && !afterClear) continue;
                if (FindPublicInterference(context,player.PlayerId,ability,unsafeAttackers,refillAbility,verifiedDrawSource:verifiedDrawSource) != null)
                {
                    if (!afterClear || refill != null) Omit("public-reactions-not-simulated");
                    continue;
                }
                var targets=new Dictionary<string,List<TargetInfo>>(StringComparer.OrdinalIgnoreCase);var valid=true;var unknownRandom=false;
                foreach(var target in enabled) {
                    if(OptionalRandomFollower(target.Config)) {
                        // A singleton pool always resolves to that follower. Do not consume the live random sequence.
                        var pool=query.ExecuteQuery(new QueryConfig{SourcePoolExpression="context.opponent.field.query(self.type == \"Follower\")"},evaluation,targets);
                        if(!pool.Success||pool.Results.Any(value=>value is not CardInstance)){valid=false;break;}
                        var followers=pool.Results.Cast<CardInstance>().ToArray();
                        if(followers.Length==1) targets[target.Key]=[TargetInfo.ForCard(followers[0])];
                        else {
                            targets[target.Key]=[];
                            unknownRandom|=followers.Length>1;
                        }
                        continue;
                    }
                    var found=query.ExecuteQuery(target.Config.Query,evaluation,targets);
                    if(!found.Success||found.Results.Any(value=>value is not CardInstance)){valid=false;break;}
                    var resolved=found.Results.OfType<CardInstance>().Select(TargetInfo.ForCard).ToList();
                    if(resolved.Count==0&&target.Config.ShouldBlockAbilityExecutionOnInsufficientTargets()){valid=false;break;}
                    targets[target.Key]=resolved;
                }
                if(!valid){Omit("target-query-failed-or-required-pool-empty");continue;}
                foreach(var effect in ability.Config.Effects.Where(effect=>effect.IsEnabled).OrderBy(effect=>effect.ExecutionOrder)) {
                    var amount=AiTargetScoring.ReadAmount(effect);
                    if(amount is not >0||amount>1000||amount!=Math.Floor(amount.Value)
                        ||!EffectTargetSelector.TrySelectTargets(effect.TargetSelector,targets,out var selected))continue;
                    var ids=selected.Select(target=>target.Card?.InstanceId??Guid.Empty).Where(id=>id!=Guid.Empty).Distinct().ToArray();
                    if (ids.Length > 0)
                    {
                        var hit=new AiTacticalSearch.PendingDamage(card.InstanceId,ids,(int)amount.Value,afterClear?0:player.Hand.Count)
                        { BeforeLaterDraw=beforeLaterDraw?.Contains(ability.RuntimeId)==true };
                        if (afterClear) emptyHandDamage.Add(hit);
                        else damage.Add(hit);
                    }
                }
                if(unknownRandom)Omit("random-follower-damage-not-projected");
            }
        }
        var shortage=refill!=null && player.DeckCount+AiTacticalSearch.PendingRefill.AddedCards<refill.CountOnBoard(player);
        return new(damage,unsafeAttackers)
        {
            Omissions=omissions,ExpiringFieldCards=AiExpiringBoardValue.Read(context,player),
            HandClearEvolutions=handClears,EmptyHandDamage=emptyHandDamage,
            Refill=shortage?null:refill,RefillBlock=refillBlock,
            DeckExhaustion=shortage?refill:null,
            LastHandPlayIntoExhaustion=shortage?lastHandPlay:null
        };
    }
    internal static AiStrategyPlanner.AutomaticEndDamageContext? ForMemory(AiTacticalSearch.EndTurnForecast? forecast,
        GameStateDto snapshot,Guid actor,Guid? attacker=null)
    {
        if(forecast==null)return null;
        var enemy=snapshot.Players.First(player=>player.PlayerId!=actor);
        var own=snapshot.Players.First(player=>player.PlayerId==actor);
        var sources=forecast.Damage.Select(hit=>hit.SourceId).Distinct().ToHashSet();
        var ids=own.Field.Concat(own.Emblem).Where(card=>sources.Contains(card.InstanceId)).Select(card=>card.CardId).Distinct().Order().Take(8).ToArray();
        var field=enemy.Field.Select(card=>card.InstanceId).ToHashSet();
        return new(ids,sources.Count,forecast.Damage.Where(hit=>enemy.LeaderCard!=null&&hit.TargetIds.Contains(enemy.LeaderCard.InstanceId)).Sum(hit=>hit.Amount),
            forecast.Damage.SelectMany(hit=>hit.TargetIds).Where(field.Contains).Distinct().Count(),forecast.Omissions.Count,
            enemy.LeaderCard!=null&&(enemy.LeaderCard.Keywords.Count>0||enemy.LeaderCard.BuffSummaries.Count>0||enemy.LeaderCard.EffectDescriptions.Count>0),
            attacker.HasValue?forecast.UnsafeAttackers.Contains(attacker.Value):null);
    }
    internal static object? ForModel(AiTacticalSearch.EndTurnForecast? forecast,GameStateDto snapshot,Guid actor)
    {
        if(forecast==null)return null;
        object? Locate(Guid id) {
            foreach(var player in snapshot.Players) {
                if(player.LeaderCard?.InstanceId==id)return new {side=player.PlayerId==actor?"own":"enemy",zone="leader",index=0,cardId=player.LeaderCard.CardId};
                var field=player.Field.FindIndex(card=>card.InstanceId==id);
                if(field>=0)return new {side=player.PlayerId==actor?"own":"enemy",zone="field",index=field,cardId=player.Field[field].CardId};
                var emblem=player.Emblem.FindIndex(card=>card.InstanceId==id);
                if(emblem>=0)return new {side=player.PlayerId==actor?"own":"enemy",zone="emblem",index=emblem,cardId=player.Emblem[emblem].CardId};
            }
            return null;
        }
        var own=snapshot.Players.Single(player=>player.PlayerId==actor);
        return new {
            scope="Public automatic damage under current source/hand conditions. For each listed handClearEvolution, discardCount is the number of current hand cards that will be discarded. requiresDiscardChoice=false means the engine automatically discards all eligible cards when the pool is short; true means the actual prompt requires selecting all listed materials. DamageAfterHandClear assumes that evolution empties the hand and no further action changes the position. Refill requires an empty hand when turn-end abilities trigger. LaterFieldDraws are separate draws if those sources are still in play and the game continues. If laterDrawsKnown is false, only the emblem batch is forecast. A refill projection does not resolve any later damage response. Counts include no draw identities. These are bounded projections.",
            expectedExpiringFieldCards=forecast.ExpiringFieldCards.Select(Locate).Where(value=>value!=null),
            expirationScope="Bounded current own self-Vanish rules: only continued board value is discounted. Not a removal replay, changed combat legality/damage, final survival or lethal proof. Unknown public protection/interception gives no discount.",
            refill=forecast.Refill is { } refill?new { source=Locate(refill.SourceId), drawCount=refill.DrawCount, requiredHandCount=0,
                laterDrawsKnown=refill.LaterDrawsKnown,laterFieldDraws=refill.LaterDraws.Select(effect=>new {source=Locate(effect.SourceId),drawCount=effect.DrawCount}) }:null,
            deckExhaustion=forecast.DeckExhaustion is { } shortage ? new
            {
                source=Locate(shortage.SourceId),drawCount=shortage.DrawCount,requiredHandCount=0,
                availableAfterInsert=own.DeckCount+AiTacticalSearch.PendingRefill.AddedCards,
                laterDraw=shortage.LaterDraws is [var late] ? new {source=Locate(late.SourceId),drawCount=late.DrawCount} : null,
                damageBeforeLaterDraw=forecast.Damage.Concat(forecast.EmptyHandDamage).Where(hit=>hit.BeforeLaterDraw)
                    .Select(hit=>new {source=Locate(hit.SourceId),baseDamagePerTarget=hit.Amount,
                        targets=hit.TargetIds.Select(Locate).Where(value=>value!=null)}),
                lastHandPlay=own.Hand is [var last] && last.InstanceId==forecast.LastHandPlayIntoExhaustion
                    ? new {handIndex=0,cardId=last.CardId,mode=nameof(PlayMode.Base)} : null
            } : null,
            handClearEvolution=forecast.HandClearEvolutions.Select(route=>new {
                source=Locate(route.Source),discardCount=route.Materials.Length,heal=route.Heal,ordinary=route.Ordinary,super=route.Super,requiresDiscardChoice=route.RequiresDiscardChoice
            }),
            damageAfterHandClear=forecast.EmptyHandDamage.Select(hit=>new {source=Locate(hit.SourceId),baseDamagePerTarget=hit.Amount,
                targets=hit.TargetIds.Select(Locate).Where(value=>value!=null)}),
            damage=forecast.Damage.Select(hit=>new {source=Locate(hit.SourceId),baseDamagePerTarget=hit.Amount,requiredHandCount=hit.HandCount,
                targets=hit.TargetIds.Select(Locate).Where(value=>value!=null)}),
            omitted=forecast.Omissions.Select(item=>new {source=Locate(item.SourceId),item.Reason})
        };
    }
}