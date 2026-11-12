using System.Collections;
using System.Reflection;
using System.Text.Json;
using AutoMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Persistence;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static partial class AiGeneratedTokenTests
{
    private static readonly Type Rules=typeof(AiLearningModel).Assembly.GetType("ShadowverseBeyond.Server.Services.AiGeneratedTokenTactics")!;
    private static object[] Read(IGameDataProvider data,int id,IEnumerable<AbilityConfig>? configs=null)=>
        ((IEnumerable)Rules.GetMethod("Read",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[data,id,configs])!).Cast<object>().ToArray();
    private static int P(object value,string name)=>(int)value.GetType().GetProperty(name)!.GetValue(value)!;
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    internal static void ConfigurationGuards()
    {
        var token=new CardDefinition {Id=2,Name="renamed",Type=CardType.Follower,Cost=1,Attack=1,Health=1,Keywords=["Storm"]};
        var source=new CardDefinition {Id=1,Name="unrelated-name",Description="not a rule",Type=CardType.Spell,Cost=2};
        var config=new AbilityConfig {Type=AbilityType.Triggered,Trigger=new(){EventTypes=[EventType.PlayCard],RelationType=TriggerRelationType.TargetSelf,ValidZones=[CardZone.Hand]},
            Targets=new(){["self"]=new(){Query=new(){SourcePoolExpression="context.owner.leader"}}},
            Effects=[new(){Type=ShadowverseBeyond.Server.Domain.Effects.EffectType.Damage,Parameters=new(){["DamageAmount"]=new StaticValueConfig{Value=3},["DistributionMode"]=new StaticValueConfig{Value="perTarget"}}},
                new(){Type=ShadowverseBeyond.Server.Domain.Effects.EffectType.Choose,RepeatCount=2,Parameters=new(){["SelectionCount"]=new StaticValueConfig{Value=1},["AbilityConfigs"]=new StaticValueConfig{Value=new[]{Branch(2)}}}}]};
        var data=Proxy<IGameDataProvider>.Make((m,a)=>m.Name switch {"GetCardDefinition"=>(int)a![0]! == 1?source:token,
            "GetAbilityConfigs"=>(int)a![0]! == 1?new List<AbilityConfig>{config}:new List<AbilityConfig>(),_=>null});
        var result=Read(data,1);
        Check(result.Length==1&&P(result[0],"Count")==3&&P(result[0],"SelfDamage")==3&&P(result[0],"TokenId")==2,"typed generation recipe missed repeat count or token identity");
        config.Effects[1].RepeatCountValue=new FormulaValueConfig{Formula="2"};Check(Read(data,1).Length==0,"formula-driven repetition entered a static proof");config.Effects[1].RepeatCountValue=null;
        config.Effects[0].Parameters["DamageAmount"]=new FormulaValueConfig{Formula="3"};Check(Read(data,1).Length==0,"formula damage entered a static proof");config.Effects[0].Parameters["DamageAmount"]=new StaticValueConfig{Value=4};
        Check(P(Read(data,1)[0],"SelfDamage")==4,"reader used a hard-coded damage amount");
        Check(Read(data,1,[]).Length==0&&Read(data,1,[config,config]).Length==0,"removed/additional runtime abilities were ignored");
        config.Conditions.Add(new(){Expression="false"});Check(Read(data,1).Length==0,"conditional source was called a verified route");config.Conditions.Clear();
        config.Effects[1].ChainedAbilities.Add(new());Check(Read(data,1).Length==0,"unmodelled chained effects entered a proof");config.Effects[1].ChainedAbilities.Clear();
        token.Keywords=["Storm","CannotAttackLeader"];Check(Read(data,1).Length==0,"a generated follower unable to attack leaders entered lethal");
        token.Keywords=[];Check(Read(data,1).Length==0,"non-Storm generation entered a face-lethal route");
        Console.WriteLine("PASS generated-token rules: actual IDs/effects/repeats, renamed cards, dynamic/conditional/chained/removed ability guards; provider calls=0");
    }
    private static AbilityConfig Branch(int id)=>new(){Name="misleading option name",Effects=[new(){Type=ShadowverseBeyond.Server.Domain.Effects.EffectType.GetToken,
        Parameters=new(){["CardDefinitionId"]=new StaticValueConfig{Value=new[]{id}},["PlayerScope"]=new StaticValueConfig{Value="self"},
            ["CardSourceMode"]=new StaticValueConfig{Value="definition"},["Amount"]=new StaticValueConfig{Value=1}}}]};

    internal static void DirectiveScope()
    {
        var own=new PlayerState(Guid.NewGuid());var enemy=new PlayerState(Guid.NewGuid());var game=new GameState("directive-scope",own,enemy,own.PlayerId);
        var source=typeof(GameState).GetProperty("AiGeneratedStormSource",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var token=typeof(GameState).GetProperty("AiGeneratedStormTokenId",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var sourceId=Guid.NewGuid();var state="";
        var games=Proxy<IGameService>.Make((m,a)=>
        {
            if(m.Name!="EnqueuePlayerActionAsync")return null;
            Check((Guid?)source.GetValue(game)==sourceId&&(int?)token.GetValue(game)==1050403,"choice binding was not installed for the submitted action");
            var action=(PlayerActionWrapper)a![1]!;
            if(state=="complete")action.MarkCompleted();else if(state=="reject")action.MarkRejected("fixture");else if(state=="fail")action.MarkFailed("fixture");
            return Task.FromResult(state!="unadmitted");
        });
        using var services=new ServiceCollection().BuildServiceProvider();
        var executor=new AiTurnExecutor(games,Proxy<IAiService>.Make((m,a)=>null),NullLogger<AiTurnExecutor>.Instance,Proxy<IMapper>.Make((m,a)=>null),services);
        var submit=typeof(AiTurnExecutor).GetMethod("SubmitAiActionAsync",BindingFlags.Instance|BindingFlags.NonPublic)!;
        foreach(var next in new[]{"complete","reject","fail","unadmitted","cancelled-wait"})
        {
            state=next;using var cts=new CancellationTokenSource();if(next=="cancelled-wait")cts.Cancel();
            var action=new PlayerActionWrapper("PlayCard",own.PlayerId,new{});
            var pending=(Task<PlayerActionCompletion?>)submit.Invoke(executor,[game,action,sourceId,cts.Token,null,null,null,null,1050403,null])!;
            try{pending.GetAwaiter().GetResult();Check(next!="cancelled-wait","cancelled decision wait completed");}
            catch(OperationCanceledException){Check(next=="cancelled-wait","unexpected cancellation");}
            Check(source.GetValue(game)==null&&token.GetValue(game)==null,"a completed/rejected/failed/cancelled route leaked into later choices");
        }
        Console.WriteLine("PASS generated-token directive: exact source/token installed and cleared on success, rejection, failure, non-admission and cancellation; no provider");
    }

    internal static void Native(string source,string output)
    {
        var previous=GameDataProvider.Instance;var oldAccessor=GameServiceProvider.ContextAccessor;
        var copy=Path.Combine(Path.GetTempPath(),"ai-generated-token-"+Guid.NewGuid().ToString("N")+".db");
        using(var input=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=source,Mode=SqliteOpenMode.ReadOnly}.ToString()))
        using(var backup=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=copy}.ToString())){input.Open();backup.Open();input.BackupDatabase(backup);}
        try
        {
            using var catalog=new ServiceCollection().AddLogging(l=>l.ClearProviders()).AddDbContext<ApplicationDbContext>(o=>o.UseSqlite(new SqliteConnectionStringBuilder{DataSource=copy,Mode=SqliteOpenMode.ReadOnly}.ToString()))
                .AddScoped<ICardRepository,EFCardRepository>().AddScoped<AbilityLoader>().BuildServiceProvider();
            var native=new GameDataProvider(catalog.GetRequiredService<IServiceScopeFactory>(),NullLogger<GameDataProvider>.Instance);native.InitializeAsync().GetAwaiter().GetResult();
            Check(Read(native,1472978).Any(r=>P(r,"TokenId")==1050403&&P(r,"Count")==3&&P(r,"SelfDamage")==3),"actual loaded 群星 configuration did not match");
            var rows=new List<object>();
            foreach(var fixture in new[]{("offline-lethal",5,20,false,false,true), ("renamed-reordered-modes",5,20,true,false,true),
                ("unaffordable",4,20,false,false,false),("self-lethal",5,3,false,false,false),("silenced-source",5,20,false,true,false),("public-heal-response",5,20,false,false,false),("local-route-disabled-control",5,20,false,false,false),
                ("hostess-own-attack-empty-hand",5,20,false,false,false),("hostess-own-attack-low-hp",5,2,false,false,false),("hostess-hand-blocks-sweep",5,2,false,false,false)})
                rows.Add(Turn(native,fixture.Item1,fixture.Item2,fixture.Item3,fixture.Item4,fixture.Item5,fixture.Item6));
            File.WriteAllText(output,JsonSerializer.Serialize(new{providerCalls=0,readOnlySource=true,cases=rows,
                scope="Production AI executor and actual native card configs, decisions and transactions; synthetic leaders/playability adapter. No real-model or full-match strength claim."},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("PASS native generated-token turns: "+rows.Count+"; LLM unregistered; source database read-only");
        }
        finally
        {
            GameDataProvider.SetInstanceForTesting(previous);GameServiceProvider.ContextAccessor=oldAccessor;
            SqliteConnection.ClearAllPools();
            if(Path.GetFullPath(copy).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(copy).StartsWith("ai-generated-token-"))
                foreach(var suffix in new[]{"","-wal","-shm"})if(File.Exists(copy+suffix))File.Delete(copy+suffix);
        }
    }
    private static object Turn(IGameDataProvider native,string name,int pp,int health,bool disguise,bool silenced,bool expected)
    {
        const int leader=2000000200;
        var cards=native.GetAllCardDefinitions().ToDictionary(p=>p.Key,p=>p.Value);cards[leader]=new(){Id=leader,Type=CardType.Leader,Health=20};
        cards[leader+2]=new(){Id=leader+2,Name="三血目标",Type=CardType.Follower,Attack=1,Health=3};
        cards[leader+1]=new(){Id=leader+1,Name="公开的使用卡牌时回复",Type=CardType.Amulet};
        var response=new AbilityConfig{Id=leader+1,CardId=leader+1,Type=AbilityType.Triggered,
            Trigger=new(){EventTypes=[EventType.PlayCard],ValidZones=[CardZone.Field]},
            Targets=new(){["heal"]=new(){Query=new(){SourcePoolExpression="context.opponent.leader"}}},
            Effects=[new(){Type=ShadowverseBeyond.Server.Domain.Effects.EffectType.Heal,Parameters=new(){["HealAmount"]=new StaticValueConfig{Value=1}}}]};
        var data=Proxy<IGameDataProvider>.Make((m,a)=>m.Name switch {"GetCardDefinition"=>cards.GetValueOrDefault((int)a![0]!),"GetAllCardDefinitions"=>cards,
            "GetAbilityConfigs"=>((int)a![0]! == leader || (int)a[0]! == leader+2)?new List<AbilityConfig>():(int)a[0]! == leader+1?new List<AbilityConfig>{response}:native.GetAbilityConfigs((int)a[0]!),_=>m.Invoke(native,a)});
        GameDataProvider.SetInstanceForTesting(data);
        var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
        GameState game=null!;ServiceProvider services=null!;AiService actualAi=null!;
        var actions=new List<string>();var selected=new List<int>();var attacks=new List<Guid>();
        var handOutcomes=new List<AiCompletedActionFact>();
        var observer=typeof(AiLearningModel).Assembly.GetType("ShadowverseBeyond.Server.Services.AiActionOutcomes")!;
        object? Observe(string method,params object?[] args)=>observer.GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,args);
        GameStateDto Snapshot(){using var scope=services.CreateScope();using var context=new GameContext(scope.ServiceProvider,scope.ServiceProvider.GetRequiredService<IMapper>(),accessor,game,game.Player1.PlayerId);return GameStateProjection.Create(game,context);}
        var pending=Proxy<IPendingActionService>.Make((m,a)=>
        {
            if(m.ReturnType==typeof(bool))return false;
            if(m.Name!="WaitForPlayerChoiceAsync")return null;
            var request=(PlayerChoiceRequest)a![1]!;
            if(disguise){request.SelectableTargetIds.Reverse();request.SelectableCardInstances=request.SelectableCardInstances.Select(c=>c with{Name="增加1张『骷髅士兵』到自己的手牌中。"}).Reverse().ToList();}
            var choice=actualAi.DecideTargetChoiceAsync(request,Snapshot(),CancellationToken.None).GetAwaiter().GetResult();
            var map=(IReadOnlyDictionary<Guid,AbilityConfig>)typeof(PlayerChoiceRequest).GetProperty("DecisionOptions",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(request)!;
            foreach(var id in choice.ChosenTargetIds)selected.Add((int?)Rules.GetMethod("Token",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[map[id]])??0);
            Check(!JsonSerializer.Serialize(request).Contains("DecisionOptions",StringComparison.OrdinalIgnoreCase),"internal configs leaked in player choice JSON");
            return Task.FromResult(choice);
        });
        async Task<bool> Enqueue(PlayerActionWrapper action)
        {
            actions.Add(action.ActionType);if(action.ActionData is AttackActionDto attack)attacks.Add(attack.TargetUid);using var scope=services.CreateScope();var sp=scope.ServiceProvider;
            using var context=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,action.PlayerId);
            IGameEvent ev=action.ActionData switch {PlayCardActionDto play=>new PlayCardRequestedEvent(action.PlayerId,play),AttackActionDto hit=>new AttackRequestedEvent(action.PlayerId,hit),_=>new TurnEndEvent(game.Player1)};
            var lease=Observe("Begin",game,action,GameStateProjection.Create(game,context),null);
            var result=await sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(context,[ev]);
            if(lease!=null)Observe("Observe",lease,game,result.ProcessedEvents,null);
            if(!result.IsSuccess||result.IsCancelled)action.MarkFailed(result.ErrorMessage??"transaction rejected");else action.MarkCompleted();
            if(lease!=null&&Observe("Finish",lease) is AiCompletedActionFact fact)handOutcomes.Add(fact);
            return true;
        }
        Task<PlayabilityResultDto> Playable(Guid uid,PlayMode mode)
        {
            var card=game.FindCardInstance(uid)!;return Task.FromResult(new PlayabilityResultDto{IsPlayable=card.CurrentCost<=game.Player1.CurrentPlayPoints,
                SelectedPlayMode=mode,PlayModes=[new(){Mode=mode,EffectiveCost=card.CurrentCost,IsAvailable=true}]});
        }
        var games=Proxy<IGameService>.Make((m,a)=>m.Name switch {"GetGameState"=>game,"EnqueuePlayerActionAsync"=>Enqueue((PlayerActionWrapper)a![1]!),
            "GetCardPlayabilityAsync"=>Playable((Guid)a![2]!,PlayMode.Base),"GetCardPlayabilityForModeAsync"=>Playable((Guid)a![2]!,(PlayMode)a[3]!),_=>null});
        using(services=AiEndTurnEngineTests.Services(data,accessor,pending,games))
        {
            using var scope=services.CreateScope();var sp=scope.ServiceProvider;var own=new PlayerState(Guid.NewGuid()){IsAI=true,TurnNumber=6,CurrentPlayPoints=pp,MaxPlayPoints=pp};var enemy=new PlayerState(Guid.NewGuid()){TurnNumber=6};
            game=new(name,own,enemy,own.PlayerId){IsLocalAiTraining=true,Status=GameStatus.InProgress,Phase=GamePhase.Main,AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<ShadowverseBeyond.Server.Domain.Buffs.IBuffManager>()};
            var factory=sp.GetRequiredService<ICardFactory>();own.Leader=factory.CreateAndRegisterNewCard(game,leader,own.PlayerId,CardZone.Field);enemy.Leader=factory.CreateAndRegisterNewCard(game,leader,enemy.PlayerId,CardZone.Field);
            var hostess=name.StartsWith("hostess-",StringComparison.Ordinal);
            own.Leader.Damage=20-health;enemy.Leader.Damage=hostess?0:17;
            if(hostess){for(var i=0;i<2;i++)factory.CreateAndRegisterNewCard(game,leader+2,enemy.PlayerId,CardZone.Field);
                for(var i=0;i<3;i++)factory.CreateAndRegisterNewCard(game,leader+2,enemy.PlayerId,CardZone.Deck);}
            if(name=="hostess-hand-blocks-sweep")factory.CreateAndRegisterNewCard(game,leader+2,own.PlayerId,CardZone.Hand);
            if(name=="public-heal-response")factory.CreateAndRegisterNewCard(game,leader+1,own.PlayerId,CardZone.Field);
            var spell=factory.CreateAndRegisterNewCard(game,hostess?1474116:1472978,own.PlayerId,hostess?CardZone.Field:CardZone.Hand);
            if(hostess)spell.ZoneStayTurn=1;var sourceHp=spell.CurrentHealth;if(silenced)typeof(CardInstance).GetProperty("IsSilenced")!.SetValue(spell,true);
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["AiModel:Path"]=Path.Combine(Path.GetTempPath(),"missing-offline-"+Guid.NewGuid()+".json"),["AiStrategy:Enabled"]="false"}).Build();
            var learned=new AiLearningModel(new Host(),config,NullLogger<AiLearningModel>.Instance);actualAi=new(NullLogger<AiService>.Instance,services.GetRequiredService<IServiceScopeFactory>(),learned);
            var ai=Proxy<IAiService>.Make((m,a)=>m.Name switch
            {
                // The generic card proposer deliberately omits the source; the local route must find it itself.
                "DecidePlayCardAsync"=>Task.FromResult<PlayCardActionDto?>(((GameStateDto)a![0]!).Players[0].Hand.FirstOrDefault(c=>c.CardId==1050403&&c.CurrentCost<=game.Player1.CurrentPlayPoints) is { } token?new(){CardUid=token.InstanceId,PlayMode=PlayMode.Base,PaidCost=token.CurrentCost}:null),
                _=>null
            });
            if(name=="local-route-disabled-control")typeof(GameState).GetProperty("LegacyGeneratedTokenPlayerId",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(game,own.PlayerId);
            var executor=new AiTurnExecutor(games,ai,NullLogger<AiTurnExecutor>.Instance,sp.GetRequiredService<IMapper>(),services,learned,config);
            executor.ExecuteTurnAsync(name,own.PlayerId).GetAwaiter().GetResult();
            if(hostess)
            {
                var exchanged=attacks.Any(id=>id!=enemy.Leader.InstanceId);
                if(name=="hostess-hand-blocks-sweep")Check(exchanged&&enemy.Field.Count>0,"AI assumed empty-hand sweep despite holding a card");
                else Check(!exchanged&&spell.CurrentHealth==sourceHp&&enemy.Field.Count==0,name+" wasted hostess health on a follower already dying at end turn");
            }
            else if(expected)Check(game.WinnerId==own.PlayerId&&own.Leader.CurrentHealth==17&&selected.SequenceEqual(new[]{1050403,1050403,1050403})&&actions.Count(a=>a=="PlayCard")==4&&actions.Count(a=>a=="PerformAttack")==3,name+" did not perform the complete native lethal");
            else Check(selected.Count==0&&own.Hand.Any(c=>c.InstanceId==spell.InstanceId)&&!actions.Contains("PlayCard"),name+" forced an unavailable route");
            Check(typeof(GameState).GetProperty("AiGeneratedStormSource",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(game)==null&&typeof(GameState).GetProperty("AiGeneratedStormTokenId",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(game)==null,"tactical choice directive leaked beyond action completion");
            if(expected) {
                var generated=handOutcomes.First().OwnHandChanges!;
                Check(generated.BeforeCount==1&&generated.AfterCount==3&&generated.Changes.Count(c=>c.Change=="departed"&&c.Before?.CardId==1472978)==1
                    &&generated.Changes.Count(c=>c.Change=="arrived"&&c.After is {CardId:1050403,Cost:1} card&&card.Keywords.Contains("Storm"))==3,
                    "actual generated Ghosts were not observed individually with native cost/keywords");
                Check(handOutcomes.Where(f=>f.Action.Kind=="PlayCard"&&f.Action.CardId==1050403).All(f=>f.OwnHandChanges!.Changes is [{Change:"departed"}]),
                    "playing generated cards fabricated new cards from hand reindexing");
            }
            return new{name,handOutcomes,expectedLethal=expected,winner=game.WinnerId==own.PlayerId,ownHp=own.Leader.CurrentHealth,enemyHp=enemy.Leader.CurrentHealth,selectedTokenIds=selected,actions,attackCount=attacks.Count,sourceHpBefore=sourceHp,sourceHpAfter=spell.CurrentHealth,attackedFollower=attacks.Any(id=>id!=enemy.Leader.InstanceId),enemyFollowers=enemy.Field.Count,passed=true};
        }
    }
    private sealed class Host:IWebHostEnvironment
    {
        public string ApplicationName{get;set;}="offline";public string EnvironmentName{get;set;}="Development";public string ContentRootPath{get;set;}=Path.GetTempPath();public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();public string WebRootPath{get;set;}=Path.GetTempPath();public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();
    }
}
