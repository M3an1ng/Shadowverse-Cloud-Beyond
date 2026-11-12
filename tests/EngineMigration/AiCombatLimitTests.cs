using System.Reflection;
using System.Text.Json;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
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

internal static class AiCombatLimitTests
{
    private const BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
    private static readonly Type Projection=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiCombatProjection")!;
    private static readonly Type Search=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiTacticalSearch")!;
    private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    internal static void Native(string? output=null)
    {
        var prior=GameDataProvider.Instance;var accessor=GameServiceProvider.ContextAccessor;var rows=new List<object>();
        try{
            foreach(var scenario in new[]{"per-turn-lethal","per-game","combined","unlimited","own-turn","wrong-turn",
                "enemy-defender","failed-condition","immune-consumes","instances","duplicate-definitions","native-counter-refresh","tuck-other-followers","tuck-immune","tuck-ambiguous"})rows.Add(Run(scenario));
            if(output!=null)File.WriteAllText(output,JsonSerializer.Serialize(new{realProviderRequests=0,syntheticDefinitions=true,
                rootNativeStateUnchangedByForecast=true,nativeScenarios=14,guardScenarios=1,nativeTransactions=28,wholeMatchStrengthClaim=false,cases=rows},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("PASS 14 limited-trigger native two-attack scenarios plus grouping guard; provider requests=0");
        }finally{GameDataProvider.SetInstanceForTesting(prior);GameServiceProvider.ContextAccessor=accessor;}
    }
    private static object Run(string scenario)
    {
        const int leader=2000001200,unit=2000001201,targetId=2000001202,otherId=2000001203;
        var defender=scenario=="enemy-defender";
        var rule=new AbilityConfig{Id=200000120101,CardId=defender?targetId:unit,Name="finite quota",
            Trigger=new(){EventTypes=[defender?EventType.Clash:EventType.Attack],RelationType=defender?TriggerRelationType.Self:TriggerRelationType.SourceSelf,
                ValidZones=[CardZone.Field],CountLimit=new(){MaxPerTurn=scenario=="per-game"?null:scenario=="unlimited"?0:1,
                    MaxPerGame=scenario=="per-game"?1:scenario=="combined"?2:scenario=="unlimited"?-5:null}},
            Targets=new(){["t"]=new(){Query=new(){SourcePoolExpression="context.opponent.leader"}}},
            Effects=[new(){Type=EffectType.Damage,Parameters=new(){["DamageAmount"]=new StaticValueConfig{Value=3}}}]};
        if(scenario is "own-turn" or "wrong-turn" or "enemy-defender")
            rule.Conditions.Add(new(){Expression=$"context.currentGameState.currentPlayer.playerId == context.{(scenario=="own-turn"?"owner":"opponent")}.playerId"});
        if(scenario=="failed-condition")rule.Conditions.Add(new(){Expression="context.triggeringEvent.TargetCard.type == \"Follower\""});
        if(scenario=="immune-consumes"){
            rule.Targets["t"].Query=new(){SourcePoolExpression="context.triggeringEvent.TargetCard"};
            rule.Effects=[new(){Type=EffectType.Vanish}];
        }
        if(scenario.StartsWith("tuck-",StringComparison.Ordinal)){
            rule.Trigger.EventTypes=[EventType.Clash];rule.Trigger.RelationType=TriggerRelationType.SourceSelf;
            rule.Targets["t"].Query=new(){SourcePoolExpression="((context.owner.field | context.opponent.field).query((self.type == \"Follower\"))) ~ (context.sourceCard)"};
            rule.Effects=[new(){Type=EffectType.Tuck}];
        }
        if(scenario=="tuck-ambiguous")rule.Targets["t"].Query=new(){SourcePoolExpression="context.owner.field | ((context.opponent.field).query(self.type == \"Follower\")) ~ context.sourceCard"};
        var copy=JsonSerializer.Deserialize<AbilityConfig>(JsonSerializer.Serialize(rule))!;copy.Name="independent same definition";
        var definitions=new Dictionary<int,CardDefinition>{[leader]=new(){Id=leader,Name="quota leader",Type=CardType.Leader,Health=30},
            [unit]=new(){Id=unit,Name="quota attacker",Type=CardType.Follower,Attack=1,Health=12},
            [targetId]=new(){Id=targetId,Name="quota defender",Type=CardType.Follower,Attack=0,Health=20,Keywords=scenario is "immune-consumes" or "tuck-immune"?["EffectImmune"]:[]},
            [otherId]=new(){Id=otherId,Name="second defender",Type=CardType.Follower,Attack=0,Health=20},
            [otherId+1]=new(){Id=otherId+1,Name="own ally moved",Type=CardType.Follower,Attack=1,Health=3}};
        var data=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch{
            "get_DataRevision"=>Guid.Empty,"GetCardDefinition"=>definitions.GetValueOrDefault((int)args![0]!),"GetAllCardDefinitions"=>definitions,
            "GetAbilityConfigs"=>(int)args![0]! == rule.CardId?scenario=="duplicate-definitions"?new List<AbilityConfig>{rule,copy}:new List<AbilityConfig>{rule}:new List<AbilityConfig>(),_=>null});
        GameDataProvider.SetInstanceForTesting(data);var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
        using var services=AiEndTurnEngineTests.Services(data,accessor);using var scope=services.CreateScope();var sp=scope.ServiceProvider;
        var own=new PlayerState(Guid.NewGuid()){TurnNumber=6};var enemy=new PlayerState(Guid.NewGuid()){TurnNumber=6};
        var game=new GameState("quota-"+scenario,own,enemy,own.PlayerId){Phase=GamePhase.Main,Status=GameStatus.InProgress,
            AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<ShadowverseBeyond.Server.Domain.Buffs.IBuffManager>()};
        var factory=sp.GetRequiredService<ICardFactory>();own.Leader=factory.CreateAndRegisterNewCard(game,leader,own.PlayerId,CardZone.Field);
        enemy.Leader=factory.CreateAndRegisterNewCard(game,leader,enemy.PlayerId,CardZone.Field);
        var source=factory.CreateAndRegisterNewCard(game,unit,own.PlayerId,CardZone.Field);source.ZoneStayTurn=1;source.MaxAttacksPerTurn=2;
        var second=scenario=="instances"?factory.CreateAndRegisterNewCard(game,unit,own.PlayerId,CardZone.Field):source;second.ZoneStayTurn=1;
        var target=factory.CreateAndRegisterNewCard(game,targetId,enemy.PlayerId,CardZone.Field);
        var other=(scenario=="immune-consumes"||scenario.StartsWith("tuck-",StringComparison.Ordinal))?factory.CreateAndRegisterNewCard(game,otherId,enemy.PlayerId,CardZone.Field):target;
        if(scenario.StartsWith("tuck-",StringComparison.Ordinal))factory.CreateAndRegisterNewCard(game,otherId+1,own.PlayerId,CardZone.Field);
        if(scenario=="per-turn-lethal")enemy.Leader.Damage=25;
        using var context=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId);
        var before=GameStateProjection.Create(game,context);var initial=JsonSerializer.Serialize(before);var revision=game.StateRevision;
        object Read(GameStateDto state)=>Projection.GetMethod("Read",Hidden)!.Invoke(null,[context,state,own.PlayerId])!;
        var frame=Read(before);
        if(scenario=="tuck-ambiguous"){
            var entries=(Array)frame.GetType().GetField("Rules",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(frame)!;
            Check(entries.Length==0,"unverified target grouping was treated as the known all-other-followers query");
            Check(initial==JsonSerializer.Serialize(GameStateProjection.Create(game,context)),"query guard changed native state");
            return new{scenario,passed=true,guardOnly=true,nativeAttacks=0};
        }
        var previous=Projection.GetMethod("ReadPrevious",Hidden)!.Invoke(null,[context,before,own.PlayerId]);
        var actors=new[]{source,second};var destinations=new[]{defender||scenario=="immune-consumes"||scenario.StartsWith("tuck-",StringComparison.Ordinal)?target:enemy.Leader,
            defender?target:scenario=="failed-condition"?target:scenario=="immune-consumes"?other:enemy.Leader};
        GameStateDto Estimate(GameStateDto state,int index,object rules)=> (GameStateDto)Search.GetMethod("EstimateConfiguredAction",Hidden)!.Invoke(null,
            [state,own.PlayerId,"attack",state.Players[0].Field.Single(card=>card.InstanceId==actors[index].InstanceId),
                destinations[index].IsLeader?state.Players[1].LeaderCard:state.Players[1].Field.Single(card=>card.InstanceId==destinations[index].InstanceId),rules])!;
        var first=Estimate(before,0,frame);var repeat=Estimate(before,0,frame);
        var predicted=Estimate(first,1,frame);
        Check(JsonSerializer.Serialize(first)==JsonSerializer.Serialize(repeat),"sibling branch consumed another branch's quota: "+scenario);
        var plan=Search.GetMethod("FindWithCombat",Hidden)!.Invoke(null,[before,own.PlayerId,CancellationToken.None,null,null,null,null,null,frame]);
        if(scenario=="per-turn-lethal"){
            var oldPlan=Search.GetMethod("FindWithCombat",Hidden)!.Invoke(null,[before,own.PlayerId,CancellationToken.None,null,null,null,null,null,previous]);
            Check((bool)plan!.GetType().GetProperty("ProjectsLethal")!.GetValue(plan)!
                &&((bool?)oldPlan?.GetType().GetProperty("ProjectsLethal")?.GetValue(oldPlan)??false)==false,"limited native damage did not reveal the missed finish");
        }
        Check(initial==JsonSerializer.Serialize(GameStateProjection.Create(game,context))&&revision==game.StateRevision,"forecast consumed live counters or changed native state");
        if(scenario=="per-turn-lethal"){
            var remapped=before with{Players=before.Players};
            Projection.GetMethod("CarryUsage",Hidden)!.Invoke(null,[first,remapped]);
            var key=Search.GetMethod("Key",Hidden)!;
            Check((string)key.Invoke(null,[before])! != (string)key.Invoke(null,[remapped])!,"visited states merged different remaining trigger quotas");
        }
        var transaction=sp.GetRequiredService<IGameActionTransactionService>();
        for(var index=0;index<2;index++){
            var liveBefore=GameStateProjection.Create(game,context);var expectedFresh=Estimate(liveBefore,index,Read(liveBefore));
            var result=transaction.ProcessTransactionAsync(context,[new AttackRequestedEvent(own.PlayerId,new AttackActionDto{AttackerUid=actors[index].InstanceId,TargetUid=destinations[index].InstanceId})]).GetAwaiter().GetResult();
            Check(result.IsSuccess&&!result.IsCancelled,"native limited attack failed: "+scenario);
            var actual=GameStateProjection.Create(game,context);
            Check(AiFeatures.Value(actual,own.PlayerId).Zip(AiFeatures.Value(expectedFresh,own.PlayerId)).All(pair=>Math.Abs(pair.First-pair.Second)<1e-10),"fresh native quota mismatch: "+scenario);
            if(index==0)Check(Projection.GetMethod("ForPosition",Hidden)!.Invoke(null,[frame,before,own.PlayerId])==null,"old rule frame survived a committed native revision");
        }
        var final=GameStateProjection.Create(game,context);
        Check(AiFeatures.Value(final,own.PlayerId).Zip(AiFeatures.Value(predicted,own.PlayerId)).All(pair=>Math.Abs(pair.First-pair.Second)<1e-10),"two-step quota projection differs from native: "+scenario);
        var ownerCard=defender?target:source;var registered=context.AbilityManager.GetEffectiveAbilitiesForCard(ownerCard.InstanceId,context).OfType<Ability>().ToArray();
        var counts=registered.Select(ability=>ability.Trigger.CurrentTurnTriggerCount).ToArray();
        if(scenario=="wrong-turn")Check(counts.All(count=>count==0),"false condition used the native quota");
        if(scenario=="combined"){
            var ability=registered.Single();ability.ResetTurnState(context);
            var available=Read(GameStateProjection.Create(game,context));var rules=(Array)available.GetType().GetField("Rules",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(available)!;
            Check(rules.Length==1&&(int?)rules.GetValue(0)!.GetType().GetProperty("Remaining")!.GetValue(rules.GetValue(0))==1,"per-game remainder did not survive native turn-counter reset");
        }
        return new{scenario,passed=true,nativeAttacks=2,nativeValueFeaturesMatch=true,hypotheticalTwoStepMatches=true,nativeCounts=counts,ownHealth=own.Leader.CurrentHealth,enemyHealth=enemy.Leader.CurrentHealth,won=game.WinnerId==own.PlayerId};
    }
}
