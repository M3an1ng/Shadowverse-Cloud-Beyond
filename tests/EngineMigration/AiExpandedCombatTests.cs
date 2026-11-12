using System.Reflection;
using System.Text.Json;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class AiExpandedCombatTests
{
    private const BindingFlags Flags=BindingFlags.Static|BindingFlags.NonPublic;
    private static readonly Type Projection=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiCombatProjection")!;
    private static readonly Type Search=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiTacticalSearch")!;
    private static readonly Type Evaluation=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiCombatEvaluation")!;
    private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    internal static void Native(string? output=null)
    {
        var prior=GameDataProvider.Instance;var accessor=GameServiceProvider.ContextAccessor;var rows=new List<object>();
        try{
            foreach(var scenario in new[]{"raw-attack","raw-attack-shield","attack-then-raw","defender-types","type-negative",
                "owner-positive","owner-negative","all-followers","all-followers-self-dies","all-followers-self-dies-defender-trigger","all-followers-immune","max-attacks-shield"})rows.Add(Run(scenario));
            var previous=RunPolicy(false);var expanded=RunPolicy(true);
            Check(previous.Won&&expanded.Won&&!previous.AllySurvived&&expanded.AllySurvived,"expanded native policy did not preserve the ally while winning");
            if(output!=null)File.WriteAllText(output,JsonSerializer.Serialize(new{realProviderCalls=0,nativeTransactions=true,nativeTransactionCount=17,syntheticDefinitions=true,wholeMatchStrengthClaim=false,cases=rows,policyComparison=new{previous,expanded}},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("PASS expanded combat: 12 native edge scenarios and two completed policy routes; provider requests=0");
        }finally{GameDataProvider.SetInstanceForTesting(prior);GameServiceProvider.ContextAccessor=accessor;}
    }
    private static object Run(string scenario)
    {
        const int leader=2000001000,unit=2000001001,targetId=2000001002,allyId=2000001003;
        var enemyRule=scenario is "defender-types" or "owner-positive" or "owner-negative";
        var config=new AbilityConfig{Id=200000100101,CardId=enemyRule?targetId:unit,Name="finite native edge",
            Trigger=new(){EventTypes=[scenario=="defender-types"?EventType.Clash:EventType.Attack],RelationType=enemyRule?TriggerRelationType.Any:TriggerRelationType.SourceSelf,ValidZones=[CardZone.Field]},
            Targets=new(){["t"]=new(){Query=new(){SourcePoolExpression="context.triggeringEvent.TargetCard"}}},
            Effects=[new(){Type=EffectType.Damage,Parameters=new(){["DamageAmount"]=new FormulaValueConfig{Formula="context.sourceCard.currentAttack"}}}]};
        if(scenario=="attack-then-raw"){
            config.Targets["s"]=new(){Query=new(){SourcePoolExpression="context.sourceCard"}};
            config.Effects[0].TargetSelector=["t"];
            config.Effects.Insert(0,new(){Type=EffectType.ModifyStatus,TargetSelector=["s"],Parameters=new(){["AttackModifier"]=new StaticValueConfig{Value=2}}});
        }
        if(scenario is "defender-types" or "type-negative"){
            config.Conditions.Add(new(){Expression=$"context.triggeringEvent.Attacker.cardType == \"{(scenario=="type-negative"?"Leader":"Follower")}\""});
            if(scenario=="defender-types")config.Conditions.Add(new(){Expression="context.triggeringEvent.Defender.cardType == \"Follower\""});
            config.Targets["t"].Query=new(){SourcePoolExpression="context.opponent.leader"};
            config.Effects[0].Parameters["DamageAmount"]=new StaticValueConfig{Value=1};
        }
        if(scenario is "owner-positive" or "owner-negative"){
            config.Conditions.Add(new(){Expression=$"context.triggeringEvent.Attacker.ownerPlayerId == context.{(scenario=="owner-positive"?"opponent":"owner")}.playerId"});
            config.Targets["t"].Query=new(){SourcePoolExpression="context.opponent.leader"};
            config.Effects[0].Parameters["DamageAmount"]=new StaticValueConfig{Value=1};
        }
        if(scenario.StartsWith("all-followers",StringComparison.Ordinal)){
            config.Targets["t"].Query=new(){SourcePoolExpression="context.currentGameState.allField.query(self.cardType == \"Follower\")"};
            config.Effects[0].Parameters["DamageAmount"]=new StaticValueConfig{Value=2};
        }
        if(scenario=="max-attacks-shield"){
            config.Targets["t"].Query=new(){SourcePoolExpression="context.sourceCard"};
            config.Effects=[new(){Type=EffectType.SetAttackTimes,Parameters=new(){["maxAttacksPerTurn"]=new StaticValueConfig{Value=2}}},
                new(){Type=EffectType.SetKeyword,Parameters=new(){["keyword"]=new StaticValueConfig{Value="Shield"},["operation"]=new StaticValueConfig{Value="Add"}}}];
            config.Conditions.Add(new(){Expression="context.triggeringEvent.TargetCard.type == \"Follower\""});
        }
        var defs=new Dictionary<int,CardDefinition>{[leader]=new(){Id=leader,Name="edge leader",Type=CardType.Leader,Health=20},
            [unit]=new(){Id=unit,Name="edge attacker",Type=CardType.Follower,Attack=scenario=="max-attacks-shield"?4:3,Health=scenario.StartsWith("all-followers-self-dies",StringComparison.Ordinal)?1:5,Keywords=scenario=="all-followers-immune"?["EffectImmune"]:[]},
            [targetId]=new(){Id=targetId,Name="edge defender",Type=CardType.Follower,Attack=2,Health=3,Keywords=scenario=="max-attacks-shield"?["Ward"]:scenario=="raw-attack-shield"?["Shield"]:[]},
            [allyId]=new(){Id=allyId,Name="public immune ally",Type=CardType.Follower,Attack=1,Health=2,Keywords=["EffectImmune"]}};
        var defenderRule=new AbilityConfig{Id=200000100202,CardId=targetId,Name="defender clash response",
            Trigger=new(){EventTypes=[EventType.Clash],RelationType=TriggerRelationType.Self,ValidZones=[CardZone.Field]},
            Targets=new(){["t"]=new(){Query=new(){SourcePoolExpression="context.opponent.leader"}}},Effects=[new(){Type=EffectType.Damage,Parameters=new(){["DamageAmount"]=new StaticValueConfig{Value=4}}}]};
        var data=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch{"get_DataRevision"=>Guid.Empty,"GetCardDefinition"=>defs.GetValueOrDefault((int)args![0]!),"GetAllCardDefinitions"=>defs,"GetAbilityConfigs"=>(int)args![0]! == config.CardId?new List<AbilityConfig>{config}:(int)args![0]! == targetId&&scenario=="all-followers-self-dies-defender-trigger"?new List<AbilityConfig>{defenderRule}:new List<AbilityConfig>(),_=>null});
        GameDataProvider.SetInstanceForTesting(data);var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
        using var services=AiEndTurnEngineTests.Services(data,accessor);using var scope=services.CreateScope();var sp=scope.ServiceProvider;
        var own=new PlayerState(Guid.NewGuid()){TurnNumber=6};var enemy=new PlayerState(Guid.NewGuid()){TurnNumber=6};
        var game=new GameState("expanded-edge-"+scenario,own,enemy,own.PlayerId){Phase=GamePhase.Main,Status=GameStatus.InProgress,AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<ShadowverseBeyond.Server.Domain.Buffs.IBuffManager>()};
        var factory=sp.GetRequiredService<ICardFactory>();own.Leader=factory.CreateAndRegisterNewCard(game,leader,own.PlayerId,CardZone.Field);enemy.Leader=factory.CreateAndRegisterNewCard(game,leader,enemy.PlayerId,CardZone.Field);
        var source=factory.CreateAndRegisterNewCard(game,unit,own.PlayerId,CardZone.Field);source.ZoneStayTurn=1;
        var target=factory.CreateAndRegisterNewCard(game,targetId,enemy.PlayerId,CardZone.Field);target.ZoneStayTurn=1;
        if(scenario=="all-followers-immune")factory.CreateAndRegisterNewCard(game,allyId,own.PlayerId,CardZone.Field);
        if(scenario=="max-attacks-shield")enemy.Leader.Damage=16;
        using var context=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId);
        var before=GameStateProjection.Create(game,context);var original=JsonSerializer.Serialize(before);var revision=game.StateRevision;
        var frame=Projection.GetMethod("Read",Flags)!.Invoke(null,[context,before,own.PlayerId]);
        var old=Projection.GetMethod("ReadPrevious",Flags)!.Invoke(null,[context,before,own.PlayerId]);
        var projected=(GameStateDto)Search.GetMethod("EstimateConfiguredAction",Flags)!.Invoke(null,[before,own.PlayerId,"attack",CardInstanceProjection.Create(source,context),CardInstanceProjection.Create(target,context),frame])!;
        var plan=Search.GetMethod("FindWithCombat",Flags)!.Invoke(null,[before,own.PlayerId,CancellationToken.None,null,null,null,null,null,frame]);
        var oldPlan=Search.GetMethod("FindWithCombat",Flags)!.Invoke(null,[before,own.PlayerId,CancellationToken.None,null,null,null,null,null,old]);
        Check(original==JsonSerializer.Serialize(before)&&revision==game.StateRevision,"prediction mutated native/snapshot state: "+scenario);
        var result=sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(context,[new AttackRequestedEvent(own.PlayerId,new AttackActionDto{AttackerUid=source.InstanceId,TargetUid=target.InstanceId})]).GetAwaiter().GetResult();
        Check(result.IsSuccess&&!result.IsCancelled,"native transaction failed: "+scenario);
        var native=GameStateProjection.Create(game,context);
        var a=AiFeatures.Value(projected,own.PlayerId);var b=AiFeatures.Value(native,own.PlayerId);
        var difference=a.Zip(b).Select((pair,index)=>(pair,index)).Where(row=>Math.Abs(row.pair.First-row.pair.Second)>1e-10).Select(row=>row.index).ToArray();
        Check(difference.Length==0,"native feature mismatch "+scenario+": "+string.Join(',',difference));
        if(scenario=="max-attacks-shield"){
            bool Lethal(object? value)=>(bool?)value?.GetType().GetProperty("ProjectsLethal")?.GetValue(value)??false;
            Check(Lethal(plan)&&!Lethal(oldPlan),"second attack lethal was missed or previous coverage was not preserved");
            var finish=sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(context,[new AttackRequestedEvent(own.PlayerId,new AttackActionDto{AttackerUid=source.InstanceId,TargetUid=enemy.Leader.InstanceId})]).GetAwaiter().GetResult();
            Check(finish.IsSuccess&&!finish.IsCancelled&&game.WinnerId==own.PlayerId,"projected double attack did not finish natively");
        }
        return new{scenario,passed=true,nativeValueFeaturesMatch=true,previousCompiledRules=old?.GetType().GetProperty("RuleCount",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(old),committed=result.ProcessedEvents.Select(item=>item.EventType.ToString()).Distinct()};
    }
    private sealed record PolicyResult(bool Expanded,bool Won,bool AllySurvived,string[] Steps);
    private static PolicyResult RunPolicy(bool expanded)
    {
        // Both routes have a native lethal. Prior coverage sacrifices the ally to clear Ward;
        // full coverage can instead use the attacker's Shield and second attack to preserve it.
        const int leader=2000001100,unit=2000001101,ward=2000001102,ally=2000001103;
        var rule=new AbilityConfig{Id=200000110101,CardId=unit,Name="native attack shield and second attack",
            Trigger=new(){EventTypes=[EventType.Attack],RelationType=TriggerRelationType.SourceSelf,ValidZones=[CardZone.Field]},
            Conditions=[new(){Expression="context.triggeringEvent.TargetCard.type == \"Follower\""}],
            Targets=new(){["self"]=new(){Query=new(){SourcePoolExpression="context.sourceCard"}}},
            Effects=[new(){Type=EffectType.SetAttackTimes,Parameters=new(){["maxAttacksPerTurn"]=new StaticValueConfig{Value=2}}},
                new(){Type=EffectType.SetKeyword,Parameters=new(){["keyword"]=new StaticValueConfig{Value="Shield"},["operation"]=new StaticValueConfig{Value="Add"}}}]};
        var definitions=new Dictionary<int,CardDefinition>{
            [leader]=new(){Id=leader,Name="policy leader",Type=CardType.Leader,Health=20},
            [unit]=new(){Id=unit,Name="policy shield attacker",Type=CardType.Follower,Attack=4,Health=5},
            [ward]=new(){Id=ward,Name="policy blocking ward",Type=CardType.Follower,Attack=8,Health=1,Keywords=["Ward"]},
            [ally]=new(){Id=ally,Name="policy surviving ally",Type=CardType.Follower,Attack=3,Health=2}};
        var data=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch{
            "get_DataRevision"=>Guid.Empty,"GetCardDefinition"=>definitions.GetValueOrDefault((int)args![0]!),"GetAllCardDefinitions"=>definitions,
            "GetAbilityConfigs"=>(int)args![0]! == unit?new List<AbilityConfig>{rule}:new List<AbilityConfig>(),_=>null});
        GameDataProvider.SetInstanceForTesting(data);var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
        using var services=AiEndTurnEngineTests.Services(data,accessor);using var scope=services.CreateScope();var sp=scope.ServiceProvider;
        var own=new PlayerState(Guid.NewGuid()){TurnNumber=6};var enemy=new PlayerState(Guid.NewGuid()){TurnNumber=6};
        var game=new GameState("expanded-policy-"+expanded,own,enemy,own.PlayerId){Phase=GamePhase.Main,Status=GameStatus.InProgress,
            AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<ShadowverseBeyond.Server.Domain.Buffs.IBuffManager>()};
        var factory=sp.GetRequiredService<ICardFactory>();
        own.Leader=factory.CreateAndRegisterNewCard(game,leader,own.PlayerId,CardZone.Field);
        enemy.Leader=factory.CreateAndRegisterNewCard(game,leader,enemy.PlayerId,CardZone.Field);enemy.Leader.Damage=16;
        var source=factory.CreateAndRegisterNewCard(game,unit,own.PlayerId,CardZone.Field);source.ZoneStayTurn=1;
        var friend=factory.CreateAndRegisterNewCard(game,ally,own.PlayerId,CardZone.Field);friend.ZoneStayTurn=1;
        factory.CreateAndRegisterNewCard(game,ward,enemy.PlayerId,CardZone.Field);
        using var context=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId);
        var steps=new List<string>();
        for(var turnStep=0;turnStep<3&&game.Status==GameStatus.InProgress;turnStep++){
            var snapshot=GameStateProjection.Create(game,context);
            var frame=Projection.GetMethod(expanded?"Read":"ReadPrevious",Flags)!.Invoke(null,[context,snapshot,own.PlayerId]);
            var plan=Search.GetMethod("FindWithCombat",Flags)!.Invoke(null,[snapshot,own.PlayerId,CancellationToken.None,null,null,null,null,null,frame]);
            Check(plan!=null&&(bool)plan.GetType().GetProperty("ProjectsLethal")!.GetValue(plan)!,"policy failed to see the native lethal");
            var move=plan!.GetType().GetProperty("First")!.GetValue(plan)!;
            Check((string)move.GetType().GetProperty("Type")!.GetValue(move)! == "PerformAttack","policy produced a non-attack");
            var attacker=(CardInstanceDto)move.GetType().GetProperty("Source")!.GetValue(move)!;
            var target=(CardInstanceDto)move.GetType().GetProperty("Target")!.GetValue(move)!;
            steps.Add((attacker.CardId==unit?"shield-attacker":"ally")+" -> "+(target.Type==CardType.Leader?"leader":"ward"));
            var result=sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(context,[new AttackRequestedEvent(own.PlayerId,
                new AttackActionDto{AttackerUid=attacker.InstanceId,TargetUid=target.InstanceId})]).GetAwaiter().GetResult();
            Check(result.IsSuccess&&!result.IsCancelled,"policy native transaction rejected");
        }
        Check(steps.Count==2,"native policy expected exactly two committed attacks");
        return new(expanded,game.WinnerId==own.PlayerId,own.Field.Any(card=>card.InstanceId==friend.InstanceId),steps.ToArray());
    }
    internal static void KeysAndComparisonIsolation()
    {
        var actor=Guid.NewGuid();var other=Guid.NewGuid();var card=new CardInstanceDto{InstanceId=Guid.NewGuid(),CardId=1,CurrentAttack=2,CurrentHealth=3,MaxHealth=3,MaxAttacksPerTurn=1};
        var own=new PlayerStateDto{PlayerId=actor,Field=[card]};var enemy=new PlayerStateDto{PlayerId=other};var state=new GameStateDto{GameId="expanded-key",Players=[own,enemy]};
        string Key(GameStateDto value)=>(string)Search.GetMethod("Key",Flags)!.Invoke(null,[value])!;
        var baseline=Key(state);
        foreach(var changed in new[]{card with{CurrentAttack=3},card with{MaxHealth=4},card with{MaxAttacksPerTurn=2},card with{Keywords=[ShadowverseBeyond.Server.Domain.Constants.CardKeyword.Bane]}})
            Check(Key(state with{Players=[own with{Field=[changed]},enemy]})!=baseline,"search merged states with different action/value consequences");
        Check(Key(state with{Players=[own with{GraveyardCount=1},enemy]})!=baseline&&Key(state with{Players=[own with{BloodPool=1},enemy]})!=baseline,"graveyard or damage resources merged");
        var game=new GameState("expanded-isolation",new(actor),new(other),actor){IsLocalAiTraining=true};
        Evaluation.GetMethod("StartPrevious",Flags)!.Invoke(null,[game,actor]);
        bool Enabled(string method,Guid id)=>(bool)Evaluation.GetMethod(method,Flags)!.Invoke(null,[game,id])!;
        Check(Enabled("ExpandedEnabled",actor)&&!Enabled("ExpandedEnabled",other)&&Enabled("ComparePrevious",actor)&&!Enabled("ComparePrevious",other),"old/new coverage crossed benchmark seats");
        Check((bool)Evaluation.GetMethod("Enabled",Flags)!.Invoke(null,[game,actor,false])!&&(bool)Evaluation.GetMethod("Enabled",Flags)!.Invoke(null,[game,other,false])!,"previous baseline disabled all projection");
        var report=(AiCombatProjectionMatchFacts)Evaluation.GetMethod("Snapshot",Flags)!.Invoke(null,[game])!;
        Check(report.Baseline=="previous-finite-coverage","benchmark result did not identify its actual baseline");
        var normal=new GameState("normal",new(actor),new(other),actor);
        Check((bool)Evaluation.GetMethod("ExpandedEnabled",Flags)!.Invoke(null,[normal,other])!,"comparison settings leaked to daily games");
    }
}
