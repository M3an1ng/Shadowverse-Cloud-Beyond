using System.Text.Json;
using ShadowverseBeyond.Server.Domain.Constants;
using System.Text.Json.Serialization;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;

namespace ShadowverseBeyond.Server.Services;

// Explicit public/own-hand references, never raw search states, action DTOs or runtime GUIDs.
internal static class AiLocalTacticalFacts
{
    internal sealed record Location(string Side,string Zone,int Index,int CardId);
    internal sealed record Step(int Order,string Kind,Location Source,Location? Target,int? PaidPP,string? PlayMode,
        int SourceAttack,int SourceHealth,int? TargetHealth);
    internal sealed record Influence(Location? Instance,string Reason,int Count=1);
    internal sealed record Facts(bool Available,string? Reason,Step[] Steps,bool ProjectsLethal,bool PreventsVisibleLethal,
        double? UtilityGain,AiTacticalSearch.Outcome? ProjectedOutcome,AiCombatThreat.Estimate VisibleThreat,
        Influence[] UnmodelledInputs,int OmittedInputs,bool NativeInputsChecked,
        string Source="Bounded action search with optional compiled finite precombat effects; not committed events or full ability simulation.",
        int CompiledCombatRules=0,int OmittedCombatRules=0,int LeaderRecovery=0)
    {
        [JsonIgnore] internal string Position {get;init;}="";
        [JsonIgnore] internal Guid Actor {get;init;}
        [JsonIgnore] internal long? Revision {get;init;}
        internal bool CanSkipPlanning => Available&&NativeInputsChecked&&UnmodelledInputs.Length==0&&OmittedInputs==0
            &&ProjectsLethal&&ProjectedOutcome is {EnemyHealth:<=0,OwnHealth:>0}&&Steps.Length>0&&Steps.All(step=>step.Kind is "PerformAttack" or "PerformEvolve" or "PerformSuperEvolve" or "PlayCard");
    }
    private static readonly HashSet<CardKeyword> KnownKeywords=[CardKeyword.Storm,CardKeyword.Rush,CardKeyword.Ward,
        CardKeyword.Shield,CardKeyword.Bane,CardKeyword.Drain,CardKeyword.DestroyResist,CardKeyword.EffectImmune,
        CardKeyword.IgnoreWard,CardKeyword.CannotAttackLeader,CardKeyword.CannotAttackFollower,CardKeyword.Shackle,
        CardKeyword.Ambush,CardKeyword.Pressure,CardKeyword.BanEvolve,CardKeyword.FreeEvolve];

    internal static Facts Read(GameContext? context,GameStateDto snapshot,Guid actor,AiTacticalSearch.Plan? plan,long? snapshotRevision=null)
        =>ReadWithCombat(context,snapshot,actor,plan,snapshotRevision,null);

    internal static Facts ReadWithCombat(GameContext? context,GameStateDto snapshot,Guid actor,AiTacticalSearch.Plan? plan,long? snapshotRevision,AiCombatProjection.Frame? combatRules)
    {
        var own=snapshot.Players.Single(player=>player.PlayerId==actor);
        var enemy=snapshot.Players.Single(player=>player.PlayerId!=actor);
        var threat=AiCombatThreat.Evaluate(enemy.Field,own.Field,own.LeaderCard);
        Facts Missing(string reason)=>new(false,reason,[],false,false,null,null,threat,[],0,false);
        var locations=new Dictionary<Guid,Location>();var visible=new List<CardInstanceDto>();
        bool Add(IEnumerable<CardInstanceDto> cards,string side,string zone)
        {
            var index=0;foreach(var card in cards)
            {
                if(card.InstanceId==Guid.Empty||!locations.TryAdd(card.InstanceId,new(side,zone,index++,card.CardId)))return false;
                visible.Add(card);
            }
            return true;
        }
        if(!Add(own.Hand,"own","hand")||!Add(own.Field,"own","field")||!Add(enemy.Field,"enemy","field")
            ||!Add(own.Emblem,"own","emblem")||!Add(enemy.Emblem,"enemy","emblem")
            ||own.LeaderCard!=null&&!Add([own.LeaderCard],"own","leader")
            ||enemy.LeaderCard!=null&&!Add([enemy.LeaderCard],"enemy","leader"))return Missing("ambiguous-visible-instance-bindings");
        var steps=new List<Step>();
        if(plan!=null)
        {
            if(plan.Steps.Count is <1 or >8||plan.Outcome==null||!double.IsFinite(plan.Gain))return Missing("bounded-search-path-unavailable");
            foreach(var move in plan.Steps)
            {
                if(!locations.TryGetValue(move.Source.InstanceId,out var source))return Missing("unbound-source-in-search-path");
                Location? target=null;
                if(move.Target!=null&&!locations.TryGetValue(move.Target.InstanceId,out target))return Missing("unbound-target-in-search-path");
                var play=move.Data as PlayCardActionDto;
                if(move.Type=="PlayCard"&&(play?.PaidCost==null||play.PlayMode==null))return Missing("incomplete-play-in-search-path");
                steps.Add(new(steps.Count+1,move.Type,source,target,play?.PaidCost,play?.PlayMode?.ToString(),
                    move.Source.CurrentAttack,move.Source.CurrentHealth,move.Target?.CurrentHealth));
            }
        }
        var revision=context?.CurrentGameState.StateRevision;
        var influences=new List<Influence>();var checkedNative=context!=null&&snapshotRevision.HasValue&&revision==snapshotRevision&&context.CurrentGameState.GameId==snapshot.GameId
            &&context.CurrentGameState.CurrentPlayerId==actor;
        if(own.GraveyardCount>0||enemy.GraveyardCount>0)
            influences.Add(new(null,"off-board-card-responses-not-evaluated"));
        if(steps.Any(step=>step.Kind is "PerformEvolve")&&!own.IsEvolutionUnlocked
            ||steps.Any(step=>step.Kind is "PerformSuperEvolve")&&!own.IsSuperEvolutionUnlocked)
            influences.Add(new(null,"evolution-unlock-not-verified"));
        var played=plan?.Steps.Where(move=>move.Type=="PlayCard").Select(move=>move.Source.InstanceId).ToHashSet()??[];
        foreach(var view in visible)
        {
            var location=locations[view.InstanceId];
            if(view.Keywords.Any(keyword=>!KnownKeywords.Contains(keyword)))influences.Add(new(location,"keyword-outside-basic-search"));
            if(!checkedNative)continue;
            try
            {
                var card=context!.CurrentGameState.FindCardInstance(view.InstanceId);
                if(card==null||card.CurrentDefinitionId!=view.CardId||card.OwnerPlayerId!=view.OwnerPlayerId||card.CurrentZone!=view.Zone)
                {checkedNative=false;influences.Add(new(location,"native-instance-unavailable-or-changed"));continue;}
                var buffs=context.BuffManager.GetBuffsForCard(card.InstanceId).Count(buff=>buff.IsActive);
                if(buffs>0)influences.Add(new(location,"runtime-buffs-not-simulated",buffs));
                var active=0;
                foreach(var current in context.AbilityManager.GetEffectiveAbilitiesForCard(card.InstanceId,context))
                {
                    if(current is not Ability ability){active++;continue;}
                    var config=ability.Config;var trigger=config.Trigger;
                    if(!config.IsEnabled||!trigger.IsEnabled||!(trigger.IsValidZone(card.CurrentZone)
                        ||played.Contains(card.InstanceId)&&trigger.IsValidZone(CardZone.Field)))continue;
                    // Unplayed cards' strictly self-play triggers cannot react to another basic action.
                    if(!played.Contains(card.InstanceId)&&config.Type==AbilityType.Triggered
                        &&trigger.RelationType==TriggerRelationType.TargetSelf&&trigger.EventTypes.Count>0
                        &&trigger.EventTypes.All(type=>type==EventType.PlayCard)&&!config.Auras.Any(a=>a.IsEnabled))continue;
                    active++;
                }
                if(active>0)influences.Add(new(location,"runtime-abilities-not-simulated",active));
            }
            catch(Exception error) when(error is InvalidOperationException or NullReferenceException or NotSupportedException)
            {checkedNative=false;influences.Add(new(location,"native-input-check-unavailable"));}
        }
        if(context!=null&&context.CurrentGameState.StateRevision!=revision){checkedNative=false;influences.Add(new(null,"native-state-changed-during-read"));}
        var omitted=Math.Max(0,influences.Count-12);
        var facts=new Facts(true,null,steps.ToArray(),plan?.ProjectsLethal==true,plan?.PreventsVisibleLethal==true,
            plan?.Gain,plan?.Outcome,threat,influences.Take(12).ToArray(),omitted,checkedNative)
            {Actor=actor,Position=AiStrategyPlanner.PositionKey(own,enemy),Revision=snapshotRevision,
                CompiledCombatRules=combatRules?.RuleCount??0,OmittedCombatRules=combatRules?.Omissions.Length??0,LeaderRecovery=plan?.LeaderRecovery??0};
        return JsonSerializer.SerializeToUtf8Bytes(facts).Length<=6144?facts:Missing("local-tactical-byte-budget");
    }

    internal static Facts ForPosition(Facts? facts,GameStateDto snapshot,Guid actor,long? currentRevision=null)
    {
        var own=snapshot.Players.Single(player=>player.PlayerId==actor);var enemy=snapshot.Players.Single(player=>player.PlayerId!=actor);
        return facts?.Actor==actor&&facts.Position==AiStrategyPlanner.PositionKey(own,enemy)
            &&(!facts.Revision.HasValue||!currentRevision.HasValue||facts.Revision==currentRevision)?facts:
            new(false,"missing-or-stale-local-tactical-frame",[],false,false,null,null,AiCombatThreat.Evaluate(enemy.Field,own.Field,own.LeaderCard),[],0,false);
    }
}
