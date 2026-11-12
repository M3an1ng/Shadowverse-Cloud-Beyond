using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Buffs;
using System.Reflection;
using System.Text.Json;
using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Persistence;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class AiEternalRecoveryTests
{
    private const int Leader=2000001500,Enemy=2000001501,Junk=2000001502;
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static object? Call(string type,string name,params object?[] args)=>typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services."+type)!.GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
    internal static void Boundaries()
    {
        var plan=new AiStrategyPlanner.TurnPlan("pressure","use native-bound spell",[1],[2],Play:new(0,1475210,"Base",6,"use"));
        var danger=(AiStrategyPlanner.TurnPlan)Call("AiPlayCandidates","ForEmergency",plan,true,false)!;
        Check(danger.Play?.CardId==1475210&&danger.Hold.Length==0&&danger.Prioritize.Length==0&&danger.Focus=="stabilize","emergency discarded legal use proposal or retained hoarding");
        Check(Call("AiPlayCandidates","ForEmergency",plan,true,true)==null,"verified local defense did not keep priority");
        Check(ReferenceEquals(Call("AiPlayCandidates","ForEmergency",plan,false,false),plan),"safe position plan changed");
        Check(((AiStrategyPlanner.TurnPlan)Call("AiPlayCandidates","ForEmergency",plan with{Play=plan.Play with{Intent="hold"}},true,false)!).Play==null,"emergency kept a hold command");
    }
    internal static void Run(string source,string output,string? weights)
    {
        var prior=GameDataProvider.Instance;var oldAccessor=GameServiceProvider.ContextAccessor;
        using var catalog=new ServiceCollection().AddLogging(l=>l.ClearProviders())
            .AddDbContext<ApplicationDbContext>(o=>o.UseSqlite(new SqliteConnectionStringBuilder{DataSource=source,Mode=SqliteOpenMode.ReadOnly}.ToString()))
            .AddScoped<ICardRepository,EFCardRepository>().AddScoped<AbilityLoader>().BuildServiceProvider();
        var cases=new List<object>();
        try{
            var native=new GameDataProvider(catalog.GetRequiredService<IServiceScopeFactory>(),NullLogger<GameDataProvider>.Instance);native.InitializeAsync().GetAwaiter().GetResult();
            foreach(var path in weights==null?new string?[]{null}:new string?[]{null,Path.GetFullPath(weights)})
            foreach(var name in new[]{"healthy-nightingale","nightingale-only","discount-dead-end","summon-insufficient-blood","emergency-spell-before-fox","discard-reward","drain-ep","drain-sep","bane-without-sep"}.Where(n=>Environment.GetEnvironmentVariable("AI_RECOVERY_CONTROL")!="true"||n is "healthy-nightingale" or "discount-dead-end").Where(n=>Environment.GetEnvironmentVariable("AI_RECOVERY_SCENE")==null||n==Environment.GetEnvironmentVariable("AI_RECOVERY_SCENE"))){
                try{cases.Add(Scenario(native,name,path));}catch(Exception e){cases.Add(new{scenario=name,model=path==null?"base":"frozen",passed=false,error=e.GetBaseException().ToString()});}
            }
            File.WriteAllText(output,JsonSerializer.Serialize(new{realProviderRequests=0,sourceDatabaseReadOnly=true,cases,
                scope="Actual card configurations and production native transactions in controlled scenes. Not a replay of incomplete historical states or a match win-rate estimate."},new JsonSerializerOptions{WriteIndented=true}));
            Check(JsonSerializer.SerializeToElement(cases).EnumerateArray().All(r=>r.GetProperty("passed").GetBoolean()),"Recovery scene failure; inspect "+output);
            Console.WriteLine("PASS native Eternal hand/recovery scenes: "+cases.Count+"; provider requests=0");
        }finally{GameDataProvider.SetInstanceForTesting(prior);GameServiceProvider.ContextAccessor=oldAccessor;}
    }
    private static object Scenario(IGameDataProvider native,string name,string? weights)
    {
        var defs=native.GetAllCardDefinitions().ToDictionary(x=>x.Key,x=>x.Value);
        defs[Leader]=new(){Id=Leader,Name="recovery test leader",Type=CardType.Leader,Health=20};
        defs[Enemy]=new(){Id=Enemy,Name="visible test follower",Type=CardType.Follower,Attack=name=="drain-ep"?2:5,Health=name=="drain-ep"?2:5,Keywords=[]};
        defs[Junk]=new(){Id=Junk,Name="unused private draw fixture",Type=CardType.Spell,Cost=20};
        var data=Proxy<IGameDataProvider>.Make((m,a)=>m.Name switch{"GetCardDefinition"=>defs.GetValueOrDefault((int)a![0]!),"GetAllCardDefinitions"=>defs,
            "GetAbilityConfigs"=>(int)a![0]!>=Leader?new List<AbilityConfig>():native.GetAbilityConfigs((int)a[0]!),_=>m.Invoke(native,a)});
        GameDataProvider.SetInstanceForTesting(data);var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["AiStrategy:Enabled"]="false",["AiModel:Path"]=weights??Path.Combine(Path.GetTempPath(),Guid.NewGuid()+"-no-model")}).Build();
        var model=new AiLearningModel(new Host(),config,NullLogger<AiLearningModel>.Instance);
        GameState game=null!;ServiceProvider services=null!;AiService ai=null!;var choices=new List<int[]>();var actions=new List<string>();
        GameStateDto Snapshot(){using var scope=services.CreateScope();using var context=new GameContext(scope.ServiceProvider,scope.ServiceProvider.GetRequiredService<IMapper>(),accessor,game,game.Player1.PlayerId);return GameStateProjection.Create(game,context);}
        var pending=Proxy<IPendingActionService>.Make((m,a)=>{
            if(m.ReturnType==typeof(bool))return false;if(m.Name!="WaitForPlayerChoiceAsync")return null;
            var request=(PlayerChoiceRequest)a![1]!;
            // Scripted mode reply, not a real-provider strength claim: the actual native transaction
            // must refund PP from mode 4 before the executor can afford Fox and empty the hand.
            var choice=name=="emergency-spell-before-fox"&&request.TargetKey=="decision"&&request.MinSelectionCount==2
                ?new PlayerChoiceResult{ChosenTargetIds=[request.SelectableTargetIds[1],request.SelectableTargetIds[3]]}
                :ai.DecideTargetChoiceAsync(request,Snapshot(),CancellationToken.None).GetAwaiter().GetResult();
            choices.Add(choice.ChosenTargetIds.Select(id=>game.FindCardInstance(id)?.CurrentDefinitionId??0).ToArray());return Task.FromResult(choice);
        });
        async Task<bool> Enqueue(PlayerActionWrapper action){
            IGameEvent ev=action.ActionData switch{PlayCardActionDto play=>new PlayCardRequestedEvent(action.PlayerId,play),AttackActionDto attack=>new AttackRequestedEvent(action.PlayerId,attack),
                EvolveActionDto evolve=>new EvolveRequestedEvent(action.PlayerId,evolve),SuperEvolveActionDto super=>new SuperEvolveRequestedEvent(action.PlayerId,super),_ when action.ActionType=="EndTurn"=>new EndTurnRequestedEvent(action.PlayerId),_=>throw new Exception("Unexpected recovery action")};
            using var scope=services.CreateScope();var sp=scope.ServiceProvider;
            using var context=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,action.PlayerId,triggeringEvent:ev);
            var result=await sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(context,[ev]);
            Check(result.IsSuccess&&!result.IsCancelled,result.ErrorMessage??"native recovery action failed");actions.Add(action.ActionType+":"+((action.ActionData as PlayCardActionDto) is {} cardAction?game.FindCardInstance(cardAction.CardUid)?.CurrentDefinitionId.ToString():""));action.MarkCompleted();return true;
        }
        Task<PlayabilityResultDto> Playable(Guid uid,PlayMode mode){var card=game.FindCardInstance(uid)!;return Task.FromResult(new PlayabilityResultDto{IsPlayable=card.CurrentCost<=game.Player1.CurrentPlayPoints
            &&(card.CardType==CardType.Spell||game.Player1.Field.Count<5),SelectedPlayMode=mode,PlayModes=[new(){Mode=mode,EffectiveCost=card.CurrentCost,IsAvailable=true}]});}
        var games=Proxy<IGameService>.Make((m,a)=>m.Name switch{"GetGameState"=>game,"EnqueuePlayerActionAsync"=>Enqueue((PlayerActionWrapper)a![1]!),
            "GetCardPlayabilityAsync"=>Playable((Guid)a![2]!,PlayMode.Base),"GetCardPlayabilityForModeAsync"=>Playable((Guid)a![2]!,(PlayMode)a[3]!),_=>null});
        using(services=AiEndTurnEngineTests.Services(data,accessor,pending,games)){
            using var scope=services.CreateScope();var sp=scope.ServiceProvider;
            var control=Environment.GetEnvironmentVariable("AI_RECOVERY_CONTROL")=="true";
            var combat=name.StartsWith("drain")||name=="bane-without-sep";
            var own=new PlayerState(Guid.NewGuid()){IsAI=true,TurnNumber=8,CurrentPlayPoints=combat?0:name=="emergency-spell-before-fox"?7:name is "discount-dead-end" or "summon-insufficient-blood"?6:name=="discard-reward"?2:4,MaxPlayPoints=8,
                InitialDeckDefinitionIds=[1474120,1474113,1473441],IsEvolutionUnlocked=true,IsSuperEvolutionUnlocked=true,
                CurrentEvolutionPoints=2,MaxEvolutionPoints=2,CurrentSuperEvolutionPoints=name=="bane-without-sep"?0:1,MaxSuperEvolutionPoints=2};
            var enemy=new PlayerState(Guid.NewGuid()){TurnNumber=8};
            game=new("eternal-recovery-"+name,own,enemy,own.PlayerId){Status=GameStatus.InProgress,Phase=GamePhase.Main,AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<IBuffManager>()};
            if(control)AiRecoveryAblationTests.Control(game,own.PlayerId);
            var factory=sp.GetRequiredService<ICardFactory>();
            CardInstance Add(int id,CardZone zone=CardZone.Hand)=>factory.CreateAndRegisterNewCard(game,id,own.PlayerId,zone);
            own.Leader=Add(Leader,CardZone.Field);enemy.Leader=factory.CreateAndRegisterNewCard(game,Leader,enemy.PlayerId,CardZone.Field);
            own.Leader.Damage=name=="emergency-spell-before-fox"?19:combat?name=="drain-ep"?17:19:name=="nightingale-only"?10:1;
            for(var i=0;i<5;i++){Add(Junk,CardZone.Deck);factory.CreateAndRegisterNewCard(game,Junk,enemy.PlayerId,CardZone.Deck);}
            ai=new(NullLogger<AiService>.Instance,services.GetRequiredService<IServiceScopeFactory>(),model,config);
            NativeActionBoundaries(game,sp,accessor,factory);
            var before=Snapshot();int? selected=null;double? discardReward=null;
            if(combat){
                var bat=Add(1050402,CardZone.Field);var snake=Add(1471926,CardZone.Field);
                if(name!="drain-ep"){
                    using var buffContext=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId);
                    var boosted=sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(buffContext,
                        [new StatChangedEvent(bat,1,1,0,DurationType.Permanent),new StatChangedEvent(snake,1,1,0,DurationType.Permanent)]).GetAwaiter().GetResult();
                    Check(boosted.IsSuccess,"could not prepare native +1/+1 tokens");
                }
                factory.CreateAndRegisterNewCard(game,Enemy,enemy.PlayerId,CardZone.Field).ZoneStayTurn=1;
                var executor=new AiTurnExecutor(games,ai,NullLogger<AiTurnExecutor>.Instance,sp.GetRequiredService<IMapper>(),services,model,config);
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));executor.ExecuteTurnAsync(game.GameId,own.PlayerId,timeout.Token).GetAwaiter().GetResult();
                Check(enemy.Field.Count==0,"did not clear the visible attacker");
                if(name=="drain-ep")Check(own.Leader.CurrentHealth>=6&&own.CurrentSuperEvolutionPoints==1,"failed to evolve Drain for healing with ordinary EP");
                if(name=="drain-sep")Check(own.Leader.CurrentHealth>=6&&own.CurrentSuperEvolutionPoints==0,"declined the only clear-and-heal SEP route");
                if(name=="bane-without-sep")Check(own.Leader.CurrentHealth==1,"claimed unsupported Drain survival instead of the Bane clear");
            }else if(name=="discard-reward"){
                var tutor=Add(1474113);var hostess=Add(1474116);var pickup=Add(1474118);
                using(var context=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId)){
                    var view=GameStateProjection.Create(game,context);var fact=Call("AiHandEconomy","Read",context,view.Players[0],view.Players[1]);
                    var entry=fact!.GetType().GetProperty("Item")!.GetValue(fact,[pickup.InstanceId]);discardReward=(double)entry!.GetType().GetProperty("DiscardReward")!.GetValue(entry)!;
                }
                // The native tutor choice is forced to its draw branch by the existing core policy.
                Add(1474120,CardZone.Deck);
                Enqueue(new("PlayCard",own.PlayerId,new PlayCardActionDto{CardUid=tutor.InstanceId,PlayMode=PlayMode.Base,PaidCost=1})).GetAwaiter().GetResult();
                Check(choices.Any(ids=>ids.Contains(1474118))&&!choices.Any(ids=>ids.Contains(1474116)),"discarded Hostess instead of the costly positive-discard material; reward="+discardReward+" choices="+JsonSerializer.Serialize(choices));
                Check(own.Leader.CurrentHealth==20&&enemy.Leader.CurrentHealth==19&&discardReward>0,"native discard reward was not applied");
                // Fusion value must remain separate: consuming a card is not proof of a Discard event.
            }else if(name=="emergency-spell-before-fox"){
                Add(1474276,CardZone.Emblem);var spell=Add(1475210);var fox=Add(1474114);own.BloodPool=23;
                factory.CreateAndRegisterNewCard(game,Enemy,enemy.PlayerId,CardZone.Field).ZoneStayTurn=1;
                var snapshot=Snapshot();var me=snapshot.Players[0];var them=snapshot.Players[1];
                var advice=new AiStrategyPlanner.PlayAdvice(0,1475210,"Base",6,"use");
                var plan=new AiStrategyPlanner.TurnPlan("stabilize","native-bound spell first",[],[],Play:advice);
                var boundType=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiStrategyPlanner+BoundPlay")!;
                var position=(string)typeof(AiStrategyPlanner).GetMethod("PositionKey",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[me,them])!;
                var bound=Activator.CreateInstance(boundType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[advice,spell.InstanceId,position],null);
                typeof(AiStrategyPlanner.TurnPlan).GetProperty("PlayBinding",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(plan,bound);
                var guarded=(AiStrategyPlanner.TurnPlan)Call("AiPlayCandidates","ForEmergency",plan,true,false)!;
                var play=ai.DecidePlayCardAsync(snapshot,own.PlayerId,guarded).GetAwaiter().GetResult();
                Check(play?.CardUid==spell.InstanceId,"discarded validated emergency spell advice");
                Enqueue(new("PlayCard",own.PlayerId,play!)).GetAwaiter().GetResult();
                Check(own.CurrentPlayPoints>=3&&own.Hand.Single().InstanceId==fox.InstanceId,"native spell sequence did not leave PP for Fox");
                var next=ai.DecidePlayCardAsync(Snapshot(),own.PlayerId).GetAwaiter().GetResult();Check(next?.CardUid==fox.InstanceId,"did not use remaining Fox to empty hand");
                Enqueue(new("PlayCard",own.PlayerId,next!)).GetAwaiter().GetResult();Check(own.Hand.Count==0,"spell-before-Fox sequence still blocked hand");
                selected=1475210;
            }
            else{
                Add(1474276,CardZone.Emblem);
                if(name is "discount-dead-end" or "summon-insufficient-blood"){Add(1474118);Add(1475210);own.BloodPool=name=="discount-dead-end"?20:0;}
                else{Add(1473441);if(name=="healthy-nightingale"){Add(1472978);Add(1474118);}}
                var snapshot=Snapshot();var play=ai.DecidePlayCardAsync(snapshot,own.PlayerId).GetAwaiter().GetResult();Check(play!=null,"no legal play selected");
                selected=game.FindCardInstance(play!.CardUid)!.CurrentDefinitionId;
                Check(selected==(control?name=="healthy-nightingale"?1473441:1474118:name=="summon-insufficient-blood"?1474118:name=="discount-dead-end"?1475210:name=="healthy-nightingale"?1472978:1473441),"wrong role/dead-end first play: "+selected);
                Enqueue(new("PlayCard",own.PlayerId,play)).GetAwaiter().GetResult();
            }
            return new{scenario=name,control,model=weights==null?"base":"frozen",selected,discardReward,choices,actions,hp=own.Leader.CurrentHealth,ep=own.CurrentEvolutionPoints,sep=own.CurrentSuperEvolutionPoints,enemyFollowers=enemy.Field.Count,passed=true};
        }
    }
    private static void NativeActionBoundaries(GameState game,IServiceProvider services,GameContextAccessor accessor,ICardFactory factory)
    {
        var own=game.Player1;var enemy=game.Player2;
        var hand=factory.CreateAndRegisterNewCard(game,1475210,own.PlayerId,CardZone.Hand);
        var hidden=factory.CreateAndRegisterNewCard(game,Junk,enemy.PlayerId,CardZone.Hand);
        JsonElement Read(IGameEvent root){
            using var context=new GameContext(services,services.GetRequiredService<IMapper>(),accessor,game,own.PlayerId,triggeringEvent:root);
            return JsonSerializer.SerializeToElement(Call("AiNativeActionFacts","Read",context,own.PlayerId,own.Leader));
        }
        using(var context=new GameContext(services,services.GetRequiredService<IMapper>(),accessor,game,own.PlayerId)){
            var snapshot=GameStateProjection.Create(game,context);var legacy=typeof(GameState).GetProperty("LegacyAiPlayerId",BindingFlags.Instance|BindingFlags.NonPublic)!;var prior=legacy.GetValue(game);
            try{legacy.SetValue(game,own.PlayerId);var facts=Call("AiHandEconomy","Read",context,snapshot.Players[0],snapshot.Players[1]);
                Check((int)facts!.GetType().GetProperty("Count")!.GetValue(facts)! ==0,"legacy comparison contaminated by new hand economics");}
            finally{legacy.SetValue(game,prior);}
        }
        var input=new PlayCardActionDto{CardUid=hand.InstanceId,PlayMode=PlayMode.Base,PaidCost=6,TargetUids=[own.Leader.InstanceId,hidden.InstanceId]};
        var first=Read(new PlayCardRequestedEvent(own.PlayerId,input));
        var fact=first.GetProperty("Fact");Check(fact.GetProperty("PaidCostQuote").GetInt32()==6&&!fact.GetProperty("SameCardAsAbilitySource").GetBoolean(),"native root quote/ability-source distinction missing");
        Check(fact.GetProperty("DeclaredTargets").GetArrayLength()==1&&fact.GetProperty("OmittedTargets").GetInt32()==1,"hidden target leaked into root metadata");
        input.PaidCost=null;Check(Read(new PlayCardRequestedEvent(own.PlayerId,input)).GetProperty("Fact").GetProperty("PaidCostQuote").ValueKind==JsonValueKind.Null,"unknown paid quote became zero");
        input.PaidCost=-1;Check(Read(new PlayCardRequestedEvent(own.PlayerId,input)).GetProperty("Fact").GetProperty("CostQuoteOmission").GetString()=="invalid-request-cost-quote","invalid quote accepted");
        Check(first.GetProperty("Fact").GetProperty("PaidCostQuote").GetInt32()==6,"earlier native snapshot mutated");
        Check(Read(new PlayCardRequestedEvent(enemy.PlayerId,new(){CardUid=hidden.InstanceId,PlayMode=PlayMode.Base,PaidCost=20})).GetProperty("Fact").ValueKind==JsonValueKind.Null,"hidden enemy root leaked");
        Check(Read(new HealEvent(own.Leader,1,own.Leader)).GetProperty("Fact").ValueKind==JsonValueKind.Null,"unsupported root invented facts");
        foreach(var root in new IGameEvent[]{new EndTurnRequestedEvent(own.PlayerId),new PpBoostRequestedEvent(own.PlayerId),
            new EvolveRequestedEvent(own.PlayerId,new(){UnitUid=own.Leader.InstanceId.ToString()}),
            new SuperEvolveRequestedEvent(own.PlayerId,new(){UnitUid=own.Leader.InstanceId.ToString()}),
            new AttackRequestedEvent(own.PlayerId,new(){AttackerUid=own.Leader.InstanceId,TargetUid=enemy.Leader.InstanceId})})
            Check(Read(root).GetProperty("Fact").ValueKind==JsonValueKind.Object,"visible typed native root unavailable");
        Check(!first.ToString().Contains(hidden.InstanceId.ToString())&&!first.ToString().Contains("unused private"),"private identity leaked");
        own.Hand.Remove(hand);enemy.Hand.Remove(hidden);
    }
    private sealed class Host:IWebHostEnvironment
    {
        public string ApplicationName{get;set;}="recovery-offline";public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();public string WebRootPath{get;set;}=Path.GetTempPath();
        public string EnvironmentName{get;set;}="Development";public string ContentRootPath{get;set;}=Path.GetTempPath();public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
    }
}
