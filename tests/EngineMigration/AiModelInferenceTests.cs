using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class AiModelInferenceTests
{
    private static readonly MethodInfo Estimate=typeof(AiLearningModel).GetMethod("EstimateAfterstate",BindingFlags.NonPublic|BindingFlags.Static)!;
    private static GameStateDto After(GameStateDto state,Guid actor,string kind,CardInstanceDto source,CardInstanceDto? target=null)
        =>(GameStateDto)Estimate.Invoke(null,[state,actor,kind,source,target])!;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private sealed record Case(string Name,string Kind="attack",string[]? SourceKeywords=null,string[]? TargetKeywords=null,bool Face=false,bool Spell=false);
    // Frozen pre-fix approximation, used only as an input-accuracy control.
    private static GameStateDto LegacyAfter(GameStateDto state,Guid actor,string kind,CardInstanceDto source,CardInstanceDto target)
    {
        var players=state.Players.Select(player=>player with{Hand=new(player.Hand),Field=new(player.Field)}).ToList();
        var own=players.First(player=>player.PlayerId==actor);var enemy=players.First(player=>player.PlayerId!=actor);
        if(kind=="play") {own.Hand.RemoveAll(card=>card.InstanceId==source.InstanceId);own.CurrentPP=Math.Max(0,own.CurrentPP-source.CurrentCost);if(source.Type!=CardType.Spell)own.Field.Add(source);}
        if(kind=="evolve") {own.Field=own.Field.Select(card=>card.InstanceId==source.InstanceId?card with{IsEvolved=true,CurrentAttack=card.CurrentAttack+2,CurrentHealth=card.CurrentHealth+2,MaxHealth=card.MaxHealth+2}:card).ToList();own.CurrentEP=Math.Max(0,own.CurrentEP-1);}
        if(kind=="attack") {
            if(target.Type==CardType.Leader&&enemy.LeaderCard!=null)enemy.LeaderCard=enemy.LeaderCard with{CurrentHealth=enemy.LeaderCard.CurrentHealth-source.CurrentAttack};
            else {enemy.Field=enemy.Field.Select(card=>card.InstanceId==target.InstanceId?card with{CurrentHealth=card.CurrentHealth-source.CurrentAttack}:card).Where(card=>card.CurrentHealth>0).ToList();own.Field=own.Field.Select(card=>card.InstanceId==source.InstanceId?card with{CurrentHealth=card.CurrentHealth-target.CurrentAttack}:card).Where(card=>card.CurrentHealth>0).ToList();}
        }
        return state with{Players=players};
    }
    internal static void NativeMechanics()
    {
        var original=GameDataProvider.Instance;var oldAccessor=GameServiceProvider.ContextAccessor;var rows=new List<object>();
        AiLearningModel? trained=null;
        if(Environment.GetEnvironmentVariable("AI_MODEL_INFERENCE_PATH") is {Length:>0} modelPath) {
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"AiModel:Path",modelPath}}).Build();
            trained=new AiLearningModel(Proxy<IWebHostEnvironment>.Make((method,args)=>null),config,NullLogger<AiLearningModel>.Instance);
            Check(trained.Current!=null,"local inference model failed to load");
        }
        try {
            foreach(var row in new[]{new Case("normal-trade"),new("shielded-target",TargetKeywords:["Shield"]),
                new("shielded-source",SourceKeywords:["Shield"]),new("drain-face",SourceKeywords:["Drain"],Face:true),
                new("bane-destroy",SourceKeywords:["Bane"]),new("bane-effect-immune",SourceKeywords:["Bane"],TargetKeywords:["EffectImmune"]),
                new("free-evolution","evolve",["FreeEvolve"]),new("boost-follower","play"),new("boost-spell","play",Spell:true)}) {
                var cards=new Dictionary<int,CardDefinition>{
                    [1]=new(){Id=1,Name="Inference leader",Type=CardType.Leader,Health=20},
                    [2]=new(){Id=2,Name="Inference source",Type=row.Spell?CardType.Spell:CardType.Follower,Cost=3,Attack=5,Health=4,Keywords=row.SourceKeywords?.ToList()??[]},
                    [3]=new(){Id=3,Name="Inference target",Type=CardType.Follower,Attack=3,Health=6,Keywords=row.TargetKeywords?.ToList()??[]}};
                var data=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch {
                    "GetCardDefinition"=>cards.GetValueOrDefault((int)args![0]!),"GetAllCardDefinitions"=>cards,
                    "GetAbilityConfigs"=>new List<AbilityConfig>(),_=>null});
                GameDataProvider.SetInstanceForTesting(data);var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
                using var services=AiEndTurnEngineTests.Services(data,accessor);using var scope=services.CreateScope();var sp=scope.ServiceProvider;
                var own=new PlayerState(Guid.NewGuid()){TurnNumber=6,MaxPlayPoints=5,CurrentPlayPoints=5,IsEvolutionUnlocked=true,IsSuperEvolutionUnlocked=true,
                    MaxEvolutionPoints=1,CurrentEvolutionPoints=1,MaxSuperEvolutionPoints=1,CurrentSuperEvolutionPoints=1};
                var enemy=new PlayerState(Guid.NewGuid()){TurnNumber=6};
                var game=new GameState("inference-"+row.Name,own,enemy,own.PlayerId){Status=GameStatus.InProgress,Phase=GamePhase.Main,
                    AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<IBuffManager>()};
                var factory=sp.GetRequiredService<ICardFactory>();
                own.Leader=factory.CreateAndRegisterNewCard(game,1,own.PlayerId,CardZone.Field);own.Leader.Damage=10;
                enemy.Leader=factory.CreateAndRegisterNewCard(game,1,enemy.PlayerId,CardZone.Field);
                var source=factory.CreateAndRegisterNewCard(game,2,own.PlayerId,row.Kind=="play"?CardZone.Hand:CardZone.Field);source.ZoneStayTurn=1;
                var target=row.Face?enemy.Leader:factory.CreateAndRegisterNewCard(game,3,enemy.PlayerId,CardZone.Field);
                if(row.Kind=="play"){own.CurrentPlayPoints=2;own.MaxPlayPoints=2;own.IsPpBoostArmed=true;own.PpBoostUsesAvailable=1;}
                using var context=new GameContext(sp,sp.GetRequiredService<AutoMapper.IMapper>(),accessor,game,own.PlayerId);
                var before=GameStateProjection.Create(game,context);var originalJson=JsonSerializer.Serialize(before);
                var sourceView=CardInstanceProjection.Create(source,context);var targetView=CardInstanceProjection.Create(target,context);
                var estimate=After(before,own.PlayerId,row.Kind,sourceView,targetView);
                Check(JsonSerializer.Serialize(before)==originalJson,"learned afterstate mutated its live input: "+row.Name);
                IGameEvent action=row.Kind switch {
                    "play"=>new PlayCardRequestedEvent(own.PlayerId,new PlayCardActionDto{CardUid=source.InstanceId}),
                    "evolve"=>new EvolveRequestedEvent(own.PlayerId,new EvolveActionDto{UnitUid=source.InstanceId.ToString()}),
                    _=>new AttackRequestedEvent(own.PlayerId,new AttackActionDto{AttackerUid=source.InstanceId,TargetUid=target.InstanceId})};
                var result=sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(context,[action]).GetAwaiter().GetResult();
                Check(result.IsSuccess&&!result.IsCancelled,"native mechanical action failed: "+row.Name+"; "+result.ErrorMessage);
                var actual=GameStateProjection.Create(game,context);
                var predicted=AiFeatures.Value(estimate,own.PlayerId);var observed=AiFeatures.Value(actual,own.PlayerId);
                var legacy=AiFeatures.Value(LegacyAfter(before,own.PlayerId,row.Kind,sourceView,targetView),own.PlayerId);
                var legacyDifferences=Enumerable.Range(0,legacy.Length).Count(index=>Math.Abs(legacy[index]-observed[index])>1e-10);
                var different=Enumerable.Range(0,predicted.Length).Where(index=>Math.Abs(predicted[index]-observed[index])>1e-10).ToArray();
                var actualValue=trained?.Value(actual,own.PlayerId);var predictedValue=trained?.Value(estimate,own.PlayerId);
                var legacyValue=trained?.Value(LegacyAfter(before,own.PlayerId,row.Kind,sourceView,targetView),own.PlayerId);
                var currentValueError=actualValue.HasValue?Math.Abs(actualValue.Value-predictedValue!.Value):(double?)null;
                var legacyValueError=actualValue.HasValue?Math.Abs(actualValue.Value-legacyValue!.Value):(double?)null;
                Check(currentValueError is null or <1e-12,"retained value weights did not receive the engine-matching features");
                rows.Add(new {scenario=row.Name,matching=different.Length==0,legacyDifferentFeatures=legacyDifferences,currentValueError,legacyValueError,differences=different.Select(index=>new {index,predicted=predicted[index],actual=observed[index]}),
                    beforeEp=before.Players[0].CurrentEP,afterEp=actual.Players[0].CurrentEP,afterSep=actual.Players[0].CurrentSEP,
                    attacks=actual.Players[0].Field.FirstOrDefault(card=>card.InstanceId==source.InstanceId)?.AttacksThisTurn});
            }
            var report=JsonSerializer.SerializeToElement(rows);
            if(Environment.GetEnvironmentVariable("AI_MODEL_AFTERSTATE_OUTPUT") is {Length:>0} output)
                File.WriteAllText(output,JsonSerializer.Serialize(new{externalProviderCalls=0,trainedModelVersion=trained?.Current?.Version,scope="Basic configurations without triggered abilities; compare all 512 value features against production transactions, not full-match policy strength.",cases=rows},new JsonSerializerOptions{WriteIndented=true}));
            Check(report.EnumerateArray().All(row=>row.GetProperty("matching").GetBoolean()),"learned afterstate differs from native engine: "+report.GetRawText());
        } finally {GameDataProvider.SetInstanceForTesting(original);GameServiceProvider.ContextAccessor=oldAccessor;}
    }
    internal static void ShieldAndModelInputs()
    {
        var own=Guid.NewGuid();var enemy=Guid.NewGuid();
        var source=new CardInstanceDto{InstanceId=Guid.NewGuid(),OwnerPlayerId=own,CardId=2,Type=CardType.Follower,Zone=CardZone.Field,
            CurrentAttack=5,CurrentHealth=4,MaxHealth=4,CanAttack=true,MaxAttacksPerTurn=1,Keywords=[CardKeyword.Shield]};
        var target=new CardInstanceDto{InstanceId=Guid.NewGuid(),OwnerPlayerId=enemy,Type=CardType.Leader,CurrentHealth=20,MaxHealth=20,Keywords=[CardKeyword.Shield]};
        var state=new GameStateDto{CurrentPlayerId=own,Players=[new(){PlayerId=own,Field=[source],LeaderCard=new(){CurrentHealth=20,MaxHealth=20}},new(){PlayerId=enemy,LeaderCard=target}]};
        foreach(var version in new[]{2,3})Check(AiFeatures.Value(state,own,version)[27]>.19,"native shield missing from imported feature slot");
        Check(AiFeatures.Value(state,own)[181]==1&&AiFeatures.Action(state,own,"attack",source,target)[35]>.19,"native shield absent from per-card/action state inputs");
        var net=new AiNetwork{Format=1,FeatureVersion=3,SnapshotId="offline-fixture",W1=new double[512*128],B1=new double[128],W2=new double[128]};
        net.W1[29]=1;net.W2[0]=1; // Own unused-attack total: detect a falsely retained attack opportunity.
        var artifact=new AiModelArtifact{Version="offline-fixture",Weights=new(){ValueModel="value"},Models=new(){{"value",net}}};
        var root=Path.Combine(Path.GetTempPath(),"ai-inference-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var path=Path.Combine(root,"model.json");
        try {
            File.WriteAllText(path,JsonSerializer.Serialize(artifact,AiLearningModel.Json));
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"AiModel:Path",path}}).Build();
            var host=Proxy<IWebHostEnvironment>.Make((method,args)=>null);
            var model=new AiLearningModel(host,config,NullLogger<AiLearningModel>.Instance);
            var after=After(state,own,"attack",source,target);
            Check(after.Players[0].Field[0].AttacksThisTurn==1&&after.Players[1].LeaderCard!.CurrentHealth==20,"afterstate retained the attack or ignored a shield");
            Check(Math.Abs(model.Score(state,own,"attack",source,target)-8*model.Value(after,own))<1e-10,"live learned scorer did not use the corrected afterstate");
            Check(model.Value(after,own)<model.Value(state,own),"value fixture still saw the attack as unspent");
            var first=new PlayerState(own);var second=new PlayerState(enemy);var game=new GameState("input-ab",first,second,own);
            typeof(GameState).GetProperty("LegacyModelInputsPlayerId",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(game,own);
            var select=typeof(AiLearningModel).GetMethod("Weights",BindingFlags.NonPublic|BindingFlags.Instance,null,[typeof(GameState),typeof(Guid)],null)!;
            var flag=typeof(AiWeights).GetProperty("LegacyInputs",BindingFlags.NonPublic|BindingFlags.Instance)!;
            var normal=(AiWeights)select.Invoke(model,[game,own])!;
            Check(!(bool)flag.GetValue(normal)!,"legacy control leaked outside local benchmark games");
            game.IsLocalAiTraining=true;
            var control=(AiWeights)select.Invoke(model,[game,own])!;var candidate=(AiWeights)select.Invoke(model,[game,enemy])!;
            Check((bool)flag.GetValue(control)!&&!(bool)flag.GetValue(candidate)!&&!(bool)flag.GetValue(model.Current!.Weights)!,"benchmark seats mutated/shared the stored model profile");
            Check(model.Score(state,own,"attack",source,target,control)>model.Score(state,own,"attack",source,target,normal),"frozen control did not reproduce the old retained attack estimate");
            Check(AiFeatures.Value(state,own,3,true)[27]==0&&AiFeatures.Value(state,own)[27]>.19,"legacy/current shield inputs were not isolated");
            Check(!JsonSerializer.Serialize(control,AiLearningModel.Json).Contains("legacy",StringComparison.OrdinalIgnoreCase),"test-only profile selector entered portable model data");
        } finally {File.Delete(path);Directory.Delete(root);}
    }
}
