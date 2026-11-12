using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Persistence;
using ShadowverseBeyond.Server.Services;

// Read-only coverage audit. The historical query knows only the recorded attacker, target, turn and HP.
// This is not replay, outcome evaluation, or reconstruction of the unrecorded hand/board.
internal static class AiSituationalHistoryProbe
{
    internal static void Run(string database,string directory,string output,string proofBuild)
    {
        const BindingFlags hidden=BindingFlags.NonPublic|BindingFlags.Static;
        var identity=typeof(AiStrategyPlanner).Assembly.GetType("ShadowverseBeyond.Server.Services.AiEngineRulesIdentity")!;
        if(!(bool)identity.GetMethod("ProveLegacyBuild",hidden)!.Invoke(null,[proofBuild])!)throw new InvalidOperationException("Unproven historical rules build");
        var prior=GameDataProvider.Instance;
        var copy=Path.Combine(Path.GetTempPath(),"ai-situational-history-"+Guid.NewGuid().ToString("N")+".db");
        using(var source=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=database,Mode=SqliteOpenMode.ReadOnly}.ToString()))
        using(var destination=new SqliteConnection("Data Source="+copy)){source.Open();destination.Open();source.BackupDatabase(destination);}
        using var services=new ServiceCollection().AddLogging(log=>log.ClearProviders())
            .AddDbContext<ApplicationDbContext>(options=>options.UseSqlite("Data Source="+copy+";Mode=ReadOnly"))
            .AddScoped<ICardRepository,EFCardRepository>().AddScoped<AbilityLoader>().BuildServiceProvider();
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web);var rows=new List<object>();
        var sourceFiles=new List<object>();
        var observedGames=0;var observedAttacks=0;var completedActions=0;var evaluated=0;var beforeHits=0;var afterHits=0;var regressions=0;
        var matchedEpisodes=0;var ordinaryDelivered=0;var focusedDelivered=0;var deliveryRegressions=0;
        var retrieval=typeof(AiStrategyPlanner).Assembly.GetType("ShadowverseBeyond.Server.Services.AiLessonRetrieval")!.GetMethod("Select",hidden)!;
        AiStrategyPlanner.Lesson[] Fallback(AiStrategyPlanner.Lesson[] lessons,string opponent)=>(AiStrategyPlanner.Lesson[])
            typeof(AiStrategyPlanner).GetMethod("SelectLessons",hidden)!.Invoke(null,[lessons,opponent])!;
        try{
            var data=new GameDataProvider(services.GetRequiredService<IServiceScopeFactory>(),NullLogger<GameDataProvider>.Instance);
            data.InitializeAsync().GetAwaiter().GetResult();GameDataProvider.SetInstanceForTesting(data);
            var rules=(AiDefinitionStamp)typeof(AiStrategyPlanner).Assembly.GetType("ShadowverseBeyond.Server.Services.AiDefinitionRules")!
                .GetMethod("Read",hidden)!.Invoke(null,[data])!;
            foreach(var file in Directory.EnumerateFiles(directory,"*.json").Where(file=>!file.EndsWith(".reflection.json",StringComparison.OrdinalIgnoreCase)).Order()){
                var bytes=File.ReadAllBytes(file);using var document=JsonDocument.Parse(bytes);if(document.RootElement.ValueKind!=JsonValueKind.Array)continue;
                var history=JsonSerializer.Deserialize<AiStrategyPlanner.Lesson[]>(bytes,json)??[];
                observedGames+=history.Length;observedAttacks+=history.Sum(lesson=>lesson.Attacks?.Length??0);completedActions+=history.Sum(lesson=>lesson.CompletedActions?.Length??0);
                for(var gameIndex=8;gameIndex<history.Length;gameIndex++){
                    var lesson=history[gameIndex];var previous=history.Take(gameIndex).Select(item=>item with{CompletedActions=null}).ToArray();
                    foreach(var (attack,index) in (lesson.Attacks??[]).Select((attack,index)=>(attack,index))){
                        var actor=Guid.NewGuid();var enemy=Guid.NewGuid();
                        var own=new PlayerStateDto{PlayerId=actor,TurnNumber=attack.Turn,IsFirst=lesson.First,
                            Field=[new(){CardId=attack.AttackerCardId,Type=CardType.Follower,CurrentAttack=attack.AttackerAttack,InstanceId=Guid.NewGuid()}],
                            LeaderCard=attack.OwnHealth.HasValue?new(){Type=CardType.Leader,CurrentHealth=attack.OwnHealth.Value}:null};
                        var other=new PlayerStateDto{PlayerId=enemy,Class=Enum.TryParse<CardClass>(lesson.OpponentClass,out var cls)?cls:CardClass.Neutral,
                            Field=attack.Face?[]:[new(){CardId=attack.TargetCardId,Type=CardType.Follower,CurrentAttack=attack.TargetAttack,CurrentHealth=attack.TargetHealth,InstanceId=Guid.NewGuid()}],
                            LeaderCard=attack.EnemyHealth.HasValue?new(){Type=CardType.Leader,CurrentHealth=attack.EnemyHealth.Value}:null};
                        var snapshot=new GameStateDto{CurrentPlayerId=actor,Players=[own,other]};
                        var normal=typeof(AiStrategyPlanner).GetMethod("SelectModelLessons",hidden)!.Invoke(null,[previous,lesson.OpponentClass]) as AiStrategyPlanner.Lesson[];
                        var selected=retrieval.Invoke(null,[previous,lesson.OpponentClass,rules,snapshot,actor,
                            (Func<AiStrategyPlanner.Lesson[],string,AiStrategyPlanner.Lesson[]>)Fallback,"historical-minimal-query",null])!;
                        var chosen=(AiStrategyPlanner.Lesson[])selected.GetType().GetProperty("Lessons")!.GetValue(selected)!;
                        var fact=(AiExperienceRetrievalFact)selected.GetType().GetProperty("Fact")!.GetValue(selected)!;
                        bool Hit(AiStrategyPlanner.Lesson[] lessons)=>lessons.Any(item=>(item.Attacks??[]).Any(a=>a.AttackerCardId==attack.AttackerCardId)
                            ||(item.CompletedActions??[]).Any(a=>a.ActorBefore?.CardId==attack.AttackerCardId));
                        var before=Hit(normal!);var after=Hit(chosen);evaluated++;if(before)beforeHits++;if(after)afterHits++;if(before&&!after)regressions++;
                        var delivery=new List<object>();
                        foreach(var match in fact.Matches){
                            if(match.LessonIndex<0||match.LessonIndex>=chosen.Length||match.HistoryIndexAtRequest>=gameIndex)
                                throw new InvalidOperationException("Unbound or future historical evidence");
                            var item=chosen[match.LessonIndex];
                            var ordinary=JsonSerializer.SerializeToElement(typeof(AiStrategyPlanner).GetMethod("LessonForModel",hidden)!.Invoke(null,[item]),json).GetProperty("observed");
                            var focused=JsonSerializer.SerializeToElement(typeof(AiStrategyPlanner).GetMethod("LessonAtMatch",hidden)!.Invoke(null,[item,match]),json).GetProperty("observed");
                            bool Delivered(JsonElement observed){
                                if(match.Evidence=="attacks"){
                                    var packed=observed.GetProperty("attacks");
                                    if(packed.ValueKind==JsonValueKind.Null)return false;
                                    if(packed.GetProperty("examples").GetArrayLength()>8)throw new InvalidOperationException("Attack excerpt budget grew");
                                    return packed.GetProperty("exampleIndices").EnumerateArray().Any(index=>index.GetInt32()==match.EvidenceIndex);
                                }
                                if(match.Evidence=="completedActions"){
                                    var packed=observed.GetProperty("completedActions");
                                    if(packed.ValueKind==JsonValueKind.Null)return false;
                                    if(packed.GetProperty("samples").GetArrayLength()>4)throw new InvalidOperationException("Completed-action excerpt budget grew");
                                    return packed.GetProperty("samples").EnumerateArray().Any(row=>row.GetProperty("sourceIndex").GetInt32()==match.EvidenceIndex);
                                }
                                return false;
                            }
                            var ordinaryHit=Delivered(ordinary);var focusedHit=Delivered(focused);
                            matchedEpisodes++;if(ordinaryHit)ordinaryDelivered++;if(focusedHit)focusedDelivered++;if(ordinaryHit&&!focusedHit)deliveryRegressions++;
                            delivery.Add(new{match.LessonIndex,category=match.Evidence,match.EvidenceIndex,ordinaryDelivered=ordinaryHit,focusedDelivered=focusedHit});
                        }
                        rows.Add(new{historyFile=Path.GetFileName(file),queryGameIndex=gameIndex,attackIndex=index,queryAttacker=attack.AttackerCardId,
                            priorGames=previous.Length,baselineObservedSourceFound=before,situationalObservedSourceFound=after,selection=fact,excerptDelivery=delivery});
                    }
                }
                sourceFiles.Add(new{file=Path.GetFileName(file),sha256=Convert.ToHexString(SHA256.HashData(bytes)),games=history.Length});
                if(!bytes.SequenceEqual(File.ReadAllBytes(file)))throw new InvalidOperationException("History changed during audit; discard the measurement");
            }
            File.WriteAllText(output,JsonSerializer.Serialize(new{realProviderRequests=0,readOnlyHistory=true,observedGames,observedAttacks,completedActions,
                evaluatedQueries=evaluated,baselineSourceCoverage=beforeHits,situationalSourceCoverage=afterHits,sourceCoverageRegressions=regressions,
                matchedEpisodes,ordinaryDelivered,focusedDelivered,deliveryRegressions,
                excerptScope="Same situationally selected history and episode, ordinary excerpts versus match-focused excerpts. Delivery is checked by retained index; at most four completed actions/eight attack contexts, no extra provider requests. Not reasoning or outcome validation.",
                sourceFiles,rules=rules.Fingerprint,queryScope="Attack-context-only audit using earlier completed-game records. CompletedActions are omitted from both retrieval inputs because the query does not know its unrecorded PP/hand/field resources. Minimal recorded attacker/target/turn/HP query, not a reconstructed hand or full position. Retrieval coverage is not semantic reasoning, learning gain, or win rate.",rows},new JsonSerializerOptions(json){WriteIndented=true}));
            Console.WriteLine(JsonSerializer.Serialize(new{observedGames,observedAttacks,completedActions,evaluated,beforeHits,afterHits,regressions,matchedEpisodes,ordinaryDelivered,focusedDelivered,deliveryRegressions,realProviderRequests=0}));
        }finally{
            GameDataProvider.SetInstanceForTesting(prior);SqliteConnection.ClearAllPools();
            if(!Path.GetFullPath(copy).StartsWith(Path.Combine(Path.GetTempPath(),"ai-situational-history-"),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unexpected audit cleanup path");
            foreach(var suffix in new[]{"","-wal","-shm"})if(File.Exists(copy+suffix))File.Delete(copy+suffix);
        }
    }
}
