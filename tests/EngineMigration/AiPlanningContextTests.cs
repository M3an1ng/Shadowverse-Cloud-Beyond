using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text.Json;
using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Effects;
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

internal static class AiPlanningContextTests
{
    const int Leader=2000001550,Small=2000001551,Junk=2000001552;
    static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    internal static void Run(string db,string output,string weights)
    {
        var previous=GameDataProvider.Instance;var prior=GameServiceProvider.ContextAccessor;
        using var catalog=new ServiceCollection().AddLogging(l=>l.ClearProviders()).AddDbContext<ApplicationDbContext>(o=>o.UseSqlite(new SqliteConnectionStringBuilder{DataSource=db,Mode=SqliteOpenMode.ReadOnly}.ToString()))
            .AddScoped<ICardRepository,EFCardRepository>().AddScoped<AbilityLoader>().BuildServiceProvider();
        try{var native=new GameDataProvider(catalog.GetRequiredService<IServiceScopeFactory>(),NullLogger<GameDataProvider>.Instance);native.InitializeAsync().GetAwaiter().GetResult();
            var rows=new List<object>();foreach(var shape in new[]{"damage","refund"})foreach(var enabled in new[]{false,true}){
                try{rows.Add(Scene(native,shape,enabled,weights));}catch(Exception e){rows.Add(new{shape,enabled,passed=false,error=e.GetBaseException().ToString()});}
            }
            File.WriteAllText(output,JsonSerializer.Serialize(new{realProviderRequests=0,cases=rows,scope="Production dependency-injected planner and AiTurnExecutor with no caller GameContext. Native cards and committed transactions, scripted HTTP answers. Disabling the short native scope reproduces missing target/refund facts. Not proof of real DeepSeek quality."},new JsonSerializerOptions{WriteIndented=true}));
            Check(JsonSerializer.SerializeToElement(rows).EnumerateArray().All(r=>r.GetProperty("passed").GetBoolean()),"production context controls failed: "+output);
            Console.WriteLine("PASS production planning context: 4 executor controls, native target key/refund delivery, correct target execution and no context retained over HTTP; real provider calls=0");
        }finally{GameDataProvider.SetInstanceForTesting(previous);GameServiceProvider.ContextAccessor=prior;}
    }
    static object Scene(IGameDataProvider native,string shape,bool enabled,string weights)
    {
        var defs=native.GetAllCardDefinitions().ToDictionary(p=>p.Key,p=>p.Value);defs[Leader]=new(){Id=Leader,Type=CardType.Leader,Health=20};defs[Small]=new(){Id=Small,Type=CardType.Follower,Attack=1,Health=1};defs[Junk]=new(){Id=Junk,Type=CardType.Spell,Cost=20};
        var data=Proxy<IGameDataProvider>.Make((m,a)=>m.Name switch{"GetCardDefinition"=>defs.GetValueOrDefault((int)a![0]!),"GetAllCardDefinitions"=>defs,"GetAbilityConfigs"=>(int)a![0]!>=Leader?new List<AbilityConfig>():native.GetAbilityConfigs((int)a[0]!),_=>m.Invoke(native,a)});
        GameDataProvider.SetInstanceForTesting(data);var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
        var folder=Path.Combine(Path.GetTempPath(),"ai-real-planning-context-"+Guid.NewGuid().ToString("N"));var sourceId=shape=="damage"?1472979:1473444;
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["AiStrategy:Enabled"]="true",["AiStrategy:Endpoint"]="https://example.invalid/chat/completions",["AiStrategy:Model"]="offline-fake",["AiStrategy:GuideCacheEnabled"]="false",["AiStrategy:MemoryPath"]=folder,["AiStrategy:ScopedPlanningFacts"]=enabled.ToString(),["AiStrategy:SkipPlainTacticalFinishes"]="false",["AiModel:Path"]=Path.GetFullPath(weights)}).Build();
        var host=new Host();var model=new AiLearningModel(host,config,NullLogger<AiLearningModel>.Instance);using var http=new Fake(shape);
        GameState game=null!;ServiceProvider services=null!;AiService ai=null!;AiStrategyPlanner planner=null!;var choices=new List<int>();var actions=new List<string>();
        GameStateDto View(){using var s=services.CreateScope();using var c=new GameContext(s.ServiceProvider,s.ServiceProvider.GetRequiredService<IMapper>(),accessor,game,game.Player1.PlayerId);return GameStateProjection.Create(game,c);}
        Task<PlayabilityResultDto> Playable(Guid uid){using var s=services.CreateScope();using var c=new GameContext(s.ServiceProvider,s.ServiceProvider.GetRequiredService<IMapper>(),accessor,game,game.Player1.PlayerId);var card=game.FindCardInstance(uid)!;var ok=card.CurrentZone==CardZone.Hand&&card.CurrentDefinitionId==sourceId&&card.CurrentCost<=game.Player1.CurrentPlayPoints;return Task.FromResult(new PlayabilityResultDto{IsPlayable=ok,SelectedPlayMode=PlayMode.Base,PlayModes=[new(){Mode=PlayMode.Base,EffectiveCost=card.CurrentCost,IsAvailable=ok}]});}
        async Task<bool> Commit(PlayerActionWrapper action){IGameEvent ev=action.ActionData switch{PlayCardActionDto p=>new PlayCardRequestedEvent(action.PlayerId,p),_ when action.ActionType=="EndTurn"=>new EndTurnRequestedEvent(action.PlayerId),_=>throw new Exception("unexpected action "+action.ActionType)};
            using var s=services.CreateScope();using var c=new GameContext(s.ServiceProvider,s.ServiceProvider.GetRequiredService<IMapper>(),accessor,game,action.PlayerId,triggeringEvent:ev);
            var result=await s.ServiceProvider.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(c,[ev]);Check(result.IsSuccess&&!result.IsCancelled,result.ErrorMessage??"native action cancelled");planner.ObserveCommittedEvents(game,result.ProcessedEvents);actions.Add(action.ActionType);action.MarkCompleted();return true;}
        var games=Proxy<IGameService>.Make((m,a)=>m.Name switch{"GetGameState"=>game,"GetCardPlayabilityAsync" or "GetCardPlayabilityForModeAsync"=>Playable((Guid)a![2]!),"EnqueuePlayerActionAsync"=>Commit((PlayerActionWrapper)a![1]!),_=>null});
        var pending=Proxy<IPendingActionService>.Make((m,a)=>{if(m.ReturnType==typeof(bool))return false;if(m.Name!="WaitForPlayerChoiceAsync")return null;var request=(PlayerChoiceRequest)a![1]!;var choice=ai.DecideTargetChoiceAsync(request,View()).GetAwaiter().GetResult();choices.AddRange(choice.ChosenTargetIds.Select(uid=>game.FindCardInstance(uid)?.CurrentDefinitionId??0));return Task.FromResult(choice);});
        using(services=AiEndTurnEngineTests.Services(data,accessor,pending,games,s=>{s.AddSingleton<IConfiguration>(config);s.AddSingleton<IHostEnvironment>(host);s.AddSingleton<IHttpClientFactory>(http);s.AddSingleton(model);s.AddSingleton<AiStrategyPlanner>();})){
            using var scope=services.CreateScope();var sp=scope.ServiceProvider;var factory=sp.GetRequiredService<ICardFactory>();planner=services.GetRequiredService<AiStrategyPlanner>();
            var own=new PlayerState(Guid.NewGuid()){IsAI=true,TurnNumber=3,CurrentPlayPoints=shape=="damage"?3:5,MaxPlayPoints=shape=="damage"?3:5,BloodPool=2,InitialDeckDefinitionIds=[sourceId,Junk]};var enemy=new PlayerState(Guid.NewGuid()){TurnNumber=3};
            game=new("real-planning-"+shape+enabled,own,enemy,own.PlayerId){Status=GameStatus.InProgress,Phase=GamePhase.Main,AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<IBuffManager>()};
            own.Leader=factory.CreateAndRegisterNewCard(game,Leader,own.PlayerId,CardZone.Field);enemy.Leader=factory.CreateAndRegisterNewCard(game,Leader,enemy.PlayerId,CardZone.Field);
            var pegasus=factory.CreateAndRegisterNewCard(game,1471971,enemy.PlayerId,CardZone.Field);var small=factory.CreateAndRegisterNewCard(game,Small,enemy.PlayerId,CardZone.Field);
            var source=factory.CreateAndRegisterNewCard(game,sourceId,own.PlayerId,CardZone.Hand);for(var i=0;i<4;i++){factory.CreateAndRegisterNewCard(game,Junk,own.PlayerId,CardZone.Deck);factory.CreateAndRegisterNewCard(game,Junk,enemy.PlayerId,CardZone.Deck);}
            ai=new(NullLogger<AiService>.Instance,services.GetRequiredService<IServiceScopeFactory>(),model,config);Check(GameServiceProvider.CurrentGameContext==null,"fixture unexpectedly retained native context");
            planner.WarmDeckAsync(game,own.PlayerId,"Neutral")!.GetAwaiter().GetResult();
            var executor=new AiTurnExecutor(games,ai,NullLogger<AiTurnExecutor>.Instance,sp.GetRequiredService<IMapper>(),services,model,config);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(40));executor.ExecuteTurnAsync(game.GameId,own.PlayerId,timeout.Token).GetAwaiter().GetResult();
            Check(GameServiceProvider.CurrentGameContext==null&&http.ContextLeaks==0,"native context leaked into caller or provider");
            var input=http.Inputs.Single(i=>i.TryGetProperty("legalPlays",out _));var keys=input.GetProperty("legalPlays")[0].GetProperty("targetKeys").GetArrayLength();var refunds=input.GetProperty("conditionalPpRefunds");
            Check((keys>0)==enabled&&refunds.GetProperty("available").GetBoolean()==enabled,"production input facts did not follow context control");
            if(shape=="damage")Check(enabled?small.CurrentZone==CardZone.Graveyard&&pegasus.CurrentZone==CardZone.Field:pegasus.CurrentZone==CardZone.Graveyard&&small.CurrentZone==CardZone.Field,"production target advice not executed or baseline not reproduced");
            else if(enabled){var fact=refunds.GetProperty("options").EnumerateArray().Single();Check(fact.GetProperty("restoreUpTo").GetInt32()==2&&fact.GetProperty("ppIfResolved").GetInt32()==3&&fact.GetProperty("ownDamage").GetInt32()==1&&own.CurrentPlayPoints==3&&own.Leader.CurrentHealth==19,"production refund facts/payment mismatch: "+fact+"; native PP="+own.CurrentPlayPoints+" HP="+own.Leader.CurrentHealth);}
            Check(actions.SequenceEqual(new[]{"PlayCard","EndTurn"})&&http.Inputs.Count==2,"executor requested extra model calls or unexpected native actions");
            return new{shape,enabled,keys,refundsAvailable=refunds.GetProperty("available").GetBoolean(),refundOptions=refunds.GetProperty("options").GetArrayLength(),choices,actions,remainingPp=own.CurrentPlayPoints,ownHp=own.Leader.CurrentHealth,contextLeaks=http.ContextLeaks,passed=true};
        }
    }
    sealed class Fake(string shape):HttpMessageHandler,IHttpClientFactory
    {
        internal ConcurrentQueue<JsonElement> Inputs=[];internal int ContextLeaks;public HttpClient CreateClient(string name)=>new(this,false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){if(GameServiceProvider.CurrentGameContext!=null)Interlocked.Increment(ref ContextLeaks);using var envelope=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));var fields=new Dictionary<string,JsonElement>();foreach(var m in envelope.RootElement.GetProperty("messages").EnumerateArray().Where(m=>m.GetProperty("role").GetString()=="user")){using var part=JsonDocument.Parse(m.GetProperty("content").GetString()!);foreach(var p in part.RootElement.EnumerateObject())fields[p.Name]=p.Value.Clone();}var input=JsonSerializer.SerializeToElement(fields,Json);Inputs.Enqueue(input);object answer;
            if(input.TryGetProperty("legalPlays",out var plays)){var c=plays[0];var keys=c.GetProperty("targetKeys");AiStrategyPlanner.TargetAdvice? target=null;if(shape=="damage"&&keys.GetArrayLength()>0)target=new(keys[0].GetProperty("key").GetString()!,"enemy","field",1,Small,keys[0].GetProperty("abilityDefinitionId").GetInt64());answer=new AiStrategyPlanner.TurnPlan("setup","offline context control",[],[],Play:new(c.GetProperty("handIndex").GetInt32(),c.GetProperty("cardId").GetInt32(),"Base",c.GetProperty("cost").GetInt32(),"use",target));}
            else answer=new AiStrategyPlanner.DeckGuide("offline guide",input.GetProperty("deck").EnumerateArray().Select(c=>new AiStrategyPlanner.CardRole(c.GetProperty("id").GetInt32(),"fixture")).ToArray());
            return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=JsonSerializer.Serialize(answer,Json)}}}}))};}
    }
    sealed class Host:IWebHostEnvironment{public string ApplicationName{get;set;}="planning context test";public string EnvironmentName{get;set;}="Test";public string WebRootPath{get;set;}=Path.GetTempPath();public string ContentRootPath{get;set;}=Path.GetTempPath();public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
}
