using System.Collections;
using System.Reflection;
using System.Text.Json;
using AutoMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Persistence;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static partial class AiGeneratedTokenTests
{
    private static readonly Type Defense=typeof(AiLearningModel).Assembly.GetType("ShadowverseBeyond.Server.Services.AiGeneratedDefenseModes")!;
    internal static void DefenseNative(string source,string output,string? weights)
    {
        var previous=GameDataProvider.Instance;var oldAccessor=GameServiceProvider.ContextAccessor;
        var copy=Path.Combine(Path.GetTempPath(),"ai-generated-defense-"+Guid.NewGuid().ToString("N")+".db");
        using(var input=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=source,Mode=SqliteOpenMode.ReadOnly}.ToString()))
        using(var backup=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=copy}.ToString())){input.Open();backup.Open();input.BackupDatabase(backup);}
        try{
            using var catalog=new ServiceCollection().AddLogging(l=>l.ClearProviders()).AddDbContext<ApplicationDbContext>(o=>o.UseSqlite(new SqliteConnectionStringBuilder{DataSource=copy,Mode=SqliteOpenMode.ReadOnly}.ToString()))
                .AddScoped<ICardRepository,EFCardRepository>().AddScoped<AbilityLoader>().BuildServiceProvider();
            var native=new GameDataProvider(catalog.GetRequiredService<IServiceScopeFactory>(),NullLogger<GameDataProvider>.Instance);native.InitializeAsync().GetAwaiter().GetResult();
            var cases=new List<object>();foreach(var path in weights==null?new string?[]{null}:new string?[]{null,Path.GetFullPath(weights)}){
                cases.Add(DefenseTurn(native,false,path));cases.Add(DefenseTurn(native,true,path));cases.Add(DefenseTurn(native,true,path,false));
            }
            File.WriteAllText(output,JsonSerializer.Serialize(new{realProviderRequests=0,cases,sourceDatabaseReadOnly=true,
                scope="Actual StarCall/Ghost/Skeleton/Reiss emblem configurations. Native source action followed by production AI executor; synthetic leaders/Ward. Current hand/board only; no full-match or learning claim."},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("PASS native generated defense: "+cases.Count+" control/candidate turns; real provider calls=0");
        }finally{
            GameDataProvider.SetInstanceForTesting(previous);GameServiceProvider.ContextAccessor=oldAccessor;SqliteConnection.ClearAllPools();
            Check(Path.GetFullPath(copy).StartsWith(Path.GetTempPath(),StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(copy).StartsWith("ai-generated-defense-"),"unexpected cleanup path");
            foreach(var suffix in new[]{"","-wal","-shm"})if(File.Exists(copy+suffix))File.Delete(copy+suffix);
        }
    }
    private static object DefenseTurn(IGameDataProvider native,bool enabled,string? weights,bool danger=true)
    {
        const int leader=2000001400,ward=2000001401,junk=2000001402;
        var cards=native.GetAllCardDefinitions().ToDictionary(pair=>pair.Key,pair=>pair.Value);
        cards[leader]=new(){Id=leader,Name="defense leader",Type=CardType.Leader,Health=20};
        cards[ward]=new(){Id=ward,Name="one public lethal attacker",Type=CardType.Follower,Attack=5,Health=1,Keywords=["Ward"]};
        cards[junk]=new(){Id=junk,Name="future draw unknown to policy",Type=CardType.Spell,Cost=20};
        var data=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch{
            "GetCardDefinition"=>cards.GetValueOrDefault((int)args![0]!),"GetAllCardDefinitions"=>cards,
            "GetAbilityConfigs"=>(int)args![0]! >= leader?new List<AbilityConfig>():native.GetAbilityConfigs((int)args[0]!),_=>method.Invoke(native,args)});
        GameDataProvider.SetInstanceForTesting(data);var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["AiStrategy:Enabled"]="false",["AiStrategy:GeneratedDefenseModes"]=enabled.ToString(),
            ["AiModel:Path"]=weights??Path.Combine(Path.GetTempPath(),"missing-defense-model-"+Guid.NewGuid()+".json")}).Build();
        var model=new AiLearningModel(new Host(),config,NullLogger<AiLearningModel>.Instance);
        GameState game=null!;ServiceProvider services=null!;AiService ai=null!;var selected=new List<int>();var decisions=new List<JsonElement>();var actions=new List<string>();var checks=new List<string>();
        object? Facts(GameContext context,GameStateDto snapshot,PlayerChoiceRequest request,CardInstance source)=>Defense.GetMethod("Read",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[context,game,snapshot,request,source]);
        GameStateDto Snapshot(){using var scope=services.CreateScope();using var context=new GameContext(scope.ServiceProvider,scope.ServiceProvider.GetRequiredService<IMapper>(),accessor,game,game.Player1.PlayerId);return GameStateProjection.Create(game,context);}
        var pending=Proxy<IPendingActionService>.Make((method,args)=>{
            if(method.ReturnType==typeof(bool))return false;if(method.Name!="WaitForPlayerChoiceAsync")return null;
            var request=(PlayerChoiceRequest)args![1]!;
            // Presentation labels and mode order cannot select the branch for us.
            request.SelectableTargetIds.Reverse();request.SelectableCardInstances=request.SelectableCardInstances.Select(card=>card with{Name="same misleading text"}).Reverse().ToList();
            var snapshot=Snapshot();var source=(CardInstance)typeof(PlayerChoiceRequest).GetProperty("OptionSourceCard",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(request)!;
            var context=GameServiceProvider.CurrentGameContext!;var facts=Facts(context,snapshot,request,source);
            if(facts!=null)decisions.Add(JsonSerializer.SerializeToElement(facts));
            if(selected.Count==0&&enabled&&danger){
                Check(facts!=null,"actual emblem/Ghost rules prevented the bounded defense read");
                var originalHealth=game.Player1.Leader.Damage;game.Player1.Leader.Damage--;
                Check(Facts(context,snapshot,request,source)==null,"stale health snapshot accepted");game.Player1.Leader.Damage=originalHealth;checks.Add("stale snapshot rejected");
                var originalPp=game.Player1.CurrentPlayPoints;game.Player1.CurrentPlayPoints=2;
                var noPp=JsonSerializer.SerializeToElement(Facts(context,Snapshot(),request,source));Check(noPp.GetProperty("PreferredIndex").ValueKind==JsonValueKind.Null,"unaffordable Ghost forced");game.Player1.CurrentPlayPoints=originalPp;checks.Add("unaffordable option not forced");
                var target=game.Player2.Field.Single();cards[ward].Keywords.Add("Shield");
                var shield=JsonSerializer.SerializeToElement(Facts(context,Snapshot(),request,source));Check(shield.ValueKind==JsonValueKind.Null||shield.GetProperty("PreferredIndex").ValueKind==JsonValueKind.Null,"shield target called cleared");cards[ward].Keywords.Remove("Shield");checks.Add("Shield not called a guaranteed clear");
                var cardFactory=context.ServiceProvider.GetRequiredService<ICardFactory>();
                var currentGhost=cardFactory.CreateAndRegisterNewCard(game,1050403,game.Player1.PlayerId,CardZone.Hand);
                var answered=JsonSerializer.SerializeToElement(Facts(context,Snapshot(),request,source));Check(answered.GetProperty("ExistingAnswer").GetBoolean()&&answered.GetProperty("PreferredIndex").ValueKind==JsonValueKind.Null,"already held Ghost not counted");
                game.Player1.Hand.Remove(currentGhost);checks.Add("already affordable hand answer avoids a duplicate");
                var extra=cardFactory.CreateAndRegisterNewCard(game,ward,game.Player2.PlayerId,CardZone.Field);
                Check(Facts(context,Snapshot(),request,source)==null,"multi-attacker state entered one-body reader");game.Player2.Field.Remove(extra);checks.Add("multiple attackers rejected");
                var ownBodies=Enumerable.Range(0,5).Select(_=>cardFactory.CreateAndRegisterNewCard(game,ward,game.Player1.PlayerId,CardZone.Field)).ToArray();
                var full=JsonSerializer.SerializeToElement(Facts(context,Snapshot(),request,source));Check(full.ValueKind==JsonValueKind.Null||full.GetProperty("PreferredIndex").ValueKind==JsonValueKind.Null,"full field forced a token play");
                foreach(var body in ownBodies)game.Player1.Field.Remove(body);checks.Add("full field does not force a follower answer");
                var hostess=cardFactory.CreateAndRegisterNewCard(game,1474116,game.Player1.PlayerId,CardZone.Field);
                Check(Facts(context,Snapshot(),request,source)==null,"existing end-turn sweep was ignored");game.Player1.Field.Remove(hostess);checks.Add("board-affecting future response rejected");
                // New native invocation must not reuse cancelled/failed earlier proposed choices.
                var parent=context.AbilityManager.GetEffectiveAbilitiesForCard(source.InstanceId,context).OfType<Ability>().Single();
                var retry=new PlayerChoiceRequest{RequestingPlayerId=request.RequestingPlayerId,TargetKey="decision",MinSelectionCount=1,MaxSelectionCount=1,SelectableTargetIds=request.SelectableTargetIds};
                typeof(PlayerChoiceRequest).GetProperty("OptionSourceCard",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(retry,source);
                typeof(PlayerChoiceRequest).GetProperty("DecisionOptions",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(retry,typeof(PlayerChoiceRequest).GetProperty("DecisionOptions",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(request));
                Check(Facts(context,Snapshot(),retry,source)==null,"unbound request gained a defense proof");
                var retryEffect=new ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue.Effect(game.Player1.PlayerId){TriggeredEventId=Guid.NewGuid(),EffectIndex=1,ExecutionIndex=0};
                Defense.GetMethod("Bind",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[retry,retryEffect,parent,true]);
                var fresh=JsonSerializer.SerializeToElement(Facts(context,Snapshot(),retry,source));Check(fresh.GetProperty("PendingTokenIds").GetArrayLength()==0&&fresh.GetProperty("PreferredIndex").ValueKind==JsonValueKind.Number,"new root reused a proposed choice");
                var preferred=retry.SelectableTargetIds[fresh.GetProperty("PreferredIndex").GetInt32()];
                Defense.GetMethod("Remember",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[game,retry,new List<Guid>{preferred}]);
                retryEffect.ExecutionIndex=1;var continuation=new PlayerChoiceRequest{RequestingPlayerId=request.RequestingPlayerId,TargetKey="decision",MinSelectionCount=1,MaxSelectionCount=1,SelectableTargetIds=request.SelectableTargetIds};
                typeof(PlayerChoiceRequest).GetProperty("OptionSourceCard",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(continuation,source);
                typeof(PlayerChoiceRequest).GetProperty("DecisionOptions",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(continuation,typeof(PlayerChoiceRequest).GetProperty("DecisionOptions",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(request));
                retryEffect.TriggeredEventId=Guid.NewGuid();Defense.GetMethod("Bind",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[continuation,retryEffect,parent,true]);
                Check(Facts(context,Snapshot(),continuation,source)==null,"unrelated invocation reused prior choices");checks.Add("unbound/missing/cross-invocation choices rejected; retry starts empty");
            }
            var choice=ai.DecideTargetChoiceAsync(request,Snapshot(),CancellationToken.None).GetAwaiter().GetResult();
            var map=(IReadOnlyDictionary<Guid,AbilityConfig>)typeof(PlayerChoiceRequest).GetProperty("DecisionOptions",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(request)!;
            foreach(var id in choice.ChosenTargetIds)selected.Add((int?)Rules.GetMethod("Token",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[map[id]])??0);
            return Task.FromResult(choice);
        });
        async Task<bool> Enqueue(PlayerActionWrapper action){
            using var scope=services.CreateScope();var sp=scope.ServiceProvider;using var context=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,action.PlayerId);
            IGameEvent ev=action.ActionData switch{PlayCardActionDto play=>new PlayCardRequestedEvent(action.PlayerId,play),AttackActionDto attack=>new AttackRequestedEvent(action.PlayerId,attack),_ when action.ActionType=="EndTurn"=>new TurnEndEvent(game.Player1),_=>throw new InvalidOperationException("unexpected defense action")};
            var result=await sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(context,[ev]);
            Check(result.IsSuccess&&!result.IsCancelled,result.ErrorMessage??"defense native transaction failed");actions.Add(action.ActionType);action.MarkCompleted();return true;
        }
        Task<PlayabilityResultDto> Playable(Guid uid,PlayMode mode){var card=game.FindCardInstance(uid)!;return Task.FromResult(new PlayabilityResultDto{IsPlayable=card.CurrentCost<=game.Player1.CurrentPlayPoints,
            SelectedPlayMode=mode,PlayModes=[new(){Mode=mode,EffectiveCost=card.CurrentCost,IsAvailable=true}]});}
        var games=Proxy<IGameService>.Make((method,args)=>method.Name switch{"GetGameState"=>game,"EnqueuePlayerActionAsync"=>Enqueue((PlayerActionWrapper)args![1]!),
            "GetCardPlayabilityAsync"=>Playable((Guid)args![2]!,PlayMode.Base),"GetCardPlayabilityForModeAsync"=>Playable((Guid)args![2]!,(PlayMode)args[3]!),_=>null});
        using(services=AiEndTurnEngineTests.Services(data,accessor,pending,games)){
            using var scope=services.CreateScope();var sp=scope.ServiceProvider;var own=new PlayerState(Guid.NewGuid()){IsAI=true,TurnNumber=6,CurrentPlayPoints=3,MaxPlayPoints=3,InitialDeckDefinitionIds=[1472978,1474120]};var enemy=new PlayerState(Guid.NewGuid()){TurnNumber=6};
            game=new("generated-defense-"+enabled,own,enemy,own.PlayerId){IsLocalAiTraining=true,Status=GameStatus.InProgress,Phase=GamePhase.Main,AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<IBuffManager>()};
            var factory=sp.GetRequiredService<ICardFactory>();own.Leader=factory.CreateAndRegisterNewCard(game,leader,own.PlayerId,CardZone.Field);own.Leader.Damage=danger?12:0;
            enemy.Leader=factory.CreateAndRegisterNewCard(game,leader,enemy.PlayerId,CardZone.Field);factory.CreateAndRegisterNewCard(game,ward,enemy.PlayerId,CardZone.Field).ZoneStayTurn=1;
            factory.CreateAndRegisterNewCard(game,1474276,own.PlayerId,CardZone.Emblem);var spell=factory.CreateAndRegisterNewCard(game,1472978,own.PlayerId,CardZone.Hand);
            for(var i=0;i<5;i++){factory.CreateAndRegisterNewCard(game,junk,own.PlayerId,CardZone.Deck);factory.CreateAndRegisterNewCard(game,junk,enemy.PlayerId,CardZone.Deck);}
            ai=new(NullLogger<AiService>.Instance,services.GetRequiredService<IServiceScopeFactory>(),model,config);
            Enqueue(new("PlayCard",own.PlayerId,new PlayCardActionDto{CardUid=spell.InstanceId,PlayMode=PlayMode.Base,PaidCost=2})).GetAwaiter().GetResult();
            var executor=new AiTurnExecutor(games,ai,NullLogger<AiTurnExecutor>.Instance,sp.GetRequiredService<IMapper>(),services,model,config);
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25));executor.ExecuteTurnAsync(game.GameId,own.PlayerId,timeout.Token).GetAwaiter().GetResult();
            Check(selected.SequenceEqual(enabled&&danger?new[]{1050403,1250402,1250402}:new[]{1250402,1250402,1250402}),"unexpected defensive mode sequence: "+string.Join(",",selected));
            Check(own.Leader.CurrentHealth==(danger?5:17)&&enemy.Field.Count==(enabled&&danger?0:1),"defensive mode failed to remove the actual native lethal Ward");
            if(enabled&&danger)Check(decisions.Count==3&&decisions.Skip(1).All(row=>row.GetProperty("ExistingAnswer").GetBoolean()),"existing generated Ghost failed to suppress duplicate defense picks");
            Check(!JsonSerializer.Serialize(decisions).Contains(own.PlayerId.ToString())&&!JsonSerializer.Serialize(decisions).Contains("future draw unknown"),"private identities/deck data leaked in defense facts");
            return new{enabled,danger,model=weights==null?"base":"frozen-current",selectedTokenIds=selected,decisions,boundaryChecks=checks,actions,ownHp=own.Leader.CurrentHealth,remainingEnemyFollowers=enemy.Field.Count,passed=true};
        }
    }
}
