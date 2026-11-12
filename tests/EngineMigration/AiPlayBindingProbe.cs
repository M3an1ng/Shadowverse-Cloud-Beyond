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
        if(Environment.GetEnvironmentVariable("AI_ALLOW_PAID_PROBES")!="true")
            throw new InvalidOperationException("Paid AI probe disabled; explicit AI_ALLOW_PAID_PROBES=true opt-in is required.");
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
            var scenarios=(from fragile in new[]{false,true} from reverse in new[]{false,true}
                select new Scenario("two-cards",2,fragile?[1]:[],reverse,4,2,4,2)).ToList();
            if(Environment.GetEnvironmentVariable("AI_PLAY_BINDING_HOLDOUT")=="true") {
                for(var i=0;i<3;i++)scenarios.Add(new("three-cards",3,[i],false,6,1,3,3));
                scenarios.Add(new("two-fragile",3,[0,2],false,4,1,2,2));
                scenarios.Add(new("zero-cost-fragile",3,[2],false,4,1,3,3,2));
            }
            foreach(var scenario in scenarios)
            {
                var fragile=scenario.FragileIndexes.Length>0;var reverse=scenario.Reverse;
                var own=new PlayerState(Guid.NewGuid()){IsAI=true,TurnNumber=5,InitialDeckDefinitionIds=Enumerable.Repeat(2,scenario.Count).ToList()};
                var enemy=new PlayerState(Guid.NewGuid());
                var game=new GameState("binding-probe",own,enemy,own.PlayerId);
                var a=new CardInstanceDto {InstanceId=Guid.NewGuid(),CardId=2,Name=cardDefinition.Name,Description=cardDefinition.Description,
                    Cost=2,CurrentCost=2,Type=CardType.Follower,Class=CardClass.Abysscraft,CurrentAttack=1,CurrentHealth=1,MaxHealth=1};
                var hand=Enumerable.Range(0,scenario.Count).Select(index=>a with {InstanceId=Guid.NewGuid(),
                    CurrentCost=scenario.FreeIndex==index?0:2,
                    EffectDescriptions=scenario.FragileIndexes.Contains(index)?["于手牌中发动。当自己使用其他卡片时，舍弃这张卡片。"]:[]}).ToList();
                var fragileUids=scenario.FragileIndexes.Select(index=>hand[index].InstanceId).ToHashSet();
                if(reverse)hand.Reverse();
                var player=new PlayerStateDto {PlayerId=own.PlayerId,TurnNumber=5,CurrentPP=scenario.PP,MaxPP=scenario.PP,Hand=hand,DeckCount=20,
                    LeaderCard=new(){CurrentHealth=20,MaxHealth=20},Emblem=[new(){CardId=3,Description=$"自己使用梦魇从者卡时，给予敌方的主战者{scenario.Damage}点伤害。"}]};
                var opponent=new PlayerStateDto {PlayerId=enemy.PlayerId,DeckCount=20,LeaderCard=new(){CurrentHealth=scenario.EnemyHealth,MaxHealth=20}};
                var snapshot=new GameStateDto {GameId=game.GameId,CurrentPlayerId=own.PlayerId,Phase=GamePhase.Main,Players=[player,opponent]};
                var currentSnapshot=snapshot;
                PlayabilityResultDto Check(Guid uid,PlayMode mode) {
                    var now=currentSnapshot.Players[0];var card=now.Hand.FirstOrDefault(card=>card.InstanceId==uid);
                    if(card==null)return new(){IsPlayable=false};
                    return new(){IsPlayable=(mode==PlayMode.Base?card.CurrentCost:4)<=now.CurrentPP,SelectedPlayMode=mode,PlayedCardDefinitionId=2,
                        PlayModes=[new(){Mode=PlayMode.Base,EffectiveCost=card.CurrentCost,IsAvailable=card.CurrentCost<=now.CurrentPP},
                            new(){Mode=PlayMode.Enhance,EffectiveCost=4,IsAvailable=4<=now.CurrentPP}]};
                }
                Task<PlayabilityResultDto> Default(Guid uid,CancellationToken token)=>Task.FromResult(Check(uid,currentSnapshot.Players[0].CurrentPP>=4?PlayMode.Enhance:PlayMode.Base));
                Task<PlayabilityResultDto> Mode(Guid uid,PlayMode mode,CancellationToken token)=>Task.FromResult(Check(uid,mode));
                var service=Proxy<IGameService>.Make((m,args)=>m.Name switch {
                    "GetGameState"=>game,"GetCardPlayabilityAsync"=>Default((Guid)args![2]!,default),
                    "GetCardPlayabilityForModeAsync"=>Mode((Guid)args![2]!, (PlayMode)args[3]!,default),_=>null});
                using var services=new ServiceCollection().AddSingleton(service).BuildServiceProvider();
                var ai=new AiService(NullLogger<AiService>.Instance,services.GetRequiredService<IServiceScopeFactory>(),local);
                planner.WarmDeckAsync(game,own.PlayerId,"Neutral")?.GetAwaiter().GetResult();
                var timer=Stopwatch.StartNew();
                var handLoss=hand.Select((card,index)=>(card,index)).Where(item=>fragileUids.Contains(item.card.InstanceId))
                    .Select(item=>new AiStrategyPlanner.HandLossRule(item.index,Enumerable.Range(0,hand.Count).Where(index=>index!=item.index).ToArray(),777)).ToArray();
                var plan=planner.PlanAsync(game,snapshot,own.PlayerId,default,Default,checkPlayMode:Mode,handLossRules:handLoss).GetAwaiter().GetResult();
                if(!planner.CanRequest)throw new InvalidOperationException(planner.ProviderStatus.Message);
                var action=plan==null?null:ai.DecidePlayCardAsync(snapshot,own.PlayerId,plan).GetAwaiter().GetResult();
                var index=action==null?-1:hand.FindIndex(card=>card.InstanceId==action.CardUid);
                var expected=fragileUids.Count==1?hand.FindIndex(card=>fragileUids.Contains(card.InstanceId)):-1;
                var initial=plan;var executed=new List<object>();var followed=plan;
                var advance=typeof(AiStrategyPlanner).GetMethod("AdvanceCommittedPlan",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
                for(var i=0;i<(initial?.Plays?.Length??0)&&i<4&&followed?.Play!=null;i++) {
                    var selected=ai.DecidePlayCardAsync(currentSnapshot,own.PlayerId,followed).GetAwaiter().GetResult();
                    if(selected==null)break;
                    var now=currentSnapshot.Players[0];var other=currentSnapshot.Players[1];
                    var used=now.Hand.Single(card=>card.InstanceId==selected.CardUid);
                    executed.Add(new {initialIndex=hand.FindIndex(card=>card.InstanceId==used.InstanceId),mode=selected.PlayMode?.ToString(),cost=selected.PaidCost});
                    var body=used with {Zone=CardZone.Field};
                    if(selected.PlayMode==PlayMode.Enhance)body=body with {CurrentAttack=body.CurrentAttack+3,CurrentHealth=body.CurrentHealth+3,MaxHealth=body.MaxHealth+3};
                    var survivors=now.Hand.Where(card=>card.InstanceId!=used.InstanceId&&!fragileUids.Contains(card.InstanceId)).ToList();
                    currentSnapshot=currentSnapshot with {Players=[now with {CurrentPP=now.CurrentPP-selected.PaidCost!.Value,Hand=survivors,Field=now.Field.Append(body).ToList()},
                        other with {LeaderCard=other.LeaderCard! with {CurrentHealth=other.LeaderCard.CurrentHealth-scenario.Damage}}]};
                    followed=(AiStrategyPlanner.TurnPlan?)advance.Invoke(planner,[game,currentSnapshot,own.PlayerId,"PlayCard",selected]);
                }
                var passed=initial?.LineConstraintsChecked==true&&initial.Plays?.Length==scenario.ExpectedPlays&&initial.Play?.Intent=="use"
                    &&action?.PlayMode==PlayMode.Base&&index>=0&&action.PaidCost==hand[index].CurrentCost
                    &&(!fragile||fragileUids.Contains(hand[index].InstanceId))&&index==initial.Play.HandIndex
                    &&executed.Count==scenario.ExpectedPlays&&currentSnapshot.Players[1].LeaderCard!.CurrentHealth<=0;
                cases.Add(new{scenario=scenario.Name,fragile,reverse,plan=initial?.Plan,advice=initial?.Play,line=initial?.Plays,lineChecked=initial?.LineConstraintsChecked,selected=index,expectedFragileIndex=expected,
                    executed,enemyHealthAfter=currentSnapshot.Players[1].LeaderCard!.CurrentHealth,mode=action?.PlayMode.ToString(),paidCost=action?.PaidCost,passed,elapsedMs=timer.ElapsedMilliseconds});
            }
            var correct=cases.Count(item=>(bool)item.GetType().GetProperty("passed")!.GetValue(item)!);
            File.WriteAllText(output,JsonSerializer.Serialize(new{correct,total=cases.Count,cases,answers=capture.Answers,usage=planner.ProviderUsage,
                scope="synthetic fixture-defined PP/hand/discard/emblem transitions with real AiService and line reconciliation for every step; not full engine effect resolution or a win-rate claim"},
                new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true}));
            if(correct!=cases.Count)throw new InvalidOperationException($"Binding reasoning probe: {correct}/{cases.Count}; inspect report");
        }
        finally{GameDataProvider.SetInstanceForTesting(source);}
    }
    private sealed record Scenario(string Name,int Count,int[] FragileIndexes,bool Reverse,int PP,int Damage,int EnemyHealth,int ExpectedPlays,int? FreeIndex=null);
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
