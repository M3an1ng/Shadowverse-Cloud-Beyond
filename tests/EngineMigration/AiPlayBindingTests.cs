using System.Net;
using System.Reflection;
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

internal static class AiPlayBindingTests
{
    internal static void Run()
    {
        var root=Path.Combine(Path.GetTempPath(),"ai-play-binding-"+Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var own=new PlayerState(Guid.NewGuid()) {IsAI=true,TurnNumber=4,InitialDeckDefinitionIds=[2,2]};
            var enemy=new PlayerState(Guid.NewGuid());
            var card=new CardInstanceDto {InstanceId=Guid.NewGuid(),CardId=2,CurrentCost=2,Cost=2,
                Name="Card 2",Description="双形态卡",Type=CardType.Follower,CurrentAttack=2,CurrentHealth=3};
            var player=new PlayerStateDto {PlayerId=own.PlayerId,TurnNumber=4,CurrentPP=5,MaxPP=5,
                Hand=[card,card with {InstanceId=Guid.NewGuid(),EffectDescriptions=["DIFFERENT_COPY_RULE"]}],
                LeaderCard=new() {CurrentHealth=20,MaxHealth=20}};
            var opponent=new PlayerStateDto {PlayerId=enemy.PlayerId,LeaderCard=new() {CurrentHealth=20,MaxHealth=20},
                Hand=[new() {Name="PRIVATE_OPPONENT_HAND"}]};
            var snapshot=new GameStateDto {GameId="play-binding",Players=[player,opponent]};
            var game=new GameState(snapshot.GameId,own,enemy,own.PlayerId);
            bool baseAvailable=true,defaultAvailable=true;
            PlayabilityResultDto CheckMode(Guid uid,PlayMode mode)=>new() {
                IsPlayable=mode==PlayMode.Base?baseAvailable:defaultAvailable,
                SelectedPlayMode=mode,PlayedCardDefinitionId=2,SelectedAbilityRuntimeId=mode==PlayMode.Enhance?Guid.Parse("00000000-0000-0000-0000-000000000020"):null,
                PlayModes=[new(){Mode=PlayMode.Base,EffectiveCost=2,IsAvailable=true},new(){Mode=PlayMode.Enhance,EffectiveCost=4,IsAvailable=true}]
            };
            Task<PlayabilityResultDto> Default(Guid uid,CancellationToken _)=>Task.FromResult(CheckMode(uid,PlayMode.Enhance));
            Task<PlayabilityResultDto> Mode(Guid uid,PlayMode mode,CancellationToken _)=>Task.FromResult(CheckMode(uid,mode));
            var service=Proxy<IGameService>.Make((m,a)=>m.Name switch {
                "GetCardPlayabilityAsync"=>Default((Guid)a![2]!,default),
                "GetCardPlayabilityForModeAsync"=>Mode((Guid)a![2]!, (PlayMode)a[3]!,default),
                "GetGameState"=>game,_=>null
            });
            using var services=new ServiceCollection().AddSingleton(service).BuildServiceProvider();
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
                ["AiStrategy:Enabled"]="true",["AiStrategy:Model"]="mock",["AiStrategy:Endpoint"]="https://example.invalid/chat/completions",
                ["AiStrategy:MemoryPath"]=root,["AiModel:Path"]=Path.Combine(root,"missing-model.json")
            }).Build();
            var host=new Host();
            using var provider=new Provider();
            using var planner=new AiStrategyPlanner(provider,config,host,NullLogger<AiStrategyPlanner>.Instance);
            var local=new AiLearningModel(host,config,NullLogger<AiLearningModel>.Instance);
            var ai=new AiService(NullLogger<AiService>.Instance,services.GetRequiredService<IServiceScopeFactory>(),local);
            planner.WarmDeckAsync(game,own.PlayerId,"Neutral")?.GetAwaiter().GetResult();
            var plan=planner.PlanAsync(game,snapshot,own.PlayerId,default,Default,checkPlayMode:Mode).GetAwaiter().GetResult();
            Check(plan?.Play is {HandIndex:1,Mode:"Base",Cost:2},"valid exact play suggestion was lost");
            var action=ai.DecidePlayCardAsync(snapshot,own.PlayerId,plan).GetAwaiter().GetResult();
            Check(action?.CardUid==player.Hand[1].InstanceId&&action.PlayMode==PlayMode.Base&&action.PaidCost==2&&action.AbilityRuntimeId==null,
                "execution mixed identical copies, auto enhanced, or retained enhance runtime binding");
            using(var input=JsonDocument.Parse(provider.LastPlanInput)) {
                var legal=input.RootElement.GetProperty("legalPlays");
                Check(legal.GetArrayLength()==4&&legal.EnumerateArray().Select(x=>x.GetProperty("handIndex").GetInt32()).Distinct().Count()==2,
                    "same-card instances or alternative forms were collapsed");
                Check(legal.EnumerateArray().Where(x=>x.GetProperty("mode").GetString()=="Base").All(x=>
                    x.GetProperty("budgetOnlyFollowUps").EnumerateArray().All(next=>
                        next.GetProperty("handIndex").GetInt32()!=x.GetProperty("handIndex").GetInt32()
                        &&next.GetProperty("cost").GetInt32()<=x.GetProperty("budgetAfterPlay").GetInt32())),
                    "resource continuations reused the same instance or exceeded remaining PP");
                Check(legal.EnumerateArray().Where(x=>x.GetProperty("handIndex").GetInt32()==1)
                    .All(x=>x.GetProperty("atRiskHandIndexes").GetArrayLength()==0),"discard risk includes the card being played");
                Check(input.RootElement.GetProperty("ownHand")[1].GetProperty("card").GetProperty("effects")[0].GetString()=="DIFFERENT_COPY_RULE",
                    "candidate's granted effects were hidden");
            }
            Check(!provider.LastPlanInput.Contains(card.InstanceId.ToString())&&!provider.LastPlanInput.Contains(own.PlayerId.ToString())
                &&!provider.LastPlanInput.Contains("PRIVATE_OPPONENT_HAND"),"runtime or private opponent identity leaked");
            foreach(var changed in new[] {
                snapshot with {Players=[player with {CurrentPP=4},opponent]},
                snapshot with {Players=[player with {Hand=[card,player.Hand[1] with {EffectDescriptions=["NEW_RULE"]}]},opponent]},
                snapshot with {Players=[player with {Hand=[card,player.Hand[1] with {InstanceId=Guid.NewGuid()}]},opponent]},
                snapshot with {Players=[player,opponent with {Field=[new(){CardId=1,Type=CardType.Follower,CurrentAttack=1,CurrentHealth=1}]}]}
            }) {
                var fallback=ai.DecidePlayCardAsync(changed,own.PlayerId,plan).GetAwaiter().GetResult();
                Check(fallback?.PlayMode==PlayMode.Enhance,"stale positional advice survived changed PP, effects, instance or board");
            }
            baseAvailable=false;
            Check(ai.DecidePlayCardAsync(snapshot,own.PlayerId,plan).GetAwaiter().GetResult()?.PlayMode==PlayMode.Enhance,
                "a newly illegal advised form was still submitted");
            baseAvailable=true;defaultAvailable=false;
            Check(ai.DecidePlayCardAsync(snapshot,own.PlayerId,plan).GetAwaiter().GetResult()?.PlayMode==PlayMode.Base,
                "valid explicit base form disappeared when auto enhance had no legal targets");
            provider.Invalid=true;own.TurnNumber+=2;player=player with {TurnNumber=own.TurnNumber};snapshot=snapshot with {Players=[player,opponent]};
            var rejected=planner.PlanAsync(game,snapshot,own.PlayerId,default,Default,checkPlayMode:Mode).GetAwaiter().GetResult();
            Check(rejected!=null&&rejected.Play==null,"invalid hand/mode/cost tuple survived parsing");
            var alternatePlayer=player with {Hand=[card with {CardId=7,CurrentAttack=999,EffectDescriptions=["OLD_BUFF_RULE"],Counters=new(){["old"]=999}}]};
            var alternateSnapshot=snapshot with {Players=[alternatePlayer,opponent]};
            var alternateGame=new GameState("alternate-binding",own,enemy,own.PlayerId);
            Task<PlayabilityResultDto> Alternate(Guid uid,CancellationToken token)=>Task.FromResult(new PlayabilityResultDto {
                IsPlayable=true,SelectedPlayMode=PlayMode.Accelerate,PlayedCardDefinitionId=8,
                PlayModes=[new(){Mode=PlayMode.Accelerate,EffectiveCost=1,IsAvailable=true}]
            });
            planner.PlanAsync(alternateGame,alternateSnapshot,own.PlayerId,default,Alternate).GetAwaiter().GetResult();
            using(var input=JsonDocument.Parse(provider.LastPlanInput)) {
                var option=input.RootElement.GetProperty("legalPlays")[0];var actual=option.GetProperty("effectiveCard");
                Check(option.GetProperty("cardId").GetInt32()==7&&actual.GetProperty("id").GetInt32()==8
                    &&actual.GetProperty("type").GetString()=="Spell"&&actual.GetProperty("currentAttack").GetInt32()!=999
                    &&actual.GetProperty("effects").GetArrayLength()==0&&actual.GetProperty("counters").EnumerateObject().Count()==0,
                    "alternate form reused original body, identity or buffs after engine resets them");
            }
            provider.Invalid=false;provider.LineScenario="hand-loss";
            var validationRules=new[]{new AiStrategyPlanner.HandLossRule(0,[1],777)};
            foreach(var scenario in new[]{"hand-loss","reuse","budget"}) {
                provider.LineScenario=scenario;provider.RefuseRepair=false;
                var testingPlayer=player with {CurrentPP=scenario=="budget"?3:5};
                var testingSnapshot=snapshot with {Players=[testingPlayer,opponent]};
                var fresh=new GameState("line-"+scenario,own,enemy,own.PlayerId);
                var attempts=provider.PlanResponses;
                var repaired=planner.PlanAsync(fresh,testingSnapshot,own.PlayerId,default,Default,checkPlayMode:Mode,
                    handLossRules:scenario=="hand-loss"?validationRules:[]).GetAwaiter().GetResult();
                Check(repaired?.Play?.HandIndex==0&&repaired.LineConstraintsChecked&&provider.PlanResponses-attempts==2,
                    "invalid "+scenario+" route was not corrected exactly once");
                Check(provider.LastPlanInput.Contains("correction")&&!provider.LastPlanInput.Contains(own.PlayerId.ToString()),
                    "repair omitted structured reason or leaked player ID");
            }
            provider.LineScenario="hand-loss";provider.RefuseRepair=true;
            var refusedGame=new GameState("line-refused",own,enemy,own.PlayerId);var beforeRefusal=provider.PlanResponses;
            Check(planner.PlanAsync(refusedGame,snapshot,own.PlayerId,default,Default,checkPlayMode:Mode,handLossRules:validationRules)
                .GetAwaiter().GetResult()==null && provider.PlanResponses-beforeRefusal==2,"repeated invalid line was executed or retried indefinitely");
            var controlConfig=new ConfigurationBuilder().AddConfiguration(config).AddInMemoryCollection(new Dictionary<string,string?>{
                ["AiStrategy:ValidatePlaySequences"]="false"}).Build();
            using(var controlPlanner=new AiStrategyPlanner(provider,controlConfig,host,NullLogger<AiStrategyPlanner>.Instance)) {
                var controlGame=new GameState("line-control",own,enemy,own.PlayerId);
                controlPlanner.WarmDeckAsync(controlGame,own.PlayerId,"Neutral")?.GetAwaiter().GetResult();
                var attempts=provider.PlanResponses;
                var control=controlPlanner.PlanAsync(controlGame,snapshot,own.PlayerId,default,Default,checkPlayMode:Mode,handLossRules:validationRules).GetAwaiter().GetResult();
                Check(control?.Play?.HandIndex==1&&!control.LineConstraintsChecked&&provider.PlanResponses-attempts==1,
                    "control toggle changed base candidates or failed to disable only line validation");
            }
            using(var wire=JsonDocument.Parse(JsonSerializer.Serialize(plan,new JsonSerializerOptions(JsonSerializerDefaults.Web))))
                Check(!wire.RootElement.TryGetProperty("playBinding",out _),"private runtime binding was serialized");
        }
        finally {Directory.Delete(root,true);}
    }
    private static void Check(bool condition,string message) {if(!condition)throw new InvalidOperationException(message);}
    private sealed class Provider : HttpMessageHandler,IHttpClientFactory
    {
        internal string LastPlanInput="";
        internal bool Invalid;
        internal string? LineScenario;
        internal bool RefuseRepair;
        internal int PlanResponses;
        public HttpClient CreateClient(string name)=>new(this,false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            using var payload=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            var contents=payload.RootElement.GetProperty("messages").EnumerateArray().Where(x=>x.GetProperty("role").GetString()=="user")
                .Select(x=>x.GetProperty("content").GetString()!).ToArray();
            var plan=contents.FirstOrDefault(x=>x.Contains("\"legalPlays\""));
            if(plan!=null){LastPlanInput=plan;PlanResponses++;}
            var answer=plan==null?"""{"gamePlan":"test","cards":[]}""":
                Invalid?"""{"focus":"value","plan":"test","prioritize":[],"hold":[],"play":{"handIndex":9,"cardId":2,"mode":"Base","cost":999,"intent":"use"}}""":
                """{"focus":"value","plan":"test","prioritize":[],"hold":[],"play":{"handIndex":1,"cardId":2,"mode":"Base","cost":2,"intent":"use"}}""";
            if(plan!=null&&LineScenario!=null) {
                var correction=plan.Contains("\"correction\"")&&!RefuseRepair;
                AiStrategyPlanner.PlayAdvice Step(int index)=>new(index,2,"Base",2,"use");
                var line=correction?(LineScenario=="budget"?new[]{Step(0)}:new[]{Step(0),Step(1)}):
                    LineScenario=="hand-loss"?new[]{Step(1),Step(0)}:LineScenario=="reuse"?new[]{Step(0),Step(0)}:new[]{Step(0),Step(1)};
                answer=JsonSerializer.Serialize(new {focus="pressure",plan="two plays",prioritize=Array.Empty<int>(),hold=Array.Empty<int>(),play=line[0],plays=line},new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=answer}}}}))};
        }
    }
    private sealed class Host : IWebHostEnvironment
    {
        public string WebRootPath{get;set;}=".";
        public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();
        public string EnvironmentName{get;set;}="Development";
        public string ApplicationName{get;set;}="test";
        public string ContentRootPath{get;set;}=".";
        public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
    }
}
