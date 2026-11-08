using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

// Real provider → typed plan → normal AI selection. Synthetic rules, no claimed battle outcome.
internal static class AiPlayBindingProbe
{
    internal static void Run(string output)
    {
        var key=Environment.GetEnvironmentVariable("AI_RULE_PROBE_KEY");
        if(string.IsNullOrWhiteSpace(key))throw new InvalidOperationException("Probe key not configured");
        var source=GameDataProvider.Instance;
        var cardDefinition=new CardDefinition {Id=2,Name="回声演奏者",Cost=2,Type=CardType.Follower,Class=CardClass.Abysscraft,
            Attack=1,Health=1,Description="【爆能强化_4】+3/+3。"};
        var definitions=new Dictionary<int,CardDefinition>{{2,cardDefinition}};
        GameDataProvider.SetInstanceForTesting(Proxy<IGameDataProvider>.Make((m,a)=>m.Name switch {
            "GetCardDefinition"=>definitions.GetValueOrDefault((int)a![0]!),"GetAllCardDefinitions"=>definitions,_=>null}));
        try
        {
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
                ["AiStrategy:Enabled"]="true",["AiStrategy:Model"]="deepseek-flash",["AiStrategy:ApiKey"]=key,
                ["AiStrategy:Endpoint"]="https://api.deepseek.com/chat/completions",["AiStrategy:DisableThinking"]="true",
                ["AiStrategy:TimeoutMs"]="10000",["AiStrategy:MemoryPath"]=Path.Combine(Path.GetDirectoryName(output)!,"binding-probe-memory"),
                ["AiModel:Path"]=Path.Combine(Path.GetDirectoryName(output)!,"no-model.json")
            }).Build();
            var host=new Host();
            using var clients=new ServiceCollection().AddHttpClient().BuildServiceProvider();
            var local=new AiLearningModel(host,config,NullLogger<AiLearningModel>.Instance);
            using var capture=new Capture();
            using var planner=new AiStrategyPlanner(capture,config,host,NullLogger<AiStrategyPlanner>.Instance);
            var cases=new List<object>();
            foreach(var fragile in new[]{false,true})foreach(var reverse in new[]{false,true})
            {
                var own=new PlayerState(Guid.NewGuid()){IsAI=true,TurnNumber=5,InitialDeckDefinitionIds=[2,2]};
                var enemy=new PlayerState(Guid.NewGuid());
                var game=new GameState("binding-probe",own,enemy,own.PlayerId);
                var a=new CardInstanceDto {InstanceId=Guid.NewGuid(),CardId=2,Name=cardDefinition.Name,Description=cardDefinition.Description,
                    Cost=2,CurrentCost=2,Type=CardType.Follower,Class=CardClass.Abysscraft,CurrentAttack=1,CurrentHealth=1,MaxHealth=1};
                var b=a with{InstanceId=Guid.NewGuid(),EffectDescriptions=fragile?["于手牌中发动。当自己使用其他卡片时，舍弃这张卡片。"]:[]};
                var hand=reverse?new List<CardInstanceDto>{b,a}:new List<CardInstanceDto>{a,b};
                var player=new PlayerStateDto {PlayerId=own.PlayerId,TurnNumber=5,CurrentPP=4,MaxPP=4,Hand=hand,DeckCount=20,
                    LeaderCard=new(){CurrentHealth=20,MaxHealth=20},Emblem=[new(){CardId=3,Description="自己使用梦魇从者卡时，给予敌方的主战者2点伤害。"}]};
                var opponent=new PlayerStateDto {PlayerId=enemy.PlayerId,DeckCount=20,LeaderCard=new(){CurrentHealth=4,MaxHealth=20}};
                var snapshot=new GameStateDto {GameId=game.GameId,Phase=GamePhase.Main,Players=[player,opponent]};
                PlayabilityResultDto Check(PlayMode mode)=>new(){IsPlayable=true,SelectedPlayMode=mode,PlayedCardDefinitionId=2,
                    PlayModes=[new(){Mode=PlayMode.Base,EffectiveCost=2,IsAvailable=true},new(){Mode=PlayMode.Enhance,EffectiveCost=4,IsAvailable=true}]};
                Task<PlayabilityResultDto> Default(Guid _,CancellationToken token)=>Task.FromResult(Check(PlayMode.Enhance));
                Task<PlayabilityResultDto> Mode(Guid _,PlayMode mode,CancellationToken token)=>Task.FromResult(Check(mode));
                var service=Proxy<IGameService>.Make((m,args)=>m.Name switch {
                    "GetGameState"=>game,"GetCardPlayabilityAsync"=>Default(Guid.Empty,default),
                    "GetCardPlayabilityForModeAsync"=>Mode(Guid.Empty,(PlayMode)args![3]!,default),_=>null});
                using var services=new ServiceCollection().AddSingleton(service).BuildServiceProvider();
                var ai=new AiService(NullLogger<AiService>.Instance,services.GetRequiredService<IServiceScopeFactory>(),local);
                planner.WarmDeckAsync(game,own.PlayerId,"Neutral")?.GetAwaiter().GetResult();
                var timer=Stopwatch.StartNew();
                var handLoss=fragile?new[]{new AiStrategyPlanner.HandLossRule(hand.FindIndex(card=>card.InstanceId==b.InstanceId),
                    [hand.FindIndex(card=>card.InstanceId==a.InstanceId)],777)}:Array.Empty<AiStrategyPlanner.HandLossRule>();
                var plan=planner.PlanAsync(game,snapshot,own.PlayerId,default,Default,checkPlayMode:Mode,handLossRules:handLoss).GetAwaiter().GetResult();
                if(!planner.CanRequest)throw new InvalidOperationException(planner.ProviderStatus.Message);
                var action=plan==null?null:ai.DecidePlayCardAsync(snapshot,own.PlayerId,plan).GetAwaiter().GetResult();
                var index=action==null?-1:hand.FindIndex(card=>card.InstanceId==action.CardUid);
                var expected=fragile?hand.FindIndex(card=>card.InstanceId==b.InstanceId):-1;
                var passed=plan?.LineConstraintsChecked==true&&plan.Plays is {Length:2}&&plan.Play?.Intent=="use"&&action?.PlayMode==PlayMode.Base&&action.PaidCost==2
                    &&(!fragile||index==expected)&&index==plan.Play.HandIndex;
                cases.Add(new{fragile,reverse,plan=plan?.Plan,advice=plan?.Play,line=plan?.Plays,lineChecked=plan?.LineConstraintsChecked,selected=index,expectedFragileIndex=expected,
                    mode=action?.PlayMode.ToString(),paidCost=action?.PaidCost,passed,elapsedMs=timer.ElapsedMilliseconds});
            }
            var correct=cases.Count(item=>(bool)item.GetType().GetProperty("passed")!.GetValue(item)!);
            File.WriteAllText(output,JsonSerializer.Serialize(new{correct,total=cases.Count,cases,answers=capture.Answers,usage=planner.ProviderUsage,
                scope="synthetic planning and real AiService action selection; no effect simulation or win-rate claim"},
                new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true}));
            if(correct!=cases.Count)throw new InvalidOperationException($"Binding reasoning probe: {correct}/{cases.Count}; inspect report");
        }
        finally{GameDataProvider.SetInstanceForTesting(source);}
    }
    private sealed class Capture:DelegatingHandler,IHttpClientFactory
    {
        internal List<string> Answers=new();
        internal Capture():base(new SocketsHttpHandler()){}
        public HttpClient CreateClient(string name)=>new(this,false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            var response=await base.SendAsync(request,token);
            if(response.IsSuccessStatusCode) {
                using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
                Answers.Add(json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()??"");
            }
            return response;
        }
    }
    private sealed class Host:IWebHostEnvironment
    {
        public string WebRootPath{get;set;}=".";public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();
        public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="Binding probe";
        public string ContentRootPath{get;set;}=".";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
    }
}
