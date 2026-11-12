using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using ShadowverseBeyond.Server.Infrastructure.Data;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

if(args.Length==1&&args[0]=="--room-exit"){await RoomExitTests.RunAsync();return 0;}
if(args.Length==3&&args[0]=="--planned-mode-real"){AiPlannedModeTests.RunReal(args[1],args[2]);return 0;}
if(args.Length==2&&args[0]=="--planned-mode-native"){AiPlannedModeTests.Run(args[1]);return 0;}
if(args.Length==1&&args[0]=="--plan-archives"){AiPlanArchiveTests.Run();return 0;}
if(args.Length==1&&args[0]=="--hand-material-rules"){AiHandMaterialRuleTests.Run();return 0;}
if(args.Length==4&&args[0]=="--planned-material-native"){AiPlannedTargetTests.Run(args[1],args[2],args[3],true);return 0;}
if(args.Length==1&&args[0]=="--provider-usage"){AiProviderUsageTests.Run();return 0;}
if(args.Length==1&&args[0]=="--resource-learning"){AiResourceLearningTests.Run();return 0;}
if(args.Length==4&&args[0]=="--refund-continuation-native"){AiRefundContinuationTests.Run(args[1],args[2],args[3]);return 0;}
if(args.Length==1&&args[0]=="--selected-hand-loss"){AiSelectedHandLossTests.Run();return 0;}
if(args.Length==4&&args[0]=="--planned-target-native"){AiPlannedTargetTests.Run(args[1],args[2],args[3]);return 0;}
if(args.Length==4 && args[0]=="--leader-recovery-native"){AiLeaderRecoveryTests.Run(args[1],args[2],args[3]);return 0;}
if(args.Length is 3 or 4 && args[0]=="--weaver-native"){AiWeaverPlanTests.Run(args[1],args[2],args.Length==4?args[3]:null);return 0;}
if(args.Length==4&&args[0]=="--planning-context-native"){AiPlanningContextTests.Run(args[1],args[2],args[3]);return 0;}
if(args.Length==1&&args[0]=="--target-outcomes"){AiTargetOutcomeTests.Run();return 0;}
if(args.Length==1&&args[0]=="--evaluation-size"){AiEvaluationLimitTests.Run();return 0;}
if(args.Length==4&&args[0]=="--composite-target-native"){AiCompositeTargetTests.Run(args[1],args[2],args[3]);return 0;}
if(args.Length is 3 or 4 && args[0]=="--turn-five-audit") { AiTurnFiveAuditTests.Run(args[1],args[2],args.Length==4?args[3]:null);return 0; }
if(args.Length is 3 or 4 && args[0]=="--eternal-recovery-native") {
    AiEternalRecoveryTests.Boundaries();AiEternalRecoveryTests.Run(args[1],args[2],args.Length==4?args[3]:null);return 0;
}
if(args.Length==2 && args[0]=="--decision-flow-native") {
    AiDecisionFlowTests.Native(args[1]);return 0;
}
if(args.Length is 3 or 4 && args[0]=="--generated-defense-native") {
    AiGeneratedTokenTests.DefenseNative(args[1],args[2],args.Length==4?args[3]:null);return 0;
}
if(args.Length is 2 or 3 && args[0]=="--zero-damage-policy") {
    AiZeroDamagePolicyProbe.Run(args[1],args.Length==3?args[2]:null);return 0;
}
if(args.Length==2 && args[0]=="--zero-damage-native") {
    AiZeroDamageTests.Native(args[1]);return 0;
}
if(args.Length==2 && args[0]=="--experience-excerpts") {
    AiLessonRetrievalTests.ExcerptBoundaries();AiLessonRetrievalTests.ExcerptPlannerAndChoice(args[1]);return 0;
}
if(args.Length==1 && args[0]=="--hand-outcomes") {
    AiActionOutcomeTests.RootCompletionAndRaces();AiActionOutcomeTests.TerminalPersistenceAndPrivacy();AiActionOutcomeTests.OwnHandEndpoints();
    Console.WriteLine("Own-hand endpoint native boundaries, immutable facts and model/history delivery passed; real provider calls=0");return 0;
}
if(args.Length is 2 or 3 && args[0]=="--combat-limit-policy") {
    AiCombatLimitPolicyProbe.Run(args[1],args.Length==3?args[2]:null);return 0;
}
if(args.Length==3 && args[0]=="--combat-limit-cards") {
    AiCombatRuleProbe.Run(args[1],args[2],false,true);return 0;
}
if(args.Length==2 && args[0]=="--combat-limits-native") {
    AiCombatLimitTests.Native(args[1]);return 0;
}
if(args.Length==5 && args[0]=="--situational-history-audit") {
    AiSituationalHistoryProbe.Run(args[1],args[2],args[3],args[4]);return 0;
}
if(args.Length==2 && args[0]=="--situational-experience") {
    AiLessonRetrievalTests.RankingAndBoundaries();AiLessonRetrievalTests.PlannerAndChoice(args[1]);return 0;
}
if(args.Length==2 && args[0]=="--expanded-combat-native") {
    AiExpandedCombatTests.Native(args[1]);AiExpandedCombatTests.KeysAndComparisonIsolation();return 0;
}
if(args.Length==4 && args[0]=="--experience-input-audit") {
    AiExperienceInputProbe.Run(args[1],args[2],args[3]);return 0;
}
if(args.Length is 4 or 5 && args[0]=="--real-tactical-probe") {
    if(args.Length==5&&args[4]!="--paid")throw new ArgumentException("Unexpected probe option");
    AiRealTacticalProbe.Run(args[1],args[2],args[3],args.Length==5);return 0;
}
if(args.Length==3 && args[0]=="--combat-rule-focus") {
    AiCombatRuleProbe.Run(args[1],args[2],true);return 0;
}
if(args.Length==3 && args[0]=="--combat-rule-probe") {
    AiCombatRuleProbe.Run(args[1],args[2]);return 0;
}
if(args.Length is 2 or 3 && args[0]=="--first-action-native") {
    AiDrawContinuationTests.NativeFirstActions(args[1],args.Length==3?args[2]:null);return 0;
}
if(args.Length is 2 or 3 && args[0]=="--local-tactics-native") {
    AiDrawContinuationTests.NativeTactics(args[1],args.Length==3?args[2]:null);return 0;
}
if(args.Length is 2 or 3 && args[0]=="--draw-continuation-native") {
    AiDrawContinuationTests.Native(args[1],args.Length==3?args[2]:null);return 0;
}
if(args.Length>=4&&args[0]=="--rules-input-audit"){AiBuildInputAudit.Run(args[1],args.Skip(2).ToArray());return 0;}
if(args.Length>=4 && args[0]=="--engine-rule-identities") {
    AiEngineIdentityTests.CompareBuilds(args[1],args.Skip(2).ToArray());return 0;
}
if(args.Length==1 && args[0]=="--eternal-core") {
    AiEternalEngineTests.CorePolicy();return 0;
}
if(args.Length is 3 or 4 && args[0]=="--eternal-source-native") {
    AiEternalEngineTests.Native(args[1],args[2],args.Length==4?args[3]:null,true,true);return 0;
}
if(args.Length is 3 or 4 && args[0]=="--eternal-mode-native") {
    AiEternalEngineTests.Native(args[1],args[2],args.Length==4?args[3]:null,true);return 0;
}
if(args.Length is 3 or 4 && args[0]=="--eternal-core-native") {
    AiEternalEngineTests.Native(args[1],args[2],args.Length==4?args[3]:null);return 0;
}
if(args.Length==1 && args[0]=="--decision-facts") {
    AiDecisionFactsTests.ExactBindingAndBudgets();
    AiDecisionFactsTests.ModelAndExperience();
    return 0;
}
if(args.Length==3 && args[0]=="--generated-token-engine") {
    AiGeneratedTokenTests.Native(args[1],args[2]);return 0;
}
// Explicit offline native-engine probe. Never registers a model client or writes the source database.
if (args.Length == 3 && args[0] == "--end-turn-engine") {
    AiEndTurnEngineTests.Run(args[1], args[2]);
    return 0;
}
if (args.Length == 1 && args[0] == "--runtime-ability-facts") {
    AiRuntimeAbilityFactsTests.RuntimeChanges();
    AiRuntimeAbilityFactsTests.PrivacyAndBudget();
    AiRuntimeAbilityFactsTests.DuplicatesAndSuppression();
    AiDefinitionRulesTests.GuideAndExperience();
    Console.WriteLine("Runtime ability facts and mock integration: 4/4; provider calls=0");
    return 0;
}
if (args.Length == 4 && args[0] == "--training-replay") {
    AiTrainingReplayTests.Diagnose(args[1], args[2], args[3]);
    return 0;
}
if (args.Length == 1 && args[0] == "--training-replay-checks") {
    AiTrainingReplayTests.RoundTripAndIdentity();
    AiTrainingReplayTests.EpochDiagnostics();
    AiTrainingReplayTests.BestCheckpoint();
    return 0;
}
if (args.Length == 1 && args[0] == "--training-positions") {
    AiTrainingPositionsTests.BoundedStrataAndLabels();
    AiTrainingPositionsTests.SparseAndInvalidObservations();
    AiTrainingPositionsTests.SuccessfulCompletionAndLeases();
    return 0;
}
if (args.Length == 1 && args[0] == "--value-search") {
    AiSearchValueTests.BoundedDecisions();
    AiSearchValueTests.CapturedDeckAndArtifact();
    AiSearchValueTests.EndTurnValueCounters();
    return 0;
}
if (args.Length == 1 && args[0] == "--model-inference") {
    AiModelInferenceTests.NativeMechanics();
    AiModelInferenceTests.ShieldAndModelInputs();
    Console.WriteLine("Native mechanics and learned inputs: passed; provider calls=0");
    return 0;
}
if (args.Length == 1 && args[0] == "--model-training") {
    AiValueTrainingTests.IndependentDecks();
    AiValueTrainingTests.RejectRegressions();
    AiValueTrainingTests.RejectInvalidSamples();
    Console.WriteLine("Independent local model training: 3/3; provider calls=0");
    return 0;
}
if (args.Length == 1 && args[0] == "--model-promotion") {
    AiModelEvaluationTests.StrengthGate();
    AiModelEvaluationTests.IsolatedModelsAndTrialPersistence();
    Console.WriteLine("Frozen model comparison and promotion gate: 2/2; provider calls=0");
    return 0;
}
if (args.Length == 3 && args[0] == "--model-promotion-report") {
    AiModelEvaluationTests.Recheck(args[1],args[2]);
    return 0;
}
if (args.Length == 1 && args[0] == "--combat-threat") {
    AiCombatThreatTests.NativeSequences();
    AiCombatThreatTests.ResourcesAndBounds();
    AiCombatThreatTests.ScopeIsolation();
    Console.WriteLine("Native combat threat and resource decisions: 3/3; provider calls=0");
    return 0;
}
if (args.Length == 3 && args[0] == "--combat-threat-source") {
    AiCombatThreatTests.SourceCards(args[1],args[2]);
    AiCombatThreatTests.ZeroAttackPlans();
    Console.WriteLine("Real source cards and zero-attack plans: passed; provider calls=0");
    return 0;
}
if (args.Length == 1 && args[0] == "--combat-zero-plans") {
    AiCombatThreatTests.ZeroAttackPlans();
    Console.WriteLine("Useful and no-op zero-attack plans: passed; provider calls=0");
    return 0;
}
if (args.Length == 1 && args[0] == "--combat-bounds") {
    AiCombatThreatTests.ResourcesAndBounds();
    AiCombatThreatTests.ScopeIsolation();
    return 0;
}
if (args.Length == 1 && args[0] == "--combat-enumeration") {
    AiCombatThreatTests.ExhaustiveSmallBoards();
    return 0;
}
var results = new List<object>();
void Test(string name, Action action) { try { action(); results.Add(new { name, passed = true }); } catch(Exception e) { results.Add(new {name,passed=false,error=e.ToString()}); } }
void Check(bool yes, string reason) { if(!yes) throw new Exception(reason); }
Test("room exit preserves settlement delivery and releases pending choices", () => RoomExitTests.RunAsync().GetAwaiter().GetResult());
Test("AI searches combat, evolution and Storm routes", AiPolicyTests.Search);
Test("AI scores damage, healing and removal targets", AiPolicyTests.Targets);
Test("AI values Act resources and Fusion materials", AiPolicyTests.Abilities);
Test("AI reads complete recursive card references and reports missing context", AiRuleContextTests.Run);
if (Environment.GetEnvironmentVariable("AI_RULE_PROBE_SOURCE") is { Length: >0 } ruleSource
    && Environment.GetEnvironmentVariable("AI_RULE_PROBE_OUTPUT") is { Length: >0 } ruleOutput)
    Test("AI resolves current catalog reference chains", () => AiRuleContextTests.Probe(ruleSource, ruleOutput));
Test("review fixes preserve reveal order, redraw recovery and escaped preview URLs",()=>{
 var script="""
 import assert from 'node:assert/strict';
 import {readFileSync} from 'node:fs';
 const read=path=>readFileSync(path,'utf8');
 const {createRevealPresentationGate}=await import('data:text/javascript;base64,'+Buffer.from(read('src/revealPresentationGate.js')).toString('base64'));
 const gate=createRevealPresentationGate(), order=[];
 let finishFirst,finishBatch,finishQueued;
 gate.dispatch('CardRevealed',()=>{order.push('first');return new Promise(r=>finishFirst=r)});
 gate.dispatch('CardRevealed',()=>{order.push('batch');return new Promise(r=>finishBatch=r)});
 gate.dispatch('BoardUpdated',()=>order.push('board'));
 gate.dispatch('CardRevealed',()=>{order.push('queued');return new Promise(r=>finishQueued=r)});
 gate.dispatch('HandUpdated',()=>order.push('hand'));
 gate.dispatch('ChatMessage',()=>order.push('chat'));
 assert.deepEqual(order,['first','chat']);
 finishFirst();await new Promise(setImmediate);assert.deepEqual(order,['first','chat','batch']);
 finishBatch();await new Promise(setImmediate);assert.deepEqual(order,['first','chat','batch','board','queued']);
 finishQueued();await new Promise(setImmediate);assert.equal(order.at(-1),'hand');
 const reentrant=createRevealPresentationGate(), nested=[];let release;
 reentrant.dispatch('CardRevealed',()=>new Promise(r=>release=r));
 reentrant.dispatch('BoardUpdated',()=>{nested.push('board');reentrant.dispatch('CardRevealed',()=>nested.push('reveal'))});
 reentrant.dispatch('HandUpdated',()=>nested.push('hand'));release();await new Promise(setImmediate);
 assert.deepEqual(nested,['board','hand','reveal']);
 const normalize=new Function(read('src/previewDescriptionReferences.js').match(/function normalizePreviewUrl\(value\) \{[\s\S]*?\n\}/)[0]+';return normalizePreviewUrl')();
 assert.equal(normalize(`/assets/a'"()\\.png`),'/assets/a%27%22%28%29%5C.png');
 const AsyncFunction=Object.getPrototypeOf(async function(){}).constructor;
 const take=read('src/takeTwo.js'),start=take.indexOf('      } catch (e) {',take.indexOf('const send ='));
 assert.ok(start>=0);const recovery=take.slice(start+'      } catch (e) {'.length,take.indexOf('        reportDraftError(e);',start));
 const server={player:{plans:{offer:['server']}}};let fetched;
 const restored=await new AsyncFunction('api','room','headers','player','checkState','render',
   'let queueGeneration=0,pending=1,queue;'+recovery+';return player;')(
   async path=>{fetched=path;return structuredClone(server)}, {id:'room'}, {}, {plans:{offer:['local-redraw']}},async()=>{},()=>{});
 assert.equal(fetched,'/rooms/room');assert.deepEqual(restored,server.player);
 const {pathToFileURL}=await import('node:url'),{resolve}=await import('node:path');
 const {createCardStylePresentation}=await import(pathToFileURL(resolve('src/cardStylePresentation.js')));
 const doc=new EventTarget(), prefs={42:'night'}, old={cardId:42,styles:[{id:'night',appearance:{name:'old'},bindings:[]}]};
 let catalog=[old],fetches=0;
 const historyRequest=async(path,options)=>{if(options?.method==='PUT'){catalog=[JSON.parse(options.body)];return catalog[0]}if(path.endsWith('/preferences'))return prefs;fetches++;return catalog};
 const domSource=read('src/cardStyleDom.js').replace(/^import .*;\r?\n/gm,'').replace(/export /g,'');
 const dom=new Function('document','historyRequest','getUserSession','createCardStylePresentation',domSource+';return {getCardStylePresenter,bindCardStyle}')(doc,historyRequest,()=>({userId:'review'}),createCardStylePresentation);
 const node={isConnected:true};let shown;
 dom.bindCardStyle(node,present=>shown=present({id:42,name:'base'}).name);await new Promise(setImmediate);assert.equal(shown,'old');
 const saveBody=read('src/cardStyles.js').match(/save.onclick = async \(\) => \{([\s\S]*?)\n      \};/)[1];
 await new AsyncFunction('save','historyRequest','id','editorScope','config','editor','loadStyles','document','CustomEvent','showAlert','render','let dirty=true;'+saveBody)(
   {disabled:false},historyRequest,42,'standard',{...old,styles:[{...old.styles[0],appearance:{name:'new'}}]}, {_styleDrafts:new Map()},async()=>catalog,doc,CustomEvent,()=>{},()=>{});
 await new Promise(setImmediate);assert.equal(shown,'new');assert.equal(fetches,2);assert.equal((await dom.getCardStylePresenter())({id:42}).name,'new');
 const gallery=read('src/cardGallery.js'),listenerStart=gallery.indexOf('async (event) => {',gallery.indexOf('"svb:card-style-changed"'));
 assert.ok(listenerStart>=0);const listenerBody=gallery.slice(listenerStart+'async (event) => {'.length,gallery.indexOf('\n      },',listenerStart));
 const displayed=await new AsyncFunction('event','page','styles','preferences','loadStyles','cards','originalCards','createCardStylePresentation','search','let preservePage=false;'+listenerBody+';return cards;')(
   {detail:{cardId:42}},{isConnected:true},[old],prefs,async()=>catalog,[],[{id:42,name:'base'}],createCardStylePresentation,{apply(){}});
 assert.equal(prefs[42],'night');assert.equal(displayed[0].name,'new');
 """;
 var start=new System.Diagnostics.ProcessStartInfo("node"){RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add("--input-type=module");
 using var node=System.Diagnostics.Process.Start(start)!;node.StandardInput.Write(script);node.StandardInput.Close();var output=node.StandardOutput.ReadToEnd();var error=node.StandardError.ReadToEnd();node.WaitForExit();Check(node.ExitCode==0,output+error);
});
Test("history batches queries and SaveChanges(false) preserves pending states",()=>{
 using var connection=new SqliteConnection("Data Source=:memory:");connection.Open();var queries=new HistoryQueryCounter();
 using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).AddInterceptors(queries).Options);db.Database.EnsureCreated();
 for(var i=0;i<16;i++){db.CardDefinitions.Add(new(){Id=99500+i,Name="批量历史"});db.CardAbilities.Add(new(){Id=995000+i,CardId=99500+i,Name="批量能力",JsonConfig="{}"});}
 queries.Selects=0;db.SaveChangesAsync().GetAwaiter().GetResult();Check(queries.Selects<=5,$"batch issued {queries.Selects} selects");Check(db.CardRevisions.Count()==16,"batch snapshots missing");
 var card=db.CardDefinitions.First();card.Description="未接受变更";db.SaveChangesAsync(false,default).GetAwaiter().GetResult();
 Check(db.Entry(card).State==EntityState.Modified&&db.Entry(card).OriginalValues.GetValue<string>(nameof(CardDefinition.Description))!="未接受变更","Save(false) accepted original card changes");
 Check(db.ChangeTracker.Entries<CardRevisionRecord>().Any(e=>e.State==EntityState.Added),"Save(false) accepted history changes");
 db.ChangeTracker.AcceptAllChanges();var added=new CardDefinition{Id=99599,Name="未接受新增"};db.Add(added);db.SaveChangesAsync(false,default).GetAwaiter().GetResult();Check(db.Entry(added).State==EntityState.Added,"Save(false) accepted insert");
 Check(db.CardRevisions.AsNoTracking().Count(h=>h.CardId==99599)==1,"insert/history written twice");
});
Test("parallel style saves, bindings, preferences, pools and history remain consistent",()=>{
 var connectionString=$"Data Source=review-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Default Timeout=10";
 using var connection=new SqliteConnection(connectionString);connection.Open();var options=new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connectionString).Options;
 using var seed=new ApplicationDbContext(options);seed.Database.EnsureCreated();var actor=new User{Id=Guid.NewGuid(),Username="并发审查",Email="review@example.invalid",PasswordHash="test",Role=UserRole.Admin,IsActive=true};seed.Add(actor);
 foreach(var id in new[]{99411,99412,99413,99414})seed.CardDefinitions.Add(new(){Id=id,Name="并发风格"});seed.SaveChangesAsync().GetAwaiter().GetResult();
 var games=Proxy<IGameService>.Make((m,a)=>m.ReturnType==typeof(Task)?Task.CompletedTask:null);var env=Proxy<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>.Make((m,a)=>null);
 T Controller<T>(T controller) where T:Microsoft.AspNetCore.Mvc.ControllerBase {controller.ControllerContext=new(){HttpContext=new Microsoft.AspNetCore.Http.DefaultHttpContext()};controller.Request.Headers["X-Editor-UserId"]=actor.Id.ToString();return controller;}
 StyleConfiguration Config(int id,int revision,int? bound=null)=>new(id,revision,new(){new("review","审查",new("风格","描述",null,null),bound.HasValue?new(){new(bound.Value,new("衍生","描述",null,null))}:new())});
 Task<Microsoft.AspNetCore.Mvc.IActionResult> Save(StyleConfiguration config)=>Task.Run(async()=>{using var db=new ApplicationDbContext(options);return await Controller(new ShadowverseBeyond.Server.Controllers.CardStylesController(db,games,env)).Save(config.CardId,config);});
 var binding=Task.WhenAll(Save(Config(99411,0,99413)),Save(Config(99412,0,99413))).GetAwaiter().GetResult();Check(binding.Count(r=>r is Microsoft.AspNetCore.Mvc.OkObjectResult)==1&&binding.Count(r=>r is Microsoft.AspNetCore.Mvc.BadRequestObjectResult)==1,"derived card bound to two parents");
 var parent=CardStyleCatalog.Parent(99413)!.Value;var versions=Task.WhenAll(Save(Config(parent,1,99413)),Save(Config(parent,1,99413))).GetAwaiter().GetResult();Check(versions.Count(r=>r is Microsoft.AspNetCore.Mvc.OkObjectResult)==1&&versions.Count(r=>r is Microsoft.AspNetCore.Mvc.ConflictObjectResult)==1,"parallel style revision overwritten");
 Save(Config(99414,0)).GetAwaiter().GetResult();
 Task Choose(int id)=>Task.Run(async()=>{using var db=new ApplicationDbContext(options);Check(await Controller(new ShadowverseBeyond.Server.Controllers.CardStylesController(db,games,env)).Choose(new(id,"review"),default) is Microsoft.AspNetCore.Mvc.OkObjectResult,"choice failed");});
 Task.WhenAll(Choose(parent),Choose(99414)).GetAwaiter().GetResult();Check(CardStyleCatalog.Preferences(actor.Id).Count==2,"parallel preferences lost a choice");
 Task<Microsoft.AspNetCore.Mvc.IActionResult> Pool()=>Task.Run(async()=>{using var db=new ApplicationDbContext(options);return await Controller(new ShadowverseBeyond.Server.Controllers.TakeTwoController(db,new TakeTwoService(new MatchService(NullLogger<MatchService>.Instance)))).SaveSettings("rotation",new(0,TakeTwoService.EmptyPool),default);});
 var pools=Task.WhenAll(Pool(),Pool()).GetAwaiter().GetResult();Check(pools.Count(r=>r is Microsoft.AspNetCore.Mvc.OkObjectResult)==1&&pools.Count(r=>r is Microsoft.AspNetCore.Mvc.ConflictObjectResult)==1,"parallel pool revision overwritten");
 seed.CardRevisions.Where(h=>h.CardId==99414).ExecuteDelete();
 Task<Microsoft.AspNetCore.Mvc.IActionResult> History()=>Task.Run(async()=>{using var db=new ApplicationDbContext(options);return await Controller(new ShadowverseBeyond.Server.Controllers.CardHistoryController(db,Proxy<IGameDataProvider>.Make((m,a)=>null),games,Proxy<IMapper>.Make((m,a)=>null))).List(99414);});
 var histories=Task.WhenAll(History(),History()).GetAwaiter().GetResult();Check(histories.All(r=>r is Microsoft.AspNetCore.Mvc.OkObjectResult)&&seed.CardRevisions.Count(h=>h.CardId==99414)==1,"parallel baseline creation failed");
});
Test("startup preserves shared original card art and explicit remote art", () => {
 var root=Path.Combine(Path.GetTempPath(),"sv-art-"+Guid.NewGuid());
 var source=Path.Combine(root,"assets","cards","1");var target=Path.Combine(root,"assets","cards","2");
 Directory.CreateDirectory(source);Directory.CreateDirectory(target);
 try {
  File.WriteAllText(Path.Combine(source,"preview.png"),"original");File.WriteAllText(Path.Combine(target,"preview.png"),"old alternate");
  var resolve=typeof(ShadowverseBeyond.Server.Infrastructure.Data.DataSeeder).GetMethod("ResolveNormalizedArtPath",BindingFlags.NonPublic|BindingFlags.Static)!;
  Check((string?)resolve.Invoke(null,new object?[]{target,2,"preview","/assets/cards/1/preview.png"})=="/assets/cards/1/preview.png","shared art overwritten");
  Check((string?)resolve.Invoke(null,new object?[]{target,2,"preview","https://example.com/art.png"})=="https://example.com/art.png","remote art overwritten");
  Check((string?)resolve.Invoke(null,new object?[]{target,2,"preview","/missing.png"})=="/assets/cards/2/preview.png","legacy fallback lost");
 } finally { Directory.Delete(root,true); }
});
Test("card history records card and ability saves transactionally",()=>{
 using var connection=new SqliteConnection("Data Source=:memory:");connection.Open();
 using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
 db.Database.EnsureCreated();var card=new CardDefinition{Id=900001,Name="历史测试",Description="旧文本",Cost=1};db.CardDefinitions.Add(card);db.SaveChangesAsync().GetAwaiter().GetResult();
 Check(db.CardRevisions.Count()==1,"create history missing");
 card.Description="新文本";card.UpdatedBy="录卡者";db.SaveChangesAsync().GetAwaiter().GetResult();Check(db.CardRevisions.Count()==2,"edit history missing");
 db.CardAbilities.Add(new AbilityDefinition{Id=90000101,CardId=card.Id,Name="测试能力",JsonConfig="{}"});db.SaveChangesAsync().GetAwaiter().GetResult();Check(db.CardRevisions.Count()==2,"same editor ability save was not merged");
 var latest=db.CardRevisions.OrderByDescending(x=>x.Revision).First();var snapshot=JsonSerializer.Deserialize<CardConfigurationSnapshot>(latest.Snapshot)!;Check(snapshot.Card.Description=="新文本"&&snapshot.Abilities.Count==1,"snapshot incomplete");
 var count=db.CardRevisions.Count();latest.Note="注释";db.SaveChangesAsync().GetAwaiter().GetResult();Check(db.CardRevisions.Count()==count,"annotation created false revision");
});
Test("history merges recent same-editor saves and preserves editor/time boundaries",()=>{
 using var connection=new SqliteConnection("Data Source=:memory:");connection.Open();
 using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);db.Database.EnsureCreated();
 var card=new CardDefinition{Id=900009,Name="合并历史",UpdatedBy="编辑甲"};db.Add(card);db.SaveChangesAsync().GetAwaiter().GetResult();
 card.Description="连续保存";db.SaveChangesAsync().GetAwaiter().GetResult();Check(db.CardRevisions.Count()==1,"new-card edits created duplicate history");
 var latest=db.CardRevisions.Single();latest.Note="保留注释";latest.ModifiedAt=DateTime.UtcNow.AddMinutes(-6);db.SaveChangesAsync().GetAwaiter().GetResult();
 card.Description="间隔保存";db.SaveChangesAsync().GetAwaiter().GetResult();Check(db.CardRevisions.Count()==2,"five-minute boundary ignored");
 card.UpdatedBy="编辑乙";card.Description="另一编辑者";db.SaveChangesAsync().GetAwaiter().GetResult();Check(db.CardRevisions.Count()==3,"different editor merged");
 card.Description="回退边界";db.RevisionOperation="restore";db.SaveChangesAsync().GetAwaiter().GetResult();Check(db.CardRevisions.Count()==4,"restore merged into prior edit");
 Check(db.CardRevisions.First().Note=="保留注释","note lost");
});
Test("history API restores complete configuration and protects the current version",()=>{
 using var connection=new SqliteConnection("Data Source=:memory:");connection.Open();
 using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);db.Database.EnsureCreated();
 var actor=new User{Id=Guid.NewGuid(),Username="测试编辑者",Email="editor@example.invalid",PasswordHash="test-only",Role=UserRole.Player,IsActive=true};db.Users.Add(actor);
 var card=new CardDefinition{Id=900002,Name="回退测试",Description="原始文本",Cost=1};db.CardDefinitions.Add(card);db.SaveChangesAsync().GetAwaiter().GetResult();var old=db.CardRevisions.Single();
 card.Description="修改后";db.CardAbilities.Add(new AbilityDefinition{Id=90000201,CardId=card.Id,Name="新增能力",JsonConfig="{}"});db.SaveChangesAsync().GetAwaiter().GetResult();
 var gameService=Proxy<IGameService>.Make((method,args)=>method.ReturnType==typeof(Task)?Task.CompletedTask:null);
 var provider=Proxy<IGameDataProvider>.Make((method,args)=>method.Name=="get_DataRevision"?Guid.Empty:null);
 var mapper=Proxy<IMapper>.Make((method,args)=>null);
 var controller=new ShadowverseBeyond.Server.Controllers.CardHistoryController(db,provider,gameService,mapper){ControllerContext=new Microsoft.AspNetCore.Mvc.ControllerContext{HttpContext=new Microsoft.AspNetCore.Http.DefaultHttpContext()}};
 controller.Request.Headers["X-Editor-UserId"]=actor.Id.ToString();
 var restored=controller.Restore(card.Id,old.Id,new(2),default).GetAwaiter().GetResult();Check(restored is Microsoft.AspNetCore.Mvc.OkObjectResult,"restore failed");
 Check(db.CardDefinitions.Single().Description=="原始文本"&&!db.CardAbilities.Any(),"configuration not restored");Check(db.CardRevisions.Count()==3,"rollback did not create revision");
 var current=db.CardRevisions.OrderByDescending(h=>h.Revision).First();Check(controller.Delete(card.Id,current.Id,default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.ConflictObjectResult,"current version deleted");
 Check(controller.Restore(card.Id,old.Id,new(2),default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.ConflictObjectResult,"stale revision accepted");
});
Test("card styles remain personal and bound cards inherit only appearance",()=>{
 var owner=Guid.NewGuid();var enemy=Guid.NewGuid();var config=new StyleConfiguration(99001,1,new(){new CardStyle("night","夜色",new("新卡名","展示描述","/assets/new.png",null),new(){new StyleBinding(99002,new("新纹章","纹章描述","/assets/crest.png",null))})});
 CardStyleCatalog.Apply(new FeatureDocument{Id="style:99001",Json=JsonSerializer.Serialize(config,CardStyleCatalog.JsonOptions)});
 CardStyleCatalog.Apply(new FeatureDocument{Id=$"style-prefs:{owner}",Json=JsonSerializer.Serialize(new Dictionary<int,string>{{99001,"night"}},CardStyleCatalog.JsonOptions)});
 Check(CardStyleCatalog.Resolve(owner,99001)?.Name=="新卡名","owner selection absent");Check(CardStyleCatalog.Resolve(owner,99002)?.Name=="新纹章","binding missing");Check(CardStyleCatalog.Resolve(enemy,99001)==null,"enemy's own appearance changed");Check(CardStyleCatalog.Parent(99002)==99001,"bound selector not identified");
 using var connection=new SqliteConnection("Data Source=:memory:");connection.Open();using(var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options)){db.Database.EnsureCreated();db.FeatureDocuments.Add(new(){Id=$"style-prefs:{enemy}",Json="{}"});db.SaveChangesAsync().GetAwaiter().GetResult();Check(db.FeatureDocuments.Count()==1,"appearance preferences did not persist");}
});
Test("style API validates permissions, bindings, revisions and per-account persistence",()=>{
 using var connection=new SqliteConnection("Data Source=:memory:");connection.Open();using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);db.Database.EnsureCreated();
 var editor=new User{Id=Guid.NewGuid(),Username="风格录卡",Email="style-editor@example.invalid",PasswordHash="test",Role=UserRole.Player,IsActive=true};var user=new User{Id=Guid.NewGuid(),Username="风格玩家",Email="style-player@example.invalid",PasswordHash="test",Role=UserRole.Player,IsActive=true};db.Users.AddRange(editor,user);db.CardDefinitions.AddRange(new CardDefinition{Id=99011,Name="主卡"},new CardDefinition{Id=99012,Name="纹章"});db.SaveChangesAsync().GetAwaiter().GetResult();
 var games=Proxy<IGameService>.Make((m,a)=>m.ReturnType==typeof(Task)?Task.CompletedTask:null);var environment=Proxy<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>.Make((m,a)=>null);
 var controller=new ShadowverseBeyond.Server.Controllers.CardStylesController(db,games,environment){ControllerContext=new Microsoft.AspNetCore.Mvc.ControllerContext{HttpContext=new Microsoft.AspNetCore.Http.DefaultHttpContext()}};
 var config=new StyleConfiguration(99011,0,new(){new CardStyle("sun","白昼",new("昼","效果说明","/assets/sun.png",null),new(){new StyleBinding(99012,new("昼纹章","对应文本",null,null))})});
 user.Role=UserRole.Guest;db.SaveChangesAsync().GetAwaiter().GetResult();controller.Request.Headers["X-Editor-UserId"]=user.Id.ToString();Check(controller.Save(99011,config,default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.StatusCodeResult denied&&denied.StatusCode==403,"guest changed shared styles");
 user.Role=UserRole.Player;db.SaveChangesAsync().GetAwaiter().GetResult();controller.Request.Headers["X-Editor-UserId"]=editor.Id.ToString();Check(controller.Save(99011,config,default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.OkObjectResult,"editor could not save");Check(controller.Save(99011,config,default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.ConflictObjectResult,"stale style revision accepted");
 controller.Request.Headers["X-Editor-UserId"]=user.Id.ToString();Check(controller.Choose(new(99011,"sun"),default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.OkObjectResult,"style choice failed");Check(CardStyleCatalog.Resolve(user.Id,99012)?.Name=="昼纹章","choice binding not applied");Check(CardStyleCatalog.Resolve(editor.Id,99012)==null,"another account changed");Check(controller.Choose(new(99012,"sun"),default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.BadRequestObjectResult,"bound card selected directly");
 Check(db.FeatureDocuments.Count(d=>d.Id.StartsWith("style-prefs:"))==1,"preference storage wrong");controller.Choose(new(99011,null),default).GetAwaiter().GetResult();Check(CardStyleCatalog.Resolve(user.Id,99011)==null,"default style could not be restored");
});
Test("Take Two supports multiple rooms and applies current host pool before drafting",()=>{
 var catalog=new List<CardDefinition>();var id=97800;
 foreach(var rarity in new[]{CardRarity.Bronze,CardRarity.Silver,CardRarity.Gold,CardRarity.Legend})for(var j=0;j<12;j++)catalog.Add(new(){Id=id++,Name="房间测试",CardSet="卡包",Class=CardClass.Forestcraft,Type=CardType.Follower,IsCollectible=true,Rarity=rarity});
 var service=new TakeTwoService(new MatchService(NullLogger<MatchService>.Instance));var owner=Guid.NewGuid();
 var first=service.Create(owner,"open",TakeTwoService.EmptyPool,catalog);
 var second=service.Create(owner,"open",TakeTwoService.EmptyPool,catalog);
 Check(first.Id!=second.Id&&!first.Closed,"multiple rooms were blocked or old room closed");
 var guest=Guid.NewGuid();service.Join(first.Code,guest);service.Join(second.Code,guest);
 var pool=TakeTwoService.EmptyPool with{Redraws=new(){{"Forestcraft",1}}};
 service.UpdatePool(first,first.Players[0],"open",pool,catalog);
 Check(first.Players.All(p=>p.Plans["Forestcraft"].Redraws==1),"current pool not applied to both seats");
 Check(second.Players.All(p=>p.Plans["Forestcraft"].Redraws==2),"another room changed");
 var denied=false;try{service.UpdatePool(first,first.Players[1],"open",pool,catalog);}catch(UnauthorizedAccessException){denied=true;}
 Check(denied,"guest changed host pool");
 service.Act(first,first.Players[0],"class",1,"Forestcraft",0);
 var locked=false;try{service.UpdatePool(first,first.Players[0],"open",pool,catalog);}catch(InvalidOperationException){locked=true;}
 Check(locked,"pool changed after picks began");
 service.Act(first,first.Players[0],"leave",999,null,0);service.Act(first,first.Players[0],"leave",0,null,0);
 Check(first.Closed&&!second.Closed,"leave was not idempotent or affected another room");
});
Test("Take Two plans exclude duplicates and redraws exclude the previous four",()=>{
 var catalog=new List<CardDefinition>();var id=98000;foreach(var rarity in new[]{CardRarity.Bronze,CardRarity.Silver,CardRarity.Gold,CardRarity.Legend})for(var j=0;j<10;j++)catalog.Add(new(){Id=id++,Name="选牌",Class=CardClass.Forestcraft,Type=CardType.Follower,IsCollectible=true,Rarity=rarity});
 var seat=TakeTwoService.Prepare(Guid.NewGuid(),catalog,TakeTwoService.EmptyPool);Check(seat.Plans.Count==1,"incomplete profession offered");var plan=seat.Plans["Forestcraft"];Check(plan.Core.Length==2&&catalog.Single(c=>c.Id==plan.Core[0]).Rarity==CardRarity.Legend,"legendary initial core missing");
 foreach(var round in plan.Rounds){var prior=round.Offer.SelectMany(x=>x).ToArray();Check(prior.Distinct().Count()==4,"same card on both sides");foreach(var redraw in round.RedrawOffers){var next=redraw.SelectMany(x=>x).ToArray();Check(next.Distinct().Count()==4&&!next.Intersect(prior).Any(),"redraw repeated previous four");prior=next;}}
 var matches=new MatchService(NullLogger<MatchService>.Instance);var service=new TakeTwoService(matches);var host=Guid.NewGuid();var guest=Guid.NewGuid();var room=service.Create(host,"open",TakeTwoService.EmptyPool,catalog);
 var blocked=false;try{service.Act(room,room.Players[0],"class",1,"Forestcraft",0);}catch(InvalidOperationException e){blocked=e.Message.Contains("等待对手");}
 Check(blocked&&room.Players[0].Cards.Count==0&&room.Players[0].Sequence==0,"host drafted before opponent joined");
 var waiting=JsonSerializer.SerializeToElement(service.View(room,room.Players[0]));Check(!waiting.GetProperty("opponentJoined").GetBoolean()&&waiting.GetProperty("role").GetString()=="host","waiting room role wrong");
 service.Join(room.Code,guest);
 foreach(var player in room.Players){service.Act(room,player,"class",1,"Forestcraft",0);service.Act(room,player,"class",1,"Forestcraft",0);Check(player.Cards.Count==2,"retry applied twice");for(var r=0;r<14;r++)service.Act(room,player,"pick",r+2,null,r%2);Check(player.Cards.Count==30,"draft deck size incorrect");service.Act(room,player,"ready",16,null,0);}
 Check(room.Match?.Status==MatchStatus.Started,"native game bridge not started");Check(room.Match.HostDeckSelection.GeneratedDraftCardIds?.Length==30,"server-built deck lost when cloned");
 Check(!JsonSerializer.Serialize(room.Match.HostDeckSelection).Contains("GeneratedDraftCardIds"),"draft deck leaked in room response");
});
Test("Take Two local test has independent seats and requires host credentials",()=>{
 var catalog=new List<CardDefinition>();var id=98000;
 foreach(var rarity in new[]{CardRarity.Bronze,CardRarity.Silver,CardRarity.Gold,CardRarity.Legend}) for(var i=0;i<12;i++) catalog.Add(new(){Id=id++,Name="本机测试",Class=CardClass.Forestcraft,Type=CardType.Follower,IsCollectible=true,Rarity=rarity});
 var localMatches=new MatchService(NullLogger<MatchService>.Instance);var service=new TakeTwoService(localMatches);var owner=Guid.NewGuid();var room=service.Create(owner,"open",TakeTwoService.EmptyPool,catalog);var host=room.Players[0];
 var denied=false;try{service.JoinLocalTest(room,Guid.NewGuid(),host.Token);}catch(UnauthorizedAccessException){denied=true;}Check(denied&&room.Players.Count==1,"stranger created a local seat");
 var guest=service.JoinLocalTest(room,owner,host.Token);Check(guest.PlayerId!=host.PlayerId&&guest.Token!=host.Token,"local seats share identity");Check(service.Authorize(room,owner,guest.Token)==guest&&service.Authorize(room,owner,host.Token)==host,"owner cannot independently operate local seats");
 Check(service.JoinLocalTest(room,owner,host.Token)==guest&&room.Players.Count==2,"reopening local test added another seat");
 service.Act(room,host,"class",1,"Forestcraft",0);Check(guest.Cards.Count==0&&guest.Sequence==0,"host draft altered guest");
 service.Act(room,guest,"class",1,"Forestcraft",0);
 foreach(var seat in room.Players){for(var r=0;r<14;r++)service.Act(room,seat,"pick",r+2,null,r%2);service.Act(room,seat,"ready",16,null,0);}
 Check(room.Match?.Status==MatchStatus.Started&&room.Match.HostPlayerId!=room.Match.GuestPlayerId,"local test did not start independent battle players");
 Check(room.Match!.GuestDeckSelection.UserId==owner,"local test lost account appearance preferences");
 foreach(var seat in room.Players){var hs=JsonSerializer.SerializeToElement(service.View(room,seat,false)).GetProperty("handshake");Check(hs.GetProperty("isTakeTwo").GetBoolean()&&!hs.GetProperty("canRematch").GetBoolean(),"Take Two local seat handshake must disable rematch");}
 var completedLocal=localMatches.CompleteGame(room.Match.ActiveGameId!,host.PlayerId).Room!;
 Check(completedLocal.Status==MatchStatus.Started&&completedLocal.HostWins==1,"Take Two completion should record win without unlocking room rematch");
 var pvp=service.Create(Guid.NewGuid(),"open",TakeTwoService.EmptyPool,catalog);service.Join(pvp.Code,Guid.NewGuid());var blocked=false;try{service.JoinLocalTest(pvp,pvp.Players[0].PlayerId,pvp.Players[0].Token);}catch(InvalidOperationException){blocked=true;}Check(blocked&&pvp.Players.Count==2,"local test replaced a real opponent");
});
Test("Take Two pool management is admin-only and saved pools are account-specific",()=>{
 using var connection=new SqliteConnection("Data Source=:memory:");connection.Open();using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);db.Database.EnsureCreated();
 var admin=new User{Id=Guid.NewGuid(),Username="卡池管理员",Email="pool-admin@example.invalid",PasswordHash="test",Role=UserRole.Admin,IsActive=true};var player=new User{Id=Guid.NewGuid(),Username="卡池玩家",Email="pool-player@example.invalid",PasswordHash="test",Role=UserRole.Player,IsActive=true};db.Users.AddRange(admin,player);db.SaveChangesAsync().GetAwaiter().GetResult();
 var service=new TakeTwoService(new MatchService(NullLogger<MatchService>.Instance));var controller=new ShadowverseBeyond.Server.Controllers.TakeTwoController(db,service){ControllerContext=new Microsoft.AspNetCore.Mvc.ControllerContext{HttpContext=new Microsoft.AspNetCore.Http.DefaultHttpContext()}};
 var settings=TakeTwoService.EmptyPool with{Redraws=new(){{"Forestcraft",4}}};controller.Request.Headers["X-Editor-UserId"]=player.Id.ToString();Check(controller.SaveSettings("rotation",new(0,settings),default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.StatusCodeResult denied&&denied.StatusCode==403,"player edited system pool");
 var poolId=Guid.NewGuid();Check(controller.SavePool(poolId,new("我的卡池",settings),default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.OkObjectResult,"personal pool could not be saved");Check(db.FeatureDocuments.Any(d=>d.Id==$"take-two-saved:{player.Id}:{poolId}"),"personal account namespace missing");
 controller.Request.Headers["X-Editor-UserId"]=admin.Id.ToString();Check(controller.SaveSettings("rotation",new(0,settings),default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.OkObjectResult,"admin pool save failed");Check(controller.SaveSettings("rotation",new(0,settings),default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.ConflictObjectResult,"pool revision overwritten");
 controller.DeletePool(poolId,default).GetAwaiter().GetResult();Check(db.FeatureDocuments.Any(d=>d.Id==$"take-two-saved:{player.Id}:{poolId}"),"another account deleted the saved pool");
});
Test("AS2P uses unrestricted rounds and guarantees a legend on rounds 7 and 14 including redraws",()=>{
 var catalog=new List<CardDefinition>();var id=97000;
 foreach(var cls in new[]{CardClass.Forestcraft,CardClass.Swordcraft}) foreach(var rarity in new[]{CardRarity.Bronze,CardRarity.Gold,CardRarity.Legend}) for(var i=0;i<12;i++) catalog.Add(new(){Id=id++,Name="AS2P测试",Class=cls,Type=CardType.Follower,IsCollectible=true,Rarity=rarity,CardSet="AS2P包"});
 var neutral=new CardDefinition{Id=id++,Name="精灵专属中立",Class=CardClass.Neutral,Type=CardType.Follower,IsCollectible=true,Rarity=CardRarity.Legend,CardSet="其他包"};catalog.Add(neutral);
 var common=new CardDefinition{Id=id++,Name="共同中立",Class=CardClass.Neutral,Type=CardType.Spell,IsCollectible=true,Rarity=CardRarity.Gold,CardSet="AS2P包"};catalog.Add(common);
 var settings=TakeTwoService.EmptyPool with {Packs=["AS2P包"],NeutralByClass=new(){{"Forestcraft",[neutral.Id]}}};
 var pool=TakeTwoService.PoolCards(catalog,"as2p",settings);
 Check(pool.Any(c=>c.Id==neutral.Id),"neutral supplement outside packs absent");
 Check(TakeTwoService.ClassPool(pool,"as2p",settings,CardClass.Forestcraft).Any(c=>c.Id==neutral.Id),"assigned neutral absent");
 Check(!TakeTwoService.ClassPool(pool,"as2p",settings,CardClass.Swordcraft).Any(c=>c.Id==neutral.Id),"neutral leaked to unassigned class");
 Check(TakeTwoService.ClassPool(pool,"as2p",settings,CardClass.Swordcraft).Any(c=>c.Id==common.Id),"shared base neutral absent");
 var seat=TakeTwoService.Prepare(Guid.NewGuid(),pool,settings,"as2p");Check(seat.Plans.Count==2,"AS2P incorrectly required silver cards");
 foreach(var plan in seat.Plans.Values) for(var index=0;index<14;index++) {
  var round=plan.Rounds[index];int[]? previous=null;
  foreach(var offer in new[]{round.Offer}.Concat(round.RedrawOffers)) {
   var four=offer.SelectMany(x=>x).ToArray();Check(four.Distinct().Count()==4,"duplicate offer");Check(previous==null||!four.Intersect(previous).Any(),"redraw repeated prior four");
   if(previous==null && index is 6 or 13) {var cards=four.Select(id=>catalog.Single(c=>c.Id==id)).ToArray();Check(cards.All(c=>c.Rarity is CardRarity.Gold or CardRarity.Legend)&&cards.Any(c=>c.Rarity==CardRarity.Legend),"special offer rarity guarantee lost");}
   previous=four;
  }
 }
 var small=catalog.Where(c=>c.Class==CardClass.Forestcraft&&c.Rarity!=CardRarity.Legend).Concat(catalog.Where(c=>c.Class==CardClass.Forestcraft&&c.Rarity==CardRarity.Legend).Take(2)).ToList();
 for(var attempt=0;attempt<20;attempt++) Check(TakeTwoService.Prepare(Guid.NewGuid(),small,settings,"as2p").Plans.Count==1,"two-legend pool randomly became unavailable during redraw generation");
 Check(TakeTwoService.PoolCards(catalog,"as2p",TakeTwoService.EmptyPool).Count==0,"unconfigured AS2P silently used full catalog");
 var roomService=new TakeTwoService(new MatchService(NullLogger<MatchService>.Instance));var room=roomService.Create(Guid.NewGuid(),"as2p",settings,catalog);roomService.Join(room.Code,Guid.NewGuid());Check(room.Players.All(p=>p.Plans.Count==2)&&room.Environment=="as2p","room/join lost AS2P rules");
});
Test("redraw ignores rarity and excludes only the current four",()=>{
 foreach(var environment in new[]{"rotation","unlimited","open"}) {
  var cards=new List<CardDefinition>();var id=98000;
  foreach(var rarity in new[]{CardRarity.Bronze,CardRarity.Silver,CardRarity.Gold,CardRarity.Legend}) for(var i=0;i<4;i++) cards.Add(new(){Id=id++,Class=CardClass.Forestcraft,Rarity=rarity,IsCollectible=true,Type=CardType.Follower});
  var seat=TakeTwoService.Prepare(Guid.NewGuid(),cards,TakeTwoService.EmptyPool,environment);
  var round=seat.Plans["Forestcraft"].Rounds[0];
  var initial=round.Offer.SelectMany(x=>x).ToHashSet();var redraw=round.RedrawOffers[0].SelectMany(x=>x).ToArray();
  Check(initial.Count==4 && initial.All(id=>cards.Single(c=>c.Id==id).Rarity==CardRarity.Bronze),"initial rarity changed");
  Check(redraw.Length==4 && redraw.Distinct().Count()==4 && redraw.All(id=>!initial.Contains(id)),"redraw duplicates current offer");
  Check(redraw.All(id=>cards.Single(c=>c.Id==id).Rarity!=CardRarity.Bronze),"redraw still constrained to bronze");
 }
 var asCards=new List<CardDefinition>();var asId=99000;
 foreach(var entry in new[]{(CardRarity.Bronze,4),(CardRarity.Gold,2),(CardRarity.Legend,2)}) for(var i=0;i<entry.Item2;i++) asCards.Add(new(){Id=asId++,Class=CardClass.Forestcraft,Rarity=entry.Item1,IsCollectible=true,Type=CardType.Follower});
 var asPlan=TakeTwoService.Prepare(Guid.NewGuid(),asCards,TakeTwoService.EmptyPool,"as2p").Plans["Forestcraft"];
 foreach(var index in new[]{6,13}) {
  var round=asPlan.Rounds[index];var initial=round.Offer.SelectMany(x=>x).ToHashSet();var redraw=round.RedrawOffers[0].SelectMany(x=>x).ToArray();var second=round.RedrawOffers[1].SelectMany(x=>x).ToHashSet();
  Check(initial.Count==4 && initial.All(id=>asCards.Single(c=>c.Id==id).Rarity is CardRarity.Gold or CardRarity.Legend),"AS2P initial special rule changed");
  Check(redraw.Distinct().Count()==4 && redraw.All(id=>asCards.Single(c=>c.Id==id).Rarity==CardRarity.Bronze),"AS2P redraw must use unrestricted pool");
  Check(second.SetEquals(initial),"second redraw excludes more than the current four");
 }

});
Test("AS2P settings are admin-only and validate neutral assignments",()=>{
 using var connection=new SqliteConnection("Data Source=:memory:");connection.Open();using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);db.Database.EnsureCreated();
 var admin=new User{Id=Guid.NewGuid(),Username="AS2P管理员",Email="as2p-admin@example.invalid",PasswordHash="test",Role=UserRole.Admin,IsActive=true};var player=new User{Id=Guid.NewGuid(),Username="AS2P玩家",Email="as2p-player@example.invalid",PasswordHash="test",Role=UserRole.Player,IsActive=true};db.Users.AddRange(admin,player);db.CardDefinitions.AddRange(new CardDefinition{Id=96901,Name="中立",Class=CardClass.Neutral,Type=CardType.Follower,IsCollectible=true},new CardDefinition{Id=96902,Name="精灵",Class=CardClass.Forestcraft,Type=CardType.Follower,IsCollectible=true});db.SaveChangesAsync().GetAwaiter().GetResult();
 var service=new TakeTwoService(new MatchService(NullLogger<MatchService>.Instance));var controller=new ShadowverseBeyond.Server.Controllers.TakeTwoController(db,service){ControllerContext=new Microsoft.AspNetCore.Mvc.ControllerContext{HttpContext=new Microsoft.AspNetCore.Http.DefaultHttpContext()}};
 var settings=TakeTwoService.EmptyPool with {NeutralByClass=new(){{"Forestcraft",[96901]}}};controller.Request.Headers["X-Editor-UserId"]=player.Id.ToString();Check(controller.SaveSettings("as2p",new(0,settings),default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.StatusCodeResult denied&&denied.StatusCode==403,"player edited AS2P settings");
 controller.Request.Headers["X-Editor-UserId"]=admin.Id.ToString();Check(controller.SaveSettings("as2p",new(0,settings),default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.OkObjectResult,"admin could not configure AS2P");
 var saved=JsonSerializer.Deserialize<TakeTwoPool>(db.FeatureDocuments.Single(d=>d.Id=="take-two:as2p").Json,CardStyleCatalog.JsonOptions)!;Check(saved.NeutralByClass["Forestcraft"].Single()==96901,"neutral assignments not persisted");
 Check(controller.SaveSettings("as2p",new(1,settings with{NeutralByClass=new(){{"Forestcraft",[96902]}}}),default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.BadRequestObjectResult,"non-neutral assignment accepted");
 Check(controller.SaveSettings("as2p",new(1,settings with{NeutralByClass=new(){{"99",[96901]}}}),default).GetAwaiter().GetResult() is Microsoft.AspNetCore.Mvc.BadRequestResult,"unknown class accepted");
});
AbilityConfig Choice() => new() { Targets = new() { ["one"] = new() { PlayerChoiceOptions = new() { IsRequired = true } } } };
Test("skip direct target selection", () => Check(AbilitySelectionPolicy.RequiresPlayerSelection(Choice()),"target"));
Test("skip mode after earlier effects atomically", () => Check(AbilitySelectionPolicy.RequiresPlayerSelection(new() { Effects = new() { new() {Type=EffectType.Heal}, new() {Type=EffectType.Choose} } }),"mode"));
Test("allow automatic targets", () => Check(!AbilitySelectionPolicy.RequiresPlayerSelection(new() {Targets=new(){["one"]=new(){PlayerChoiceOptions=new(){IsRequired=false}}}}),"auto"));
Test("ignore disabled target", () => { var c=Choice();c.Targets["one"].IsEnabled=false;Check(!AbilitySelectionPolicy.RequiresPlayerSelection(c),"disabled"); });
Test("skip choice in chained ability", () => Check(AbilitySelectionPolicy.RequiresPlayerSelection(new(){Effects=new(){new(){Type=EffectType.Draw,ChainedAbilities=new(){Choice()}}}}),"chain"));
foreach(var type in new[]{EffectType.Sequence,EffectType.Random}) {
 Test($"skip choice inside {type}",()=> Check(AbilitySelectionPolicy.RequiresPlayerSelection(new(){Effects=new(){new(){Type=type,Parameters=new(){["AbilityConfigs"]=new StaticValueConfig{Value=new[]{Choice()}}}}}}),"branch"));
 Test($"allow pure branches inside {type}",()=> Check(!AbilitySelectionPolicy.RequiresPlayerSelection(new(){Effects=new(){new(){Type=type,Parameters=new(){["AbilityConfigs"]=new StaticValueConfig{Value=new[]{new AbilityConfig()}}}}}}),"pure"));
}
Test("serialized branch inspection",()=>Check(AbilitySelectionPolicy.RequiresPlayerSelection(new(){Effects=new(){new(){Type=EffectType.Sequence,Parameters=new(){["AbilityConfigs"]=new StaticValueConfig{Value=JsonSerializer.Deserialize<JsonElement>("[{\"targets\":{\"a\":{\"playerChoiceOptions\":{\"isRequired\":true}}}}]")}}}}}),"json"));
Test("cycle cannot partially invoke",()=>{var c=new AbilityConfig();c.Effects.Add(new(){ChainedAbilities=new(){c}});Check(AbilitySelectionPolicy.RequiresPlayerSelection(c),"cycle");});
Test("legacy defaults keep trigger behavior",()=>Check(!new TriggerAbilityEffectParams().SkipTargetAndModeSelection,"default"));
foreach(var keyword in new[]{AbilityKeyword.Accelerate,AbilityKeyword.Crystalize}) Test($"normalize {keyword} effect declaration",()=>{var c=new AbilityConfig{AbilityKeywords=new(){new(){Keyword=keyword,Value=0}},Effects=new(){new(){Type=keyword==AbilityKeyword.Accelerate?EffectType.Accelerate:EffectType.Crystalize,Parameters=new(){["CardDefinitionId"]=new StaticValueConfig{Value=7}}}}};c.NormalizeKeywords();Check(c.AlternatePlay?.PlayAsCardDefinitionId==7,"declaration lost");});

var definitions=new Dictionary<int,CardDefinition>();
for(int i=1;i<=9;i++)definitions[i]=new(){Id=i,Name=$"Card {i}",Cost=i,Type=CardType.Follower,Attack=i,Health=i+1};
definitions[7].ArtPreview="/original.png";definitions[8].ArtPreview="/spell.png";definitions[9].ArtPreview="/amulet.png";
definitions[8].Type=CardType.Spell;definitions[9].Type=CardType.Amulet;
var data=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch {"GetCardDefinition"=>definitions.GetValueOrDefault((int)args![0]!),"GetAbilityConfigs"=>new List<AbilityConfig>(),"GetAllCardDefinitions"=>definitions,"get_Cards"=>definitions.Values.ToList(),_=>null});
GameDataProvider.SetInstanceForTesting(data);
Test("terminal transactions retain victory once and skip queued actions only after game over",AiEndTurnEngineTests.TerminalBoundary);
Test("chained destruction after copy transform preserves the original effect source", ChainedTransformTests.DestroyReanimatedOriginalAfterCopyTransform);
Test("runtime AI facts track actual grants, removals and counts without stale caches",AiRuntimeAbilityFactsTests.RuntimeChanges);
Test("runtime AI facts exclude hidden zones and bound complete instance configurations",AiRuntimeAbilityFactsTests.PrivacyAndBudget);
Test("runtime AI facts keep duplicate grants and suppression separate by instance",AiRuntimeAbilityFactsTests.DuplicatesAndSuppression);
Test("ability owner perspective reverses correctly for enemy and observer seats without identifiers",AiRuntimeAbilityFactsTests.OwnerRelativePerspective);
Test("learned value afterstates match native basic combat and resource features",AiModelInferenceTests.NativeMechanics);
Test("imported model inputs recognize native shields and consumed attack opportunities",AiModelInferenceTests.ShieldAndModelInputs);
Test("Eternal resource core controls mulligan, retention and setup without leaking across actors",AiEternalEngineTests.CorePolicy);
Test("Emergency advice retains a bound use only when no native defense is proven",AiEternalRecoveryTests.Boundaries);
Test("Production training Start accepts four LLM games and guards ordinary training",AiEvaluationLimitTests.Run);
Test("Static player-scoped draw and crest do not suppress card-target scores",AiCompositeTargetTests.Boundaries);
Test("Native self-expiry valuation rejects conditional, quota, choice and unrelated targets",AiTurnFiveAuditTests.Boundaries);
Test("Recovery policy ablation isolates current and pre-fix actors without model fitting",AiRecoveryAblationTests.Boundaries);
Test("Native summon mode facts distinguish current lethal from resource setup",AiEternalEngineTests.SummonModeRules);
Test("native decision facts bind exact branches under a shared whole-rule budget",AiDecisionFactsTests.ExactBindingAndBudgets);
Test("native choice flow preserves ordered proposals without fabricating settled effects",AiDecisionFlowTests.Boundaries);
Test("mode cache and committed experience track actual native rule identity",AiDecisionFactsTests.ModelAndExperience);
Test("generated Storm recipes read actual effects and reject unsafe dynamic shapes",AiGeneratedTokenTests.ConfigurationGuards);
Test("generated-token decision bindings never leak after a submitted action",AiGeneratedTokenTests.DirectiveScope);
Test("local learning keeps deck specialists independent and active weights immutable",AiValueTrainingTests.IndependentDecks);
Test("local learning rejects held-out regressions and insufficient independent games",AiValueTrainingTests.RejectRegressions);
Test("local learning rejects malformed samples and cancellation",AiValueTrainingTests.RejectInvalidSamples);
Test("local numeric training replay binds exact model bytes and preserves complete data",AiTrainingReplayTests.RoundTripAndIdentity);
Test("training diagnostics reproduce epoch and observation-stratum validation",AiTrainingReplayTests.EpochDiagnostics);
Test("value fitting keeps the best whole-game checkpoint rather than the final epoch", AiTrainingReplayTests.BestCheckpoint);
Test("training keeps bounded, balanced settled-position strata and exact outcome labels",AiTrainingPositionsTests.BoundedStrataAndLabels);
Test("training fills sparse strata and rejects invalid observations",AiTrainingPositionsTests.SparseAndInvalidObservations);
Test("training observes only successful completed local actions and releases exact leases",AiTrainingPositionsTests.SuccessfulCompletionAndLeases);
Test("model promotion requires complete balanced evidence and no deck/role regression",AiModelEvaluationTests.StrengthGate);
Test("LLM benchmark only scores complete four-game groups with the same actual opening",AiModelEvaluationTests.StrategyBenchmarkGroups);
Test("LLM experience pairs exclude incomplete groups, changed openings, and history in controls",AiModelEvaluationTests.StrategyExperiencePairs);
Test("benchmark mulligan is repeatable and independent of the other seat's submission order",AiModelEvaluationTests.SeededMulligan);
Test("seeded battle sampling is reproducible without consuming settlement randomness in previews",AiSeededBattleTests.PreviewAndTransactionIsolation);
Test("LLM opening evidence fingerprints actual ordered hands and decks without runtime identities",AiModelEvaluationTests.OpeningEvidence);
Test("unvalidated model artifacts stay in isolated training seats and trial files",AiModelEvaluationTests.IsolatedModelsAndTrialPersistence);
Test("native projection comparison isolates seats and counts only exact committed actions",AiCombatEvaluationTests.IsolationAndCommitMatching);
Test("expanded combat and multi-attack lethal agree with native transactions",()=>AiExpandedCombatTests.Native());
Test("search identity keeps changed combat resources distinct and comparison seats isolated",AiExpandedCombatTests.KeysAndComparisonIsolation);
Test("finite precombat projection rejects unsupported conditions, chain, shared-name transaction limits, silence and stale rules",AiCombatProjectionTests.Guards);
Test("situational experience retrieves observed relevant cases without hidden zones or outcome ranking",AiLessonRetrievalTests.RankingAndBoundaries);
Test("finite trigger quotas match native attacks, isolate branches and refresh after commits",()=>AiCombatLimitTests.Native());
Test("situational experience reaches planning and discover with unchanged request cap",()=>AiLessonRetrievalTests.PlannerAndChoice());
Test("verified end-turn draw sources retrieve the exact settled action during planning",AiLessonRetrievalTests.DrawSourcePlanning);
Test("precombat removal, health, healing and protections agree with native transactions",AiCombatRemovalTests.NativeEffectsAndProtections);
Test("basic combat threat matches native legal attack routes including zero attack",AiCombatThreatTests.NativeSequences);
Test("combat threat avoids false resource emergencies and preserves bounded uncertainty",AiCombatThreatTests.ResourcesAndBounds);
Test("combat policy comparisons isolate concurrent execution scopes",AiCombatThreatTests.ScopeIsolation);
Test("local search finds useful zero-attack routes without promoting no-op attacks",AiCombatThreatTests.ZeroAttackPlans);
Test("learned search influences close decisions while preserving lethal, defense and reserves",AiSearchValueTests.BoundedDecisions);
Test("search captures one exact deck network and frozen model artifact",AiSearchValueTests.CapturedDeckAndArtifact);
Test("end-turn value features include isolated sequential damage and death resources",AiSearchValueTests.EndTurnValueCounters);
Test("compact combat threat matches exhaustive basic-action enumeration",AiCombatThreatTests.ExhaustiveSmallBoards);
Test("root action outcomes require completion and resist overlapping and subsequent action races",AiActionOutcomeTests.RootCompletionAndRaces);
Test("completed AI decisions keep their exact local alternative and observed results in learning",AiActionOutcomeTests.CompletedDecisionAttribution);
Test("committed discovers match the actual hand card and local alternative without leaking instances",AiActionOutcomeTests.CommittedDiscoverComparison);
Test("terminal outcomes persist once with bounded public facts and hidden targets excluded",AiActionOutcomeTests.TerminalPersistenceAndPrivacy);
Test("completed own hand endpoints preserve native identities, cost, keywords and bounded history without hidden data",AiActionOutcomeTests.OwnHandEndpoints);
Test("retrieved middle observations stay in bounded model excerpts with original retained indices",AiLessonRetrievalTests.ExcerptBoundaries);
Test("same retrieved episode reaches planning and discovery without extra model requests",()=>AiLessonRetrievalTests.ExcerptPlannerAndChoice());
Test("unlimited self damage rewrites agree with native combat and conservatively reject unknown interventions",()=>AiZeroDamageTests.Native());
Test("native AI avoids a health-losing attack into an unconditional zero-damage Ward",()=>AiZeroDamagePolicyProbe.Run());
Test("historical model prose is preserved for review but excluded from live playing facts",AiLearningEvidenceTests.ProseIsReviewMaterial);
Test("accepted planning explanations retain complete history with explicit archive bounds",AiPlanArchiveTests.Run);
Test("committed public leader health is owner-bound, deduplicated and retained with coverage",AiLearningEvidenceTests.NativeLeaderHealthIsCommittedAndBounded);
Test("strategy planner isolates hidden information and reuses finished match experience", AiStrategyTests.Run);
Test("strategy benchmark freezes learning inputs across games while preserving results", AiStrategyTests.FrozenBenchmarkExperience);
Test("rule-identical guides persist without age retries while failures cool down and coalesce", AiStrategyTests.GuideReuseAndCooldown);
Test("current-rule learning excludes obsolete outcome statistics without deleting history", AiStrategyTests.CompatibleExperience);
Test("AI-only builds preserve rule identity while changed rule/compiler inputs expire it",AiEngineIdentityTests.Separation);
Test("guide cache binds exact prompt, verified routes and resource principles",AiEngineIdentityTests.GuideInputs);
Test("committed own draws can be reconciled without accepting unbound hand changes",AiDrawEvidenceTests.Run);
Test("AI rule identity covers configurations while ignoring editor/media metadata",AiDefinitionRulesTests.SemanticIdentity);
Test("AI rule identity caches coherent provider revisions",AiDefinitionRulesTests.RevisionCache);
Test("AI guide and experience reuse require current definition identity",AiDefinitionRulesTests.GuideAndExperience);
Test("AI configuration facts preserve actual conditions, effects, targets and budget coverage",AiDefinitionRulesTests.ConfigurationFacts);
Test("AI recursively resolves actual definition ID references without evaluating dynamic DSL",AiDefinitionRulesTests.DefinitionDependencies);
if(Environment.GetEnvironmentVariable("AI_RULE_IDENTITY_SOURCE") is {Length:>0} identitySource
    &&Environment.GetEnvironmentVariable("AI_RULE_IDENTITY_OUTPUT") is {Length:>0} identityOutput)
 Test("native loaded catalog has a stable cached definition fingerprint",()=>AiDefinitionRulesTests.NativeCatalog(identitySource,identityOutput));
Test("long AI traces preserve opening and late-game evidence with explicit omissions",AiExperienceTraceTests.LongGame);
Test("AI comparisons require the exact committed action and preserve resource context",AiExperienceTraceTests.CommitMatching);
Test("all-rejected model matches retain fallback facts without claiming successful advice",AiExperienceTraceTests.RejectedMatch);
Test("paid provider probes cannot start accidentally from ordinary offline tests",()=>{
 var previous=Environment.GetEnvironmentVariable("AI_ALLOW_PAID_PROBES");
 try {
  Environment.SetEnvironmentVariable("AI_ALLOW_PAID_PROBES",null);
  foreach(var run in new Action<string>[] {AiPlayBindingProbe.Run,AiRuleReasoningProbe.Run}) {
   var rejected=false;
   try{run("unused-paid-probe.json");}catch(InvalidOperationException error){rejected=error.Message.Contains("AI_ALLOW_PAID_PROBES");}
   Check(rejected,"real provider probe did not reject before key lookup or network calls");
  }
 } finally {Environment.SetEnvironmentVariable("AI_ALLOW_PAID_PROBES",previous);}
});
Test("AI binds exact hand instances and validated play forms, then expires stale advice", AiPlayBindingTests.Run);
var activeAbilities = new List<IAbility>();
var abilityManager=Proxy<IAbilityManager>.Make((method,args)=>method.Name switch {"GetAbilitiesForCard" or "GetEffectiveAbilitiesForCard" or "CreateAbilitiesForCard"=>activeAbilities,_=>null});
var buffs=new BuffManager(NullLogger<BuffManager>.Instance,abilityManager);
var buffFactory=new BuffFactory(NullLogger<BuffFactory>.Instance,data);
var evaluator=new BuffEvaluator(Proxy<IConditionEvaluator>.Make((m,a)=>true),NullLogger<BuffEvaluator>.Instance);
var buffService=new BuffService(buffs,evaluator,buffFactory,NullLogger<BuffService>.Instance,new BuffStackingEngine(NullLogger<BuffStackingEngine>.Instance));
var factory=new CardFactory(NullLogger<CardFactory>.Instance,buffFactory,buffs);
var compiler=new DslCompiler(NullLogger<DslCompiler>.Instance,new FunctionRegistry(NullLogger<FunctionRegistry>.Instance),new DslExpressionCache(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())),data);
var accessor=new GameContextAccessor(); GameServiceProvider.ContextAccessor=accessor;
var services=new ServiceCollection().AddSingleton(data).AddSingleton<IBuffService>(buffService).AddSingleton<ICardFactory>(factory).AddSingleton<IDslCompiler>(compiler).AddSingleton<IConditionEvaluator>(new DslConditionEvaluator(compiler,NullLogger<DslConditionEvaluator>.Instance)).AddSingleton<IEventHistoryService>(Proxy<IEventHistoryService>.Make((m,a)=>null)).BuildServiceProvider();
var p1=new PlayerState(Guid.NewGuid());var p2=new PlayerState(Guid.NewGuid());
var game=new GameState("migration-tests",p1,p2,p1.PlayerId){AbilityManager=abilityManager,BuffManager=buffs};
p1.Leader=factory.CreateAndRegisterNewCard(game,1,p1.PlayerId,CardZone.Staging);p2.Leader=factory.CreateAndRegisterNewCard(game,1,p2.PlayerId,CardZone.Staging);
using var context=new GameContext(services,new MapperConfiguration(cfg=>{}).CreateMapper(),accessor,game,p1.PlayerId,sourceCard:p1.Leader);
Test("discover requires selection, honors automatic selection and resolves the owning player",()=>{
 Check(AbilitySelectionPolicy.RequiresPlayerSelection(new(){Effects=new(){new(){Type=EffectType.Discover}}}),"discover selection was not detected");
 var handler=new DiscoverEffectHandler(NullLogger<DiscoverEffectHandler>.Instance);
 using var noSource=context with{SourceCard=null};
 var ability=Proxy<IAbility>.Make((m,a)=>m.Name=="get_SourceCard"?p2.Leader:null);
 foreach(var effect in new[]{new Effect(p1.PlayerId){SourceAbility=ability},new Effect(p2.PlayerId)}) {
  var request=handler.Apply(effect,noSource,new(){CardDefinitionIds=new(){2,3,4}}).GeneratedEvents.OfType<DiscoverRequestEvent>().Single();
  Check(request.Source?.Player==p2,"discover used the current turn's player");
  using var automatic=noSource with{InvocationOptions=new(){AutoRandomTargetSelection=true}};request.ResolveAsync(automatic).GetAwaiter().GetResult();Check(request.Selected!=null&&new[]{2,3,4}.Contains(request.Selected.CurrentDefinitionId),"automatic discover requested manual input");
 }
});
var reviewManager=new AbilityManager(NullLogger<AbilityManager>.Instance,Proxy<IAbilityFactory>.Make((m,a)=>null),Array.Empty<IAbility>());
IAbility ReviewAbility(long id,Func<TargetResolutionResult> resolve){var runtimeId=Guid.NewGuid();return Proxy<IAbility>.Make((m,a)=>m.Name switch{
 "get_RuntimeId"=>runtimeId,"get_DefinitionId"=>id,"get_SourceCard"=>p1.Leader,"get_Keywords"=>new List<AbilityKeyword>{AbilityKeyword.Intromezzo},"get_Targets"=>new List<ShadowverseBeyond.Server.Domain.Abilities.Runtime.Target>(),
 "get_TriggerConfig"=>new ShadowverseBeyond.Server.Domain.Abilities.Triggers.TriggerConfig{LimitSameNameOncePerTransaction=true},"CheckConditions"=>true,
 "ResolveTargetsAsync"=>Task.FromResult(resolve()),"CreateEffectsAsync"=>Task.FromResult(new List<Effect>{new(p1.PlayerId){EffectConfig=new(){Type=EffectType.None}}}),_=>null});}
Test("Intromezzo ignores skipped and cancelled targets even when target resolution is deferred",()=>{
 foreach(var active in new[]{true,false})foreach(var cancelled in new[]{false,true}) {
  p1.IsIntromezzoActive=active;var ability=ReviewAbility(99601,()=>new(){Status=cancelled?ResolutionStatus.Failed:ResolutionStatus.Skipped,WasCancelled=cancelled});
  var outcome=reviewManager.ProcessSingleAbilityAsync(ability,context,true,true).GetAwaiter().GetResult();
  Check(p1.IsIntromezzoActive==active&&outcome.Effects.Count==0&&outcome.IsCancelled==cancelled,"target failure changed Intromezzo");
 }
 p1.IsIntromezzoActive=false;var ready=ReviewAbility(99602,()=>new(){Status=ResolutionStatus.Completed});
 Check(reviewManager.ProcessSingleAbilityAsync(ready,context,false,true).Result.Effects.Count==0&&p1.IsIntromezzoActive,"inactive Intromezzo did not advance");
 Check(reviewManager.ProcessSingleAbilityAsync(ready,context,false,true).Result.Effects.Count==1&&!p1.IsIntromezzoActive,"active Intromezzo did not execute");
});
Test("inactive Intromezzo does not take the transaction's same-name slot",()=>{
 p1.IsIntromezzoActive=false;var abilities=new List<IAbility>{ReviewAbility(99603,()=>new(){Status=ResolutionStatus.Completed}),ReviewAbility(99603,()=>new(){Status=ResolutionStatus.Completed}),ReviewAbility(99603,()=>new(){Status=ResolutionStatus.Completed})};
 var manager=Proxy<IAbilityManager>.Make((m,a)=>m.Name switch{"GetTriggeredAbilitiesAsync"=>Task.FromResult(abilities),"ProcessSingleAbilityAsync"=>reviewManager.ProcessSingleAbilityAsync((IAbility)a![0]!, (GameContext)a[1]!, (bool)a[2]!, (bool)a[3]!, (bool)a[4]!),_=>null});
 var transaction=new GameActionTransactionService(NullLogger<GameActionTransactionService>.Instance,manager,null!,null!,null!,null!,null!,null!);
 game.AbilityManager=manager;using var triggerContext=new GameContext(services,new MapperConfiguration(cfg=>{}).CreateMapper(),accessor,game,p1.PlayerId,sourceCard:p1.Leader);
 try {
  var method=typeof(GameActionTransactionService).GetMethod("GetTriggerAbilityEffectsAsync",BindingFlags.NonPublic|BindingFlags.Instance)!;
  var task=(Task)method.Invoke(transaction,new object[]{triggerContext,new DiscoverEvent(p1,p1.Leader),TriggerTiming.On})!;task.GetAwaiter().GetResult();
  var tuple=task.GetType().GetProperty("Result")!.GetValue(task)!;var effects=(List<Effect>)tuple.GetType().GetField("Item2")!.GetValue(tuple)!;
  Check(effects.Count==1&&!p1.IsIntromezzoActive,"skipped same-name ability blocked the next actual activation");
 } finally {game.AbilityManager=abilityManager;p1.IsIntromezzoActive=false;}
});
Test("Before intercepts advance inactive Intromezzo once and prevent discover before player choice",()=>{
 var query=Proxy<ShadowverseBeyond.Server.Domain.Query.IQueryExecutor>.Make((m,a)=>null);var pending=Proxy<IPendingActionService>.Make((m,a)=>throw new Exception("prevented discover asked for a choice"));
 using var provider=new ServiceCollection().AddSingleton(query).AddSingleton<IBuffService>(buffService).AddSingleton(services.GetRequiredService<IEventHistoryService>()).BuildServiceProvider();
 var selection=new TargetSelectionService(NullLogger<TargetSelectionService>.Instance,query,pending);
 Ability Make(bool intermezzo,EffectType effect)=>new(new(){Id=99604,AbilityKeywords=intermezzo?new(){new(){Keyword=AbilityKeyword.Intromezzo}}:new(),Trigger=new(){Timing=TriggerTiming.Before},Effects=new(){new(){Type=effect}}},Proxy<IConditionFactory>.Make((m,a)=>null),Proxy<ITargetFactory>.Make((m,a)=>null),selection,p1.Leader);
 var intercept=Make(true,EffectType.PreventEvent);var ordinary=Make(false,EffectType.None);var ordinaryProbes=0;
 var manager=Proxy<IAbilityManager>.Make((m,a)=>m.Name switch{
  "GetTriggeredAbilitiesAsync"=>Task.FromResult((TriggerTiming)a![2]! ==TriggerTiming.Before&&((IGameEvent)a[0]!).EventType==EventType.DiscoverRequest?new List<IAbility>{ordinary,intercept}:new()),
  "ProcessSingleAbilityAsync"=>Process((IAbility)a![0]!,(GameContext)a[1]!, (bool)a[2]!, (bool)a[3]!, (bool)a[4]!),_=>null});
 Task<AbilityProcessResult> Process(IAbility ability,GameContext c,bool cancellable,bool defer,bool record){if(ability==ordinary)ordinaryProbes++;return reviewManager.ProcessSingleAbilityAsync(ability,c,cancellable,defer,record);}
 var executor=Proxy<IEffectExecutor>.Make((m,a)=>new PreventEventEffectHandler(NullLogger<PreventEventEffectHandler>.Instance).Apply((Effect)a![0]!, (GameContext)a[1]!,new()));
 var transaction=new GameActionTransactionService(NullLogger<GameActionTransactionService>.Instance,manager,null!,pending,executor,null!,null!,null!);game.AbilityManager=manager;
 using var c=new GameContext(provider,new MapperConfiguration(cfg=>{}).CreateMapper(),accessor,game,p1.PlayerId,sourceCard:p1.Leader);
 object Run(string methodName,IGameEvent ev){var method=typeof(GameActionTransactionService).GetMethod(methodName,BindingFlags.Instance|BindingFlags.NonPublic)!;var args=method.GetParameters().Select(p=>p.ParameterType==typeof(GameContext)?(object)c:p.ParameterType==typeof(IGameEvent)?ev:p.HasDefaultValue?p.DefaultValue:Activator.CreateInstance(p.ParameterType)).ToArray();var task=(Task)method.Invoke(transaction,args)!;task.GetAwaiter().GetResult();return task.GetType().GetProperty("Result")!.GetValue(task)!;}
 try {
  p1.IsIntromezzoActive=false;var ev=new DiscoverRequestEvent(p1,new(){p1.Leader},false,Guid.Empty);Run("ApplyBeforeInterceptsAsync",ev);Check(p1.IsIntromezzoActive&&ordinaryProbes==0&&intercept.TriggerCountThisGame==0,"inactive intercept was not advanced or probed an ordinary Before ability");
  var result=(PausableTransactionResult)Run("ProcessEventRecursivelyAsync",ev);Check(result.IsSuccess&&ev.Selected==null&&!p1.IsIntromezzoActive,"discover resolved before prevention");
 } finally {game.AbilityManager=abilityManager;p1.IsIntromezzoActive=false;}
});
Test("AI reads actual automatic end-turn conditions without mutating runtime targets", () => {
 var own = new PlayerState(Guid.NewGuid()); var enemy = new PlayerState(Guid.NewGuid());
 var runtime = new List<IAbility>();
 var manager = Proxy<IAbilityManager>.Make((m,a)=>m.Name=="GetEffectiveAbilitiesForCard"
     ? runtime.Where(ability=>ability.SourceCardId==(Guid)a![0]!).ToList():null);
 var match = new GameState("ai-end-turn",own,enemy,own.PlayerId){AbilityManager=manager,BuffManager=buffs};
 var waiter = factory.CreateAndRegisterNewCard(match,2,own.PlayerId,CardZone.Field);
 var fox = factory.CreateAndRegisterNewCard(match,3,own.PlayerId,CardZone.Field);
 var target = factory.CreateAndRegisterNewCard(match,4,enemy.PlayerId,CardZone.Field);
 var query = Proxy<IQueryExecutor>.Make((m,a)=>new QueryResult{Results=new object[]{target}});
 var pending=Proxy<IPendingActionService>.Make((m,a)=>throw new Exception("forecast must not request player input"));
 var selection=new TargetSelectionService(NullLogger<TargetSelectionService>.Instance,query,pending);
 var conditionEvaluator=new DslConditionEvaluator(compiler,NullLogger<DslConditionEvaluator>.Instance);
 var mapper=new MapperConfiguration(cfg=>{}).CreateMapper();
 var conditionFactory=new ConditionFactory(NullLoggerFactory.Instance,mapper,conditionEvaluator);
 var targetFactory=new TargetFactory(selection,NullLogger<Target>.Instance);
 var config=new AbilityConfig{Name="款待者回合结束",Trigger=new(){EventTypes=[EventType.TurnEnd],ValidZones=[CardZone.Field]},
  Conditions=[new(){Expression="count(context.owner.hand) == 0"},new(){Expression="context.triggeringEvent.EndingPlayer.playerId == context.owner.playerId"}],
  Targets=new(){["target1"]=new(){Query=new(){SourcePoolExpression="context.opponent.field"}}},
  Effects=[new(){Type=EffectType.Damage,Parameters=new(){["DamageAmount"]=new StaticValueConfig{Value=3},["DistributionMode"]=new StaticValueConfig{Value="perTarget"}}}]};
 var ability=new Ability(config,conditionFactory,targetFactory,selection,waiter);runtime.Add(ability);
 using var provider=new ServiceCollection().AddSingleton(query).AddSingleton<IConditionEvaluator>(conditionEvaluator).AddSingleton<IBuffService>(buffService).AddSingleton<IEventHistoryService>(Proxy<IEventHistoryService>.Make((m,a)=>null)).BuildServiceProvider();
 using var ctx=new GameContext(provider,mapper,accessor,match,own.PlayerId,sourceCard:waiter);
 var view=new ShadowverseBeyond.Server.Dtos.PlayerStateDto{PlayerId=own.PlayerId,Field=[new(){InstanceId=waiter.InstanceId},new(){InstanceId=fox.InstanceId}]};
 var read=typeof(AiTurnExecutor).Assembly.GetType("ShadowverseBeyond.Server.Services.AiEndTurnForecast")!.GetMethod("ReadAsync",BindingFlags.Static|BindingFlags.NonPublic)!;
 object Forecast(){var task=(Task)read.Invoke(null,new object[]{ctx,view})!;task.GetAwaiter().GetResult();return task.GetType().GetProperty("Result")!.GetValue(task)!;}
 int Count(object result)=>((System.Collections.ICollection)result.GetType().GetProperty("Damage")!.GetValue(result)!).Count;
 Check(Count(Forecast())==1,"available automatic sweep was not read");
 Check(ability.Targets[0].NeedsResolution&&ability.Targets[0].ResolutionCount==0&&ability.Conditions.All(condition=>condition.EvaluationCount==0),"forecast mutated runtime selection or condition bookkeeping");
 factory.CreateAndRegisterNewCard(match,5,own.PlayerId,CardZone.Hand);Check(Count(Forecast())==0,"nonempty hand incorrectly passed empty-hand condition");own.Hand.Clear();
 config.Targets["target1"].PlayerChoiceOptions=new(){IsRequired=true};Check(Count(Forecast())==0,"interactive end-turn effect treated as automatic");
});
Test("AI keeps independent end-turn leader damage without sampling optional targets or leaking hidden cards",()=>{
 var own=new PlayerState(Guid.NewGuid());var enemy=new PlayerState(Guid.NewGuid());var runtime=new List<IAbility>();var lookedUp=new List<Guid>();
 var manager=Proxy<IAbilityManager>.Make((m,a)=>{
  if(m.Name!="GetEffectiveAbilitiesForCard")return null;
  lookedUp.Add((Guid)a![0]!);return runtime.Where(ability=>ability.SourceCardId==(Guid)a[0]!).ToList();
 });
 var match=new GameState("partial-end-turn",own,enemy,own.PlayerId){AbilityManager=manager,BuffManager=buffs};
 own.Leader=factory.CreateAndRegisterNewCard(match,1,own.PlayerId,CardZone.Staging);
 enemy.Leader=factory.CreateAndRegisterNewCard(match,1,enemy.PlayerId,CardZone.Staging);
 var source=factory.CreateAndRegisterNewCard(match,2,own.PlayerId,CardZone.Field);
 var enemyUnit=factory.CreateAndRegisterNewCard(match,4,enemy.PlayerId,CardZone.Field);
 var fox=factory.CreateAndRegisterNewCard(match,3,own.PlayerId,CardZone.Field);
 var hidden=factory.CreateAndRegisterNewCard(match,5,enemy.PlayerId,CardZone.Hand);
 var queries=new List<string>();
 var query=Proxy<IQueryExecutor>.Make((m,a)=>{
  var text=((QueryConfig)a![0]!).SourcePoolExpression;queries.Add(text);
  if(text.Contains("random",StringComparison.OrdinalIgnoreCase))throw new Exception("forecast consumed a random selection");
  return new QueryResult{Success=true,Results=text=="context.opponent.leader"?new object[]{enemy.Leader}:enemy.Field.Cast<object>().ToArray()};
 });
 var pending=Proxy<IPendingActionService>.Make((m,a)=>throw new Exception("forecast prompted a player"));
 var selection=new TargetSelectionService(NullLogger<TargetSelectionService>.Instance,query,pending);
 var evaluator=new DslConditionEvaluator(compiler,NullLogger<DslConditionEvaluator>.Instance);
 var mapper=new MapperConfiguration(cfg=>{}).CreateMapper();
 var conditionFactory=new ConditionFactory(NullLoggerFactory.Instance,mapper,evaluator);var targetFactory=new TargetFactory(selection,NullLogger<Target>.Instance);
 var config=new AbilityConfig{Name="generic mixed end-turn damage",Trigger=new(){EventTypes=[EventType.TurnEnd],ValidZones=[CardZone.Field]},
  Conditions=[new(){Expression="count(context.owner.hand) == 0"},new(){Expression="context.triggeringEvent.EndingPlayer.playerId == context.owner.playerId"}],
  Targets=new(){["leader"]=new(){Query=new(){SourcePoolExpression="context.opponent.leader"}},
   ["random"]=new(){Query=new(){SourcePoolExpression="context.opponent.field.query(self.type == \"Follower\").take(random 1)"}}},
  Effects=[new(){Type=EffectType.Damage,Parameters=new(){["DamageAmount"]=new StaticValueConfig{Value=2},["DistributionMode"]=new StaticValueConfig{Value="perTarget"}}}]};
 var ability=new Ability(config,conditionFactory,targetFactory,selection,source);runtime.Add(ability);
 using var provider=new ServiceCollection().AddSingleton(query).AddSingleton<IConditionEvaluator>(evaluator).AddSingleton<IBuffService>(buffService).AddSingleton<IEventHistoryService>(Proxy<IEventHistoryService>.Make((m,a)=>null)).BuildServiceProvider();
 using var ctx=new GameContext(provider,mapper,accessor,match,own.PlayerId,sourceCard:source);
 var ownView=new ShadowverseBeyond.Server.Dtos.PlayerStateDto{PlayerId=own.PlayerId,CurrentEP=1,CanEvolve=true,CurrentSEP=1,CanSuperEvolve=true,
  LeaderCard=new(){InstanceId=own.Leader.InstanceId,CardId=1,Type=CardType.Leader,CurrentHealth=20},
  Field=[new(){InstanceId=source.InstanceId,CardId=2,OwnerPlayerId=own.PlayerId,Type=CardType.Follower,CurrentAttack=2,CurrentHealth=3,MaxHealth=3},
   new(){InstanceId=fox.InstanceId,CardId=3,OwnerPlayerId=own.PlayerId,Type=CardType.Follower,CurrentAttack=3,CurrentHealth=2,MaxHealth=2,CanAttack=true,Keywords=[CardKeyword.Storm]}]};
 var enemyView=new ShadowverseBeyond.Server.Dtos.PlayerStateDto{PlayerId=enemy.PlayerId,DisplayName="PRIVATE_ACCOUNT",LeaderCard=new(){InstanceId=enemy.Leader.InstanceId,CardId=1,Type=CardType.Leader,CurrentHealth=2},
  Field=[new(){InstanceId=enemyUnit.InstanceId,CardId=4,Type=CardType.Follower,CurrentAttack=1,CurrentHealth=3,MaxHealth=3}],Hand=[new(){Name="PRIVATE_HIDDEN_HAND"}]};
 var snapshot=new ShadowverseBeyond.Server.Dtos.GameStateDto{Players=[ownView,enemyView]};
 var type=typeof(AiTurnExecutor).Assembly.GetType("ShadowverseBeyond.Server.Services.AiEndTurnForecast")!;
 var read=type.GetMethod("ReadAsync",BindingFlags.NonPublic|BindingFlags.Static)!;
 object Forecast(){var task=(Task)read.Invoke(null,[ctx,ownView])!;task.GetAwaiter().GetResult();return task.GetType().GetProperty("Result")!.GetValue(task)!;}
 int Count(object value)=>((System.Collections.ICollection)value.GetType().GetProperty("Damage")!.GetValue(value)!).Count;
 var otherEnemyUnit=factory.CreateAndRegisterNewCard(match,4,enemy.PlayerId,CardZone.Field);
 enemyView.Field.Add(enemyView.Field[0] with {InstanceId=otherEnemyUnit.InstanceId});
 var forecast=Forecast();Check(Count(forecast)==1,"mixed optional target hid deterministic leader damage");
 var facts=JsonSerializer.SerializeToElement(type.GetMethod("ForModel",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[forecast,snapshot,own.PlayerId]),new JsonSerializerOptions(JsonSerializerDefaults.Web));
 Check(facts.GetProperty("damage")[0].GetProperty("baseDamagePerTarget").GetInt32()==2&&facts.GetProperty("damage")[0].GetProperty("targets").GetArrayLength()==1
  &&facts.GetProperty("damage")[0].GetProperty("targets")[0].GetProperty("zone").GetString()=="leader"
  &&facts.GetProperty("omitted")[0].GetProperty("reason").GetString()=="random-follower-damage-not-projected","model projection mixed known and unknown damage");
 enemy.Field.Remove(otherEnemyUnit);enemyView.Field.RemoveAt(1);
 Check(!facts.GetRawText().Contains("PRIVATE_")&&!facts.GetRawText().Contains(own.PlayerId.ToString())&&!lookedUp.Contains(hidden.InstanceId),"forecast inspected/leaked a hidden card or identity");
 Check(ability.Targets.All(target=>target.NeedsResolution&&target.ResolutionCount==0)&&ability.Conditions.All(condition=>condition.EvaluationCount==0),"forecast mutated runtime bookkeeping");
 Check(ability.Trigger.TotalTriggerCount==0&&ability.Trigger.CurrentTurnTriggerCount==0,"forecast recorded a real ability trigger");
 config.AbilityKeywords.Add(new(){Keyword=AbilityKeyword.Enhance,Value=2});Check(Count(Forecast())==0,"keyword-gated ability was assumed unconditional");config.AbilityKeywords.Clear();
 config.Trigger.LimitSameNameOncePerTransaction=true;Check(Count(Forecast())==0,"transaction-once ability was double-countable");config.Trigger.LimitSameNameOncePerTransaction=false;
 var search=typeof(AiTurnExecutor).Assembly.GetType("ShadowverseBeyond.Server.Services.AiTacticalSearch")!;
 var plan=search.GetMethod("FindWithEndTurn",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[snapshot,own.PlayerId,CancellationToken.None,null,null,forecast])!;
 Check((string)plan.GetType().GetProperty("First")!.GetValue(plan)!.GetType().GetProperty("Type")!.GetValue(plan.GetType().GetProperty("First")!.GetValue(plan)!)! =="EndTurn","certain end-turn lethal spent an attack/evolution instead");
 enemy.Field.Clear();Check(Count(Forecast())==1,"optional empty random pool blocked leader damage");enemy.Field.Add(enemyUnit);
 config.Targets["random"].TargetHandling=TargetHandlingStrategy.BlockPlayerOperation;Check(Count(Forecast())==0,"required random pool was assumed independent");config.Targets["random"].TargetHandling=null;
 config.Effects[0].Parameters["DistributionMode"]=new StaticValueConfig{Value="split"};Check(Count(Forecast())==0,"split damage treated as per-target damage");
 config.Effects[0].Parameters["DistributionMode"]=new FormulaValueConfig{Formula="\"perTarget\""};Check(Count(Forecast())==0,"dynamic distribution silently assumed per-target");
 config.Effects[0].Parameters["DistributionMode"]=new StaticValueConfig{Value="perTarget"};
 var reaction=new Ability(new(){Trigger=new(){EventTypes=[EventType.Damage],ValidZones=[CardZone.Field]},Effects=[new(){Type=EffectType.Heal}]},conditionFactory,targetFactory,selection,enemyUnit);
 runtime.Add(reaction);Check(Count(Forecast())==0,"public damage reaction ignored in terminal forecast");runtime.Remove(reaction);
 var hiddenReaction=new Ability(new(){Trigger=new(){EventTypes=[EventType.Damage],ValidZones=[CardZone.Hand]},Effects=[new(){Type=EffectType.Heal}]},conditionFactory,targetFactory,selection,hidden);
 runtime.Add(hiddenReaction);Check(Count(Forecast())==1&&!lookedUp.Contains(hidden.InstanceId),"projection used hidden enemy ability knowledge");runtime.Remove(hiddenReaction);
 var selfBuff=new Ability(new(){Trigger=new(){EventTypes=[EventType.TurnEnd],ValidZones=[CardZone.Field]},
  Targets=new(){["self"]=new(){Query=new(){SourcePoolExpression="context.sourceCard"}}},
  Effects=[new(){Type=EffectType.SetKeyword,Parameters=new(){["Keyword"]=new StaticValueConfig{Value="Shield"}}}]},conditionFactory,targetFactory,selection,fox);
 runtime.Add(selfBuff);Check(Count(Forecast())==1,"harmless own self-keyword end-turn ability blocked projection");runtime.Remove(selfBuff);
 var combatReaction=new Ability(new(){Trigger=new(){EventTypes=[EventType.Clash],ValidZones=[CardZone.Field]},Effects=[new(){Type=EffectType.Heal}]},conditionFactory,targetFactory,selection,enemyUnit);
 runtime.Add(combatReaction);var combatForecast=Forecast();
 Check(Count(combatForecast)==1&&((IReadOnlySet<Guid>)combatForecast.GetType().GetProperty("UnsafeAttackers")!.GetValue(combatForecast)!).Contains(fox.InstanceId),"enemy clash wrongly blocked immediate end turn or stayed trusted after an attack");
 var memory=(AiStrategyPlanner.AutomaticEndDamageContext)type.GetMethod("ForMemory",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[combatForecast,snapshot,own.PlayerId,fox.InstanceId])!;
 Check(memory.EnemyLeaderBaseDamage==2&&memory.EnemyFieldTargetCount==1&&memory.UnmodeledParts==0&&memory.AttackInvalidates==true&&memory.SourceCardIds.SequenceEqual(new[]{2}),"decision estimate lost singleton damage or attack invalidation");
 runtime.Remove(combatReaction);
 factory.CreateAndRegisterNewCard(match,5,own.PlayerId,CardZone.Hand);Check(Count(Forecast())==0,"nonempty real hand passed empty-hand condition");
});
Test("AI derives automatic hand loss from live abilities, not labels, without mutating the game",()=>{
 var own=new PlayerState(Guid.NewGuid());var enemy=new PlayerState(Guid.NewGuid());var runtime=new List<IAbility>();
 var manager=Proxy<IAbilityManager>.Make((m,a)=>m.Name=="GetEffectiveAbilitiesForCard"?runtime.Where(ability=>ability.SourceCardId==(Guid)a![0]!).ToList():null);
 var match=new GameState("hand-loss-probe",own,enemy,own.PlayerId){AbilityManager=manager,BuffManager=buffs};
 var first=factory.CreateAndRegisterNewCard(match,2,own.PlayerId,CardZone.Hand);
 var fragile=factory.CreateAndRegisterNewCard(match,3,own.PlayerId,CardZone.Hand);
 var query=Proxy<IQueryExecutor>.Make((m,a)=>throw new Exception("self-only forecast should not query mutable selections"));
 var pending=Proxy<IPendingActionService>.Make((m,a)=>throw new Exception("hand loss must not prompt"));
 var selection=new TargetSelectionService(NullLogger<TargetSelectionService>.Instance,query,pending);
 var conditionEvaluator=new DslConditionEvaluator(compiler,NullLogger<DslConditionEvaluator>.Instance);
 var mapper=new MapperConfiguration(cfg=>{}).CreateMapper();
 var cfg=new AbilityConfig {Id=777,Name="arbitrary label",Trigger=new(){EventTypes=[EventType.PlayCard],RelationType=TriggerRelationType.TargetOther,ValidZones=[CardZone.Hand]},
  Conditions=[new(){Expression="(context.triggeringEvent.PlayingPlayer.playerId) == (context.owner.playerId)"}],
  Targets=new(){["target1"]=new(){Query=new(){SourcePoolExpression="context.SourceCard"}}},Effects=[new(){Type=EffectType.Discard}]};
 string? nativeRuleHash=null;
 if(Environment.GetEnvironmentVariable("AI_HAND_LOSS_PROBE_SOURCE") is {Length:>0} catalogPath) {
  using var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=catalogPath,Mode=SqliteOpenMode.ReadOnly}.ToString());connection.Open();
  using var command=connection.CreateCommand();command.CommandText="SELECT JsonConfig FROM CardAbilities WHERE CardId=1474113";
  using var reader=command.ExecuteReader();JsonElement? native=null;
  IEnumerable<JsonElement> Objects(JsonElement node) {
   if(node.ValueKind==JsonValueKind.Object) {yield return node;foreach(var property in node.EnumerateObject())foreach(var child in Objects(property.Value))yield return child;}
   else if(node.ValueKind==JsonValueKind.Array)foreach(var item in node.EnumerateArray())foreach(var child in Objects(item))yield return child;
  }
  while(reader.Read()&&native==null) {
   using var parsed=JsonDocument.Parse(reader.GetString(0));
   foreach(var node in Objects(parsed.RootElement))if(node.TryGetProperty("trigger",out var trigger)
      &&trigger.TryGetProperty("validZones",out var zones)&&zones.EnumerateArray().Any(zone=>zone.GetString()=="Hand")
      &&node.TryGetProperty("effects",out var effects)&&effects.GetArrayLength()==1
      &&effects[0].GetProperty("type").GetString()=="Discard") {native=node.Clone();break;}
  }
  Check(native!=null,"current ArduNight catalog no longer contains the expected automatic self-discard grant");
  var nativeOptions=new JsonSerializerOptions(JsonSerializerDefaults.Web);nativeOptions.Converters.Add(new JsonStringEnumConverter());
  cfg=JsonSerializer.Deserialize<AbilityConfig>(native!.Value.GetRawText(),nativeOptions)!;
  nativeRuleHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(native.Value.GetRawText())));
 }
 var ability=new Ability(cfg,new ConditionFactory(NullLoggerFactory.Instance,mapper,conditionEvaluator),new TargetFactory(selection,NullLogger<Target>.Instance),selection,fragile);runtime.Add(ability);
 using var provider=new ServiceCollection().AddSingleton<IConditionEvaluator>(conditionEvaluator).AddSingleton<IBuffService>(buffService).AddSingleton<IEventHistoryService>(Proxy<IEventHistoryService>.Make((m,a)=>null)).BuildServiceProvider();
 using var ctx=new GameContext(provider,mapper,accessor,match,own.PlayerId);
 var view=new ShadowverseBeyond.Server.Dtos.PlayerStateDto{PlayerId=own.PlayerId,Hand=[new(){InstanceId=first.InstanceId},new(){InstanceId=fragile.InstanceId}]};
 var method=typeof(AiTurnExecutor).Assembly.GetType("ShadowverseBeyond.Server.Services.AiPlaySequence")!.GetMethod("ReadHandLossAsync",BindingFlags.Static|BindingFlags.NonPublic)!;
 AiStrategyPlanner.HandLossRule[] Read(){var task=(Task<AiStrategyPlanner.HandLossRule[]>)method.Invoke(null,[ctx,view])!;return task.GetAwaiter().GetResult();}
 var before=own.Hand.Select(card=>card.InstanceId).ToArray();var rules=Read();
 Check(rules.Length==1&&rules[0].DiscardedHandIndex==1&&rules[0].WhenPlayedHandIndexes.SequenceEqual([0]),"actual discard dependency not detected");
 Check(own.Hand.Select(card=>card.InstanceId).SequenceEqual(before)&&ability.TotalTriggerCount==0&&ability.Conditions.All(condition=>condition.EvaluationCount==0)&&ability.Targets.All(target=>target.ResolutionCount==0),"forecast altered runtime or bookkeeping");
 view=view with {Hand=view.Hand.AsEnumerable().Reverse().ToList()};rules=Read();Check(rules[0].DiscardedHandIndex==0&&rules[0].WhenPlayedHandIndexes.SequenceEqual([1]),"dependency did not follow instance after reorder");
 cfg.Conditions[0].Expression="false";Check(Read().Length==0,"false predicate predicted loss");cfg.Conditions[0].Expression="context.triggeringEvent.PlayingPlayer.playerId == context.owner.playerId";
 typeof(CardInstance).GetProperty("IsSilenced")!.SetValue(fragile,true);Check(Read().Length==0,"silenced ability predicted loss");typeof(CardInstance).GetProperty("IsSilenced")!.SetValue(fragile,false);
 cfg.Targets["target1"].PlayerChoiceOptions=new(){IsRequired=true};Check(Read().Length==0,"manual choice treated as automatic");cfg.Targets["target1"].PlayerChoiceOptions=null;
 cfg.Effects.Add(new(){Type=EffectType.None});Check(Read().Length==0,"unknown composite claimed as proven loss");cfg.Effects.RemoveAt(1);
 runtime.Clear();Check(Read().Length==0,"removed granted ability remained in forecast");
 if(Environment.GetEnvironmentVariable("AI_HAND_LOSS_PROBE_OUTPUT") is {Length:>0} outputPath)
  File.WriteAllText(outputPath,JsonSerializer.Serialize(new{sourceCardId=1474113,nativeRuleHash,passed=true,readOnly=true,scope="current catalog grant config through runtime Ability and DSL condition evaluator; no real match mutation"}));
});
Test("inverse of all candidates resolves to an explicit empty target list", () => {
 var query = Proxy<IQueryExecutor>.Make((m,a) => new QueryResult { Results = new object[] { p1.Leader } });
 var pending = Proxy<IPendingActionService>.Make((m,a) => throw new Exception("automatic targets should not prompt"));
 var selection = new TargetSelectionService(NullLogger<TargetSelectionService>.Instance, query, pending);
 using var provider = new ServiceCollection().AddSingleton(query).AddSingleton<IBuffService>(buffService).AddSingleton<IEventHistoryService>(Proxy<IEventHistoryService>.Make((m,a)=>null)).BuildServiceProvider();
 using var ctx = new GameContext(provider, new MapperConfiguration(cfg=>{}).CreateMapper(),accessor,game,p1.PlayerId,sourceCard:p1.Leader);
 foreach (var (required, inverse, expected) in new[] { (false, false, 1), (false, true, 0), (true, true, 0) }) {
  var config = new TargetConfig { PlayerChoiceOptions = new() { IsRequired = required, MinCount = 2, MaxCount = 2, IsInverseSelection = inverse } };
  var target = new Target("cards", config, selection, NullLogger<Target>.Instance);
  var ability = Proxy<IAbility>.Make((m,a) => m.Name switch { "get_Targets" => new List<Target> { target }, "HasKeyword" => false, _ => null });
  var resolved = selection.ResolveTargetsForAbilityAsync(ability,ctx,false).GetAwaiter().GetResult();
  Check(resolved.Status == ResolutionStatus.Completed && resolved.ResolvedTargets["cards"].Count == expected, "wrong complement or missing pool");
 }
});
CardInstance Destroyed(int id) {var card=factory.CreateAndRegisterNewCard(game,id,p1.PlayerId,CardZone.Graveyard); typeof(CardInstance).GetMethod("SetLastMoveReason",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(card,new object[]{CardMoveReason.Destroyed});return card;}
var reanimate=new ReanimateEffectHandler(NullLogger<ReanimateEffectHandler>.Instance,new TargetProcessingService(NullLogger<TargetProcessingService>.Instance),factory);
GameStateActionResult Reanimate(int maximum,List<ConditionConfig>? conditions=null) => reanimate.Apply(new Effect(p1.PlayerId),context,new(){MaxCost=maximum,PoolConditions=conditions??new()});
int SummonedId(GameStateActionResult result)=>result.GeneratedEvents.OfType<SummonEvent>().Single().Target.Single().Card!.BaseDefinitionId;
Test("reanimate highest destroyed base cost",()=>{p1.Graveyard.Clear();Destroyed(3);Destroyed(5);Check(SummonedId(Reanimate(4))==3,"highest");});
Test("filter pool before highest cost",()=>{p1.Graveyard.Clear();Destroyed(3);Destroyed(5);Check(SummonedId(Reanimate(9,new(){new(){Expression="self.BaseCost == 3"}}))==3,"filter ignored");});
Test("disabled pool filter ignored",()=>Check(SummonedId(Reanimate(9,new(){new(){Expression="false",IsEnabled=false}}))==5,"disabled"));
Test("filter applies to candidate not invoking card",()=>Check(SummonedId(Reanimate(9,new(){new(){Expression="self.BaseDefinitionId == 5"}}))==5,"self"));
Test("stacked reanimate keyword corrections",()=>{p1.Graveyard.Clear();Destroyed(3);Destroyed(5);for(int i=0;i<2;i++)buffs.RegisterBuffInstance(new Buff(Guid.Empty,p1.Leader.InstanceId,0,new(){new AbilityKeywordBuffEffect(new(){new(AbilityKeyword.Reanimate,1,ModifierLayer.Additive)})}),game);Check(SummonedId(Reanimate(3))==5,"stack");});
Test("non-destroyed graveyard entries excluded",()=>{p1.Graveyard.Clear();factory.CreateAndRegisterNewCard(game,7,p1.PlayerId,CardZone.Graveyard);Destroyed(2);Check(SummonedId(Reanimate(9))==2,"reason");});
Test("other owner's pool excluded",()=>{p1.Graveyard.Clear();Destroyed(2);var other=factory.CreateAndRegisterNewCard(game,7,p2.PlayerId,CardZone.Graveyard);typeof(CardInstance).GetMethod("SetLastMoveReason",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(other,new object[]{CardMoveReason.Destroyed});Check(SummonedId(Reanimate(9))==2,"owner");});
foreach(var zone in new[]{CardZone.Field,CardZone.Hand,CardZone.Deck,CardZone.Graveyard,CardZone.Extra,CardZone.Emblem}) Test($"transform new entity identity in {zone}",()=>{var list=zone switch{CardZone.Field=>p1.Field,CardZone.Hand=>p1.Hand,CardZone.Deck=>p1.Deck,CardZone.Graveyard=>p1.Graveyard,CardZone.Extra=>p1.Extra,_=>p1.Emblem};list.Clear();var old=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,zone);var oldId=old.InstanceId;var index=list.IndexOf(old);var ev=new TransformEvent(old,3);Check(ev.Commit(context).Success,"commit");var current=list[index];Check(current.InstanceId!=oldId&&current.BaseDefinitionId==3,"identity");Check(old.CurrentZone==CardZone.Removed&&game.FindCardInstance(oldId)==null,"old remains");Check(game.FindCardInstance(current.InstanceId)==current,"registry");Check(ev.TransformingCard==current&&ev.TransformingCardSnapshot!.InstanceId==oldId,"event identity");});
Test("stale targets do not point at transformed card",()=>{p1.Field.Clear();var old=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,CardZone.Field);var target=TargetInfo.ForCard(old);var ev=new TransformEvent(old,4);ev.Commit(context);Check(target.Card!=ev.TransformingCard&&target.InstanceId!=ev.TransformingCard!.InstanceId,"stale target rebound");});
Test("reveal identifies owner without mutating hand",()=>{p2.Hand.Clear();var card=factory.CreateAndRegisterNewCard(game,4,p2.PlayerId,CardZone.Hand);var effect=new Effect(p1.PlayerId){SelectedTargets=new(){TargetInfo.ForCard(card)}};var r=new RevealEffectHandler(NullLogger<RevealEffectHandler>.Instance).Apply(effect,context,new());var ev=r.GeneratedEvents.OfType<CardRevealedEvent>().Single();Check(ev.Source!.Player==p2,"wrong owner");ev.Commit(context);Check(p2.Hand.Count==1&&card.CurrentZone==CardZone.Hand,"reveal moved card");});

Test("empty filtered pool is harmless",()=>Check(Reanimate(9,new(){new(){Expression="false"}}).GeneratedEvents.Count==0,"empty"));
Test("all enabled filters intersect",()=>{p1.Graveyard.Clear();Destroyed(2);Destroyed(3);Destroyed(5);Check(SummonedId(Reanimate(9,new(){new(){Expression="self.BaseCost > 2"},new(){Expression="self.BaseCost < 5"}}))==3,"intersection");});
foreach (var positions in new[] { new[]{1}, new[]{2,0} }) Test("mulligan retains selected slots " + string.Join(",",positions),()=>{
 var a=new PlayerState(Guid.NewGuid());var b=new PlayerState(Guid.NewGuid());var match=new GameState("mulligan-slots",a,b,a.PlayerId){AbilityManager=abilityManager,BuffManager=buffs};
 var original=Enumerable.Range(0,4).Select(_=>factory.CreateAndRegisterNewCard(match,2,a.PlayerId,CardZone.Hand)).ToList();
 for(int i=0;i<5;i++)factory.CreateAndRegisterNewCard(match,3,a.PlayerId,CardZone.Deck);
 var result=a.PerformMulligan(positions.Select(i=>original[i].InstanceId).ToList(),match);
 Check(result.Success&&a.Hand.Count==4&&a.Deck.Count==5,"zone sizes changed");
 for(int i=0;i<4;i++)if(!positions.Contains(i))Check(a.Hand[i]==original[i],"kept slot moved");
 Check(a.Hand.Concat(a.Deck).Select(c=>c.InstanceId).Distinct().Count()==9,"card identity lost");
});
Test("mulligan can draw a returned card again",()=>{
 var a=new PlayerState(Guid.NewGuid());var b=new PlayerState(Guid.NewGuid());var match=new GameState("mulligan-returned",a,b,a.PlayerId){AbilityManager=abilityManager,BuffManager=buffs};
 var card=factory.CreateAndRegisterNewCard(match,2,a.PlayerId,CardZone.Hand);
 Check(a.PerformMulligan(new(){card.InstanceId},match).Success&&a.Hand.Single()==card,"returned card was excluded");
});
Test("reanimate modifier applies only to selected player",()=>{p1.Graveyard.Clear();Destroyed(5);Check(buffService.GetEffectiveAbilityKeywordValue(p2.Leader,AbilityKeyword.Reanimate,3,context)==3,"opponent polluted");});
foreach(var mode in new[]{AbilityKeyword.Accelerate,AbilityKeyword.Crystalize}) {
 Test($"zero-cost {mode} is playable",()=>{
   var alternate=new AlternatePlayConfig{PlayAsCardDefinitionId=mode==AbilityKeyword.Accelerate?8:9};
   activeAbilities.Clear(); activeAbilities.Add(Proxy<IAbility>.Make((method,args)=>method.Name switch{
     "get_AbilityKeywords"=>new List<AbilityKeywordConfig>{new(){Keyword=mode,Value=0}},
     "get_AlternatePlay"=>alternate,"get_RuntimeId"=>Guid.NewGuid(),"HasKeyword"=>(AbilityKeyword)args![0]! == mode,_=>null }));
   var card=factory.CreateAndRegisterNewCard(game,7,p1.PlayerId,CardZone.Hand);p1.CurrentPlayPoints=0;
   var resolution=PlayModeResolver.Resolve(card,p1,context);Check(resolution.SelectedCost==0 && resolution.SelectedAlternateCardDefinitionId==alternate.PlayAsCardDefinitionId,"zero cost");
   activeAbilities.Clear();
 });
}
Test("AI mode preflight checks base/enhance and rejects alternate field and priority conflicts",()=>{
 var server=new GameService(Proxy<Microsoft.AspNetCore.SignalR.IHubContext<ShadowverseBeyond.Server.Hubs.GameHub>>.Make((m,a)=>null),
  NullLogger<GameService>.Instance,context.Mapper,Proxy<IClientMethodMapper>.Make((m,a)=>null),data,
  Proxy<IMatchService>.Make((m,a)=>null),Proxy<IFirstPlayerSelector>.Make((m,a)=>null),services.GetRequiredService<IServiceScopeFactory>(),
  Proxy<IPendingActionService>.Make((m,a)=>null),Proxy<IGameStatsRuntimeTracker>.Make((m,a)=>null));
 var core=typeof(GameService).GetMethod("CheckPlayabilityCoreLogicAsync",BindingFlags.Instance|BindingFlags.NonPublic)!;
 var card=factory.CreateAndRegisterNewCard(game,7,p1.PlayerId,CardZone.Hand);
 ShadowverseBeyond.Server.Dtos.PlayabilityResultDto CheckMode(ShadowverseBeyond.Server.Dtos.Enums.PlayMode? mode)=>
  ((Task<ShadowverseBeyond.Server.Dtos.PlayabilityResultDto>)core.Invoke(server,[context with {SourceCard=card},p1,card,definitions[7],mode])!).GetAwaiter().GetResult();
 IAbility Ability(AbilityKeyword keyword,int cost,int? alternate=null) {
  var uid=Guid.NewGuid();return Proxy<IAbility>.Make((m,a)=>m.Name switch {
   "get_AbilityKeywords"=>new List<AbilityKeywordConfig>{new(){Keyword=keyword,Value=cost}},"get_RuntimeId"=>uid,
   "GetKeywordValue"=>(AbilityKeyword)a![0]! == keyword?cost:null,
   "get_AlternatePlay"=>alternate.HasValue?new AlternatePlayConfig{PlayAsCardDefinitionId=alternate.Value}:null,
   "HasKeyword"=>(AbilityKeyword)a![0]! == keyword,_=>null});
 }
 var savedField=p1.Field.ToArray();var savedPP=p1.CurrentPlayPoints;
 try {
  p1.Field.Clear();p1.CurrentPlayPoints=10;activeAbilities.Clear();activeAbilities.Add(Ability(AbilityKeyword.Enhance,9));
  var auto=CheckMode(null);var basic=CheckMode(ShadowverseBeyond.Server.Dtos.Enums.PlayMode.Base);
  Check(auto.IsPlayable&&auto.SelectedPlayMode==ShadowverseBeyond.Server.Dtos.Enums.PlayMode.Enhance,"auto enhance fixture failed");
  Check(basic.IsPlayable&&basic.SelectedPlayMode==ShadowverseBeyond.Server.Dtos.Enums.PlayMode.Base&&basic.SelectedAbilityRuntimeId==null
   &&basic.PlayedCardDefinitionId==7,"explicit base inherited enhance binding");
  p1.CurrentPlayPoints=2;activeAbilities.Clear();activeAbilities.Add(Ability(AbilityKeyword.Accelerate,1,8));
  for(var i=0;i<5;i++)p1.Field.Add(factory.CreateAndRegisterNewCard(game,1,p1.PlayerId,CardZone.Staging));
  var spell=CheckMode(ShadowverseBeyond.Server.Dtos.Enums.PlayMode.Accelerate);
  Check(spell.IsPlayable&&spell.PlayedCardDefinitionId==8,"spell alternate incorrectly blocked by original follower field space");
  Check(!CheckMode(ShadowverseBeyond.Server.Dtos.Enums.PlayMode.Base).IsPlayable,"unaffordable base allowed");
  activeAbilities.Clear();activeAbilities.Add(Ability(AbilityKeyword.Crystalize,1,9));
  Check(!CheckMode(ShadowverseBeyond.Server.Dtos.Enums.PlayMode.Crystalize).IsPlayable,"amulet alternate ignored full field");
  p1.Field.Clear();activeAbilities.Add(Ability(AbilityKeyword.Accelerate,2,8));
  Check(!CheckMode(ShadowverseBeyond.Server.Dtos.Enums.PlayMode.Crystalize).IsPlayable,"non-selected alternate bypassed engine priority");
  Check(CheckMode(ShadowverseBeyond.Server.Dtos.Enums.PlayMode.Accelerate).IsPlayable,"selected alternate unavailable");
 } finally {activeAbilities.Clear();p1.Field.Clear();p1.Field.AddRange(savedField);p1.CurrentPlayPoints=savedPP;}
});
Test("alternate type mismatch is unavailable",()=>{
 activeAbilities.Clear();activeAbilities.Add(Proxy<IAbility>.Make((method,args)=>method.Name switch{"get_AbilityKeywords"=>new List<AbilityKeywordConfig>{new(){Keyword=AbilityKeyword.Accelerate,Value=0}},"get_AlternatePlay"=>new AlternatePlayConfig{PlayAsCardDefinitionId=9},"HasKeyword"=>true,_=>null}));
 var card=factory.CreateAndRegisterNewCard(game,7,p1.PlayerId,CardZone.Hand);p1.CurrentPlayPoints=0;Check(!PlayModeResolver.Resolve(card,p1,context).HasAvailableMode,"wrong type accepted");activeAbilities.Clear();
});
Test("transform removes old counters, damage and modifiers",()=>{
 p1.Field.Clear();var old=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,CardZone.Field);old.SetCounter("example",9);old.Damage=1;old.IsEvolved=true;
 buffs.RegisterBuffInstance(new Buff(Guid.Empty,old.InstanceId,0,new(){new AbilityKeywordBuffEffect(new(){new(AbilityKeyword.Act,-2,ModifierLayer.Additive)})}),game);
 var ev=new TransformEvent(old,3);Check(ev.Commit(context).Success,"transform");var current=ev.TransformingCard!;
 Check(current.GetCounter("example")==0&&current.Damage==0&&!current.IsEvolved,"runtime carried");Check(buffs.GetBuffsForCard(current.InstanceId).Count==0,"buff carried");
});

foreach(var mode in new[]{PlayMode.Accelerate,PlayMode.Crystalize}) Test($"{mode} pays once and preserves preflight entity",()=>{
 activeAbilities.Clear();p1.Hand.Clear();p1.Field.Clear();p1.MaxPlayPoints=2;p1.CurrentPlayPoints=2;var old=factory.CreateAndRegisterNewCard(game,7,p1.PlayerId,CardZone.Hand);var combo=p1.Combo;
 var ev=new PlayCardEvent(p1,old,mode,paidCost:1,alternateCardDefinitionId:mode==PlayMode.Accelerate?8:9);var result=ev.Commit(context);
 Check(result.Success,"play failed: "+result.ErrorMessage);Check(result.GeneratedEvents.OfType<PpUpdateEvent>().Count()==1,"payment event count");foreach(var payment in result.GeneratedEvents.OfType<PpUpdateEvent>())payment.Commit(context);Check(p1.CurrentPlayPoints==1,"PP charged twice");Check(p1.Combo==combo+1,"combo charged twice");Check(ev.PlayedCard!.InstanceId==old.InstanceId && ReferenceEquals(ev.PlayedCard,old),"preflight identity lost");Check(!p1.Hand.Any(card=>card.BaseDefinitionId==7),"original definition remains");Check(ev.PlayedCard.BaseDefinitionId==(mode==PlayMode.Accelerate?8:9),"wrong card definition");Check(ShadowverseBeyond.Server.Mappings.CardInstanceProjection.Create(ev.PlayedCard,context).ArtPreview=="/original.png","original art lost");
 var processor = new EventProcessor(NullLogger<EventProcessor>.Instance, services.GetRequiredService<IEventHistoryService>());
 var dto = processor.ProcessEventAsync(ev, context).GetAwaiter().GetResult()
     .ResultingDtos.OfType<ShadowverseBeyond.Server.Dtos.SignalR.Events.CardPlayedEventDto>().Single();
 Check(dto.PlayMode == mode && dto.PaidCost == 1, "play animation lost the committed mode or cost");
});
Test("transform event transports old-to-new identity",()=>{
 activeAbilities.Clear();p1.Field.Clear();var old=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,CardZone.Field);var ev=new TransformEvent(old,3);ev.Commit(context);
 var processor=new EventProcessor(NullLogger<EventProcessor>.Instance,services.GetRequiredService<IEventHistoryService>());
 var method=typeof(EventProcessor).GetMethod("ConvertTransformEvent",BindingFlags.Instance|BindingFlags.NonPublic)!;
 var dto=(ShadowverseBeyond.Server.Dtos.SignalR.Events.CardInstanceUpdatedEventDto)method.Invoke(processor,new object[]{ev,context})!;
 Check(dto.ReplacedCardInstanceIds[old.InstanceId]==ev.TransformingCard!.InstanceId,"transport mapping missing");
});
Test("play animation receives the committed cost, including zero", () =>
{
    p1.Field.Clear();
    var card = factory.CreateAndRegisterNewCard(game, 7, p1.PlayerId, CardZone.Field);
    var processor = new EventProcessor(NullLogger<EventProcessor>.Instance, services.GetRequiredService<IEventHistoryService>());
    var play = new PlayCardEvent(p1, card, PlayMode.Base, paidCost: 0);
    var dto = processor.ProcessEventAsync(play, context).GetAwaiter().GetResult()
        .ResultingDtos.OfType<ShadowverseBeyond.Server.Dtos.SignalR.Events.CardPlayedEventDto>().Single();
    Check(dto.PaidCost == 0 && dto.PlayedCardInstance.CurrentCost == 7, "animation cost was not kept separate from the card snapshot");
    var serialized = JsonSerializer.SerializeToElement(dto);
    Check(serialized.GetProperty("PaidCost").GetInt32() == 0, "free play cost was not sent");
    var legacy = new ShadowverseBeyond.Server.Dtos.SignalR.Events.CardPlayedEventDto(p1.PlayerId, dto.PlayedCardInstance);
    var restored = JsonSerializer.Deserialize<ShadowverseBeyond.Server.Dtos.SignalR.Events.CardPlayedEventDto>(
        JsonSerializer.Serialize(legacy));
    Check(restored?.PaidCost is null && restored?.PlayMode is null && restored?.PlayedCardInstance.CurrentCost == 7,
        "older events without a paid cost or mode could not be read");
});
Test("stacked numeric attached effects remain visible",()=>{
 activeAbilities.Clear();p1.Field.Clear();var card=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,CardZone.Field);
 for(int i=0;i<2;i++)buffs.RegisterBuffInstance(new Buff(Guid.Empty,card.InstanceId,0,new(){new AbilityKeywordBuffEffect(new(){new(AbilityKeyword.Act,-1,ModifierLayer.Additive)})}){EffectKey="stacked",SourceCardName="Source",SourceAbilityName="Discount"},game);
 var dto=ShadowverseBeyond.Server.Mappings.CardInstanceProjection.Create(card,context);Check(dto.BuffSummaries.Single().Details.Count==2,"stacked amounts lost");
});

Test("applied permanent numeric buffs combine across source turns",()=>{
 activeAbilities.Clear();p1.Field.Clear();var card=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,CardZone.Field);
 for(int i=0;i<2;i++) buffs.RegisterBuffInstance(new Buff(Guid.Empty,card.InstanceId,i,new(){new AbilityKeywordBuffEffect(new(){new(AbilityKeyword.Act,-2,ModifierLayer.Additive)})}){EffectKey="turn"+i,SourceCardName="Source"+i,SourceAbilityName="Discount"+i},game);
 var summaries=ShadowverseBeyond.Server.Mappings.CardInstanceProjection.Create(card,context).BuffSummaries;
 Check(summaries.Count==2,"sources lost");Check(summaries.SelectMany(x=>x.NumericEffects).Select(x=>x.PolicyKey).Distinct().Count()==1,"source turn incorrectly separates permanent buffs");
});
Test("reveal transport includes hand position and log announcement",()=>{
 p2.Hand.Clear();var card=factory.CreateAndRegisterNewCard(game,4,p2.PlayerId,CardZone.Hand);var ev=new CardRevealedEvent(p2,card);
 var processor=new EventProcessor(NullLogger<EventProcessor>.Instance,services.GetRequiredService<IEventHistoryService>());
 var method=typeof(EventProcessor).GetMethod("ConvertCardRevealedEvent",BindingFlags.Instance|BindingFlags.NonPublic)!;
 var dto=(ShadowverseBeyond.Server.Dtos.SignalR.Events.CardRevealedEventDto)method.Invoke(processor,new object[]{ev,context})!;
 Check(dto.HandIndex==0&&dto.RevealingPlayerId==p2.PlayerId,"hand origin lost");Check(ev.GetAnnouncement(game).Contains("公开了"),"log missing");
});
Test("granted ability names carry rules text when description is empty",()=>{
 activeAbilities.Clear();p1.Field.Clear();var card=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,CardZone.Field);
 var text="于手牌中发动。当自己使用其他卡片时，舍弃这张卡片";
 var config=new AbilityConfig{Name=text,Description=""};
 var grant=new Buff(Guid.Empty,card.InstanceId,0,new(){new AbilityBuffEffect(new(){config})}){SourceCardName="荒诞夜恸",SourceAbilityName="Ability1"};
 buffs.RegisterBuffInstance(grant,game);
 var selection=(TargetSelectionService)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(TargetSelectionService));
 var conditionFactory=Proxy<IConditionFactory>.Make((m,a)=>null);
 var targetFactory=Proxy<ITargetFactory>.Make((m,a)=>null);
 activeAbilities.Add(new Ability(config,conditionFactory,targetFactory,selection,card,sourceBuffId:grant.Id));
 var summary=ShadowverseBeyond.Server.Mappings.CardInstanceProjection.Create(card,context).BuffSummaries.Single();Check(summary.GrantedAbilities.Single().Name==text,"rules text missing");Check(summary.Details.Contains(text),"fallback description missing");
});
Test("stacked one-use Act modifiers survive reads and expire after successful use",()=>{
 activeAbilities.Clear();p1.Field.Clear();var card=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,CardZone.Field);
 for(int i=0;i<2;i++)buffs.RegisterBuffInstance(new Buff(Guid.Empty,card.InstanceId,i,new(){new AbilityKeywordBuffEffect(new(){new(AbilityKeyword.Act,-1,ModifierLayer.Additive)})}){RemoveAfterUse=true,SourceCardName="双界异兽",SourceAbilityName="减费"},game);
 Check(buffService.GetEffectiveAbilityKeywordValue(card,AbilityKeyword.Act,2,context)==0,"stacked cost incorrect");
 Check(buffService.GetEffectiveAbilityKeywordValue(card,AbilityKeyword.Act,2,context)==0,"reading consumed reduction");
 var dto=ShadowverseBeyond.Server.Mappings.CardInstanceProjection.Create(card,context);Check(dto.BuffSummaries.SelectMany(x=>x.NumericEffects).All(x=>x.RemoveAfterUse),"one-use display flag lost");
 var result=new ActInvokedEvent(p1,card,0).Commit(context);Check(result.Success,"Act failed");Check(buffService.GetEffectiveAbilityKeywordValue(card,AbilityKeyword.Act,2,context)==2,"reduction not consumed");
 Check(!buffs.GetBuffsForCard(card.InstanceId).Any(x=>x.IsActive),"consumed effect still active");Check(result.GeneratedEvents.OfType<CardInstanceUpdatedEvent>().Any(),"UI not refreshed");
 var copy=buffFactory.CreateBuffCopy(buffs.GetBuffsForCard(card.InstanceId).First());Check(copy.RemoveAfterUse&&!copy.IsActive,"copy revived consumed effect");
});
Test("failed Act does not consume one-use cost modifiers",()=>{
 activeAbilities.Clear();p1.Field.Clear();var card=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,CardZone.Field);
 var buff=new Buff(Guid.Empty,card.InstanceId,0,new(){new AbilityKeywordBuffEffect(new(){new(AbilityKeyword.Act,-1,ModifierLayer.Additive)})}){RemoveAfterUse=true};buffs.RegisterBuffInstance(buff,game);
 var result=new ActInvokedEvent(p1,card,999).Commit(context);Check(!result.Success&&buff.IsActive,"failed action consumed buff");
});
Test("destroy snapshots exist before trigger condition evaluation",()=>{
 var target=factory.CreateAndRegisterNewCard(game,2,p2.PlayerId,CardZone.Field);var source=factory.CreateAndRegisterNewCard(game,3,p1.PlayerId,CardZone.Field);
 var ev=new DestroyEvent(target,source,EventReasonSource.Ability);Check(ev.DestroyTargetSnapshot!=null&&ev.DestroySourceSnapshot!=null,"snapshot missing before On");
 Check(ev.DestroyTargetSnapshot.InstanceId==target.InstanceId,"wrong snapshot");
});
Test("inactive attached effects are hidden",()=>{
 activeAbilities.Clear();p1.Field.Clear();var card=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,CardZone.Field);
 buffs.RegisterBuffInstance(new Buff(Guid.Empty,card.InstanceId,0,new(){new AbilityKeywordBuffEffect(new(){new(AbilityKeyword.Act,-1,ModifierLayer.Additive)})}){IsActive=false,SourceCardName="Source",SourceAbilityName="Discount"},game);
 Check(ShadowverseBeyond.Server.Mappings.CardInstanceProjection.Create(card,context).BuffSummaries.Count==0,"inactive still shown");
});
Test("unsourced active effects retain readable numeric details",()=>{
 activeAbilities.Clear();p1.Field.Clear();var card=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,CardZone.Field);
 buffs.RegisterBuffInstance(new Buff(Guid.Empty,card.InstanceId,0,new(){new AbilityKeywordBuffEffect(new(){new(AbilityKeyword.Act,-2,ModifierLayer.Additive)})}),game);
 Check(ShadowverseBeyond.Server.Mappings.CardInstanceProjection.Create(card,context).BuffSummaries.Single().Details.Single().Contains("-2"),"details hidden");
});
Test("runtime alternate preview includes referenced face data",()=>{
 activeAbilities.Clear();activeAbilities.Add(Proxy<IAbility>.Make((method,args)=>method.Name switch{"get_AbilityKeywords"=>new List<AbilityKeywordConfig>{new(){Keyword=AbilityKeyword.Accelerate,Value=0}},"get_AlternatePlay"=>new AlternatePlayConfig{PlayAsCardDefinitionId=8},"HasKeyword"=>(AbilityKeyword)args![0]! == AbilityKeyword.Accelerate,_=>null}));
 var card=factory.CreateAndRegisterNewCard(game,7,p1.PlayerId,CardZone.Hand);var dto=ShadowverseBeyond.Server.Mappings.CardInstanceProjection.Create(card,context);Check(dto.AlternatePlayReferences.Single().Id==8,"reference missing");Check(dto.AlternatePlayReferences.Single().Name==definitions[8].Name,"face missing");activeAbilities.Clear();
});
Test("alternate availability conditions are evaluated",()=>{
 activeAbilities.Clear();activeAbilities.Add(Proxy<IAbility>.Make((method,args)=>method.Name switch{"get_AbilityKeywords"=>new List<AbilityKeywordConfig>{new(){Keyword=AbilityKeyword.Accelerate,Value=0}},"get_AlternatePlay"=>new AlternatePlayConfig{PlayAsCardDefinitionId=8,AvailabilityConditions=new(){new(){Expression="false"}}},"HasKeyword"=>(AbilityKeyword)args![0]! == AbilityKeyword.Accelerate,_=>null}));
 var card=factory.CreateAndRegisterNewCard(game,7,p1.PlayerId,CardZone.Hand);p1.CurrentPlayPoints=0;Check(!PlayModeResolver.Resolve(card,p1,context).HasAvailableMode,"condition ignored");activeAbilities.Clear();
});
var options=new JsonSerializerOptions{WriteIndented=true};Test("crystallize preflight does not run Last Words or Act while the card is played",()=>{
 var configs=new List<AbilityConfig>{
  new(){Name="月兔妖谢幕曲",Trigger=new(){EventTypes=new(){EventType.Destroy},RelationType=TriggerRelationType.TargetSelf,Timing=TriggerTiming.On}},
  new(){Name="月兔妖启动",Trigger=new(){EventTypes=new(){EventType.Act},RelationType=TriggerRelationType.TargetSelf,Timing=TriggerTiming.On}},
  new(){Name="合法入场曲",Trigger=new(){EventTypes=new(){EventType.PlayCard},RelationType=TriggerRelationType.TargetSelf,Timing=TriggerTiming.On}}
 };
 var selection=(TargetSelectionService)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(TargetSelectionService));
 var conditionFactory=Proxy<IConditionFactory>.Make((m,a)=>null);var targetFactory=Proxy<ITargetFactory>.Make((m,a)=>null);
 var alternateData=Proxy<IGameDataProvider>.Make((m,a)=>m.Name switch{"GetCardDefinition"=>definitions[9],"GetAbilityConfigs"=>configs,_=>null});
 var alternateFactory=Proxy<IAbilityFactory>.Make((m,a)=>m.Name=="CreateAbilities"?configs.Select(config=>new Ability(config,conditionFactory,targetFactory,selection,(CardInstance)a![0]!)).ToList():null);
 var provider=new ServiceCollection().AddSingleton(alternateData).AddSingleton(alternateFactory).AddSingleton<IBuffService>(buffService).AddSingleton<IEventHistoryService>(Proxy<IEventHistoryService>.Make((m,a)=>null)).BuildServiceProvider();
 using var testContext=new GameContext(provider,new MapperConfiguration(cfg=>{}).CreateMapper(),accessor,game,p1.PlayerId,sourceCard:p1.Leader);
 var transaction=(GameActionTransactionService)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(GameActionTransactionService));
 var method=typeof(GameActionTransactionService).GetMethod("ResolveAlternatePlayAbilitiesAsync",BindingFlags.Instance|BindingFlags.NonPublic)!;
 var pending=(Task)method.Invoke(transaction,new object[]{testContext,new PlayCardEvent(p1,p1.Leader,PlayMode.Crystalize,1,null,9),TriggerTiming.On})!;pending.GetAwaiter().GetResult();
 var result=pending.GetType().GetProperty("Result")!.GetValue(pending)!;
 var abilities=(List<IAbility>)result.GetType().GetField("Item2")!.GetValue(result)!;
 Check(abilities.Count==1&&abilities[0].Name=="合法入场曲","unrelated alternate card abilities ran during play");
});
definitions[10]=new(){Id=10,Name="证据护符",Type=CardType.Amulet,Keywords=new(){nameof(CardKeyword.Evidence)}};
definitions[11]=new(){Id=11,Name="土之印护符",Type=CardType.Amulet,Keywords=new(){nameof(CardKeyword.EarthSigil)}};
var evidenceSettlement=new SettleEvidenceEffectHandler(NullLogger<SettleEvidenceEffectHandler>.Instance);
var evidenceModifier=new ModifyEvidenceEffectHandler(NullLogger<ModifyEvidenceEffectHandler>.Instance,new TargetProcessingService(NullLogger<TargetProcessingService>.Instance));
void CommitEvidenceEvents(IEnumerable<IGameEvent> events){var queue=new Queue<IGameEvent>(events);var steps=0;while(queue.Count>0){Check(++steps<100,"evidence event loop");if(queue.Dequeue() is ICommittableEvent ev){var committed=ev.Commit(context);Check(committed.Success,"evidence event commit failed: "+committed.ErrorMessage);foreach(var next in committed.GeneratedEvents)queue.Enqueue(next);}}}
Test("evidence enters with one and merges only friendly evidence amulets by vanishing",()=>{
 activeAbilities.Clear();p1.Field.Clear();p2.Field.Clear();
 var old=factory.CreateAndRegisterNewCard(game,10,p1.PlayerId,CardZone.Field);old.SetCounter(CardInstance.EvidenceCounterKey,3);
 var old2=factory.CreateAndRegisterNewCard(game,10,p1.PlayerId,CardZone.Field);old2.SetCounter(CardInstance.EvidenceCounterKey,2);
 var earth=factory.CreateAndRegisterNewCard(game,11,p1.PlayerId,CardZone.Field);
 var enemy=factory.CreateAndRegisterNewCard(game,10,p2.PlayerId,CardZone.Field);enemy.SetCounter(CardInstance.EvidenceCounterKey,8);
 var entering=factory.CreateAndRegisterNewCard(game,10,p1.PlayerId,CardZone.Field);Check(entering.Evidence==1,"initial evidence not one");
 var result=evidenceSettlement.Apply(new Effect(p1.PlayerId){SelectedTargets=new(){TargetInfo.ForCard(entering)}},context,new());
 Check(result.Success&&entering.Evidence==6,"merged evidence total wrong");Check(result.GeneratedEvents.OfType<LeaveFieldEvent>().Count()==2&&result.GeneratedEvents.OfType<LeaveFieldEvent>().All(e=>e.RequestedKind==LeaveFieldKind.Vanish),"old evidence not vanished");
 CommitEvidenceEvents(result.GeneratedEvents);Check(old.CurrentZone==CardZone.Removed&&old2.CurrentZone==CardZone.Removed,"merged amulets still on field");Check(earth.CurrentZone==CardZone.Field&&earth.Earthsigil==1&&enemy.Evidence==8&&enemy.CurrentZone==CardZone.Field,"earth or opponent evidence affected");
});
Test("evidence cannot be chosen by enemy abilities but is not immune to untargeted effects or ability destruction",()=>{
 p1.Field.Clear();p2.Field.Clear();var own=factory.CreateAndRegisterNewCard(game,10,p1.PlayerId,CardZone.Field);var enemy=factory.CreateAndRegisterNewCard(game,10,p2.PlayerId,CardZone.Field);
 var targets=new List<TargetInfo>{TargetInfo.ForCard(own),TargetInfo.ForCard(enemy)};
 var method=typeof(TargetSelectionService).GetMethod("ApplyAbilityTargetKeywordRules",BindingFlags.Static|BindingFlags.NonPublic)!;
 var chosen=(List<TargetInfo>)method.Invoke(null,new object[]{targets,new TargetConfig(),context,true,true})!;
 Check(chosen.Count==1&&chosen[0].Card==own,"enemy could choose evidence or own evidence blocked");
 var automatic=(List<TargetInfo>)method.Invoke(null,new object[]{targets,new TargetConfig(),context,false,true})!;Check(automatic.Count==2,"random/all ability effects blocked");
 Check(enemy.CanBeDestroyed(context,EventReasonSource.Ability),"evidence incorrectly gained earth destruction protection");
 var earth=factory.CreateAndRegisterNewCard(game,11,p2.PlayerId,CardZone.Field);Check(!earth.CanBeDestroyed(context,EventReasonSource.Ability),"earth destruction protection regressed");
});
Test("evidence add set consume and insufficient consumption preserve counter semantics",()=>{
 p1.Field.Clear();var card=factory.CreateAndRegisterNewCard(game,10,p1.PlayerId,CardZone.Field);var effect=new Effect(p1.PlayerId){SelectedTargets=new(){TargetInfo.ForCard(card)}};
 var added=evidenceModifier.Apply(effect,context,new(){Operation=CounterOperation.Add,Amount=3});Check(added.Success&&card.Evidence==4,"add failed");
 var set=evidenceModifier.Apply(effect,context,new(){Operation=CounterOperation.Set,Amount=7});Check(set.Success&&card.Evidence==7,"set failed");
 var consumed=evidenceModifier.Apply(effect,context,new(){Operation=CounterOperation.Consume,Amount=2});Check(consumed.Success&&card.Evidence==5,"consume failed");
 var insufficient=evidenceModifier.Apply(effect,context,new(){Operation=CounterOperation.Consume,Amount=6});Check(!insufficient.Success&&card.Evidence==5&&!insufficient.GeneratedEvents.Any(),"insufficient consume mutated evidence");
 var invalid=evidenceModifier.Apply(effect,context,new(){Operation=CounterOperation.Add,Amount=-1});Check(!invalid.Success&&card.Evidence==5,"negative evidence accepted");
 var fromPlayer=evidenceModifier.Apply(new Effect(p1.PlayerId),context,new(){Amount=1,PlayerScope=PlayerScope.Self});Check(fromPlayer.Success&&card.Evidence==6,"player scope did not resolve evidence");
 var dto=ShadowverseBeyond.Server.Mappings.CardInstanceProjection.Create(card,context);Check(dto.Evidence==6&&dto.Counters[CardInstance.EvidenceCounterKey]==6,"network counter snapshot missing");
 Check(CardInstanceSnapshot.From(card).Evidence==6&&card.Counters.Evidence==6,"DSL snapshot evidence missing");
});
Test("evidence consumption follows the player scope with inherited card targets", () =>
{
    p1.Field.Clear();
    p2.Field.Clear();
    var ownEvidence = factory.CreateAndRegisterNewCard(game, 10, p1.PlayerId, CardZone.Field);
    var opponentEvidence = factory.CreateAndRegisterNewCard(game, 10, p2.PlayerId, CardZone.Field);
    ownEvidence.SetCounter(CardInstance.EvidenceCounterKey, 2);
    opponentEvidence.SetCounter(CardInstance.EvidenceCounterKey, 2);
    var shuffledCard = factory.CreateAndRegisterNewCard(game, 2, p1.PlayerId, CardZone.Deck);
    var effect = new Effect(p1.PlayerId) { SelectedTargets = [TargetInfo.ForCard(shuffledCard)] };

    var result = evidenceModifier.Apply(effect, context, new()
    {
        Operation = CounterOperation.Consume,
        Amount = 1,
        PlayerScope = PlayerScope.Self
    });

    Check(result.Success && ownEvidence.Evidence == 1 && opponentEvidence.Evidence == 2,
        "an inherited shuffle target prevented consumption of the owner's evidence");
    Check(result.GeneratedEvents.OfType<CounterChangedEvent>().Count() == 1,
        "evidence consumption did not emit one counter update");

    effect.SelectedTargets = [TargetInfo.ForCard(opponentEvidence)];
    result = evidenceModifier.Apply(effect, context, new()
    {
        Operation = CounterOperation.Consume,
        Amount = 1,
        PlayerScope = PlayerScope.Self
    });
    Check(result.Success && ownEvidence.Evidence == 0 && opponentEvidence.Evidence == 2,
        "an inherited evidence target overrode the selected player scope");
});
foreach(var operation in new[]{CounterOperation.Set,CounterOperation.Consume}) Test($"evidence reaching zero by {operation} is system-destroyed",()=>{
 p1.Field.Clear();var card=factory.CreateAndRegisterNewCard(game,10,p1.PlayerId,CardZone.Field);var result=evidenceModifier.Apply(new Effect(p1.PlayerId){SelectedTargets=new(){TargetInfo.ForCard(card)}},context,new(){Operation=operation,Amount=operation==CounterOperation.Set?0:1});
 Check(result.Success&&card.Evidence==0,"zero modification failed");var counter=result.GeneratedEvents.OfType<CounterChangedEvent>().Single();var zero=counter.Commit(context);Check(zero.GeneratedEvents.OfType<LeaveFieldEvent>().Single().ReasonSource==EventReasonSource.System,"zero did not create system destruction");CommitEvidenceEvents(zero.GeneratedEvents);Check(card.CurrentZone==CardZone.Graveyard,"zero evidence did not reach graveyard");
});
Test("generic variable effects also destroy depleted evidence without affecting other counters",()=>{
 p1.Field.Clear();var card=factory.CreateAndRegisterNewCard(game,10,p1.PlayerId,CardZone.Field);var generic=new ModifyVariableEffectHandler(NullLogger<ModifyVariableEffectHandler>.Instance,new TargetProcessingService(NullLogger<TargetProcessingService>.Instance));
 var result=generic.Apply(new Effect(p1.PlayerId){SelectedTargets=new(){TargetInfo.ForCard(card)}},context,new(){TargetType=TargetType.Card,CounterName="EVIDENCE",Operation=CounterOperation.Set,Amount=0});
 CommitEvidenceEvents(result.GeneratedEvents);Check(card.CurrentZone==CardZone.Graveyard,"generic evidence modification bypassed zero destruction");
 var earth=factory.CreateAndRegisterNewCard(game,11,p1.PlayerId,CardZone.Field);earth.SetCounter("example",0);Check(!new CounterChangedEvent(earth,"example",1,0).Commit(context).GeneratedEvents.Any(),"unrelated zero counter destroyed card");
});
Test("evidence system ability responds to both summon causes and is registered with editable Chinese metadata",()=>{
 p1.Field.Clear();var card=factory.CreateAndRegisterNewCard(game,10,p1.PlayerId,CardZone.Field);var system=new ShadowverseBeyond.Server.Domain.Abilities.System.EvidenceSystemAbility();
 foreach(var reason in new[]{EventReasonSource.System,EventReasonSource.Ability}){var summon=new SummonEvent(p1,card,reasonSource:reason);Check(system.CanTrigger(summon,context).GetAwaiter().GetResult(),"evidence summon trigger missing");using var summonContext=new GameContext(services,new MapperConfiguration(cfg=>{}).CreateMapper(),accessor,game,p1.PlayerId,triggeringEvent:summon);var resolved=system.ResolveTargetsAsync(summonContext).GetAwaiter().GetResult();Check(system.CreateEffectsAsync(summonContext,resolved).GetAwaiter().GetResult().Single().Type==EffectType.SettleEvidence,"system did not enqueue settlement");}
 var metadata=new EffectUIMetadataService(new EffectHandlerRegistry(NullLogger<EffectHandlerRegistry>.Instance));var entry=metadata.GetEffectUIMetadata(EffectType.ModifyEvidence)!;Check(entry.DisplayName=="证据数修改"&&entry.CategoryLabel=="资源数值"&&entry.Parameters.Any(p=>p.EnumName==nameof(CounterOperation)),"evidence editor metadata missing");Check(!metadata.GetAllEffectUIMetadata().Any(m=>m.EffectTypeName==nameof(EffectType.SettleEvidence)),"internal evidence settlement exposed in editor");
});
var starlightPatch=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText("data/mini-starlight-migration-20261030.json"))!;
System.Text.Json.Nodes.JsonNode StarlightAfter(int id)=>starlightPatch["patches"]!.AsArray().Single(p=>p!["cardId"]!.GetValue<int>()==id)!["abilities"]![0]!["after"]!;
Test("starlight hand-movement conditions accept only own non-draw hand entry",()=>{
 foreach(var id in new[]{1472298,1472406,1472407}){
  var conditions=StarlightAfter(id)["conditions"]!.AsArray().Select(c=>compiler.CompilePredicate(c!["expression"]!.GetValue<string>())).ToList();
  foreach(var row in new[]{(Own:true,Reason:CardMoveReason.AddedToHand,Zone:CardZone.Hand,Expected:true),(Own:true,Reason:CardMoveReason.Drawn,Zone:CardZone.Hand,Expected:false),(Own:false,Reason:CardMoveReason.AddedToHand,Zone:CardZone.Hand,Expected:false),(Own:true,Reason:CardMoveReason.AddedToDeck,Zone:CardZone.Deck,Expected:false)}){
   var owner=row.Own?p1:p2;var moved=factory.CreateAndRegisterNewCard(game,2,owner.PlayerId,CardZone.Staging);var ev=new CardMovedEvent(moved,CardZone.Staging,row.Zone,row.Reason);using var c=new GameContext(services,new MapperConfiguration(cfg=>{}).CreateMapper(),accessor,game,p1.PlayerId,triggeringEvent:ev,sourceCard:p1.Leader);
   Check(conditions.All(predicate=>predicate(c))==row.Expected,$"card {id} movement scope is wrong");
  }
 }
});
Test("starlight summon-reason condition excludes played followers and supports ability summons",()=>{
 var predicate=compiler.CompilePredicate(StarlightAfter(1472921)["conditions"]![1]!["expression"]!.GetValue<string>());var card=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,CardZone.Field);
 foreach(var reason in new[]{EventReasonSource.System,EventReasonSource.Ability}){using var c=new GameContext(services,new MapperConfiguration(cfg=>{}).CreateMapper(),accessor,game,p1.PlayerId,triggeringEvent:new SummonEvent(p1,card,reasonSource:reason),sourceCard:p1.Leader);Check(predicate(c)==(reason==EventReasonSource.Ability),"wrong summon reason");}
});
Test("starlight training history expressions compile with current-player identity filter",()=>{
 foreach(var id in new[]{1471951,1471952}){var expression=StarlightAfter(id)["conditions"]![0]!["expression"]!.GetValue<string>();Check(expression.Contains("self.CurrentPlayerId == context.owner.playerId"),"owner turn identity missing");compiler.CompilePredicate(expression);}
});
Test("starlight Sato heart captures summoned instance id rather than applying to the source",()=>{
 p1.Field.Clear();var summoned=factory.CreateAndRegisterNewCard(game,2,p1.PlayerId,CardZone.Field);var other=factory.CreateAndRegisterNewCard(game,3,p1.PlayerId,CardZone.Field);var config=StarlightAfter(1472650);var ev=new SummonEvent(p1,summoned);using var summonContext=new GameContext(services,new MapperConfiguration(cfg=>{}).CreateMapper(),accessor,game,p1.PlayerId,triggeringEvent:ev,sourceCard:p1.Leader);
 var captured=compiler.CompileGetter<object>(config["effects"]![0]!["outputs"]!["心随从"]!.GetValue<string>())(new object(),summonContext);Check(captured is Guid guid&&guid==summoned.InstanceId,"summoned instance not captured");
 Check(config["effects"]![0]!["chainedAbilities"]!.AsArray().All(a=>a!["targets"]![0]!["query"]!["sourcePoolExpression"]!.GetValue<string>().Contains("self.instanceId == context.triggeringEvent.Outputs.心随从")),"chain lost captured target");
 var completed=new EffectCompletedEvent(EffectType.None,new Dictionary<string,object>(),new List<TargetInfo>(),new List<IGameEvent>(),new List<CardInstance>(),new List<CardInstanceSnapshot>(),new Dictionary<string,object?>{{"心随从",captured}},true,p1.Leader);
 using var chainContext=new GameContext(services,new MapperConfiguration(cfg=>{}).CreateMapper(),accessor,game,p1.PlayerId,triggeringEvent:completed,sourceCard:p1.Leader);
 var targetExpression=config["effects"]![0]!["chainedAbilities"]![0]!["targets"]![0]!["query"]!["sourcePoolExpression"]!.GetValue<string>();
 var selected=compiler.CompileGetter<object>(targetExpression)(new object(),chainContext);Check(selected is IEnumerable<CardInstance> pool&&pool.Single()==summoned,"chain resolved a different follower");
});
Test("starlight Savior sword activation selects its own amulet",()=>{
 var target=StarlightAfter(1474098)["targets"]!.AsArray().Single()!;var expression=target["query"]!["sourcePoolExpression"]!.GetValue<string>();Check(expression=="context.SourceCard","sword self target missing");
 var getter=compiler.CompileGetter<object>(expression);Check(ReferenceEquals(getter(new object(),context),p1.Leader),"self reference not resolved");
});
Test("match room isolates seat tokens, blocks unauthenticated rejoin, and enforces in-lock token checks",()=>{
 var matches=new MatchService(NullLogger<MatchService>.Instance);
 var hub=Proxy<Microsoft.AspNetCore.SignalR.IHubContext<ShadowverseBeyond.Server.Hubs.GameHub,ShadowverseBeyond.Server.Hubs.ClientInterfaces.IGameClient>>.Make((m,a)=>null);
 var games=Proxy<IGameService>.Make((m,a)=>m.ReturnType==typeof(Task)?Task.CompletedTask:null);
 var decks=Proxy<IDeckService>.Make((m,a)=>null);
 var deckVal=Proxy<IDeckValidationService>.Make((m,a)=>null);
 var deckCode=Proxy<IDeckCodeService>.Make((m,a)=>null);
 var ctrl=new ShadowverseBeyond.Server.Controllers.MatchController(matches,NullLogger<ShadowverseBeyond.Server.Controllers.MatchController>.Instance,hub,games,decks,deckVal,deckCode){ControllerContext=new Microsoft.AspNetCore.Mvc.ControllerContext{HttpContext=new Microsoft.AspNetCore.Http.DefaultHttpContext()}};
 var hostId=Guid.NewGuid();var guestId=Guid.NewGuid();
 var createdRes=(ctrl.Create(new(){HostPlayerId=hostId,IsPublic=false}).Result as Microsoft.AspNetCore.Mvc.CreatedAtActionResult)!.Value as ShadowverseBeyond.Server.Controllers.MatchController.MatchResponse;
 Check(createdRes!=null&&!string.IsNullOrEmpty(createdRes.HostToken)&&createdRes.GuestToken==null&&!string.IsNullOrEmpty(createdRes.JoinCode),"create token scope wrong");
 var joinRes=(ctrl.Join(createdRes!.RoomId,new(){JoinCode=createdRes.JoinCode,PlayerId=guestId}).Result as Microsoft.AspNetCore.Mvc.OkObjectResult)!.Value as ShadowverseBeyond.Server.Controllers.MatchController.MatchResponse;
 Check(joinRes!=null&&joinRes.HostToken==null&&!string.IsNullOrEmpty(joinRes.GuestToken),"join leaked host token");
 var publicGet=(ctrl.Get(createdRes.RoomId).Result as Microsoft.AspNetCore.Mvc.OkObjectResult)!.Value as ShadowverseBeyond.Server.Controllers.MatchController.MatchResponse;
 Check(publicGet!=null&&publicGet.HostToken==null&&publicGet.GuestToken==null&&string.IsNullOrEmpty(publicGet.JoinCode),"unauthenticated GET leaked tokens or private joinCode");
 var spoofRejoin=matches.TryJoin(createdRes.RoomId,createdRes.JoinCode,guestId);
 Check(!spoofRejoin.Success&&spoofRejoin.Error==MatchOperationError.AlreadyFull,"spoofed guestPlayerId stole occupied seat token");
 var validRejoin=matches.TryJoin(createdRes.RoomId,createdRes.JoinCode,guestId,joinRes!.GuestToken);
 Check(validRejoin.Success&&validRejoin.Room!.GuestToken==joinRes.GuestToken,"valid token rejoin failed");
 var wrongTokenSubmit=matches.SubmitDeckSelection(createdRes.RoomId,guestId,new(){DeckId=Guid.NewGuid(),DeckClass=CardClass.Forestcraft},token:"wrong-token");
 Check(!wrongTokenSubmit.Success&&wrongTokenSubmit.Error==MatchOperationError.Forbidden,"in-lock token check bypassed");
});
Test("match room requires self deck before ready, supports cancel ready, kick guest, and join by code",()=>{
 var matches=new MatchService(NullLogger<MatchService>.Instance);
 var hostId=Guid.NewGuid();var guestId=Guid.NewGuid();
 var created=matches.Create(hostId,isPublic:true).Room!;
 Check(!matches.MarkReady(created.RoomId,hostId,true,created.HostToken).Success,"ready allowed before deck selection");
 Check(matches.SubmitDeckSelection(created.RoomId,hostId,new(){DeckId=Guid.NewGuid(),DeckClass=CardClass.Forestcraft,Environment=DeckEnvironment.Rotation,Rule=DeckRule.Standard},token:created.HostToken).Success,"host deck select failed");
 Check(matches.MarkReady(created.RoomId,hostId,true,created.HostToken).Success,"host ready after deck failed");
 var joinedByCode=matches.TryJoinByCode(created.JoinCode,guestId);
 Check(joinedByCode.Success&&joinedByCode.Room!.RoomId==created.RoomId,"join by 6-char code failed");
 var guestToken=joinedByCode.Room.GuestToken!;
 Check(matches.SubmitDeckSelection(created.RoomId,guestId,new(){DeckId=Guid.NewGuid(),DeckClass=CardClass.Swordcraft,Environment=DeckEnvironment.Rotation,Rule=DeckRule.Standard},token:guestToken).Success,"guest deck select failed");
 Check(matches.MarkReady(created.RoomId,guestId,true,guestToken).Room!.Status==MatchStatus.Ready,"both ready did not set Ready status");
 Check(matches.MarkReady(created.RoomId,guestId,false,guestToken).Room!.Status==MatchStatus.Waiting,"cancel ready did not revert to Waiting");
 Check(matches.MarkReady(created.RoomId,guestId,true,guestToken).Room!.Status==MatchStatus.Ready,"re-ready failed");
 var conflictDeck=matches.SubmitDeckSelection(created.RoomId,hostId,new(){DeckId=Guid.NewGuid(),DeckClass=CardClass.Forestcraft,Environment=DeckEnvironment.Unlimited,Rule=DeckRule.Standard},token:created.HostToken).Room!;
 Check(!conflictDeck.GuestReady&&conflictDeck.Status==MatchStatus.Waiting,"format conflict did not reset opponent ready");
 var kicked=matches.KickGuest(created.RoomId,hostId,created.HostToken,guestId);
 Check(kicked.Success&&!kicked.Room!.GuestPlayerId.HasValue,"kick guest failed");
 var publicRejoin=matches.TryJoin(created.RoomId,null,Guid.NewGuid(),allowPublicWithoutCode:true);
 Check(publicRejoin.Success,"public room direct join without code failed");
});
Test("match room completes game idempotently, enables multi-round rematch, and preserves Match Lab first-player config",()=>{
 object?[] configArgs={new ShadowverseBeyond.Server.Controllers.DevToolsMatchesController.DevMatchConfigRequest{FirstPlayerMode="guest"},null,null};typeof(ShadowverseBeyond.Server.Controllers.DevToolsMatchesController).GetMethod("TryBuildConfig",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,configArgs);var cfg=(DevMatchConfig?)configArgs[1];var err=(string?)configArgs[2];
 Check(cfg!=null&&cfg.FirstPlayerMode=="guest"&&err==null,"firstPlayerMode validation failed");
 var matches=new MatchService(NullLogger<MatchService>.Instance);
 var hostId=Guid.NewGuid();var guestId=Guid.NewGuid();
 var room=matches.Create(hostId,devMatchConfig:cfg).Room!;
 Check(room.DevMatchConfig?.FirstPlayerMode=="guest","CloneConfig lost FirstPlayerMode");
 var joined=matches.TryJoin(room.RoomId,room.JoinCode,guestId).Room!;
 matches.SubmitDeckSelection(room.RoomId,hostId,new(){DeckId=Guid.NewGuid(),DeckClass=CardClass.Forestcraft},token:room.HostToken);
 matches.SubmitDeckSelection(room.RoomId,guestId,new(){DeckId=Guid.NewGuid(),DeckClass=CardClass.Swordcraft},token:joined.GuestToken);
 matches.MarkReady(room.RoomId,hostId,true,room.HostToken);
 matches.MarkReady(room.RoomId,guestId,true,joined.GuestToken);
 var started1=matches.Start(room.RoomId,hostId,room.HostToken).Room!;
 var game1=started1.ActiveGameId!;
 Check(started1.Round==1&&started1.Status==MatchStatus.Started,"round 1 start failed");
 var completed1=matches.CompleteGame(game1,hostId).Room!;
 var completedDup=matches.CompleteGame(game1,hostId).Room!;
 Check(completed1.HostWins==1&&completedDup.HostWins==1&&completedDup.Status==MatchStatus.Waiting&&!completedDup.HostReady&&!completedDup.GuestReady&&completedDup.ActiveGameId==game1,"CompleteGame not idempotent or did not reset to Waiting while preserving ActiveGameId");
 matches.MarkReady(room.RoomId,hostId,true,room.HostToken);
 matches.MarkReady(room.RoomId,guestId,true,joined.GuestToken);
 var started2=matches.Start(room.RoomId,hostId,room.HostToken).Room!;
 Check(started2.Round==2&&started2.Status==MatchStatus.Started&&started2.ActiveGameId!=game1,"round 2 rematch start failed");
 var mirageRoom=matches.Create(hostId,mode:GameMode.Mirage).Room!;
 Check(mirageRoom.Mode==GameMode.Mirage,"Mirage room mode not preserved");
 var mirageGuest=matches.TryJoin(mirageRoom.RoomId,mirageRoom.JoinCode,guestId).Room!;
 Check(matches.MarkReady(mirageRoom.RoomId,hostId,true,mirageRoom.HostToken).Success,"Mirage host ready without deck failed");
 Check(matches.MarkReady(mirageRoom.RoomId,guestId,true,mirageGuest.GuestToken).Room!.Status==MatchStatus.Ready,"Mirage guest ready without deck failed");
 Check(matches.Start(mirageRoom.RoomId,hostId,mirageRoom.HostToken).Success,"Mirage room start without deck failed");
});
if (Environment.GetEnvironmentVariable("AI_RULE_REASONING_OUTPUT") is { Length: >0 } reasoningOutput)
    Test("real provider reads class, maximum health and late mode rules", () => AiRuleReasoningProbe.Run(reasoningOutput));
if (Environment.GetEnvironmentVariable("AI_PLAY_BINDING_OUTPUT") is { Length: >0 } bindingOutput)
    Test("real provider selects cheaper lethal forms and preserves fragile copies", () => AiPlayBindingProbe.Run(bindingOutput));
Console.WriteLine(JsonSerializer.Serialize(results,options));
return results.Any(r=>((bool)r.GetType().GetProperty("passed")!.GetValue(r)!) == false)?1:0;

public class Proxy<T> : DispatchProxy where T:class {
 public Func<MethodInfo,object?[]?,object?> Handler = null!;
 public static T Make(Func<MethodInfo,object?[]?,object?> handler){var p=Create<T,Proxy<T>>();((Proxy<T>)(object)p).Handler=handler;return p;}
 protected override object? Invoke(MethodInfo? method,object?[]? args){var value=Handler(method!,args);if(value!=null)return value;var type=method!.ReturnType;if(type==typeof(void))return null;if(type==typeof(bool))return false;if(type.IsValueType)return Activator.CreateInstance(type);return null;}
}
public sealed class HistoryQueryCounter : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor {
 public int Selects;
 private void Count(System.Data.Common.DbCommand command){if(command.CommandText.TrimStart().StartsWith("SELECT",StringComparison.OrdinalIgnoreCase))Interlocked.Increment(ref Selects);}
 public override System.Data.Common.DbDataReader ReaderExecuted(System.Data.Common.DbCommand command,Microsoft.EntityFrameworkCore.Diagnostics.CommandExecutedEventData eventData,System.Data.Common.DbDataReader result){Count(command);return result;}
 public override ValueTask<System.Data.Common.DbDataReader> ReaderExecutedAsync(System.Data.Common.DbCommand command,Microsoft.EntityFrameworkCore.Diagnostics.CommandExecutedEventData eventData,System.Data.Common.DbDataReader result,CancellationToken cancellationToken=default){Count(command);return ValueTask.FromResult(result);}
}
