using System.Net;
using Microsoft.AspNetCore.Hosting;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using System.Reflection;
using System.Text.Json;
using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class AiDrawContinuationTests
{
    private const int Leader=2000000600,Source=2000000601,Body=2000000602,Junk=2000000603;
    private static void Check(bool ok,string error){if(!ok)throw new InvalidOperationException(error);}
    private static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    public static void Native(string output,string? modelPath=null)
    {
        var prior=GameDataProvider.Instance;var accessorBefore=GameServiceProvider.ContextAccessor;
        try
        {
            var scenarios=new[]{"strict-control","draw-one","draw-two","fresh-blocked","own-field-change","enemy-draw","draw-lethal"};
            var cases=scenarios.Select(scenario=>Run(scenario)).ToList();
            if(modelPath!=null)
            {
                var settings=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["AiModel:Path"]=Path.GetFullPath(modelPath)}).Build();
                var frozen=new AiLearningModel(new Host(),settings,NullLogger<AiLearningModel>.Instance);
                Check(frozen.Current!=null,"requested frozen model did not load");
                cases.AddRange(scenarios.Select(scenario=>Run(scenario,frozen)));
            }
            File.WriteAllText(output,JsonSerializer.Serialize(new{realProviderCalls=0,syntheticLeadersAndDefinitions=true,
                realNativeTransactions=true,playabilityAdapter=true,scriptedModelPlans=true,playingStrengthClaim=false,cases},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine($"PASS native draw continuation: {cases.Count} complete AI turns; one scripted plan each; real provider calls=0");
        }
        finally {GameDataProvider.SetInstanceForTesting(prior);GameServiceProvider.ContextAccessor=accessorBefore;}
    }
    public static void NativeTactics(string output,string? modelPath=null)
    {
        var prior=GameDataProvider.Instance;var accessorBefore=GameServiceProvider.ContextAccessor;
        try
        {
            var scenarios=new[]{"tactic-win","tactic-control","tactic-evolve","tactic-super","tactic-locked","tactic-response","tactic-private"};
            var cases=scenarios.Select(scenario=>Run(scenario)).ToList();
            if(modelPath!=null)
            {
                var settings=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["AiModel:Path"]=Path.GetFullPath(modelPath)}).Build();
                var frozen=new AiLearningModel(new Host(),settings,NullLogger<AiLearningModel>.Instance);
                Check(frozen.Current!=null,"requested frozen model did not load");
                cases.AddRange(scenarios.Select(scenario=>Run(scenario,frozen)));
            }
            var localOnlyHistory=LocalOnlyHistory();
            File.WriteAllText(output,JsonSerializer.Serialize(new{realProviderCalls=0,syntheticLeadersAndDefinitions=true,
                realNativeTransactions=true,scriptedModelPlans=true,playingStrengthClaim=false,localOnlyHistory,cases},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine($"PASS native local tactics: {cases.Count} complete turns; real provider calls=0");
        }
        finally {GameDataProvider.SetInstanceForTesting(prior);GameServiceProvider.ContextAccessor=accessorBefore;}
    }
    public static void NativeFirstActions(string output,string? modelPath=null)
    {
        var prior=GameDataProvider.Instance;var accessorBefore=GameServiceProvider.ContextAccessor;
        try {
            var scenarios=new[]{"tactic-model-evolve","tactic-model-control","tactic-model-mechanics","tactic-model-second","tactic-model-invalid","tactic-model-protect",
                "tactic-model-end","tactic-model-conflict","tactic-model-evo-locked","tactic-model-defend","tactic-model-budget","tactic-model-target","tactic-model-stale","tactic-model-missing"};
            var cases=scenarios.Select(scenario=>Run(scenario)).ToList();
            if(modelPath!=null) {
                var settings=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["AiModel:Path"]=Path.GetFullPath(modelPath)}).Build();
                var frozen=new AiLearningModel(new Host(),settings,NullLogger<AiLearningModel>.Instance);
                Check(frozen.Current!=null,"requested frozen model did not load");cases.AddRange(scenarios.Select(scenario=>Run(scenario,frozen)));
            }
            File.WriteAllText(output,JsonSerializer.Serialize(new{realProviderCalls=0,scriptedProvider=true,realNativeTransactions=true,
                playabilityAdapter=true,syntheticCardsAndLeaders=true,playingStrengthClaim=false,cases},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine($"PASS native first-action planning: {cases.Count} complete turns; real provider calls=0");
        } finally {GameDataProvider.SetInstanceForTesting(prior);GameServiceProvider.ContextAccessor=accessorBefore;}
    }
    private static object Run(string scenario,AiLearningModel? frozen=null,string? sharedDirectory=null)
    {
        var tactical=scenario.StartsWith("tactic-",StringComparison.Ordinal);var firstAction=scenario.StartsWith("tactic-model-",StringComparison.Ordinal);
        var directory=sharedDirectory??Path.Combine(Path.GetTempPath(),"ai-draw-line-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var defs=new Dictionary<int,CardDefinition>{
            [Leader]=new(){Id=Leader,Name="fixture leader",Type=CardType.Leader,Health=20},
            [Source]=new(){Id=Source,Name="fixture draw",Type=CardType.Spell,Cost=1,Description="抽牌后按原路线使用手牌。"},
            [Body]=new(){Id=Body,Name="fixture follower",Type=CardType.Follower,Cost=2,Attack=2,Health=3},
            [Junk]=new(){Id=Junk,Name=scenario=="tactic-private"?"PRIVATE_ENEMY_HAND_CANARY":"fixture draw",Type=scenario=="draw-lethal"?CardType.Follower:CardType.Spell,Cost=scenario=="draw-lethal"?0:9,Attack=1,Health=1,Keywords=scenario=="draw-lethal"?["Storm"]:[]}
        };
        var effects=new List<EffectConfig>{new(){Type=EffectType.Draw,Parameters=new(){["DrawAmount"]=new StaticValueConfig{Value=scenario=="draw-two"?2:1}}}};
        if(scenario=="own-field-change")effects.Add(new(){Type=EffectType.Summon,ExecutionOrder=1,Parameters=new(){["CardDefinitionId"]=new StaticValueConfig{Value=new[]{Body}},["SummonCount"]=new StaticValueConfig{Value=1}}});
        if(scenario=="enemy-draw")effects.Add(new(){Type=EffectType.Draw,ExecutionOrder=1,Parameters=new(){["DrawAmount"]=new StaticValueConfig{Value=1},["PlayerScope"]=new StaticValueConfig{Value=PlayerScope.Opponent}}});
        var config=new AbilityConfig{Id=200000060101,CardId=Source,Name="fixture draw on play",
            Trigger=new(){EventTypes=[EventType.PlayCard],RelationType=TriggerRelationType.TargetSelf,ValidZones=[],Timing=TriggerTiming.On},Effects=effects};
        var allConfigs=defs.Keys.ToDictionary(id=>id,id=>id==Source?new List<AbilityConfig>{config}:new List<AbilityConfig>());
        var data=Proxy<IGameDataProvider>.Make((m,a)=>m.Name switch {
            "get_DataRevision"=>Guid.Empty,"GetCardDefinition"=>defs.GetValueOrDefault((int)a![0]!),"GetAllCardDefinitions"=>defs,
            "GetAbilityConfigs"=>allConfigs.GetValueOrDefault((int)a![0]!)??[],"GetAllAbilityConfigs"=>allConfigs,
            "GetAllBuffDefinitions"=>new Dictionary<string,BuffDefinition>(),_=>null});
        GameDataProvider.SetInstanceForTesting(data);var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
        using var provider=new ScriptedProvider{Scenario=scenario};
        var settings=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["AiStrategy:Enabled"]="true",["AiStrategy:Model"]="fixture",["AiStrategy:Endpoint"]="https://example.invalid/chat/completions",
            ["AiStrategy:MemoryPath"]=directory,["AiStrategy:RevalidateDrawContinuations"]=scenario=="strict-control"?"false":"true",
            ["AiStrategy:SkipPlainTacticalFinishes"]=scenario=="tactic-control"||firstAction?"false":"true",
            ["AiStrategy:ProjectCombatEffects"]=scenario=="tactic-model-control"?"false":"true",
            ["AiModel:Path"]=Path.Combine(directory,"missing-model.json")}).Build();
        using var planner=new AiStrategyPlanner(provider,settings,new Host(),NullLogger<AiStrategyPlanner>.Instance,frozen);
        var model=frozen??new AiLearningModel(new Host(),settings,NullLogger<AiLearningModel>.Instance);
        GameState game=null!;ServiceProvider services=null!;Guid desiredSecond=Guid.Empty;
        var submitted=new List<(string Kind,Guid? Card)>();var attackers=new List<Guid>();var targets=new List<Guid>();var nativeDraws=0;var firstDone=false;
        async Task<bool> Enqueue(PlayerActionWrapper action)
        {
            var ev=action.ActionData switch {PlayCardActionDto play=>(IGameEvent)new PlayCardRequestedEvent(action.PlayerId,play),
                AttackActionDto attack=>new AttackRequestedEvent(action.PlayerId,attack),
                EvolveActionDto evolve=>new EvolveRequestedEvent(action.PlayerId,evolve),
                SuperEvolveActionDto super=>new SuperEvolveRequestedEvent(action.PlayerId,super),_=>new TurnEndEvent(game.Player1)};
            submitted.Add((action.ActionType,(action.ActionData as PlayCardActionDto)?.CardUid));
            if(action.ActionData is AttackActionDto hit){attackers.Add(hit.AttackerUid);targets.Add(hit.TargetUid);}
            using var sc=services.CreateScope();var sp=sc.ServiceProvider;
            using var cx=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,action.PlayerId);
            var result=await sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(cx,[ev]);
            Check(result.IsSuccess&&!result.IsCancelled,result.ErrorMessage??"native action failed");
            nativeDraws+=result.ProcessedEvents.OfType<DrawEvent>().Where(draw=>draw.DrawingPlayer?.PlayerId==game.Player1.PlayerId).Sum(draw=>draw.DrawnCards.Count);
            // The fixture replaces persistence/statistics; feed the same actual committed event list to the observer.
            planner.ObserveCommittedEvents(game,result.ProcessedEvents);
            if(action.ActionData is PlayCardActionDto played&&played.CardUid==game.Player1.Graveyard.FirstOrDefault(card=>card.BaseDefinitionId==Source)?.InstanceId)firstDone=true;
            action.MarkCompleted();return true;
        }
        Task<PlayabilityResultDto> Playable(Guid uid)
        {
            var card=game.FindCardInstance(uid)!;var blocked=firstDone&&scenario=="fresh-blocked"&&uid==desiredSecond;
            return Task.FromResult(new PlayabilityResultDto{IsPlayable=!blocked&&card.CurrentZone==CardZone.Hand&&card.CurrentCost<=game.Player1.CurrentPlayPoints,
                SelectedPlayMode=PlayMode.Base,PlayModes=[new(){Mode=PlayMode.Base,EffectiveCost=card.CurrentCost,IsAvailable=!blocked}]});
        }
        var games=Proxy<IGameService>.Make((m,a)=>m.Name switch{
            "GetGameState"=>game,"GetCardPlayabilityAsync"=>Playable((Guid)a![2]!),"GetCardPlayabilityForModeAsync"=>Playable((Guid)a![2]!),
            "EnqueuePlayerActionAsync"=>Enqueue((PlayerActionWrapper)a![1]!),_=>null});
        try
        {
            using(services=AiEndTurnEngineTests.Services(data,accessor,games:games,configure:items=>items.AddSingleton(planner)))
            {
                using var scope=services.CreateScope();var sp=scope.ServiceProvider;
                var own=new PlayerState(Guid.NewGuid()){IsAI=true,TurnNumber=4,CurrentPlayPoints=5,MaxPlayPoints=5,InitialDeckDefinitionIds=[Source,Body,Body]};
                var enemy=new PlayerState(Guid.NewGuid());game=new("draw-line-"+scenario,own,enemy,own.PlayerId){IsLocalAiTraining=true,
                    Phase=GamePhase.Main,Status=GameStatus.InProgress,AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<IBuffManager>()};
                typeof(GameState).GetProperty("StrategyBenchmarkPlayerId",Hidden)!.SetValue(game,own.PlayerId);
                var factory=sp.GetRequiredService<ICardFactory>();
                own.Leader=factory.CreateAndRegisterNewCard(game,Leader,own.PlayerId,CardZone.Field);
                enemy.Leader=factory.CreateAndRegisterNewCard(game,Leader,enemy.PlayerId,CardZone.Field);
                if(scenario=="draw-lethal")enemy.Leader.Damage=19;
                CardInstance? draw=null,first=null,second=null;
                if(!tactical) {
                    draw=factory.CreateAndRegisterNewCard(game,Source,own.PlayerId,CardZone.Hand);
                    first=factory.CreateAndRegisterNewCard(game,Body,own.PlayerId,CardZone.Hand);
                    second=factory.CreateAndRegisterNewCard(game,Body,own.PlayerId,CardZone.Hand);desiredSecond=second.InstanceId;
                } else {
                    var ready=factory.CreateAndRegisterNewCard(game,Body,own.PlayerId,CardZone.Field);ready.ZoneStayTurn=1;
                    var hp=scenario is "tactic-evolve" or "tactic-locked" or "tactic-model-evo-locked"?4:scenario=="tactic-super"?5:scenario is "tactic-model-second" or "tactic-model-end" or "tactic-model-conflict" or "tactic-model-defend" or "tactic-model-target" or "tactic-model-stale"?10:scenario=="tactic-model-budget"?20:1;
                    enemy.Leader.Damage=20-hp;
                    own.IsEvolutionUnlocked=scenario is "tactic-evolve" or "tactic-model-evolve" or "tactic-model-control" or "tactic-model-mechanics" or "tactic-model-defend";own.IsSuperEvolutionUnlocked=scenario=="tactic-super";
                    own.MaxEvolutionPoints=1;own.CurrentEvolutionPoints=scenario is "tactic-evolve" or "tactic-locked" or "tactic-model-evolve" or "tactic-model-control" or "tactic-model-mechanics" or "tactic-model-defend" or "tactic-model-evo-locked"?1:0;
                    own.MaxSuperEvolutionPoints=1;own.CurrentSuperEvolutionPoints=scenario=="tactic-super"?1:0;
                    if(scenario is "tactic-response" or "tactic-private" or "tactic-model-evolve" or "tactic-model-control" or "tactic-model-mechanics") {
                        var response=new AbilityConfig{Id=200000060201,Name="public attack response",CardId=Body,
                            Trigger=new(){EventTypes=[EventType.Attack],Timing=TriggerTiming.Before,ValidZones=[CardZone.Emblem]},
                            Targets=new(){["leader"]=new(){Query=new(){SourcePoolExpression="context.owner.leader"}}},
                            Effects=[new(){Type=EffectType.Heal,Parameters=new(){["HealAmount"]=new StaticValueConfig{Value=2}}}]};
                        var emblem=factory.CreateAndRegisterNewCard(game,Body,enemy.PlayerId,CardZone.Emblem);
                        game.AbilityManager.CreateAbilitiesForCard(emblem,[response],null,game);
                    }
                    if(scenario=="tactic-model-second")factory.CreateAndRegisterNewCard(game,Body,own.PlayerId,CardZone.Field).ZoneStayTurn=1;
                    if(scenario=="tactic-model-conflict")factory.CreateAndRegisterNewCard(game,Body,own.PlayerId,CardZone.Hand);
                    if(scenario=="tactic-model-defend") {own.Leader.Damage=18;factory.CreateAndRegisterNewCard(game,Body,enemy.PlayerId,CardZone.Field).ZoneStayTurn=1;}
                    if(scenario=="tactic-model-target")for(var i=0;i<2;i++){var body=factory.CreateAndRegisterNewCard(game,Body,enemy.PlayerId,CardZone.Field);body.ZoneStayTurn=1;body.Damage=2;}
                    if(scenario=="tactic-model-budget")for(var i=0;i<4;i++)factory.CreateAndRegisterNewCard(game,Body,own.PlayerId,CardZone.Field).ZoneStayTurn=1;
                    if(scenario=="tactic-model-budget")for(var i=0;i<5;i++)factory.CreateAndRegisterNewCard(game,Body,enemy.PlayerId,CardZone.Field).ZoneStayTurn=1;
                    if(scenario=="tactic-private")factory.CreateAndRegisterNewCard(game,Junk,enemy.PlayerId,CardZone.Hand);
                    using var factsContext=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId);
                    CheckTacticalFrame(factsContext,game,own.PlayerId);
                }
                for(var i=0;i<3;i++){factory.CreateAndRegisterNewCard(game,Junk,own.PlayerId,CardZone.Deck);factory.CreateAndRegisterNewCard(game,Junk,enemy.PlayerId,CardZone.Deck);}
                var initialField=own.Field.Select(card=>card.InstanceId).ToArray();var initialTargets=enemy.Field.Select(card=>card.InstanceId).ToArray();
                if(scenario=="tactic-model-stale")provider.AfterPlan=()=>{
                    own.Field[0].AttacksThisTurn=1;
                    typeof(GameState).GetMethod("IncrementStateRevision",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(game,null);
                };
                var ai=new AiService(NullLogger<AiService>.Instance,services.GetRequiredService<IServiceScopeFactory>(),model);
                var executor=new AiTurnExecutor(games,ai,NullLogger<AiTurnExecutor>.Instance,sp.GetRequiredService<IMapper>(),services,model,settings);
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25));
                executor.ExecuteTurnAsync(game.GameId,own.PlayerId,timeout.Token).GetAwaiter().GetResult();
                planner.ObserveCommittedEvents(game,[game.WinnerId==own.PlayerId||!tactical
                    ?new GameEndEvent(own.PlayerId,enemy.PlayerId,"fixture complete"):new GameEndEvent(enemy.PlayerId,own.PlayerId,"fixture complete")]);
                var history=(IReadOnlyList<AiStrategyPlanner.HistoryEntry>)typeof(AiStrategyPlanner).GetMethod("ReadHistory",Hidden)!.Invoke(planner,null)!;
                var lesson=sharedDirectory==null?history.Single().Lesson:history.OrderByDescending(item=>item.Lesson.FinishedAt).First().Lesson;
                if(tactical) {
                    var bypass=lesson.LocalTacticalBypasses??[];
                    var expectedSkip=scenario is "tactic-win" or "tactic-evolve" or "tactic-super";
                    Check(expectedSkip?provider.Requests==0&&bypass.Length==1&&lesson.Plans.Length==0
                        :provider.Plans==1&&bypass.Length==0,"plain-lethal bypass or response planning request count is wrong");
                    if(firstAction) {
                        var sequence=provider.PlanInputs.Single().GetProperty("nativeCombatSequence").GetString()!;
                        Check(sequence.Contains("CombatResolution")&&sequence.Contains("Before/On/After")&&sequence.Contains("离场")&&sequence.Contains("旧攻击力"),
                            "current native combat ordering was not sent with the exact action candidates");
                        var proposals=lesson.ProposedTactics??[];var selected=proposals.Where(item=>item.Status=="selected-before-submit").ToArray();
                        var blocked=proposals.Where(item=>item.Status=="blocked-before-submit").ToArray();
                        var rejected=proposals.Where(item=>item.Status=="rejected").ToArray();
                        if(scenario=="tactic-model-evolve")Check(game.WinnerId==own.PlayerId&&submitted[0].Kind=="PerformEvolve"&&selected.Length==1,
                            "model-selected evolution did not correct the optimistic basic finish under an actual public heal response");
                        if(scenario=="tactic-model-control")Check(game.WinnerId!=own.PlayerId&&submitted[0].Kind=="PerformAttack"&&proposals.Length==0,
                            "projection-disabled no-advice control no longer exercises the optimistic basic attack");
                        if(scenario=="tactic-model-mechanics")Check(game.WinnerId==own.PlayerId&&submitted[0].Kind=="PerformEvolve"&&proposals.Length==0,
                            "same-state projection-enabled local policy failed to evolve before attacking into the public heal");
                        if(scenario=="tactic-model-second")Check(attackers[0]==initialField[1]&&selected.Single().Choice!.Value.GetProperty("source").GetProperty("index").GetInt32()==1
                            &&lesson.ActionComparisons![0].Changed&&lesson.ActionComparisons[0].Selected.FieldIndex==1,
                            "same-name attacker selection was rebound to the wrong instance");
                        if(scenario is "tactic-model-invalid" or "tactic-model-missing")Check(game.WinnerId==own.PlayerId&&rejected.Single().Reason=="candidate-not-in-current-frame"&&selected.Length==0,
                            "invalid choice did not fall back without retry");
                        if(scenario=="tactic-model-protect")Check(game.WinnerId==own.PlayerId&&blocked.Single().Reason=="known-current-finish-has-priority"&&submitted[0].Kind=="PerformAttack",
                            "model end-turn suggestion overrode a checked plain finish");
                        if(scenario is "tactic-model-end" or "tactic-model-budget")Check(submitted.Count==1&&submitted[0].Kind=="EndTurn"&&selected.Length==1,
                            "legal explicit end-turn suggestion did not execute");
                        if(scenario=="tactic-model-conflict")Check(rejected.Single().Reason=="conflicting-first-actions"&&selected.Length==0,
                            "conflicting explicit first actions were executed");
                        if(scenario=="tactic-model-evo-locked")Check(!submitted.Any(item=>item.Kind.Contains("Evolve"))&&rejected.Length==1
                            &&!provider.PlanInputs.Single().GetProperty("tacticalChoices").GetProperty("options").EnumerateArray().Any(item=>item.GetProperty("kind").GetString()!.Contains("Evolve")),
                            "native-locked evolution was offered or submitted");
                        if(scenario=="tactic-model-defend")Check(blocked.Single().Reason=="visible-lethal-defense-has-priority"&&submitted[0].Kind!="EndTurn"&&enemy.Field.Count==0,
                            "model end-turn suggestion bypassed a necessary known defense");
                        if(scenario=="tactic-model-target")Check(targets[0]==initialTargets[1]&&enemy.Field.All(card=>card.InstanceId!=initialTargets[1])&&enemy.Field.Any(card=>card.InstanceId==initialTargets[0])
                            &&lesson.ActionComparisons![0].Changed&&lesson.ActionComparisons[0].Selected.TargetFieldIndex==1,
                            "same-name enemy targets were conflated");
                        if(scenario=="tactic-model-stale")Check(attackers.Count==0&&submitted[0].Kind=="EndTurn"&&blocked.Single().Reason=="position-or-turn-changed",
                            "changed-while-planning attack was submitted or silently rebound");
                        if(scenario=="tactic-model-budget")Check(provider.PlanInputs.Single().GetProperty("tacticalChoices").GetProperty("omitted").GetInt32()>0,
                            "candidate budget fixture did not reach the real payload bound");
                        if(scenario is "tactic-model-evolve" or "tactic-model-control" or "tactic-model-mechanics") {
                            var emblem=provider.PlanInputs.Single().GetProperty("currentRuntimeRules").GetProperty("instances").EnumerateArray()
                                .Single(item=>item.GetProperty("instance").GetProperty("side").GetString()=="enemy"&&item.GetProperty("instance").GetProperty("zone").GetString()=="emblem");
                            var response=emblem.GetProperty("changes").EnumerateArray().Single(item=>item.GetProperty("config").GetProperty("name").GetString()=="public attack response").GetProperty("config");
                            Check(response.GetProperty("trigger").GetProperty("timing").GetString()=="Before"
                                &&response.GetProperty("trigger").GetProperty("events").EnumerateArray().Any(item=>item.GetString()=="Attack")
                                &&response.GetProperty("effects").EnumerateArray().Any(item=>item.GetProperty("type").GetString()=="Heal"
                                    &&item.GetProperty("params").GetProperty("HealAmount").GetProperty("value").GetInt32()==2),
                                "model action choices were sent without the real public before-attack heal rule");
                            if(scenario!="tactic-model-control")Check(provider.PlanInputs.Single().GetProperty("tacticalChoices").GetProperty("options").EnumerateArray()
                                .Any(item=>item.TryGetProperty("knownCombatProjection",out var known)&&known.GetProperty("enemyHealth").GetInt32()==1),
                                "compiled public precombat heal outcome was absent from model action candidates");
                        }
                        var allInputs=string.Join("\n",provider.Inputs);
                        Check(!allInputs.Contains(own.PlayerId.ToString())&&!allInputs.Contains(enemy.PlayerId.ToString())
                            &&!initialField.Concat(own.Field.Select(card=>card.InstanceId)).Concat(enemy.Field.Select(card=>card.InstanceId)).Any(id=>allInputs.Contains(id.ToString())),
                            "native tactical bindings leaked runtime identifiers");
                        Check(lesson.TurnDecisions?.Attempts==1&&provider.Plans==1,"first-action advice caused extra planning/repair requests");
                        return new{scenario,trained=frozen!=null,modelVersion=frozen?.Current?.Version,nativeWon=game.WinnerId==own.PlayerId,
                            firstAction=submitted[0].Kind,enemyHp=enemy.Leader.CurrentHealth,modelRequests=provider.Plans,actualProviderCalls=0,
                            selected=selected.Length,blocked=blocked.Length,rejected=rejected.Length,proposals,
                            actions=submitted.Select(item=>item.Kind).ToArray(),passed=true};
                    }
                    Check(expectedSkip?game.WinnerId==own.PlayerId:true,"native basic/evolution finish failed");
                    Check(scenario!="tactic-locked"||!submitted.Any(item=>item.Kind.Contains("Evolve")),"locked evolution was submitted");
                    if(scenario is "tactic-response" or "tactic-private") {
                        Check(game.WinnerId!=own.PlayerId&&enemy.Leader.CurrentHealth>0,"public before-attack heal did not alter the projected finish");
                        var facts=provider.PlanInputs.Single().GetProperty("localTactics");
                        Check(facts.GetProperty("available").GetBoolean()&&facts.GetProperty("steps").GetArrayLength()>0
                            &&facts.GetProperty("unmodelledInputs").EnumerateArray().Any(row=>row.GetProperty("reason").GetString()=="runtime-abilities-not-simulated"),
                            "model did not receive the local path and its unsimulated public response warning");
                    }
                    var inputs=string.Join("\n",provider.Inputs);
                    Check(!inputs.Contains("PRIVATE_ENEMY_HAND_CANARY"),"enemy hidden hand text leaked to scripted provider");
                    Check(!inputs.Contains(own.PlayerId.ToString())&&!inputs.Contains(enemy.PlayerId.ToString())
                        &&!own.Hand.Concat(enemy.Hand).Concat(own.Field).Concat(enemy.Field).Concat(own.Emblem).Concat(enemy.Emblem).Concat(own.Deck).Concat(enemy.Deck).Concat(new[]{own.Leader,enemy.Leader}).Any(card=>inputs.Contains(card.InstanceId.ToString())),"runtime instance/player GUID leaked in tactical input");
                    Check(!expectedSkip||!(bool)typeof(AiStrategyPlanner).GetField("guideRunning",Hidden)!.GetValue(planner)!,"local-only matches queued automatic reflection/guide work");
                    Check(lesson.TurnDecisions?.Attempts==(expectedSkip?0:1),"local-only win was classified as a model planning attempt");
                    return new{scenario,trained=frozen!=null,modelVersion=frozen?.Current?.Version,
                        nativeWon=game.WinnerId==own.PlayerId,enemyHp=enemy.Leader.CurrentHealth,
                        skippedPlanning=expectedSkip,modelPlanRequests=provider.Plans,allFakeRequests=provider.Requests,
                        bypassCount=bypass.Length,actions=submitted.Select(item=>item.Kind).ToArray(),passed=true};
                }
                var drawCard=draw!;var firstCard=first!;var secondCard=second!;var follows=lesson.PlanFollows!;var plays=submitted.Where(action=>action.Card.HasValue).Select(action=>action.Card!.Value).ToArray();
                var resumed=scenario is "draw-one" or "draw-two";
                Console.WriteLine(JsonSerializer.Serialize(new{scenario,nativeDraws,plans=provider.Plans,submitted=submitted.Select(x=>x.Kind),follows,ownHand=own.Hand.Select(c=>c.BaseDefinitionId),ownField=own.Field.Select(c=>c.BaseDefinitionId)}));
                Check(nativeDraws==(scenario=="draw-two"?2:1),"native draw count differs from fixture");
                Check(provider.Plans==1,"draw continuation made another model plan request");
                Check(plays.Length>=1&&plays[0]==drawCard.InstanceId,"executor did not start with the scripted draw plan");
                if(resumed)Check(plays.SequenceEqual(new[]{drawCard.InstanceId,secondCard.InstanceId,firstCard.InstanceId})
                    &&follows.Select(follow=>follow.Status).SequenceEqual(new[]{"revalidated","continued","finished"}),"settled draw lost exact-instance ordered plan");
                else if(scenario=="draw-lethal")Check(game.WinnerId==own.PlayerId&&plays.Length==2
                    &&!plays.Contains(firstCard.InstanceId)&&!plays.Contains(secondCard.InstanceId)&&follows[0].Status=="revalidated"
                    &&follows.Any(follow=>follow.Status=="interrupted"),"newly drawn immediate lethal was subordinated to the old body plan");
                else Check(follows[0].Status=="interrupted"&&!follows.Any(follow=>follow.Status=="revalidated"),"changed or unverified route was continued");
                if(scenario=="fresh-blocked")Check(!plays.Contains(secondCard.InstanceId),"freshly blocked planned card was submitted");
                return new{scenario,trained=frozen!=null,modelVersion=frozen?.Current?.Version,resumed,nativeDraws,modelPlanRequests=provider.Plans,realProviderCalls=0,
                    playOrder=plays.Select(uid=>uid==drawCard.InstanceId?"draw":uid==secondCard.InstanceId?"planned-second":uid==firstCard.InstanceId?"planned-third":"new-draw").ToArray(),
                    follows,newDrawLethal=scenario=="draw-lethal"&&game.WinnerId==own.PlayerId,fieldCount=own.Field.Count,paidPp=5-own.CurrentPlayPoints,passed=true};
            }
        }
        finally
        {
            planner.Dispose();
            var full=Path.GetFullPath(directory);Check(full.StartsWith(Path.Combine(Path.GetTempPath(),"ai-draw-line-"),StringComparison.OrdinalIgnoreCase),"fixture cleanup escaped temporary root");
            if(sharedDirectory==null&&Directory.Exists(full))Directory.Delete(full,true);
        }
    }
    private static object LocalOnlyHistory()
    {
        var directory=Path.Combine(Path.GetTempPath(),"ai-draw-line-local-only-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            for(var i=0;i<4;i++)Run("tactic-win",sharedDirectory:directory);
            using var provider=new ScriptedProvider();
            var settings=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
                ["AiStrategy:Enabled"]="true",["AiStrategy:Model"]="fixture",["AiStrategy:Endpoint"]="https://example.invalid/chat/completions",
                ["AiStrategy:MemoryPath"]=directory}).Build();
            using var planner=new AiStrategyPlanner(provider,settings,new Host(),NullLogger<AiStrategyPlanner>.Instance);
            var history=(IReadOnlyList<AiStrategyPlanner.HistoryEntry>)typeof(AiStrategyPlanner).GetMethod("ReadHistory",Hidden)!.Invoke(planner,null)!;
            Check(history.Count==4&&history.All(item=>item.Lesson.LocalTacticalBypasses?.Length==1&&item.Lesson.Plans.Length==0&&item.Lesson.TurnDecisions?.Attempts==0),
                "local-only matches lost completed results or were recorded as model suggestions");
            var summary=typeof(AiStrategyPlanner).GetMethod("SummarizeExperience",Hidden)!.Invoke(planner,[history.Select(item=>item.Lesson).ToArray(),history[0].Lesson.OpponentClass,history[0].DeckKey]);
            var counts=JsonSerializer.SerializeToElement(summary,new JsonSerializerOptions(JsonSerializerDefaults.Web)).GetProperty("participationCounts");
            Check(counts.GetProperty("localTacticalOnly").GetInt32()==4&&counts.GetProperty("withTurnPlanningAttempts").GetInt32()==0,"participation summary misclassified local wins");
            // Positive control: these same four compatible samples do satisfy the reflection threshold.
            // Only this explicit, isolated fake-provider call is allowed; normal local-only game completion did not queue it.
            var eligible=(bool)typeof(AiStrategyPlanner).GetMethod("QueueReflection",Hidden)!.Invoke(planner,[history[0].DeckKey,history.Select(item=>item.Lesson).ToArray()])!;
            Check(eligible,"reflection test did not meet the real sample threshold");
            Check(SpinWait.SpinUntil(()=>!(bool)typeof(AiStrategyPlanner).GetField("guideRunning",Hidden)!.GetValue(planner)!,TimeSpan.FromSeconds(5)),"fake reflection did not finish");
            Check(provider.Requests>0,"reflection positive control never reached the scripted provider");
            return new{games=history.Count,automaticModelRequests=0,explicitFakeReflectionPositiveControlRequests=provider.Requests,participationCounts=counts,passed=true};
        }
        finally
        {
            var full=Path.GetFullPath(directory);Check(full.StartsWith(Path.Combine(Path.GetTempPath(),"ai-draw-line-local-only-"),StringComparison.OrdinalIgnoreCase),"history cleanup escaped temporary root");
            if(Directory.Exists(full))Directory.Delete(full,true);
        }
    }
    private static void CheckTacticalFrame(GameContext context,GameState game,Guid actor)
    {
        var assembly=typeof(AiTurnExecutor).Assembly;var search=assembly.GetType("ShadowverseBeyond.Server.Services.AiTacticalSearch")!;
        var factsType=assembly.GetType("ShadowverseBeyond.Server.Services.AiLocalTacticalFacts")!;
        var hidden=BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
        var snapshot=GameStateProjection.Create(game,context);
        var plan=search.GetMethod("Find",hidden)!.Invoke(null,[snapshot,actor,CancellationToken.None,null,null]);
        var read=factsType.GetMethod("Read",hidden)!;
        var facts=read.Invoke(null,[context,snapshot,actor,plan,game.StateRevision])!;
        var json=JsonSerializer.Serialize(facts,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var parsed=JsonDocument.Parse(json);
        Check(parsed.RootElement.GetProperty("available").GetBoolean()&&parsed.RootElement.GetProperty("nativeInputsChecked").GetBoolean(),"native tactical input binding failed");
        Check(System.Text.Encoding.UTF8.GetByteCount(json)<=6144&&!json.Contains(actor.ToString()),"tactical facts exceeded allowance or leaked player identity");
        Check(!json.Contains("PRIVATE_"),"tactical projection included hidden card data");
        var stale=factsType.GetMethod("ForPosition",hidden)!.Invoke(null,[facts,snapshot,actor,game.StateRevision+1])!;
        Check(!(bool)stale.GetType().GetProperty("Available")!.GetValue(stale)!,"stale native frame was accepted");
        var own=snapshot.Players.Single(player=>player.PlayerId==actor);
        var changed=snapshot with {Players=snapshot.Players.Select(player=>player.PlayerId==actor?player with{CurrentPP=player.CurrentPP+1}:player).ToList()};
        stale=factsType.GetMethod("ForPosition",hidden)!.Invoke(null,[facts,changed,actor,game.StateRevision])!;
        Check(!(bool)stale.GetType().GetProperty("Available")!.GetValue(stale)!,"changed PP frame was accepted");
        var duplicate=snapshot with{Players=snapshot.Players.Select(player=>player.PlayerId==actor?player with{Field=own.Field.Concat(own.Field).ToList()}:player).ToList()};
        var ambiguous=read.Invoke(null,[context,duplicate,actor,plan,game.StateRevision])!;
        Check(!(bool)ambiguous.GetType().GetProperty("Available")!.GetValue(ambiguous)!,"ambiguous same-instance binding was accepted");
        var noContext=read.Invoke(null,[null,snapshot,actor,plan,null])!;
        Check(!(bool)noContext.GetType().GetProperty("CanSkipPlanning",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(noContext)!,"DTO-only projection skipped native verification");
        Check(!own.Field.Any(card=>json.Contains(card.InstanceId.ToString())),"tactical source GUID leaked");
        var choicesType=assembly.GetType("ShadowverseBeyond.Server.Services.AiTacticalCandidates")!;
        var frame=choicesType.GetMethod("Read",hidden)!.Invoke(null,[context,snapshot,actor,plan,game.StateRevision])!;
        var packed=JsonSerializer.SerializeToElement(frame,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Check(packed.GetProperty("available").GetBoolean()&&System.Text.Encoding.UTF8.GetByteCount(packed.GetRawText())<=6144,"native first-action choices unavailable or over budget");
        Check(!own.Field.Any(card=>packed.GetRawText().Contains(card.InstanceId.ToString())),"private choice bindings were serialized");
        var last=packed.GetProperty("options").EnumerateArray().Last().GetProperty("index").GetInt32();
        var bound=choicesType.GetMethod("Bind",hidden)!.Invoke(null,[frame,last]);Check(bound!=null,"last retained choice lost its binding");
        Check(choicesType.GetMethod("CheckNow",hidden)!.Invoke(null,[bound,context,snapshot,actor,game.StateRevision])==null,
            "fresh legal choice was rejected after reordering/bounding");
        Check(choicesType.GetMethod("CheckNow",hidden)!.Invoke(null,[bound,context,snapshot,actor,game.StateRevision+1])!=null,
            "stale first-action binding was accepted");
        var beforeProvider=GameDataProvider.Instance;
        try {
            var changedProvider=Proxy<IGameDataProvider>.Make((m,a)=>m.Name=="get_DataRevision"?Guid.NewGuid():m.Invoke(beforeProvider,a));
            GameDataProvider.SetInstanceForTesting(changedProvider);
            Check((string?)choicesType.GetMethod("CheckNow",hidden)!.Invoke(null,[bound,context,snapshot,actor,game.StateRevision])=="card-rules-changed",
                "definition edits during planning did not invalidate the exact action binding");
        } finally {GameDataProvider.SetInstanceForTesting(beforeProvider);}
        Check(choicesType.GetMethod("Bind",hidden)!.Invoke(null,[frame,-1])==null&&choicesType.GetMethod("Bind",hidden)!.Invoke(null,[frame,999])==null,
            "unoffered action index accepted");
    }
    private sealed class ScriptedProvider:HttpMessageHandler,IHttpClientFactory
    {
        public string Scenario="";
        public Action? AfterPlan;
        public int Plans,Requests;
        public List<JsonElement> PlanInputs {get;}=[];
        public List<string> Inputs {get;}=[];
        public HttpClient CreateClient(string name)=>new(this,false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            var body=await request.Content!.ReadAsStringAsync(token);Requests++;Inputs.Add(body);
            using var payload=JsonDocument.Parse(body);
            var input=payload.RootElement.GetProperty("messages").EnumerateArray().Where(row=>row.GetProperty("role").GetString()=="user")
                .Select(row=>row.GetProperty("content").GetString()!).FirstOrDefault(text=>text.Contains("\"legalPlays\""));
            object answer;
            if(input!=null){Plans++;using var frame=JsonDocument.Parse(input);PlanInputs.Add(frame.RootElement.Clone());AiStrategyPlanner.PlayAdvice Step(int i,int id,int cost)=>new(i,id,"Base",cost,"use");var line=new[]{Step(0,Source,1),Step(2,Body,2),Step(1,Body,2)};
                answer=frame.RootElement.GetProperty("legalPlays").GetArrayLength()==0
                    ?new{focus="pressure",plan="按可见战场检查直接攻击路线",prioritize=Array.Empty<int>(),hold=Array.Empty<int>(),play=(AiStrategyPlanner.PlayAdvice?)null,plays=Array.Empty<AiStrategyPlanner.PlayAdvice>()}
                    :new{focus="setup",plan="抽牌后按既有实例次序完成两张随从",prioritize=Array.Empty<int>(),hold=Array.Empty<int>(),play=(AiStrategyPlanner.PlayAdvice?)line[0],plays=line};}
            else answer=new{gamePlan="fixture",cards=new[]{new{id=Source,role="draw"},new{id=Body,role="board"}}};
            if(input!=null&&Scenario.StartsWith("tactic-model-")) {
                var options=PlanInputs.Last().GetProperty("tacticalChoices").GetProperty("options").EnumerateArray().ToArray();
                var desired=Scenario is "tactic-model-evolve" or "tactic-model-evo-locked"?"PerformEvolve":Scenario is "tactic-model-second" or "tactic-model-target" or "tactic-model-stale"?"PerformAttack":"EndTurn";
                var choice=options.FirstOrDefault(option=>option.GetProperty("kind").GetString()==desired
                    &&(Scenario!="tactic-model-second"||option.GetProperty("source").GetProperty("index").GetInt32()==1&&option.GetProperty("target").GetProperty("zone").GetString()=="leader")
                    &&(Scenario!="tactic-model-target"||option.GetProperty("target").GetProperty("zone").GetString()=="field"&&option.GetProperty("target").GetProperty("index").GetInt32()==1));
                var index=Scenario=="tactic-model-invalid"||choice.ValueKind==JsonValueKind.Undefined?999:choice.GetProperty("index").GetInt32();
                answer=new{focus="pressure",plan="fixture native first action",prioritize=Array.Empty<int>(),hold=Array.Empty<int>(),
                    play=Scenario=="tactic-model-conflict"?new AiStrategyPlanner.PlayAdvice(0,Body,"Base",2,"use"):null,
                    plays=Array.Empty<AiStrategyPlanner.PlayAdvice>(),ability=(object?)null,
                    nextAction=Scenario is "tactic-model-control" or "tactic-model-mechanics"?null:(Scenario=="tactic-model-missing"?(object)new{}:new AiStrategyPlanner.TacticalAdvice(index))};
            }
            if(input!=null){AfterPlan?.Invoke();AfterPlan=null;}
            var json=JsonSerializer.Serialize(answer,new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=json}}}}))};
        }
    }
    private sealed class Host:IWebHostEnvironment
    {
        public string WebRootPath{get;set;}=".";public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();
        public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="test";
        public string ContentRootPath{get;set;}=".";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
    }
}
