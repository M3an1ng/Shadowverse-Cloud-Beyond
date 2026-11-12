using System.Reflection;
using System.Text.Json;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class AiCombatRemovalTests
{
    private const BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
    private static readonly Type Projection=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiCombatProjection")!;
    private static readonly Type Search=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiTacticalSearch")!;
    private static void Check(bool ok,string reason){if(!ok)throw new InvalidOperationException(reason);}
    internal static void NativeEffectsAndProtections()
    {
        var prior=GameDataProvider.Instance;var priorAccessor=GameServiceProvider.ContextAccessor;
        try {
            foreach(var scenario in new[]{"destroy","vanish","defender-destroy","self-destroy","self-immune-destroy","immune-destroy","immune-vanish",
                "destroy-resist","vanish-resist","earth-sigil","super-own","super-enemy","health-add","health-negative","owner-heal","enemy-debuff",
                "target-buff-fallback","source-buff-fallback","unknown-duration","chained-removal"})Run(scenario);
        } finally {GameDataProvider.SetInstanceForTesting(prior);GameServiceProvider.ContextAccessor=priorAccessor;}
    }
    private static void Run(string scenario)
    {
        const int leader=2000000920,unit=2000000921,targetId=2000000922;
        var self=scenario is "self-destroy" or "self-immune-destroy" or "super-own";
        var defender=scenario=="defender-destroy";
        var stats=scenario is "health-add" or "health-negative" or "enemy-debuff" or "target-buff-fallback" or "source-buff-fallback";
        var sourceKeywords=scenario=="self-immune-destroy"?new List<string>{"EffectImmune"}:new List<string>();
        var targetKeywords=scenario switch {"immune-destroy" or "immune-vanish"=>new List<string>{"EffectImmune"},"destroy-resist"=>new(){"DestroyResist"},"vanish-resist"=>new(){"VanishResist"},"earth-sigil"=>new(){"EarthSigil"},_=>new List<string>()};
        var definitions=new Dictionary<int,CardDefinition> {
            [leader]=new(){Id=leader,Name="native guard leader",Type=CardType.Leader,Health=20},
            [unit]=new(){Id=unit,Name="native guard attacker",Type=CardType.Follower,Attack=3,Health=5,Keywords=sourceKeywords},
            [targetId]=new(){Id=targetId,Name="native guard defender",Type=CardType.Follower,Attack=3,Health=3,Keywords=targetKeywords}};
        var config=new AbilityConfig{Id=200000092101,CardId=defender?targetId:unit,Name="finite combat guard",
            Trigger=new(){EventTypes=[defender?EventType.Clash:EventType.Attack],RelationType=defender?TriggerRelationType.TargetSelf:TriggerRelationType.SourceSelf,ValidZones=[CardZone.Field]},
            Targets=new(){["t"]=new(){Query=new(){SourcePoolExpression=self||stats&&scenario is not ("enemy-debuff" or "target-buff-fallback")?"context.sourceCard":defender?"((context.triggeringEvent.Attacker) | (context.triggeringEvent.Defender)) ~ (context.sourceCard)":scenario=="owner-heal"?"context.owner.field.query(self.type == \"Follower\") | context.owner.leader":"context.triggeringEvent.TargetCard"}}},
            Effects=[new(){Type=stats?EffectType.ModifyStatus:scenario=="owner-heal"?EffectType.Heal:scenario.Contains("vanish")?EffectType.Vanish:EffectType.Destroy}]};
        if(stats) {
            config.Effects[0].Parameters=new(){[scenario is "enemy-debuff" or "target-buff-fallback"?"AttackModifier":"HealthModifier"]=new StaticValueConfig{Value=scenario=="health-negative"?-5:scenario is "enemy-debuff" or "target-buff-fallback"?-2:2}};
        }
        if(scenario=="owner-heal")config.Effects[0].Parameters=new(){["HealAmount"]=new StaticValueConfig{Value=3}};
        if(scenario=="unknown-duration")config.Effects[0].Parameters["DurationType"]=new StaticValueConfig{Value="unknown"};
        if(scenario=="chained-removal")config.Effects[0].ChainedAbilities.Add(new(){Name="unsupported follow-up"});
        var data=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch {
            "get_DataRevision"=>Guid.Empty,"GetCardDefinition"=>definitions.GetValueOrDefault((int)args![0]!),"GetAllCardDefinitions"=>definitions,
            "GetAbilityConfigs"=>(int)args![0]! == config.CardId?new List<AbilityConfig>{config}:new List<AbilityConfig>(),_=>null});
        GameDataProvider.SetInstanceForTesting(data);var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
        using var services=AiEndTurnEngineTests.Services(data,accessor);using var scope=services.CreateScope();var sp=scope.ServiceProvider;
        var own=new PlayerState(Guid.NewGuid()){TurnNumber=6};var enemy=new PlayerState(Guid.NewGuid()){TurnNumber=6};
        var game=new GameState("removal-guard-"+scenario,own,enemy,own.PlayerId){Phase=GamePhase.Main,Status=GameStatus.InProgress,
            AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<IBuffManager>()};
        var factory=sp.GetRequiredService<ICardFactory>();own.Leader=factory.CreateAndRegisterNewCard(game,leader,own.PlayerId,CardZone.Field);
        enemy.Leader=factory.CreateAndRegisterNewCard(game,leader,enemy.PlayerId,CardZone.Field);
        var attacker=factory.CreateAndRegisterNewCard(game,unit,own.PlayerId,CardZone.Field);attacker.ZoneStayTurn=1;
        var target=factory.CreateAndRegisterNewCard(game,targetId,enemy.PlayerId,CardZone.Field);target.ZoneStayTurn=1;
        if(scenario=="health-add")attacker.Damage=2;
        if(scenario=="owner-heal"){own.Leader.Damage=7;attacker.Damage=2;}
        if(scenario is "super-own" or "super-enemy")typeof(CardInstance).GetProperty("IsSuperEvolved")!.SetValue(scenario=="super-own"?attacker:target,true);
        using var context=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId);
        var transactions=sp.GetRequiredService<IGameActionTransactionService>();
        if(scenario is "target-buff-fallback" or "source-buff-fallback") {
            var prep=transactions.ProcessTransactionAsync(context,[new StatChangedEvent(scenario=="target-buff-fallback"?target:attacker,attackModifier:7,healthModifier:null,costModifier:null,modifierLayer:ModifierLayer.Set)]).GetAwaiter().GetResult();
            Check(prep.IsSuccess&&!prep.IsCancelled,scenario+" native buff setup failed");
        }
        var before=GameStateProjection.Create(game,context);var json=JsonSerializer.Serialize(before);var revision=game.StateRevision;
        var frame=Projection.GetMethod("Read",Hidden)!.Invoke(null,[context,before,own.PlayerId]);
        var sourceView=CardInstanceProjection.Create(attacker,context);var targetView=CardInstanceProjection.Create(target,context);
        var projected=(GameStateDto)Search.GetMethod("EstimateConfiguredAction",Hidden)!.Invoke(null,[before,own.PlayerId,"attack",sourceView,targetView,frame])!;
        Check(JsonSerializer.Serialize(GameStateProjection.Create(game,context))==json&&game.StateRevision==revision,scenario+" prediction mutated native state");
        if(scenario is "target-buff-fallback" or "source-buff-fallback" or "unknown-duration" or "chained-removal") {
            var basic=(GameStateDto)Search.GetMethod("EstimateKnownAction",Hidden)!.Invoke(null,[before,own.PlayerId,"attack",sourceView,targetView])!;
            Check(AiFeatures.Value(basic,own.PlayerId).SequenceEqual(AiFeatures.Value(projected,own.PlayerId)),scenario+" flattened an unsupported layer or follow-up");return;
        }
        var result=transactions.ProcessTransactionAsync(context,[new AttackRequestedEvent(own.PlayerId,new(){AttackerUid=attacker.InstanceId,TargetUid=target.InstanceId})]).GetAwaiter().GetResult();
        Check(result.IsSuccess&&!result.IsCancelled,scenario+" native attack failed: "+result.ErrorMessage);
        var actual=GameStateProjection.Create(game,context);
        var predicted=AiFeatures.Value(projected,own.PlayerId);var observed=AiFeatures.Value(actual,own.PlayerId);
        Check(predicted.Zip(observed).All(pair=>Math.Abs(pair.First-pair.Second)<1e-10),scenario+" projection disagreed with native combat, stats or graveyard");
        if(scenario is "destroy" or "vanish")Check(own.Field.Count==1&&attacker.CurrentHealth==5&&enemy.Field.Count==0,scenario+" still dealt retaliation after target left");
        if(scenario=="defender-destroy")Check(own.Field.Count==0&&enemy.Field.Count==1&&target.CurrentHealth==3,"defender removal still applied basic damage");
        if(scenario=="health-negative")Check(own.Field.Count==0&&enemy.Field.Count==1,"health reduction did not leave before the original combat");
    }
}
