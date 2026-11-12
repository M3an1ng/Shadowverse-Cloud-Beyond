using System.Reflection;
using System.Text.Json;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class AiZeroDamageTests
{
    private const BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
    private static readonly Type Projection=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiCombatProjection")!;
    private static readonly Type Search=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiTacticalSearch")!;
    private static void Check(bool ok,string reason){if(!ok)throw new InvalidOperationException(reason);}
    internal static AbilityConfig Zero(int id,long number=200000170001)=>new(){Id=number,CardId=id,Name="native zero incoming damage",
        Trigger=new(){EventTypes=[EventType.Damage],Timing=TriggerTiming.Before,RelationType=TriggerRelationType.TargetSelf,ValidZones=[CardZone.Field]},
        Effects=[new(){Type=EffectType.RewriteEventParams,Parameters=new(){["ParamName"]=new StaticValueConfig{Value="Damage"},["Value"]=new StaticValueConfig{Value="0"}}}]};
    internal static void Native(string? output=null)
    {
        var prior=GameDataProvider.Instance;var accessor=GameServiceProvider.ContextAccessor;var rows=new List<object>();
        try{
            foreach(var scenario in new[]{"defender-zero","attacker-zero","both-zero","negative-zero","shield-defender","shield-attacker","bane-zero","immune-bane-zero","drain-zero",
                "ability-damage","immune-ability-damage","silenced","suppressed","leader-only-guard","unrelated-target-guard"})rows.Add(Run(scenario,true));
            foreach(var scenario in new[]{"conditional","count-limited","chained","unknown-overwrite","global-unknown","formula-value","leader-card","guard-budget"})rows.Add(Run(scenario,false));
            if(output!=null)File.WriteAllText(output,JsonSerializer.Serialize(new{realProviderRequests=0,syntheticDefinitions=true,nativeTransactions=23,
                nativeAgreementCases=15,conservativeFallbackCases=8,fullStateOrWholeMatchClaim=false,cases=rows},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("PASS native zero-damage scenes=23; supported agreements=15, conservative guards=8; provider requests=0");
        }finally{GameDataProvider.SetInstanceForTesting(prior);GameServiceProvider.ContextAccessor=accessor;}
    }
    private static object Run(string scenario,bool supported)
    {
        const int leader=2000001700,unit=2000001701,targetId=2000001702,otherId=2000001703;
        var ownZero=scenario is "attacker-zero" or "both-zero" or "shield-attacker";
        var sourceKeys=scenario switch {"bane-zero" or "immune-bane-zero"=>new List<string>{"Bane"},"drain-zero"=>new(){"Drain"},"shield-attacker"=>new(){"Shield"},_=>new List<string>()};
        var targetKeys=scenario switch {"immune-bane-zero" or "immune-ability-damage"=>new List<string>{"EffectImmune"},"shield-defender"=>new(){"Shield"},_=>new List<string>()};
        var zero=Zero(ownZero?unit:targetId);var rules=new Dictionary<int,List<AbilityConfig>>{[leader]=[],[unit]=[],[targetId]=[],[otherId]=[]};
        rules[ownZero?unit:targetId].Add(zero);if(scenario=="both-zero")rules[targetId].Add(Zero(targetId,200000170002));
        if(scenario=="negative-zero")zero.Effects[0].Parameters["Value"]=new StaticValueConfig{Value=-99};
        if(scenario=="conditional")zero.Conditions.Add(new(){Expression="true"});
        if(scenario=="count-limited")zero.Trigger.CountLimit=new(){MaxPerTurn=1};
        if(scenario=="chained")zero.Effects[0].ChainedAbilities.Add(new(){Effects=[new(){Type=EffectType.Draw,Parameters=new(){["DrawAmount"]=new StaticValueConfig{Value=0}}}]});
        if(scenario=="formula-value")zero.Effects[0].Parameters["Value"]=new FormulaValueConfig{Formula="0"};
        if(scenario is "unknown-overwrite" or "global-unknown" or "leader-only-guard" or "unrelated-target-guard"){
            var unknown=Zero(otherId,200000170003);unknown.Effects[0].Parameters["Value"]=new StaticValueConfig{Value=1};
            if(scenario!="unrelated-target-guard")unknown.Trigger.RelationType=TriggerRelationType.Any;
            if(scenario=="leader-only-guard")unknown.Conditions.Add(new(){Expression="context.triggeringEvent.DamageTarget.cardType == \"Leader\""});
            rules[scenario=="unknown-overwrite"?targetId:otherId].Add(unknown);
        }
        if(scenario=="guard-budget")for(var i=0;i<100;i++)rules[otherId].Add(Zero(otherId,200000171000+i));
        if(scenario=="leader-card"){rules[targetId].Clear();rules[leader].Add(Zero(leader));}
        if(scenario is "ability-damage" or "immune-ability-damage")rules[unit].Add(new(){Id=200000170004,CardId=unit,
            Trigger=new(){EventTypes=[EventType.Attack],RelationType=TriggerRelationType.SourceSelf,ValidZones=[CardZone.Field]},
            Targets=new(){["t"]=new(){Query=new(){SourcePoolExpression="context.triggeringEvent.TargetCard"}}},
            Effects=[new(){Type=EffectType.Damage,Parameters=new(){["DamageAmount"]=new StaticValueConfig{Value=2}}}]});
        var definitions=new Dictionary<int,CardDefinition>{[leader]=new(){Id=leader,Name="zero-damage scene leader",Type=CardType.Leader,Health=20},
            [unit]=new(){Id=unit,Name="zero-damage attacker",Type=CardType.Follower,Attack=3,Health=5,Keywords=sourceKeys},
            [targetId]=new(){Id=targetId,Name="zero-damage defender",Type=CardType.Follower,Attack=2,Health=4,Keywords=targetKeys},
            [otherId]=new(){Id=otherId,Name="other visible listener",Type=CardType.Amulet}};
        var revision=Guid.NewGuid();var data=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch{
            "get_DataRevision"=>revision,"GetAllCardDefinitions"=>definitions,"GetCardDefinition"=>definitions.GetValueOrDefault((int)args![0]!),
            "GetAbilityConfigs"=>rules.GetValueOrDefault((int)args![0]!)??[],_=>null});
        GameDataProvider.SetInstanceForTesting(data);var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
        using var services=AiEndTurnEngineTests.Services(data,accessor);using var scope=services.CreateScope();var sp=scope.ServiceProvider;
        var own=new PlayerState(Guid.NewGuid()){TurnNumber=6};var enemy=new PlayerState(Guid.NewGuid()){TurnNumber=6};
        var game=new GameState("zero-"+scenario,own,enemy,own.PlayerId){Status=GameStatus.InProgress,Phase=GamePhase.Main,
            AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<IBuffManager>()};
        var factory=sp.GetRequiredService<ICardFactory>();own.Leader=factory.CreateAndRegisterNewCard(game,leader,own.PlayerId,CardZone.Field);own.Leader.Damage=5;
        enemy.Leader=factory.CreateAndRegisterNewCard(game,leader,enemy.PlayerId,CardZone.Field);
        var source=factory.CreateAndRegisterNewCard(game,unit,own.PlayerId,CardZone.Field);source.ZoneStayTurn=1;
        var target=factory.CreateAndRegisterNewCard(game,targetId,enemy.PlayerId,CardZone.Field);
        factory.CreateAndRegisterNewCard(game,otherId,enemy.PlayerId,CardZone.Field);
        if(scenario=="silenced")typeof(CardInstance).GetProperty("IsSilenced")!.SetValue(target,true);
        if(scenario=="suppressed")game.BuffManager.RegisterBuffInstance(new Buff(Guid.Empty,target.InstanceId,6,[new AbilitySuppressionBuffEffect(RemoveAbilityMode.All)]),game);
        using var context=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId);
        var before=GameStateProjection.Create(game,context);var sourceView=CardInstanceProjection.Create(source,context);var targetView=CardInstanceProjection.Create(target,context);
        var beforeJson=JsonSerializer.Serialize(before);var nativeRevision=game.StateRevision;
        var frame=Projection.GetMethod("Read",Hidden)!.Invoke(null,[context,before,own.PlayerId]);
        var prediction=(GameStateDto)Search.GetMethod("EstimateConfiguredAction",Hidden)!.Invoke(null,[before,own.PlayerId,"attack",sourceView,targetView,frame])!;
        var sibling=(GameStateDto)Search.GetMethod("EstimateConfiguredAction",Hidden)!.Invoke(null,[before,own.PlayerId,"attack",sourceView,targetView,frame])!;
        var basic=(GameStateDto)Search.GetMethod("EstimateKnownAction",Hidden)!.Invoke(null,[before,own.PlayerId,"attack",sourceView,targetView])!;
        Check(JsonSerializer.Serialize(before)==beforeJson&&JsonSerializer.Serialize(GameStateProjection.Create(game,context))==beforeJson&&game.StateRevision==nativeRevision,
            scenario+" mutated a native state or the source snapshot");
        Check(AiFeatures.Value(prediction,own.PlayerId).SequenceEqual(AiFeatures.Value(sibling,own.PlayerId)),scenario+" leaked damage protection between hypothetical siblings");
        if(!supported)Check(AiFeatures.Value(prediction,own.PlayerId).SequenceEqual(AiFeatures.Value(basic,own.PlayerId)),scenario+" guessed unsupported damage rewrites");
        if(scenario=="defender-zero"){
            var projectedTarget=prediction.Players[1].Field.Single(card=>card.InstanceId==target.InstanceId);
            int Incoming()=> (int)Projection.GetMethod("IncomingDamage",Hidden)!.Invoke(null,[prediction,projectedTarget,3,source.InstanceId])!;
            Check(Incoming()==0,"prepared hypothetical state lost its verified zero-damage protection");
            var priorRevision=revision;revision=Guid.NewGuid();Check(Incoming()==3,"stale card catalog reused old zero-damage facts");revision=priorRevision;
        }
        var result=sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(context,
            [new AttackRequestedEvent(own.PlayerId,new(){AttackerUid=source.InstanceId,TargetUid=target.InstanceId})]).GetAwaiter().GetResult();
        Check(result.IsSuccess&&!result.IsCancelled,scenario+" native attack failed: "+result.ErrorMessage);
        var actual=GameStateProjection.Create(game,context);var matches=AiFeatures.Value(prediction,own.PlayerId).SequenceEqual(AiFeatures.Value(actual,own.PlayerId));
        if(supported)Check(matches,scenario+" did not match native value features");
        Check(Projection.GetMethod("ForPosition",Hidden)!.Invoke(null,[frame,before,own.PlayerId])==null,scenario+" reused protections after native commit");
        if(scenario=="defender-zero"){
            var projectedTarget=prediction.Players[1].Field.Single(card=>card.InstanceId==target.InstanceId);
            Check((int)Projection.GetMethod("IncomingDamage",Hidden)!.Invoke(null,[prediction,projectedTarget,3,source.InstanceId])! == 3,
                "old hypothetical damage protection survived a native state revision");
        }
        if(scenario=="bane-zero")Check(enemy.Field.All(card=>card.InstanceId!=target.InstanceId),"native zero damage must still allow Bane");
        if(scenario=="drain-zero")Check(own.Leader.CurrentHealth==15,"zero combat damage gave false Drain recovery");
        if(scenario is "shield-defender" or "shield-attacker")Check(!(scenario=="shield-defender"?target:source).Keywords.Contains(CardKeyword.Shield),"native zero damage did not consume Shield");
        return new{scenario,supported,nativeCompleted=true,featuresMatch=matches,conservativeFallback=!supported,passed=true};
    }
}
