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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Models;
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
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

// Explicit two-request semantic check. It does not run training, write the live database, or auto-reflect.
internal static class AiRealTacticalProbe
{
    private const int Leader=2000000900,Target=2000000901,Response=2000000902,Private=2000000903;
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(string source,string output,string modelPath,bool paid)
    {
        if(paid&&Environment.GetEnvironmentVariable("AI_ALLOW_PAID_PROBES")!="true")throw new InvalidOperationException("Explicit paid opt-in missing");
        var key=paid?Environment.GetEnvironmentVariable("AI_RULE_PROBE_KEY"):"offline";
        if(string.IsNullOrWhiteSpace(key))throw new InvalidOperationException("Provider key is not configured");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        var budgetFile=output+".budget.json";
        if(paid){using var reserved=new FileStream(budgetFile,FileMode.CreateNew,FileAccess.Write);JsonSerializer.Serialize(reserved,new{limit=2,reserved=true});}
        var prior=GameDataProvider.Instance;var oldAccessor=GameServiceProvider.ContextAccessor;
        var copy=Path.Combine(Path.GetTempPath(),"ai-real-tactics-"+Guid.NewGuid().ToString("N")+".db");
        using(var input=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=source,Mode=SqliteOpenMode.ReadOnly}.ToString()))
        using(var destination=new SqliteConnection("Data Source="+copy)){input.Open();destination.Open();input.BackupDatabase(destination);}
        using var catalog=new ServiceCollection().AddLogging(log=>log.ClearProviders())
            .AddDbContext<ApplicationDbContext>(options=>options.UseSqlite("Data Source="+copy+";Mode=ReadOnly"))
            .AddScoped<ICardRepository,EFCardRepository>().AddScoped<AbilityLoader>().BuildServiceProvider();
        using var provider=new BudgetProvider(paid,budgetFile);
        var cases=new List<object>();
        try {
            var native=new GameDataProvider(catalog.GetRequiredService<IServiceScopeFactory>(),NullLogger<GameDataProvider>.Instance);
            native.InitializeAsync().GetAwaiter().GetResult();
            foreach(var name in new[]{"precombat-destroy-preserves-evolution","heal-response-evolution-finish"}) {
                provider.Begin(name);
                try{cases.Add(Execute(native,name,key!,Path.GetFullPath(modelPath),provider,Path.GetDirectoryName(Path.GetFullPath(output))!));}
                catch(Exception error){cases.Add(new{scenario=name,passed=false,error=error.GetBaseException().Message,providerRequests=provider.Sent});}
                Save();
            }
        } finally {
            Save();GameDataProvider.SetInstanceForTesting(prior);GameServiceProvider.ContextAccessor=oldAccessor;SqliteConnection.ClearAllPools();
            if(!Path.GetFullPath(copy).StartsWith(Path.Combine(Path.GetTempPath(),"ai-real-tactics-"),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unexpected fixture cleanup path");
            foreach(var suffix in new[]{"","-wal","-shm"})if(File.Exists(copy+suffix))File.Delete(copy+suffix);
        }
        void Save()=>File.WriteAllText(output,JsonSerializer.Serialize(new {paid,realProviderRequests=provider.Sent,maximumRealProviderRequests=2,
            localGuidePlaceholders=provider.LocalGuides,blockedExtraRequests=provider.Blocked,
            scope="Two native tactical fixtures using real card configurations, synthetic leaders/target and one synthetic public healing response. Not a whole-match win-rate or learning-quality claim.",
            cases},new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true}));
    }
    private static object Execute(IGameDataProvider native,string scenario,string key,string weights,BudgetProvider provider,string directory)
    {
        var removal=scenario.StartsWith("precombat",StringComparison.Ordinal);var cardId=removal?1472426:1250402;
        var defs=native.GetAllCardDefinitions().ToDictionary(pair=>pair.Key,pair=>pair.Value);
        defs[Leader]=new(){Id=Leader,Name="测试主战者",Type=CardType.Leader,Health=20};
        defs[Target]=new(){Id=Target,Name="守护测试随从",Type=CardType.Follower,Attack=8,Health=12,Cost=8,Keywords=["Ward"]};
        defs[Response]=new(){Id=Response,Name="公开的攻击前回复",Type=CardType.Crest,Description="当敌方随从攻击时，在交战伤害之前回复自己的主战者2点生命值。"};
        defs[Private]=new(){Id=Private,Name="PRIVATE_HIDDEN_HAND_CANARY",Type=CardType.Spell,Cost=9};
        var data=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch {
            "GetCardDefinition"=>defs.GetValueOrDefault((int)args![0]!),"GetAllCardDefinitions"=>defs,
            "GetAbilityConfigs"=>(int)args![0]!>=Leader?new List<AbilityConfig>():native.GetAbilityConfigs((int)args[0]!),_=>method.Invoke(native,args)});
        GameDataProvider.SetInstanceForTesting(data);var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["AiStrategy:Enabled"]="true",["AiStrategy:Model"]="deepseek-flash",["AiStrategy:ApiKey"]=key,
            ["AiStrategy:Endpoint"]="https://api.deepseek.com/chat/completions",["AiStrategy:DisableThinking"]="true",
            ["AiStrategy:TimeoutMs"]="10000",["AiStrategy:SkipPlainTacticalFinishes"]="false",
            ["AiStrategy:MemoryPath"]=Path.Combine(directory,"isolated-memory-"+scenario),["AiModel:Path"]=weights}).Build();
        var model=new AiLearningModel(new Host(),config,NullLogger<AiLearningModel>.Instance);
        using var planner=new AiStrategyPlanner(provider,config,new Host(),NullLogger<AiStrategyPlanner>.Instance,model);
        GameState game=null!;ServiceProvider services=null!;var actions=new List<string>();var committed=new List<string>();
        async Task<bool> Enqueue(PlayerActionWrapper action) {
            IGameEvent ev=action.ActionData switch {
                AttackActionDto attack=>new AttackRequestedEvent(action.PlayerId,attack),
                EvolveActionDto evolve=>new EvolveRequestedEvent(action.PlayerId,evolve),
                SuperEvolveActionDto super=>new SuperEvolveRequestedEvent(action.PlayerId,super),_=>new TurnEndEvent(game.Player1)};
            using var scope=services.CreateScope();var sp=scope.ServiceProvider;
            using var context=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,action.PlayerId);
            var result=await sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(context,[ev]);
            if(!result.IsSuccess||result.IsCancelled)throw new InvalidOperationException(result.ErrorMessage??"Native transaction cancelled");
            actions.Add(action.ActionType);committed.AddRange(result.ProcessedEvents.Select(item=>item.EventType.ToString()));
            planner.ObserveCommittedEvents(game,result.ProcessedEvents);action.MarkCompleted();return true;
        }
        var games=Proxy<IGameService>.Make((method,args)=>method.Name switch {
            "GetGameState"=>game,"EnqueuePlayerActionAsync"=>Enqueue((PlayerActionWrapper)args![1]!),_=>null});
        using(services=AiEndTurnEngineTests.Services(data,accessor,games:games,configure:items=>items.AddSingleton(planner))) {
            using var scope=services.CreateScope();var sp=scope.ServiceProvider;
            var own=new PlayerState(Guid.NewGuid()){IsAI=true,TurnNumber=6,CurrentPlayPoints=0,MaxPlayPoints=6,InitialDeckDefinitionIds=[cardId,cardId,cardId],
                IsEvolutionUnlocked=true,IsSuperEvolutionUnlocked=true,MaxEvolutionPoints=1,CurrentEvolutionPoints=1,MaxSuperEvolutionPoints=1,CurrentSuperEvolutionPoints=1};
            var enemy=new PlayerState(Guid.NewGuid()){TurnNumber=6};
            game=new("real-tactics-"+scenario,own,enemy,own.PlayerId){IsLocalAiTraining=true,Phase=GamePhase.Main,Status=GameStatus.InProgress,
                AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<IBuffManager>()};
            typeof(GameState).GetProperty("StrategyBenchmarkPlayerId",Hidden)!.SetValue(game,own.PlayerId);
            var factory=sp.GetRequiredService<ICardFactory>();
            own.Leader=factory.CreateAndRegisterNewCard(game,Leader,own.PlayerId,CardZone.Field);
            enemy.Leader=factory.CreateAndRegisterNewCard(game,Leader,enemy.PlayerId,CardZone.Field);
            var body=factory.CreateAndRegisterNewCard(game,cardId,own.PlayerId,CardZone.Field);body.ZoneStayTurn=1;
            if(removal)factory.CreateAndRegisterNewCard(game,Target,enemy.PlayerId,CardZone.Field).ZoneStayTurn=1;
            else {
                enemy.Leader.Damage=19;
                var emblem=factory.CreateAndRegisterNewCard(game,Response,enemy.PlayerId,CardZone.Emblem);
                var ability=new AbilityConfig{Id=200000090201,CardId=Response,Name="公开的攻击前回复",Description=defs[Response].Description,
                    Trigger=new(){EventTypes=[EventType.Attack],Timing=TriggerTiming.Before,RelationType=TriggerRelationType.Any,ValidZones=[CardZone.Emblem]},
                    Targets=new(){["leader"]=new(){Query=new(){SourcePoolExpression="context.owner.leader"}}},
                    Effects=[new(){Type=EffectType.Heal,Parameters=new(){["HealAmount"]=new StaticValueConfig{Value=2}}}]};
                game.AbilityManager.CreateAbilitiesForCard(emblem,[ability],null,game);
            }
            for(var i=0;i<6;i++){factory.CreateAndRegisterNewCard(game,cardId,own.PlayerId,CardZone.Deck);factory.CreateAndRegisterNewCard(game,cardId,enemy.PlayerId,CardZone.Deck);}
            var hidden=factory.CreateAndRegisterNewCard(game,Private,enemy.PlayerId,CardZone.Hand);
            provider.PrivateIds=[own.PlayerId,enemy.PlayerId,own.Leader.InstanceId,enemy.Leader.InstanceId,body.InstanceId,hidden.InstanceId];
            provider.ModelRepliesBefore=provider.Sent;
            var health=body.CurrentHealth;
            var ai=new AiService(NullLogger<AiService>.Instance,services.GetRequiredService<IServiceScopeFactory>(),model);
            var executor=new AiTurnExecutor(games,ai,NullLogger<AiTurnExecutor>.Instance,sp.GetRequiredService<IMapper>(),services,model,config);
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25));
            executor.ExecuteTurnAsync(game.GameId,own.PlayerId,timeout.Token).GetAwaiter().GetResult();
            var report=typeof(AiStrategyPlanner).GetMethod("GetReport",Hidden)!.Invoke(planner,[game]);
            var matches=typeof(AiStrategyPlanner).GetField("matches",Hidden)!.GetValue(planner)!;
            object?[] matchArguments=[game,null];matches.GetType().GetMethod("TryGetValue")!.Invoke(matches,matchArguments);
            var tactics=(IEnumerable<AiStrategyPlanner.TacticalAdviceFact>)matchArguments[1]!.GetType().GetProperty("ProposedTactics",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.GetValue(matchArguments[1])!;
            var choices=tactics.ToArray();
            var leaderHealth=((IEnumerable<AiStrategyPlanner.LeaderHealthFact>)matchArguments[1]!.GetType().GetProperty("LeaderHealthChanges")!.GetValue(matchArguments[1])!).ToArray();
            var recordedEnemyHeal=leaderHealth.Any(fact=>fact.TargetSide=="enemy"&&fact.Change==2);
            var completedActions=((IEnumerable<AiCompletedActionFact>)matchArguments[1]!.GetType().GetProperty("CompletedActions")!.GetValue(matchArguments[1])!).ToArray();
            var completedRootObserved=completedActions.Any(fact=>fact.Action.Kind=="PerformAttack"&&(removal||fact.Result=="won"));
            var explicitFirst=choices.Any(fact=>fact.Status=="selected-before-submit"&&fact.Choice?.GetProperty("kind").GetString()==actions.FirstOrDefault());
            var passed=removal?actions.FirstOrDefault()=="PerformAttack"&&enemy.Field.Count==0&&body.CurrentHealth==health&&own.CurrentEvolutionPoints==1&&own.CurrentSuperEvolutionPoints==1
                :actions.FirstOrDefault() is "PerformEvolve" or "PerformSuperEvolve"&&game.WinnerId==own.PlayerId&&recordedEnemyHeal;
            return new{scenario,modelVersion=model.Current?.Version,sourceCardId=cardId,actions,committed=committed.Distinct(),sourceHpBefore=health,sourceHpAfter=game.FindCardInstance(body.InstanceId)?.CurrentHealth,
                ep=own.CurrentEvolutionPoints,sep=own.CurrentSuperEvolutionPoints,enemyHp=enemy.Leader.CurrentHealth,enemyField=enemy.Field.Select(card=>card.CurrentDefinitionId),
                nativeWon=game.WinnerId==own.PlayerId,nativeOutcomePassed=passed,explicitAdviceExecuted=explicitFirst,passed=passed&&explicitFirst&&completedRootObserved,choices,leaderHealth,recordedEnemyHeal,completedActions,completedRootObserved,modelRequests=provider.Sent-provider.ModelRepliesBefore,provider.ReceivedPlan,provider.ModelAnswer,
                report,usage=planner.ProviderUsage,provider=planner.ProviderStatus};
        }
    }
    private sealed class BudgetProvider(bool paid,string budgetFile):HttpMessageHandler,IHttpClientFactory
    {
        private readonly HttpMessageInvoker network=new(new HttpClientHandler());private string scenario="";private int scenarioSent;
        internal int Sent,Blocked,LocalGuides,ModelRepliesBefore;
        internal Guid[] PrivateIds=[];internal JsonElement? ReceivedPlan;internal string? ModelAnswer;
        internal void Begin(string name){scenario=name;scenarioSent=0;ReceivedPlan=null;ModelAnswer=null;}
        public HttpClient CreateClient(string name)=>new(this,false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) {
            var raw=await request.Content!.ReadAsStringAsync(token);using var payload=JsonDocument.Parse(raw);
            var input=payload.RootElement.GetProperty("messages").EnumerateArray().Where(row=>row.GetProperty("role").GetString()=="user")
                .Select(row=>row.GetProperty("content").GetString()!).FirstOrDefault(text=>text.Contains("\"legalPlays\""));
            if(input==null){LocalGuides++;return Reply(new{gamePlan="本轮未生成攻略，仅按原生规则与当前局面规划。",cards=Array.Empty<object>()});}
            ReceivedPlan=JsonDocument.Parse(input).RootElement.Clone();
            if(scenario=="heal-response-evolution-finish") {
                var instance=ReceivedPlan.Value.GetProperty("currentRuntimeRules").GetProperty("instances").EnumerateArray().Single(row=>row.GetProperty("instance").GetProperty("zone").GetString()=="emblem");
                if(instance.GetProperty("perspective").GetProperty("dslOwnerSide").GetString()!="enemy"||instance.GetProperty("perspective").GetProperty("dslOpponentSide").GetString()!="own")
                    throw new InvalidOperationException("Public response owner perspective missing before provider invocation");
            }
            if(raw.Contains("PRIVATE_HIDDEN_HAND_CANARY")||PrivateIds.Any(id=>raw.Contains(id.ToString())))throw new InvalidOperationException("Privacy guard blocked request before network");
            if(scenarioSent>=1||Sent>=2){Blocked++;return new(HttpStatusCode.TooManyRequests){Content=new StringContent("Extra probe request blocked locally")};}
            scenarioSent++;
            if(!paid) {
                var options=ReceivedPlan.Value.GetProperty("tacticalChoices").GetProperty("options").EnumerateArray();
                var desired=scenario.StartsWith("precombat",StringComparison.Ordinal)?"PerformAttack":"PerformEvolve";
                var choice=options.First(option=>option.GetProperty("kind").GetString()==desired);
                ModelAnswer=JsonSerializer.Serialize(new{focus="pressure",plan="offline fixture",prioritize=Array.Empty<int>(),hold=Array.Empty<int>(),nextAction=new{index=choice.GetProperty("index").GetInt32()}});
                return Envelope(ModelAnswer);
            }
            if(request.RequestUri?.AbsoluteUri!="https://api.deepseek.com/chat/completions")throw new InvalidOperationException("Unexpected provider endpoint");
            Sent++;File.WriteAllText(budgetFile,JsonSerializer.Serialize(new{limit=2,sent=Sent,scenario,reservedBeforeSend=true}));
            var body=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(raw)!;
            body["max_tokens"]=JsonSerializer.SerializeToElement(700);
            request.Content=new StringContent(JsonSerializer.Serialize(body),System.Text.Encoding.UTF8,"application/json");
            var response=await network.SendAsync(request,token);var text=await response.Content.ReadAsStringAsync(token);
            if(response.IsSuccessStatusCode) {
                using var json=JsonDocument.Parse(text);ModelAnswer=json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            } else ModelAnswer="HTTP "+(int)response.StatusCode;
            return new(response.StatusCode){Content=new StringContent(text,System.Text.Encoding.UTF8,"application/json")};
        }
        private static HttpResponseMessage Reply(object answer)=>Envelope(JsonSerializer.Serialize(answer));
        private static HttpResponseMessage Envelope(string answer)=>new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=answer}}}}))};
        protected override void Dispose(bool disposing){if(disposing)network.Dispose();base.Dispose(disposing);}
    }
    private sealed class Host:IWebHostEnvironment {
        public string ApplicationName{get;set;}="native-probe";public string EnvironmentName{get;set;}="Development";public string ContentRootPath{get;set;}=".";
        public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();public string WebRootPath{get;set;}=".";public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();
    }
}
