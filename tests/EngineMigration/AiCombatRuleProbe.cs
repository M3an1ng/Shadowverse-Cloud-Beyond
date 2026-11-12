using System.Reflection;
using System.Text.Json;
using AutoMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Persistence;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

// Differential evidence, not a claim that the basic search simulates every card effect.
internal static class AiCombatRuleProbe
{
    private const int Leader=2000000700,Target=2000000701;
    private static readonly HashSet<EffectType> Atomic=[EffectType.Damage,EffectType.Heal,EffectType.ModifyStatus,
        EffectType.SetKeyword,EffectType.Destroy,EffectType.Vanish,EffectType.SetAttackTimes,EffectType.ResetAttackTimes];
    private static readonly BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
    private static readonly Type Projection=typeof(AiLearningModel).Assembly.GetType("ShadowverseBeyond.Server.Services.AiCombatProjection")!;
    private static readonly Type Search=typeof(AiLearningModel).Assembly.GetType("ShadowverseBeyond.Server.Services.AiTacticalSearch")!;
    private static object? Property(object? value,string name)=>value?.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(value);
    private static readonly MethodInfo After=typeof(AiLearningModel).GetMethod("EstimateAfterstate",Hidden)!;
    private static bool Combat(AbilityConfig config)=>config.IsEnabled&&config.Trigger.IsEnabled
        &&config.Trigger.IsValidZone(CardZone.Field)&&config.Trigger.EventTypes.Any(type=>type is EventType.Attack or EventType.Clash);
    private static bool FiniteAtomic(AbilityConfig config)=>config.Effects.Any(effect=>effect.IsEnabled)
        &&config.Effects.Where(effect=>effect.IsEnabled).All(effect=>Atomic.Contains(effect.Type)
            &&effect.ChainedAbilities.Count==0&&effect.RepeatCount==0&&effect.RepeatCountValue==null&&effect.RepeatCondition==null);

    internal static void Run(string source,string output,bool focusedOnly=false,bool limitedCards=false)
    {
        var original=GameDataProvider.Instance;var oldAccessor=GameServiceProvider.ContextAccessor;
        var file=Path.Combine(Path.GetTempPath(),"ai-combat-rule-probe-"+Guid.NewGuid().ToString("N")+".db");
        using(var input=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=source,Mode=SqliteOpenMode.ReadOnly}.ToString()))
        using(var copy=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=file}.ToString())){input.Open();copy.Open();input.BackupDatabase(copy);}
        using var catalog=new ServiceCollection().AddLogging(log=>log.ClearProviders())
            .AddDbContext<ApplicationDbContext>(options=>options.UseSqlite(new SqliteConnectionStringBuilder{DataSource=file,Mode=SqliteOpenMode.ReadOnly}.ToString()))
            .AddScoped<ICardRepository,EFCardRepository>().AddScoped<AbilityLoader>().BuildServiceProvider();
        try {
            var native=new GameDataProvider(catalog.GetRequiredService<IServiceScopeFactory>(),NullLogger<GameDataProvider>.Instance);
            native.InitializeAsync().GetAwaiter().GetResult();
            var all=native.GetAllCardDefinitions().Values.Where(card=>card.Type==CardType.Follower)
                .Select(card=>(Card:card,Rules:native.GetAbilityConfigs(card.Id).Where(Combat).ToArray()))
                .Where(row=>row.Rules.Length>0).OrderBy(row=>row.Card.Id).ToArray();
            // A deterministic bounded corpus; counts describe this filter, not overall rule coverage.
            var chosen=all.Where(row=>row.Rules.All(FiniteAtomic)).Take(48).ToArray();
            var results=new List<JsonElement>();
            if(!focusedOnly&&!limitedCards)foreach(var row in chosen)foreach(var face in new[]{false,true}) {
                try {results.Add(JsonSerializer.SerializeToElement(Execute(native,row.Card,row.Rules,face)));}
                catch(Exception error){results.Add(JsonSerializer.SerializeToElement(new{cardId=row.Card.Id,row.Card.Name,face,nativeCompleted=false,error=error.GetBaseException().Message}));}
            }
            if(limitedCards){
                foreach(var id in new[]{1471592,1471930,1471605}){
                    var definition=native.GetCardDefinition(id)!;
                    results.Add(JsonSerializer.SerializeToElement(Execute(native,definition,native.GetAbilityConfigs(id).Where(Combat).ToArray(),false,30,true,"actual-limited-clash")));
                }
                if(results.Any(row=>!row.GetProperty("nativeCompleted").GetBoolean()||!row.GetProperty("projectedValueFeaturesMatch").GetBoolean()))
                    throw new InvalidOperationException("Actual limited-card projection mismatch");
            }
            if(focusedOnly) {
                foreach(var item in new[]{(Id:1471747,Face:true,Hp:3,Ward:false,Name:"attack-lowers-own-damage"),
                    (Id:1472404,Face:false,Hp:1,Ward:true,Name:"clash-wins-before-combat"),
                    (Id:1473053,Face:true,Hp:7,Ward:false,Name:"attack-adds-face-damage"),
                    (Id:1471768,Face:false,Hp:30,Ward:true,Name:"attack-shield-avoids-retaliation"),
                    (Id:1471926,Face:false,Hp:30,Ward:true,Name:"attack-gains-bane-before-combat")}) {
                    var definition=native.GetCardDefinition(item.Id)!;
                    results.Add(JsonSerializer.SerializeToElement(Execute(native,definition,native.GetAbilityConfigs(item.Id).Where(Combat).ToArray(),item.Face,item.Hp,item.Ward,item.Name)));
                }
                var byId=results.ToDictionary(row=>row.GetProperty("cardId").GetInt32());
                void Check(bool ok,string reason){if(!ok)throw new InvalidOperationException(reason);}
                Check(byId.Values.All(row=>row.GetProperty("nativeCompleted").GetBoolean()),"focused native transaction incomplete");
                Check(byId.Values.All(row=>row.GetProperty("projectedValueFeaturesMatch").GetBoolean()),"focused configured projection differs from actual native value features");
                foreach(var id in new[]{1472404,1473053})Check(byId[id].GetProperty("searchLethal").GetBoolean(),"local search missed compiled precombat lethal");
                Check(!byId[1471747].GetProperty("searchLethal").GetBoolean(),"local search retained the precombat self-debuff false lethal");
                Check(byId[1471747].GetProperty("basicProjectsLethal").GetBoolean()&&!byId[1471747].GetProperty("nativeWon").GetBoolean(),"real attack debuff did not demonstrate the false basic finish");
                foreach(var id in new[]{1472404,1473053})Check(!byId[id].GetProperty("basicProjectsLethal").GetBoolean()&&byId[id].GetProperty("nativeWon").GetBoolean(),"real configured precombat damage did not demonstrate a missed basic finish");
                Check(byId[1471768].GetProperty("native").GetProperty("ownField")[0].GetProperty("CurrentHealth").GetInt32()>
                    byId[1471768].GetProperty("basic").GetProperty("ownField")[0].GetProperty("CurrentHealth").GetInt32(),"attack shield did not preserve actual health");
                Check(byId[1471926].GetProperty("native").GetProperty("enemyField").GetArrayLength()==0&&byId[1471926].GetProperty("basic").GetProperty("enemyField").GetArrayLength()==1,"before-combat gained Bane did not clear the native Ward");
            }
            var completed=results.Where(row=>row.GetProperty("nativeCompleted").GetBoolean()).ToArray();
            var different=completed.Where(row=>!row.GetProperty("basicValueFeaturesMatch").GetBoolean()).ToArray();
            var projectionDifferences=completed.Count(row=>!row.GetProperty("projectedValueFeaturesMatch").GetBoolean());
            var regressions=completed.Count(row=>row.GetProperty("basicValueFeaturesMatch").GetBoolean()&&!row.GetProperty("projectedValueFeaturesMatch").GetBoolean());
            var report=new{projectedValueFeatureDifferences=projectionDifferences,regressions,realProviderCalls=0,actualNativeTransactions=true,sourceReadOnly=true,isolatedDatabase=true,
                candidates=all.Length,filtered=all.Count(row=>row.Rules.All(FiniteAtomic)),selected=limitedCards?3:focusedOnly?5:chosen.Length,focusedOnly,
                nativeCompleted=completed.Length,nativeIncomplete=results.Count-completed.Length,basicValueFeatureDifferences=different.Length,
                limitedCards,scope="Finite precombat overlay compared with the same unchanged native transaction; value-feature agreement is not a full-state proof. "+(limitedCards?"Three named actual limited Clash cards against neutral 3/3 Ward. ":focusedOnly?"Five named real-card scenarios covering precombat self-debuff, leader damage, Shield and Bane with explicit expected native outcomes. ":"First 48 real follower IDs with Field Attack/Clash root effects restricted to finite atomic effects, both leader and follower targets. ")+" Neutral leaders, 3/3 target and dummy decks, six-PP main phase, no mulligan/fanfare setup, no LLM. Conditional abilities may not trigger. Only the 512 value features are compared, with selected public stats exported; a feature match does not prove complete state or ability fidelity. Differences are expected approximation gaps, not engine bug labels or win-rate evidence.",cases=results};
            File.WriteAllText(output,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine(JsonSerializer.Serialize(new{selected=limitedCards?3:focusedOnly?5:chosen.Length,nativeCompleted=completed.Length,nativeIncomplete=results.Count-completed.Length,basicValueFeatureDifferences=different.Length,
                exampleIds=different.Select(row=>row.GetProperty("cardId").GetInt32()).Distinct().Take(10),realProviderCalls=0}));
        }
        finally {
            GameDataProvider.SetInstanceForTesting(original);GameServiceProvider.ContextAccessor=oldAccessor;SqliteConnection.ClearAllPools();
            var resolved=Path.GetFullPath(file);if(!resolved.StartsWith(Path.Combine(Path.GetTempPath(),"ai-combat-rule-probe-"),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unexpected probe cleanup path");
            foreach(var suffix in new[]{"","-wal","-shm"})if(File.Exists(resolved+suffix))File.Delete(resolved+suffix);
        }
    }
    private static object Execute(IGameDataProvider native,CardDefinition definition,AbilityConfig[] rules,bool face,int enemyHp=30,bool ward=false,string? scenario=null)
    {
        var defs=native.GetAllCardDefinitions().ToDictionary(pair=>pair.Key,pair=>pair.Value);
        defs[Leader]=new(){Id=Leader,Name="Probe leader",Type=CardType.Leader,Health=30};
        defs[Target]=new(){Id=Target,Name="Probe neutral target",Type=CardType.Follower,Attack=3,Health=3,Cost=2,Keywords=ward?["Ward"]:[]};
        var data=Proxy<IGameDataProvider>.Make((m,a)=>m.Name switch {
            "GetCardDefinition"=>defs.GetValueOrDefault((int)a![0]!),"GetAllCardDefinitions"=>defs,
            "GetAbilityConfigs"=>(int)a![0]!>=Leader?new List<AbilityConfig>():native.GetAbilityConfigs((int)a[0]!),_=>m.Invoke(native,a)});
        GameDataProvider.SetInstanceForTesting(data);var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
        using var services=AiEndTurnEngineTests.Services(data,accessor);using var scope=services.CreateScope();var sp=scope.ServiceProvider;
        var own=new PlayerState(Guid.NewGuid()){TurnNumber=6,MaxPlayPoints=6,CurrentPlayPoints=6};
        var enemy=new PlayerState(Guid.NewGuid()){TurnNumber=6};
        var game=new GameState("combat-rule-probe",own,enemy,own.PlayerId){Phase=GamePhase.Main,Status=GameStatus.InProgress,
            AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<IBuffManager>()};
        var factory=sp.GetRequiredService<ICardFactory>();own.Leader=factory.CreateAndRegisterNewCard(game,Leader,own.PlayerId,CardZone.Field);
        own.Leader.Damage=10;enemy.Leader=factory.CreateAndRegisterNewCard(game,Leader,enemy.PlayerId,CardZone.Field);enemy.Leader.Damage=30-enemyHp;
        var source=factory.CreateAndRegisterNewCard(game,definition.Id,own.PlayerId,CardZone.Field);source.ZoneStayTurn=1;
        var target=factory.CreateAndRegisterNewCard(game,Target,enemy.PlayerId,CardZone.Field);
        for(var i=0;i<6;i++){factory.CreateAndRegisterNewCard(game,Target,own.PlayerId,CardZone.Deck);factory.CreateAndRegisterNewCard(game,Target,enemy.PlayerId,CardZone.Deck);}
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(6));
        using var context=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId,timeout.Token);
        var before=GameStateProjection.Create(game,context);var sourceView=CardInstanceProjection.Create(source,context);
        var destination=face?enemy.Leader:target;var targetView=CardInstanceProjection.Create(destination,context);
        var estimate=(GameStateDto)After.Invoke(null,[before,own.PlayerId,"attack",sourceView,targetView])!;
        var projectionTimer=System.Diagnostics.Stopwatch.StartNew();
        var readerRevisionAtStart=game.StateRevision;var beforeJson=JsonSerializer.Serialize(before);
        var frame=Projection.GetMethod("Read",Hidden)!.Invoke(null,[context,before,own.PlayerId]);
        var prepared=(GameStateDto)Search.GetMethod("EstimateConfiguredAction",Hidden)!.Invoke(null,[before,own.PlayerId,"attack",sourceView,targetView,frame])!;
        var plan=Search.GetMethod("FindWithCombat",Hidden)!.Invoke(null,[before,own.PlayerId,timeout.Token,null,null,null,null,null,frame]);
        if(readerRevisionAtStart!=game.StateRevision||JsonSerializer.Serialize(before)!=beforeJson||JsonSerializer.Serialize(GameStateProjection.Create(game,context))!=beforeJson)throw new InvalidOperationException("Combat forecast mutated native or snapshot state");
        var projectionMilliseconds=projectionTimer.Elapsed.TotalMilliseconds;
        var readerRevision=game.StateRevision;
        var result=sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(context,
            [new AttackRequestedEvent(own.PlayerId,new AttackActionDto{AttackerUid=source.InstanceId,TargetUid=destination.InstanceId})]).GetAwaiter().GetResult();
        if(!result.IsSuccess||result.IsCancelled)return new{cardId=definition.Id,definition.Name,face,nativeCompleted=false,error=result.ErrorMessage??"cancelled"};
        var actual=GameStateProjection.Create(game,context);var projected=AiFeatures.Value(prepared,own.PlayerId);var guessed=AiFeatures.Value(estimate,own.PlayerId);var observed=AiFeatures.Value(actual,own.PlayerId);
        var correctedMatches=projected.Zip(observed).All(pair=>Math.Abs(pair.First-pair.Second)<1e-10);
        var differences=guessed.Zip(observed).Select((pair,index)=>(pair,index)).Where(item=>Math.Abs(item.pair.First-item.pair.Second)>1e-10)
            .Select(item=>new{index=item.index,basic=item.pair.First,native=item.pair.Second}).ToArray();
        object View(GameStateDto state)=>new{ownHp=state.Players[0].LeaderCard?.CurrentHealth,enemyHp=state.Players[1].LeaderCard?.CurrentHealth,
            ownField=state.Players[0].Field.Select(card=>new{card.CardId,card.CurrentAttack,card.CurrentHealth,keywords=card.Keywords.Select(keyword=>keyword.ToString())}),
            enemyField=state.Players[1].Field.Select(card=>new{card.CardId,card.CurrentAttack,card.CurrentHealth,keywords=card.Keywords.Select(keyword=>keyword.ToString())})};
        return new{cardId=definition.Id,definition.Name,face,scenario,enemyHpBefore=enemyHp,ward,nativeCompleted=true,basicProjectsLethal=estimate.Players[1].LeaderCard!.CurrentHealth<=0,nativeWon=game.WinnerId==own.PlayerId,basicValueFeaturesMatch=differences.Length==0,differences,
            rootEffectKinds=rules.SelectMany(rule=>rule.Effects).Where(effect=>effect.IsEnabled).Select(effect=>effect.Type.ToString()).Distinct(),
            projectedValueFeaturesMatch=correctedMatches,compiledRules=(int?)Property(frame,"RuleCount")??0,
            deckCounts=new{nativeOwn=actual.Players[0].DeckCount,nativeEnemy=actual.Players[1].DeckCount,projectedOwn=prepared.Players[0].DeckCount,projectedEnemy=prepared.Players[1].DeckCount},
            corrected=View(prepared),searchFirst=Property(Property(plan,"First"),"Type"),searchLethal=(bool?)Property(plan,"ProjectsLethal")??false,readerRevision,projectionMilliseconds,
            basic=View(estimate),native=View(actual),committedEventTypes=result.ProcessedEvents.Select(ev=>ev.EventType.ToString()).Distinct().ToArray()};
    }
}
