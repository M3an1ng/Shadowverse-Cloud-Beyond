using System.Reflection;
using System.Text.Json;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class AiCombatProjectionTests
{
    private static readonly BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
    private static readonly Type Projection=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiCombatProjection")!;
    private static readonly Type Search=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiTacticalSearch")!;
    private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    internal static void Guards()
    {
        var prior=GameDataProvider.Instance;var priorAccessor=GameServiceProvider.ContextAccessor;
        try {
            foreach(var scenario in new[]{"normal","condition","precondition","chain","limit","random-pool","silenced","own-hand","stale-catalog","attack-plus","hand-global"}) {
                const int leader=2000000800,unit=2000000801;
                var config=new AbilityConfig{Id=200000080101,CardId=unit,Name="guard attack rule",
                    Trigger=new(){EventTypes=[EventType.Attack],RelationType=TriggerRelationType.SourceSelf,ValidZones=[CardZone.Field]},
                    Targets=new(){["target1"]=new(){Query=new(){SourcePoolExpression="context.opponent.leader"}}},
                    Effects=[new(){Type=EffectType.Damage,Parameters=new(){["DamageAmount"]=new StaticValueConfig{Value=2}}}]};
                if(scenario=="hand-global")config.Trigger.RelationType=TriggerRelationType.Any;
                if(scenario=="condition")config.Conditions.Add(new(){IsEnabled=true,Expression="true && true"});
                if(scenario=="precondition")config.PreConditions.Add(new(){IsEnabled=true,Expression="context.owner.hand.count > 0"});
                if(scenario=="chain")config.Effects[0].ChainedAbilities.Add(new(){Name="not numerically projected"});
                if(scenario=="limit")config.Trigger.LimitSameNameOncePerTransaction=true;
                if(scenario=="random-pool")config.Targets["target1"].Query=new(){SourcePoolExpression="context.opponent.field.takeRandom(1)"};
                if(scenario=="attack-plus") {
                    config.Targets["target1"].Query=new(){SourcePoolExpression="context.SourceCard"};
                    config.Effects=[new(){Type=EffectType.ModifyStatus,Parameters=new(){["AttackModifier"]=new StaticValueConfig{Value=1},["ModifierLayer"]=new StaticValueConfig{Value="additive"}}}];
                }
                var defs=new Dictionary<int,CardDefinition>{[leader]=new(){Id=leader,Name="guard leader",Type=CardType.Leader,Health=20},
                    [unit]=new(){Id=unit,Name="guard Storm body",Type=CardType.Follower,Attack=scenario=="attack-plus"?0:2,Health=3,Cost=1,Keywords=["Storm"]}};
                var catalog=Guid.NewGuid();
                var data=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch {
                    "get_DataRevision"=>catalog,"GetCardDefinition"=>defs.GetValueOrDefault((int)args![0]!),"GetAllCardDefinitions"=>defs,
                    "GetAbilityConfigs"=>(int)args![0]! == unit?new List<AbilityConfig>{config}:new List<AbilityConfig>(),_=>null});
                GameDataProvider.SetInstanceForTesting(data);var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
                using var services=AiEndTurnEngineTests.Services(data,accessor);using var scope=services.CreateScope();var sp=scope.ServiceProvider;
                var own=new PlayerState(Guid.NewGuid()){TurnNumber=6,MaxPlayPoints=5,CurrentPlayPoints=5};var enemy=new PlayerState(Guid.NewGuid()){TurnNumber=6};
                var game=new GameState("projection-guard-"+scenario,own,enemy,own.PlayerId){Phase=GamePhase.Main,Status=GameStatus.InProgress,
                    AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<ShadowverseBeyond.Server.Domain.Buffs.IBuffManager>()};
                var factory=sp.GetRequiredService<ICardFactory>();own.Leader=factory.CreateAndRegisterNewCard(game,leader,own.PlayerId,CardZone.Field);
                enemy.Leader=factory.CreateAndRegisterNewCard(game,leader,enemy.PlayerId,CardZone.Field);
                var source=factory.CreateAndRegisterNewCard(game,unit,own.PlayerId,scenario is "own-hand" or "hand-global"?CardZone.Hand:CardZone.Field);source.ZoneStayTurn=1;
                if(scenario=="own-hand")enemy.Leader.Damage=16;
                if(scenario=="silenced")source.ApplySilence();
                using var context=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId);
                var state=GameStateProjection.Create(game,context);var original=JsonSerializer.Serialize(state);var revision=game.StateRevision;
                var frame=Projection.GetMethod("Read",Hidden)!.Invoke(null,[context,state,own.PlayerId])!;
                var count=(int)frame.GetType().GetProperty("RuleCount",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(frame)!;
                var expected=scenario is "normal" or "own-hand" or "stale-catalog" or "attack-plus"?1:0;Check(count==expected,scenario+" accepted an unsupported, silenced or out-of-zone rule");
                if(scenario=="hand-global")continue;
                if(scenario=="stale-catalog") {
                    catalog=Guid.NewGuid();Check(Projection.GetMethod("ForPosition",Hidden)!.Invoke(null,[frame,state,own.PlayerId])==null,"stale catalog reused numeric rules");continue;
                }
                var view=CardInstanceProjection.Create(source,context);
                if(scenario=="own-hand") {
                    var moveType=Search.GetNestedType("Move",BindingFlags.NonPublic)!;
                    var moves=(System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(moveType))!;
                    moves.Add(Activator.CreateInstance(moveType,new object?[]{"PlayCard",new ShadowverseBeyond.Server.Dtos.SignalR.Actions.PlayCardActionDto{CardUid=source.InstanceId,PlayMode=PlayMode.Base,PaidCost=1},view,null}));
                    var plan=Search.GetMethod("FindWithCombat",Hidden)!.Invoke(null,[state,own.PlayerId,CancellationToken.None,moves,null,null,null,null,frame]);
                    Check(plan!=null&&(bool)plan.GetType().GetProperty("ProjectsLethal")!.GetValue(plan)!,"own-hand future attack rule did not enable the generated field line");
                    var factType=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiLocalTacticalFacts")!;
                    var facts=factType.GetMethod("ReadWithCombat",Hidden)!.Invoke(null,[context,state,own.PlayerId,plan,game.StateRevision,frame])!;
                    Check((bool)facts.GetType().GetProperty("NativeInputsChecked")!.GetValue(facts)!
                        &&!(bool)facts.GetType().GetProperty("CanSkipPlanning",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(facts)!,
                        "future field abilities were ignored when treating a hand-to-field line as a plain guaranteed finish");
                }

                if(scenario=="own-hand")state=state with{Players=state.Players.Select(p=>p.PlayerId==own.PlayerId?p with{Hand=[],Field=[view with{Zone=CardZone.Field}]}:p).ToList()};
                var after=(GameStateDto)Search.GetMethod("EstimateConfiguredAction",Hidden)!.Invoke(null,[state,own.PlayerId,"attack",view,enemy.Leader==null?null:CardInstanceProjection.Create(enemy.Leader,context),frame])!;
                Check(after.Players[1].LeaderCard!.CurrentHealth==(scenario=="own-hand"?0:scenario=="attack-plus"?19:expected==1?16:18),scenario+" numeric guard changed unsupported behavior or missed finite damage");
                Check(game.StateRevision==revision&&JsonSerializer.Serialize(GameStateProjection.Create(game,context))==original,"forecast mutated the live fixture");
                if(scenario=="attack-plus") {
                    var result=sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(context,
                        [new ShadowverseBeyond.Server.Domain.Events.ActionRequests.AttackRequestedEvent(own.PlayerId,new(){AttackerUid=source.InstanceId,TargetUid=enemy.Leader!.InstanceId})]).GetAwaiter().GetResult();
                    Check(result.IsSuccess&&!result.IsCancelled&&enemy.Leader.CurrentHealth==19&&source.CurrentAttack==1,"native zero-attack +1 preparation did not match the numeric projection");
                }
            }
        } finally {GameDataProvider.SetInstanceForTesting(prior);GameServiceProvider.ContextAccessor=priorAccessor;}
    }
}
