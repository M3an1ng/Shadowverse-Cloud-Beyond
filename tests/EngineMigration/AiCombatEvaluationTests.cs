using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Services;

internal static class AiCombatEvaluationTests
{
    private static readonly Type Evaluation=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiCombatEvaluation")!;
    private static readonly Type Search=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiTacticalSearch")!;
    private const BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
    private static object? Call(string name,params object?[] args)=>Evaluation.GetMethod(name,Hidden)!.Invoke(null,args);
    private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    internal static void IsolationAndCommitMatching()
    {
        var actor=Guid.NewGuid();var other=Guid.NewGuid();var first=Guid.NewGuid();var second=Guid.NewGuid();var target=Guid.NewGuid();
        var own=new PlayerState(actor){TurnNumber=6};var enemy=new PlayerState(other);
        var game=new GameState("combat-evaluation-guard",own,enemy,actor){IsLocalAiTraining=true,Phase=GamePhase.Main,Status=GameStatus.InProgress};
        Call("Start",game,actor);
        Check((bool)Call("Enabled",game,actor,false)!&&!(bool)Call("Enabled",game,other,true)!,"comparison did not bind candidate versus baseline seats");
        var unrelated=new GameState(game.GameId,new(actor),new(other),actor);
        Check((bool)Call("Enabled",unrelated,actor,true)!&&!(bool)Call("NeedsComparison",unrelated,actor)!,"comparison leaked through a reused game ID");
        var source=new CardInstanceDto{InstanceId=first,OwnerPlayerId=actor,CardId=1,Type=CardType.Follower,Zone=CardZone.Field,
            CurrentAttack=3,CurrentHealth=3,MaxHealth=3,CanAttack=true,ZoneStayTurn=1,MaxAttacksPerTurn=1};
        var destination=new CardInstanceDto{InstanceId=target,OwnerPlayerId=other,CardId=3,Type=CardType.Leader,Zone=CardZone.Field,CurrentHealth=3,MaxHealth=20};
        var original=new GameStateDto{GameId=game.GameId,CurrentPlayerId=actor,Phase=GamePhase.Main,Players=[
            new(){PlayerId=actor,TurnNumber=6,LeaderCard=destination with{InstanceId=Guid.NewGuid(),OwnerPlayerId=actor,CurrentHealth=20},Field=[source]},
            new(){PlayerId=other,LeaderCard=destination}]};
        object? Plan(Guid id)=>Search.GetMethod("FindWithValue",Hidden)!.Invoke(null,[original with{Players=[original.Players[0] with{Field=[source with{InstanceId=id}]},original.Players[1]]},actor,CancellationToken.None,null,null,null,null,null]);
        var before=Plan(first);var after=Plan(second);
        var decision=Call("Record",game,actor,original,game.StateRevision,before,after)!;
        AiCombatProjectionMatchFacts Read()=>(AiCombatProjectionMatchFacts)Call("Snapshot",game)!;
        var data=new AttackActionDto{AttackerUid=second,TargetUid=target};
        Check(Call("BeginSubmit",game,actor,decision,"PerformAttack",new AttackActionDto{AttackerUid=first,TargetUid=target})==null,"unchosen basic request was labelled as a changed proposal");
        var directory=Path.Combine(Path.GetTempPath(),"ai-combat-eval-"+Guid.NewGuid().ToString("N"));
        try {
            var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["AiStrategy:Enabled"]="false",["AiStrategy:MemoryPath"]=directory}).Build();
            using var handler=new NeverSend();using var planner=new AiStrategyPlanner(handler,configuration,new Host(),NullLogger<AiStrategyPlanner>.Instance);
            var configured=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
                ["AiStrategy:Enabled"]="true",["AiStrategy:ApiKey"]="offline-test",["AiStrategy:Model"]="offline-mock",
                ["AiStrategy:Endpoint"]="http://127.0.0.1:1/never-send",["AiStrategy:MemoryPath"]=directory}).Build();
            using(var enabledPlanner=new AiStrategyPlanner(handler,configured,new Host(),NullLogger<AiStrategyPlanner>.Instance)) {
                Check(enabledPlanner.IsConfigured,"configured-provider budget guard was not exercised");
                Check(enabledPlanner.WarmDeckAsync(game,actor,"Neutral")==null &&
                    enabledPlanner.PlanAsync(game,original,actor,CancellationToken.None,(_,_)=>throw new InvalidOperationException("Offline comparison must not ask for a provider plan")).GetAwaiter().GetResult()==null &&
                    handler.Requests==0,"free comparison called the configured provider or warmed a guide");
            }
            var attacker=new CardInstance(second,1,actor);var defender=new CardInstance(target,3,other);var committed=new AttackEvent(attacker,defender,true);
            using(var lease=(IDisposable?)Call("BeginSubmit",game,actor,decision,"PerformAttack",data)) {
                Check(lease!=null&&Read().CommittedChangedSteps==0,"queue preparation was counted as a native commit");
                planner.ObserveCommittedEvents(game,[new AttackEvent(new CardInstance(first,1,actor),defender,true)]);
                Check(Read().CommittedChangedSteps==0,"wrong instance committed another request");
                planner.ObserveCommittedEvents(game,[committed]);planner.ObserveCommittedEvents(game,[committed]);
                Check(Read().CommittedChangedSteps==1,"exact native commit missing or duplicate counted");
            }
            planner.ObserveCommittedEvents(game,[committed]);Check(Read().CommittedChangedSteps==1,"disposed request remained observable");
            using(var outer=(IDisposable?)Call("BeginSubmit",game,actor,decision,"PerformAttack",data)) {
                using var inner=(IDisposable?)Call("BeginSubmit",game,actor,decision,"PerformAttack",data);
                outer?.Dispose();planner.ObserveCommittedEvents(game,[committed]);
            }
            Check(Read().CommittedChangedSteps==2,"old lease removed a newer pending request");
            for(var i=0;i<20;i++)using((IDisposable?)Call("BeginSubmit",game,actor,decision,"PerformAttack",data))planner.ObserveCommittedEvents(game,[committed]);
            var metrics=Read();var json=JsonSerializer.Serialize(metrics);
            Check(metrics.CommittedChangedSteps==22&&metrics.Examples.Length==16&&metrics.SearchComparisons==1&&metrics.DifferentFirstSteps==1,"bounded metrics lost full counts or confused search with completed actions");
            Check(!new[]{actor,other,first,second,target}.Any(id=>json.Contains(id.ToString()))&&handler.Requests==0,"offline metrics leaked runtime identifiers or called a provider");
            var request=new LocalTrainingRequest(Guid.NewGuid(),Guid.NewGuid(),24,CompareCombatProjection:true);
            Check((bool)typeof(LocalTrainingRequest).GetProperty("IsBenchmark",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(request)!,"projection comparison would fall through into fitting");
        } finally {
            var resolved=Path.GetFullPath(directory);
            if(!resolved.StartsWith(Path.Combine(Path.GetTempPath(),"ai-combat-eval-"),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unexpected fixture cleanup path");
            if(Directory.Exists(resolved))Directory.Delete(resolved,true);
        }
    }
    private sealed class NeverSend:HttpMessageHandler,IHttpClientFactory
    {public HttpClient CreateClient(string name)=>new(this,false);internal int Requests;protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Requests++;throw new InvalidOperationException("No provider request expected");}}
    private sealed class Host:IWebHostEnvironment
    {
        public string ApplicationName{get;set;}="offline";public string EnvironmentName{get;set;}="Development";
        public string ContentRootPath{get;set;}=Path.GetTempPath();public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
        public string WebRootPath{get;set;}=Path.GetTempPath();public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();
    }
}
