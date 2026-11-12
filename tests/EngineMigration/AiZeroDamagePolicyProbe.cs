using System.Reflection;
using System.Text.Json;
using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class AiZeroDamagePolicyProbe
{
    private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    private sealed record Result(string Model,bool Projection,bool Won,string[] Actions,string[] AttackTargets,int OwnHealth,int EnemyHealth,int OwnSourceHealth,int EnemyFollowers);
    internal static void Run(string? output=null,string? weights=null)
    {
        var prior=GameDataProvider.Instance;var accessor=GameServiceProvider.ContextAccessor;var rows=new List<Result>();
        var directory=Path.Combine(Path.GetTempPath(),"ai-zero-policy-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try{
            foreach(var path in weights==null?new string?[]{null}:new string?[]{null,Path.GetFullPath(weights)}){
                var baseline=Execute(false,path,Path.Combine(directory,path==null?"base-control":"frozen-control"));
                var candidate=Execute(true,path,Path.Combine(directory,path==null?"base-candidate":"frozen-candidate"));rows.Add(baseline);rows.Add(candidate);
                Check(!baseline.Won&&!candidate.Won&&baseline.OwnSourceHealth==5&&candidate.OwnSourceHealth==6&&baseline.EnemyFollowers==candidate.EnemyFollowers&&candidate.EnemyFollowers==1,"native policy did not preserve health against the zero-damage Ward");
                Check(baseline.Actions.SequenceEqual(new[]{"PerformAttack","EndTurn"})&&candidate.Actions.SequenceEqual(new[]{"EndTurn"}),"candidate did not avoid the unnecessary native attack");
            }
            if(output!=null)File.WriteAllText(output,JsonSerializer.Serialize(new{realProviderRequests=0,syntheticDefinitions=true,completeNativeAiTurns=true,
                singleZeroDamageRuleOnlyProjectionDifference=true,wholeMatchStrengthClaim=false,learningClaim=false,cases=rows},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("PASS native zero-damage policy turns="+rows.Count+"; provider requests=0");
        }finally{
            GameDataProvider.SetInstanceForTesting(prior);GameServiceProvider.ContextAccessor=accessor;
            if(!Path.GetFullPath(directory).StartsWith(Path.Combine(Path.GetTempPath(),"ai-zero-policy-"),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unexpected policy cleanup path");
            if(Directory.Exists(directory))Directory.Delete(directory,true);
        }
    }
    private static Result Execute(bool projection,string? weights,string directory)
    {
        const int leader=2000001300,unit=2000001301,targetId=2000001302,junk=2000001303;
        var rule=AiZeroDamageTests.Zero(targetId);
        var definitions=new Dictionary<int,CardDefinition>{[leader]=new(){Id=leader,Name="policy leader",Type=CardType.Leader,Health=20},
            [unit]=new(){Id=unit,Name="attacker that should preserve its health",Type=CardType.Follower,Attack=6,Health=6},
            [targetId]=new(){Id=targetId,Name="zero incoming damage Ward",Type=CardType.Follower,Attack=1,Health=3,Keywords=["Ward"]},
            [junk]=new(){Id=junk,Name="neutral future draw",Type=CardType.Spell,Cost=9}};
        var revision=Guid.NewGuid();var data=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch{
            "get_DataRevision"=>revision,"GetCardDefinition"=>definitions.GetValueOrDefault((int)args![0]!),"GetAllCardDefinitions"=>definitions,
            "GetAbilityConfigs"=>(int)args![0]! == targetId?new List<AbilityConfig>{rule}:new List<AbilityConfig>(),
            "GetAllAbilityConfigs"=>definitions.Keys.ToDictionary(id=>id,id=>id==targetId?new List<AbilityConfig>{rule}:new List<AbilityConfig>()),
            "GetAllBuffDefinitions"=>new Dictionary<string,BuffDefinition>(),_=>null});
        GameDataProvider.SetInstanceForTesting(data);var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["AiStrategy:Enabled"]="false",["AiStrategy:MemoryPath"]=directory,["AiStrategy:ProjectCombatEffects"]=projection.ToString(),
            ["AiModel:Path"]=weights??Path.Combine(directory,"missing-zero-policy-model.json")}).Build();
        var model=new AiLearningModel(new Host(),config,NullLogger<AiLearningModel>.Instance);
        using var provider=new NeverProvider();using var planner=new AiStrategyPlanner(provider,config,new Host(),NullLogger<AiStrategyPlanner>.Instance,model);
        GameState game=null!;ServiceProvider services=null!;var actions=new List<string>();var targets=new List<string>();
        async Task<bool> Enqueue(PlayerActionWrapper action){
            IGameEvent ev=action.ActionData switch{
                AttackActionDto attack=>new AttackRequestedEvent(action.PlayerId,attack),
                _ when action.ActionType=="EndTurn"=>new TurnEndEvent(game.Player1),
                _=>throw new InvalidOperationException("Unexpected action in bounded no-resource policy scene: "+action.ActionType)};
            using var scope=services.CreateScope();var sp=scope.ServiceProvider;
            using var context=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,action.PlayerId);
            var result=await sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(context,[ev]);
            Check(result.IsSuccess&&!result.IsCancelled,result.ErrorMessage??"native policy action failed");
            actions.Add(action.ActionType);if(action.ActionData is AttackActionDto chosen)targets.Add(chosen.TargetUid==game.Player2.Leader!.InstanceId?"leader":"follower");
            planner.ObserveCommittedEvents(game,result.ProcessedEvents);action.MarkCompleted();return true;
        }
        var games=Proxy<IGameService>.Make((method,args)=>method.Name switch{"GetGameState"=>game,"EnqueuePlayerActionAsync"=>Enqueue((PlayerActionWrapper)args![1]!),_=>null});
        using(services=AiEndTurnEngineTests.Services(data,accessor,games:games,configure:items=>items.AddSingleton(planner))){
            using var scope=services.CreateScope();var sp=scope.ServiceProvider;
            var own=new PlayerState(Guid.NewGuid()){IsAI=true,TurnNumber=6,CurrentEvolutionPoints=0,CurrentSuperEvolutionPoints=0,
                PpBoostUsesAvailable=0,CurrentPlayPoints=0,MaxPlayPoints=6,InitialDeckDefinitionIds=[unit]};
            var enemy=new PlayerState(Guid.NewGuid()){TurnNumber=6};
            game=new("zero-policy-"+projection,own,enemy,own.PlayerId){IsLocalAiTraining=true,Phase=GamePhase.Main,Status=GameStatus.InProgress,
                AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<IBuffManager>()};
            var factory=sp.GetRequiredService<ICardFactory>();own.Leader=factory.CreateAndRegisterNewCard(game,leader,own.PlayerId,CardZone.Field);own.Leader.Damage=0;
            enemy.Leader=factory.CreateAndRegisterNewCard(game,leader,enemy.PlayerId,CardZone.Field);enemy.Leader.Damage=0;
            var attacker=factory.CreateAndRegisterNewCard(game,unit,own.PlayerId,CardZone.Field);attacker.ZoneStayTurn=1;
            factory.CreateAndRegisterNewCard(game,targetId,enemy.PlayerId,CardZone.Field).ZoneStayTurn=1;
            for(var i=0;i<3;i++){factory.CreateAndRegisterNewCard(game,junk,own.PlayerId,CardZone.Deck);factory.CreateAndRegisterNewCard(game,junk,enemy.PlayerId,CardZone.Deck);}
            var ai=new AiService(NullLogger<AiService>.Instance,services.GetRequiredService<IServiceScopeFactory>(),model);
            var executor=new AiTurnExecutor(games,ai,NullLogger<AiTurnExecutor>.Instance,sp.GetRequiredService<IMapper>(),services,model,config);
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25));
            executor.ExecuteTurnAsync(game.GameId,own.PlayerId,timeout.Token).GetAwaiter().GetResult();
            Check(provider.Requests==0,"provider request occurred in free native policy control");
            return new(weights==null?"base":"frozen-current",projection,game.WinnerId==own.PlayerId,actions.ToArray(),targets.ToArray(),own.Leader.CurrentHealth,enemy.Leader.CurrentHealth,attacker.CurrentHealth,enemy.Field.Count);
        }
    }
    private sealed class NeverProvider:HttpMessageHandler,IHttpClientFactory
    {
        internal int Requests;public HttpClient CreateClient(string name)=>new(this,false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Requests++;throw new InvalidOperationException("No provider expected");}
    }
    private sealed class Host:IWebHostEnvironment
    {
        public string ApplicationName{get;set;}="offline";public string EnvironmentName{get;set;}="Development";public string ContentRootPath{get;set;}=".";
        public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();public string WebRootPath{get;set;}=".";public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();
    }
}
