using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Persistence;
using ShadowverseBeyond.Server.Domain.Interfaces;
using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Auras;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class AiDefinitionRulesTests
{
    private static readonly Type Rules=typeof(AiStrategyPlanner).Assembly.GetType("ShadowverseBeyond.Server.Services.AiDefinitionRules")!;
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    private static AiDefinitionStamp Read(IGameDataProvider provider)=>(AiDefinitionStamp)Rules.GetMethod("Read",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[provider])!;
    private static string Compare(AiDefinitionStamp? old,AiDefinitionStamp now,bool mixed=false)=>(string)Rules.GetMethod("Compatibility",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[old,now,mixed])!;
    private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    private static MethodInfo PlannerMethod(string name)=>typeof(AiStrategyPlanner).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!;

    internal static void SemanticIdentity()
    {
        var f=new Catalog();var original=Read(f.Provider);
        Check(original.Available&&original.Cards==2&&original.Abilities==1&&original.Buffs==1,"complete fixture catalog not identified");
        void Changes(Action change,Action restore,string label) {
            var before=Read(f.Provider);change();var after=Read(f.Provider);
            try{Check(after.Available&&before.Fingerprint!=after.Fingerprint,"missed "+label);}
            finally{restore();}
            Check(Read(f.Provider).Fingerprint==before.Fingerprint,"read mutated config or restoration failed for "+label);
        }
        Changes(()=>f.Damage.Value=2,()=>f.Damage.Value=1,"static effect amount with unchanged card text");
        Changes(()=>f.Formula.Formula="context.owner.currentPP+2",()=>f.Formula.Formula="context.owner.currentPP+1","dynamic formula");
        Changes(()=>f.Ability.Conditions[0].Expression="false",()=>f.Ability.Conditions[0].Expression="true","trigger condition");
        Changes(()=>f.Ability.PreConditions.Add(new(){Expression="false"}),()=>f.Ability.PreConditions.Clear(),"playability precondition");
        Changes(()=>f.Ability.Trigger.CountLimit!.MaxPerTurn=2,()=>f.Ability.Trigger.CountLimit!.MaxPerTurn=1,"trigger count limit");
        Changes(()=>f.Ability.Trigger.LimitSameNameOncePerTransaction=true,()=>f.Ability.Trigger.LimitSameNameOncePerTransaction=false,"transaction limit");
        Changes(()=>f.Ability.Trigger.ValidZones.Add(CardZone.Hand),()=>f.Ability.Trigger.ValidZones.Remove(CardZone.Hand),"trigger zone");
        Changes(()=>f.Ability.Effects[0].RepeatCount=2,()=>f.Ability.Effects[0].RepeatCount=0,"repetition");
        Changes(()=>f.Child.Conditions[0].Expression="false",()=>f.Child.Conditions[0].Expression="true","chained ability condition");
        Changes(()=>f.Child.Id=999,()=>f.Child.Id=102,"nested definition ID used by trigger limits");
        Changes(()=>f.Ability.Targets["target"].TargetHandling=TargetHandlingStrategy.BlockPlayerOperation,
            ()=>f.Ability.Targets["target"].TargetHandling=null,"target handling");
        Changes(()=>f.Ability.CounterRegistrations[0].InitialValue=3,()=>f.Ability.CounterRegistrations[0].InitialValue=0,"counter initial value");
        Changes(()=>f.Ability.Targets["target"].Query.Operations.Add(new FilterOperation([new(new FormulaValueConfig{Formula="self.cost"},new StaticValueConfig{Value=2})])),
            ()=>f.Ability.Targets["target"].Query.Operations.Clear(),"polymorphic query filter");
        Changes(()=>f.Ability.IsEnabled=false,()=>f.Ability.IsEnabled=true,"disabled ability");
        Changes(()=>f.Ability.Auras.Add(new AuraConfig{Name="new aura"}),()=>f.Ability.Auras.Clear(),"aura configuration");
        Changes(()=>f.Buff.JsonConfig="{\"amount\":2,\"kind\":\"stat\"}",()=>f.Buff.JsonConfig="{\"amount\":1,\"kind\":\"stat\"}","external buff template");
        Changes(()=>f.Cards[2].Cost=7,()=>f.Cards[2].Cost=2,"card outside initial deck used by global pools");
        Changes(()=>f.Cards[1].CardSetDefinition=new(){Id=1,Environments=[DeckEnvironment.Rotation]},()=>f.Cards[1].CardSetDefinition=null,"environment card pool metadata");
        Changes(()=>f.Ability.Effects.Reverse(),()=>f.Ability.Effects.Reverse(),"equal-order effect list ordering");
        var before=Read(f.Provider);
        f.Ability.Effects[0].Parameters=f.Ability.Effects[0].Parameters.Reverse().ToDictionary(pair=>pair.Key,pair=>pair.Value);
        f.Buff.JsonConfig="{\"kind\":\"stat\",\"amount\":1}";
        Check(Read(f.Provider).Fingerprint==before.Fingerprint,"dictionary insertion order changed semantic identity");
        f.Cards[1].ArtPreview="/changed.webp";f.Cards[1].UpdatedBy="PRIVATE_EDITOR";f.Cards[1].UpdatedAt=DateTime.UtcNow;
        f.Buff.CreatedAt=DateTime.UtcNow;f.Buff.UpdatedAt=DateTime.UtcNow;f.Buff.RowVersion=[1,2,3];
        var cosmetic=Read(f.Provider);
        Check(cosmetic.Fingerprint==before.Fingerprint&&!JsonSerializer.Serialize(cosmetic).Contains("PRIVATE_EDITOR"),"editor/media metadata affected or leaked into identity");
        Check(Compare(null,original)=="unknown"&&Compare(original,original)=="same-config-and-engine"
            &&Compare(original,original with {EngineFingerprint="another-engine"})=="engine-changed"
            &&Compare(original,original,true)=="mixed-observed-configs","compatibility classifications incorrect");
        f.Child.Effects.Add(new(){Type=EffectType.Draw,ChainedAbilities=[f.Ability]});
        Check(!Read(f.Provider).Available,"cyclic configuration claimed a verified identity");f.Child.Effects.Clear();
        f.ExposeBuffs=false;Check(!Read(f.Provider).Available,"unavailable referenced buff templates claimed a verified identity");f.ExposeBuffs=true;
        Check(Read(f.Provider).Available,"temporary invalid data permanently poisoned the catalog");
    }

    internal static void RevisionCache()
    {
        var f=new Catalog{Revision=Guid.NewGuid()};
        var first=Read(f.Provider);var reads=f.CatalogReads;
        Check(Read(f.Provider)==first&&f.CatalogReads==reads,"unchanged published revision was rehashed");
        f.Damage.Value=9;f.Revision=Guid.NewGuid();
        var changed=Read(f.Provider);
        Check(changed.Fingerprint!=first.Fingerprint&&f.CatalogReads==reads+1,"new revision did not invalidate the content cache");
        f.RevisionReader=()=>Guid.NewGuid();
        Check(!Read(f.Provider).Available,"concurrent revision changes produced a trusted mixed snapshot");
    }

    internal static void GuideAndExperience()
    {
        var originalProvider=GameDataProvider.Instance;var f=new Catalog();f.Cards[3]=new(){Id=3,Name="Visible discovered card"};
        f.Cards[4]=new(){Id=4,Name="Config-only token",Cost=0,Attack=1,Health=1,Type=CardType.Follower,Keywords=["Storm"]};
        f.Cards[5]=new(){Id=5,Name="Runtime-only token",Cost=0};
        f.Ability.Effects.Add(new(){Type=EffectType.Summon,Parameters=new(){["CardDefinitionId"]=new StaticValueConfig{Value=4}}});
        GameDataProvider.SetInstanceForTesting(f.Provider);
        var root=Path.Combine(Path.GetTempPath(),"ai-rule-identity-"+Guid.NewGuid().ToString("N"));
        using var http=new Provider();
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["AiStrategy:Enabled"]="true",["AiStrategy:Endpoint"]="https://example.invalid/chat/completions",
            ["AiStrategy:Model"]="fixture",["AiStrategy:MemoryPath"]=root
        }).Build();
        AiStrategyPlanner New()=>new(http,config,new Host(),NullLogger<AiStrategyPlanner>.Instance);
        var own=new PlayerState(Guid.NewGuid()){IsAI=true,TurnNumber=3,InitialDeckDefinitionIds=[1]};var enemy=new PlayerState(Guid.NewGuid());
        var game=new GameState("rule-identity",own,enemy,own.PlayerId);
        try {
            using var planner=New();
            void Warm()=>planner.WarmDeckAsync(game,own.PlayerId,"Neutral")!.GetAwaiter().GetResult();
            Warm();Warm();Check(http.Calls==1,"same rules paid for guide twice");
            Check(http.Prefixes[0].GetProperty("abilityRules").GetProperty("cards")[0].GetProperty("abilities")[0].GetProperty("effects")[0].GetProperty("params").GetProperty("DamageAmount").GetProperty("value").GetInt32()==1,"guide request omitted actual config");
            var referenced=http.Prefixes[0].GetProperty("deckReferences").GetProperty("cards").EnumerateArray().Single(card=>card.GetProperty("id").GetInt32()==4);
            Check(referenced.GetProperty("cost").GetInt32()==0&&referenced.GetProperty("keywords")[0].GetString()=="Storm","guide has config ID but no actual generated-card rules");
            f.Damage.Value=2;Warm();Check(http.Calls==2,"changed actual damage reused old text-matching guide");
            Check(http.Prefixes[1].GetProperty("abilityRules").GetProperty("cards")[0].GetProperty("abilities")[0].GetProperty("effects")[0].GetProperty("params").GetProperty("DamageAmount").GetProperty("value").GetInt32()==2,"new guide only received a changed hash instead of changed damage");
            using(var restarted=New())restarted.WarmDeckAsync(game,own.PlayerId,"Neutral")!.GetAwaiter().GetResult();
            Check(http.Calls==2,"matching rules did not restore disk guide after restart");
            f.Damage.Value=1;Warm();Check(http.Calls==2,"restored rules lost their matching cached guide");
            f.Cards[1].ArtPreview="/new.webp";Warm();Check(http.Calls==2,"art-only update paid again for guide");
            var snapshot=new GameStateDto{GameId=game.GameId,Players=[new(){PlayerId=own.PlayerId,TurnNumber=3,
                Hand=[new(){CardId=2,InstanceId=Guid.NewGuid(),Description="Visible generated card"}],
                Deck=[new(){CardId=405,Name="PRIVATE_OWN_DECK_ORDER"}]},
                new(){PlayerId=enemy.PlayerId,Hand=[new(){CardId=404,Name="PRIVATE_ENEMY_HAND"}],Deck=[new(){CardId=406,Name="PRIVATE_ENEMY_DECK"}]}]};
            Check(planner.PlanAsync(game,snapshot,own.PlayerId,default,(_,_)=>Task.FromResult(new PlayabilityResultDto()),endTurnProjection:new{
                refill=new{drawCount=4,requiredHandCount=0,laterFieldDraws=new[]{new{drawCount=1}}},
                handClearEvolution=new[]{new{discardCount=1,ordinary=true,super=true}}
            },runtimeAbilityRules:AiRuntimeAbilityFactsTests.Sample()).GetAwaiter().GetResult()!=null,"fixture planning failed");
            var turnInput=http.Inputs.Last();var current=turnInput.GetProperty("currentAbilityRules");
            var ending=turnInput.GetProperty("endTurnProjection");
            var refill=ending.GetProperty("refill");
            Check(refill.GetProperty("drawCount").GetInt32()==4 && refill.GetProperty("requiredHandCount").GetInt32()==0
                && refill.GetProperty("laterFieldDraws")[0].GetProperty("drawCount").GetInt32()==1
                && ending.GetProperty("handClearEvolution")[0].GetProperty("ordinary").GetBoolean(),
                "separate refill stages or ordinary hand-clear route did not reach turn planner");
            Check(http.LastTask.Contains("handClearEvolution",StringComparison.Ordinal)
                && http.LastTask.Contains("refill为空时补牌结果记为未知",StringComparison.Ordinal),
                "turn task omitted how to use the refill forecast");
            Check(current.GetProperty("requested").GetInt32()==2&&current.GetProperty("cards").EnumerateArray().Select(card=>card.GetProperty("cardId").GetInt32()).Order().SequenceEqual(new[]{2,5}),"visible/generated runtime reference missing or stable deck rules duplicated");
            var runtime=turnInput.GetProperty("currentRuntimeRules");
            Check(runtime.GetProperty("available").GetBoolean()&&runtime.GetProperty("instances").GetArrayLength()==1
                &&runtime.GetProperty("instances")[0].GetProperty("missingBase")[0].GetProperty("definitionId").GetInt64()==101,"actual runtime grant/removal facts did not reach model input");
            Check(runtime.GetProperty("contentBytes").GetInt32()+current.GetProperty("contentBytes").GetInt32()<=16384
                &&current.GetProperty("byteLimit").GetInt32()==16384-runtime.GetProperty("contentBytes").GetInt32(),"static and runtime facts exceeded shared current-rule allowance");
            Check(!turnInput.GetRawText().Contains("PRIVATE_")&&!current.GetRawText().Contains("404")&&!current.GetRawText().Contains("405")&&!current.GetRawText().Contains("406"),"hidden hand/deck data reached rule facts");
            Check(http.Prefixes.First().GetRawText()==http.Prefixes.Last().GetRawText(),"guide and turn do not share identical rule prefix");
            var candidates=new List<CardInstanceDto>{new(){CardId=2,InstanceId=Guid.NewGuid(),Name="Other card"},new(){CardId=3,InstanceId=Guid.NewGuid(),Name="Visible discovered card"}};
            var choice=new PlayerChoiceRequest{TargetKey="discover",RequestingPlayerId=own.PlayerId,MinSelectionCount=1,MaxSelectionCount=1,
                SelectableTargetIds=candidates.Select(card=>card.InstanceId).ToList(),SelectableCardInstances=candidates};
            List<Guid>? selected=null;
            AiRuntimeAbilityFactsTests.WithChoiceContext(game,f.Provider,own.PlayerId,actual=> {
                candidates=actual;choice.SelectableTargetIds=actual.Select(card=>card.InstanceId).ToList();choice.SelectableCardInstances=actual;
                selected=((Task<List<Guid>?>)PlannerMethod("ChooseFromOptionsAsync").Invoke(planner,[game,snapshot,choice,null,null,CancellationToken.None])!).GetAwaiter().GetResult();
            });
            Check(selected?.Single()==candidates[0].InstanceId,"mock discover selection failed");
            var choiceRuntime=http.Inputs.Last().GetProperty("currentRuntimeRules");
            Check(choiceRuntime.GetProperty("available").GetBoolean()&&choiceRuntime.GetProperty("instances").EnumerateArray()
                .Any(row=>row.GetProperty("instance").GetProperty("zone").GetString()=="choice"&&row.GetProperty("changes").GetArrayLength()==1),"copied discover candidate lacked actual runtime configuration");
            var discoverFacts=http.Inputs.Last().GetProperty("currentAbilityRules");
            Check(discoverFacts.GetProperty("cards").EnumerateArray().Select(card=>card.GetProperty("cardId").GetInt32()).Order().SequenceEqual(new[]{2,3}),"discover candidates lack actual definition facts");
            Check(http.Prefixes.First().GetRawText()==http.Prefixes.Last().GetRawText(),"discover changed shared rule prefix");
            planner.ObserveCommittedEvents(game,[new PlayCardEvent(own,new CardInstance(Guid.NewGuid(),1,own.PlayerId)),new GameEndEvent(own.PlayerId,enemy.PlayerId,"fixture end")]);
            AiStrategyPlanner.HistoryEntry[] History()=>((IReadOnlyList<AiStrategyPlanner.HistoryEntry>)PlannerMethod("ReadHistory").Invoke(planner,null)!).ToArray();
            var entry=History().Single();Check(entry.RuleCompatibility=="same-config-and-engine"&&entry.Lesson.DefinitionRules?.Available==true,"new lesson lost rule identity");
            f.Ability.Trigger.CountLimit!.MaxPerTurn=2;
            Check(History().Single().RuleCompatibility=="config-changed","old experience was treated as current after configuration change");
            var reflection=new AiStrategyPlanner.StrategyReflection(DateTimeOffset.UtcNow,[new("#1 hypothesis",[0])],[entry.Lesson],3);
            var project=typeof(AiStrategyPlanner).GetMethod("ReflectionForModel",BindingFlags.Static|BindingFlags.NonPublic)!;
            using(var model=JsonDocument.Parse(JsonSerializer.Serialize(project.Invoke(null,[reflection,new[]{entry.Lesson}]),Json))) {
                Check(model.RootElement.GetProperty("evidence")[0].GetProperty("ruleCompatibility").GetString()=="config-changed"
                    &&model.RootElement.GetProperty("insightRules")[0].GetProperty("sources")[0].GetProperty("status").GetString()=="config-changed",
                    "shared reflection references lost rule-mismatch labels");
            }
            var mixed=new GameState("mixed-rules",own,enemy,own.PlayerId);
            planner.PlanAsync(mixed,snapshot,own.PlayerId,default,(_,_)=>Task.FromResult(new PlayabilityResultDto())).GetAwaiter().GetResult();
            f.Damage.Value=4;
            planner.ObserveCommittedEvents(mixed,[new PlayCardEvent(own,new CardInstance(Guid.NewGuid(),1,own.PlayerId)),new GameEndEvent(own.PlayerId,enemy.PlayerId,"mixed fixture end")]);
            Check(History().Any(item=>item.RuleCompatibility=="mixed-observed-configs"&&item.Lesson.DefinitionRulesChanged),"rule update during observation was not disclosed");
            var calls=http.Calls;f.Child.Effects.Add(new(){Type=EffectType.Draw,ChainedAbilities=[f.Ability]});
            Check(planner.WarmDeckAsync(game,own.PlayerId,"Neutral")==null&&http.Calls==calls,"invalid catalog restored or regenerated a paid guide");
        }
        finally {
            GameDataProvider.SetInstanceForTesting(originalProvider);var resolved=Path.GetFullPath(root);
            if(!resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)
                ||!Path.GetFileName(resolved).StartsWith("ai-rule-identity-",StringComparison.Ordinal))throw new InvalidOperationException("Unexpected cleanup directory");
            if(Directory.Exists(resolved))Directory.Delete(resolved,true);
        }
    }

    private static JsonElement Facts(IGameDataProvider provider,IEnumerable<int> ids,int bytes=49152) {
        var type=typeof(AiStrategyPlanner).Assembly.GetType("ShadowverseBeyond.Server.Services.AiAbilityFacts")!;
        return JsonSerializer.SerializeToElement(type.GetMethod("Read",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[provider,ids,bytes]),Json);
    }
    internal static void ConfigurationFacts()
    {
        var f=new Catalog();var facts=Facts(f.Provider,[1,2]);
        var card=facts.GetProperty("cards")[0];var ability=card.GetProperty("abilities")[0];
        Check(facts.GetProperty("omitted").GetArrayLength()==0&&facts.GetProperty("requested").GetInt32()==2,"fixture facts missing");
        Check(ability.GetProperty("effects")[0].GetProperty("params").GetProperty("DamageAmount").GetProperty("value").GetInt32()==1,"actual damage not projected");
        Check(ability.GetProperty("properties").GetProperty("calculated").GetProperty("dsl").GetString()==f.Formula.Formula,"DSL missing or evaluated");
        Check(ability.GetProperty("trigger").GetProperty("limit").GetProperty("maxPerTurn").GetInt32()==1,"trigger limits missing");
        Check(ability.GetProperty("targets").GetProperty("target").GetProperty("query").GetProperty("sourcePoolExpression").GetString()=="context.opponent.leader","target pool missing");
        Check(card.GetProperty("buffTemplates")[0].GetProperty("config").GetProperty("amount").GetInt32()==1,"external template missing");
        f.Damage.Value=9;
        Check(Facts(f.Provider,[1]).GetProperty("cards")[0].GetProperty("abilities")[0].GetProperty("effects")[0].GetProperty("params").GetProperty("DamageAmount").GetProperty("value").GetInt32()==9,"changed damage hidden behind unchanged text");
        f.Ability.Effects[0].RepeatCount=2;f.Ability.Effects[0].RepeatCountValue=f.Formula;
        f.Ability.Effects[0].Outputs=new(){["result"]="context.sourceCard"};
        f.Child.IsEnabled=false;f.Child.Conditions[0].IsEnabled=false;
        f.Ability.Targets["target"].PlayerChoiceOptions=new(){IsRequired=true,IsInverseSelection=true,MinCount=2,MaxCount=3};
        f.Ability.Targets["target"].Query.Operations.Add(new FilterOperation([new(new FormulaValueConfig{Formula="self.cost"},new StaticValueConfig{Value=0})]));
        f.Ability.Properties["literalZero"]=new StaticValueConfig{Value=0};
        f.Ability.Properties["literalFalse"]=new StaticValueConfig{Value=false};
        f.Ability.Properties["literalDslString"]=new StaticValueConfig{Value="context.owner.currentPP"};
        f.Ability.Properties["embeddedModes"]=new StaticValueConfig{Value=new[]{new AbilityConfig{Id=909,Name=new string('文',1800)+"TAIL_MODE",Effects=[new(){Type=EffectType.Draw}]}}};
        var detailed=Facts(f.Provider,[1]).GetProperty("cards")[0].GetProperty("abilities")[0];
        var effect=detailed.GetProperty("effects")[0];
        Check(effect.GetProperty("extraRepeats").GetInt32()==2&&effect.GetProperty("extraRepeatsValue").GetProperty("dsl").GetString()==f.Formula.Formula,"repeat precedence inputs missing");
        Check(effect.GetProperty("then")[0].GetProperty("enabled").GetBoolean()==false&&effect.GetProperty("then")[0].GetProperty("conditions")[0].GetProperty("enabled").GetBoolean()==false,"disabled nested config changed meaning");
        Check(effect.GetProperty("outputs").GetProperty("result").GetString()=="context.sourceCard","chain outputs missing");
        var target=detailed.GetProperty("targets").GetProperty("target");
        Check(target.GetProperty("choice").GetProperty("isInverseSelection").GetBoolean()&&target.GetProperty("query").GetProperty("operations")[0].GetProperty("$type").GetString()!=null,"choice or polymorphic filter lost");
        var values=detailed.GetProperty("properties");
        var embedded=values.GetProperty("embeddedModes").GetProperty("value")[0];
        Check(embedded.GetProperty("name").GetString()!.EndsWith("TAIL_MODE")&&embedded.GetProperty("definitionId").GetInt64()==909,"embedded mode text/identity lost");
        Check(values.GetProperty("literalZero").GetProperty("value").GetInt32()==0&&!values.GetProperty("literalFalse").GetProperty("value").GetBoolean()
            &&values.GetProperty("literalDslString").GetProperty("value").GetString()=="context.owner.currentPP","literal values changed semantics");
        var tiny=Facts(f.Provider,[1,2,404,1],80);
        Check(tiny.GetProperty("requested").GetInt32()==3&&tiny.GetProperty("cards").GetArrayLength()==1
            &&tiny.GetProperty("cards")[0].GetProperty("cardId").GetInt32()==2&&tiny.GetProperty("omitted").GetArrayLength()==2,"whole-card budget or missing definition disclosure wrong");
        f.ExposeBuffs=false;
        Check(Facts(f.Provider,[1]).GetProperty("cards")[0].GetProperty("unavailableBuffTemplates")[0].GetString()=="template","unresolved template hidden");
        f.Cards[1].ArtPreview="PRIVATE_ART";f.Cards[1].UpdatedBy="PRIVATE_EDITOR";
        var wire=Facts(f.Provider,[1]).GetRawText();Check(!wire.Contains("PRIVATE_"),"art/editor data leaked");
        f.Child.Effects.Add(new(){ChainedAbilities=[f.Ability]});
        Check(Facts(f.Provider,[1]).GetProperty("omitted")[0].GetProperty("reason").GetString()=="configuration-unreadable","cyclic facts not disclosed");
        var cached=new Catalog{Revision=Guid.NewGuid()};var initial=Facts(cached.Provider,[1]);
        cached.Damage.Value=7;cached.Revision=Guid.NewGuid();
        var refreshed=Facts(cached.Provider,[1]);
        Check(initial.GetProperty("cards")[0].GetProperty("abilities")[0].GetProperty("effects")[0].GetProperty("params").GetProperty("DamageAmount").GetProperty("value").GetInt32()==1
            &&refreshed.GetProperty("cards")[0].GetProperty("abilities")[0].GetProperty("effects")[0].GetProperty("params").GetProperty("DamageAmount").GetProperty("value").GetInt32()==7,"published fact cache retained changed data or mutated previous prompt");
        cached.RevisionReader=()=>Guid.NewGuid();
        Check(Facts(cached.Provider,[1]).GetProperty("cards").GetArrayLength()==0,"concurrent changes claimed coherent facts");
    }

    private static JsonElement Configured(IGameDataProvider provider,int[] ids,string[] texts,int limit=40,int scans=256,int[]? supplied=null) {
        var type=typeof(AiStrategyPlanner).Assembly.GetType("ShadowverseBeyond.Server.Services.AiDefinitionReferences")!;
        return JsonSerializer.SerializeToElement(type.GetMethod("Collect",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[provider,ids,texts,limit,scans,supplied]),Json);
    }
    internal static void DefinitionDependencies()
    {
        var f=new Catalog();
        f.Cards[1].Description="No quoted token names";
        f.Cards[2].Description="Then use 『名字卡』";
        f.Cards[3]=new(){Id=3,Name="同名",Cost=0,Attack=1,Health=1,Type=CardType.Follower,Keywords=["Storm"]};
        f.Cards[4]=new(){Id=4,Name="Transform form",Cost=1};
        f.Cards[5]=new(){Id=5,Name="名字卡",Cost=2};
        f.Cards[6]=new(){Id=6,Name="同名",Cost=9};
        f.Ability.Effects=[new(){Type=EffectType.Summon,RepeatCount=6,Parameters=new(){
            ["CardDefinitionId"]=new StaticValueConfig{Value=new[]{2,3}},["DamageAmount"]=new StaticValueConfig{Value=6},
            ["AbilityConfigs"]=new StaticValueConfig{Value=new[]{new AbilityConfig{Effects=[new(){Type=EffectType.Transform,Parameters=new(){["TargetCardDefinitionId"]=new FormulaValueConfig{Formula="([4])"}}}]}}}}}];
        f.ExtraAbilities[2]=[new(){Effects=[new(){Type=EffectType.Summon,Parameters=new(){["CardDefinitionId"]=new StaticValueConfig{Value=4}}}]}];
        f.ExtraAbilities[3]=[new(){Effects=[new(){Type=EffectType.Summon,Parameters=new(){["CardDefinitionIds"]=new StaticValueConfig{Value=new[]{99}}}}]}];
        f.ExtraAbilities[4]=[new(){AlternatePlay=new(){PlayAsCardDefinitionId=1}}];
        var graph=Configured(f.Provider,[1],[]);
        var rows=graph.GetProperty("cards").EnumerateArray().ToArray();
        Check(rows.Select(card=>card.GetProperty("id").GetInt32()).SequenceEqual(new[]{2,3,4,5}),"ID/mode/transform references or mixed quoted recursion missing; unrelated numeric/name ID included");
        var token=rows.Single(card=>card.GetProperty("id").GetInt32()==3);
        Check(token.GetProperty("cost").GetInt32()==0&&token.GetProperty("keywords")[0].GetString()=="Storm","static generated token lacks real stats and keywords");
        Check(graph.GetProperty("unresolvedIds")[0].GetInt32()==99&&graph.GetProperty("configurationGapCount").GetInt32()==0,"missing static definition not disclosed or constant DSL treated as dynamic");
        var before=graph.GetRawText();
        var reversed=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch {
            "get_DataRevision"=>Guid.Empty,"GetAllCardDefinitions"=>f.Cards.Reverse().ToDictionary(pair=>pair.Key,pair=>pair.Value),
            "GetCardDefinition"=>f.Cards.GetValueOrDefault((int)args![0]!),"GetAbilityConfigs"=>(int)args![0]! ==1?new List<AbilityConfig>{f.Ability}:f.ExtraAbilities.GetValueOrDefault((int)args[0]!)??[],
            "GetAllBuffDefinitions"=>new Dictionary<string,BuffDefinition>(),_=>null});
        Check(Configured(reversed,[1,1],[]).GetRawText()==before,"input dictionary order/duplicate roots broke rule prefix stability");
        var small=Configured(f.Provider,[1],[],1);
        Check(small.GetProperty("cards").GetArrayLength()==1&&small.GetProperty("omittedIdCount").GetInt32()==3&&small.GetProperty("unresolvedIds")[0].GetInt32()==99,"reference limit concealed downstream missing definitions");
        var scanLimited=Configured(f.Provider,[1],[],40,1);
        Check(scanLimited.GetProperty("configurationScanCount").GetInt32()==1&&scanLimited.GetProperty("configurationGapCount").GetInt32()>0,"configuration scan limit missing from coverage");
        Check(Configured(f.Provider,[2],[],40,256,[1,2,3,4,5]).GetProperty("cards").GetArrayLength()==0,"already supplied definitions were repeated");
        var type=typeof(AiStrategyPlanner).Assembly.GetType("ShadowverseBeyond.Server.Services.AiDefinitionReferences")!;
        JsonElement Extract(object payload)=>JsonSerializer.SerializeToElement(type.GetMethod("Extract",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[JsonSerializer.SerializeToElement(payload,Json)]),Json);
        var links=Extract(new Dictionary<string,object?> {
            ["CardId"]=6,["description"]="CardDefinitionId 6",["CardDefinitionIds"]=new{dsl="([2, 3])"},
            ["TargetCardDefinitionId"]=new{value=4},["PlayAsCardDefinitionId"]="5",
            ["nested"]=new{CardDefinitionId=new{dsl="context.owner.hand.select(self.cardId)+999"}},
            ["raw"]=new Dictionary<string,object>{{"CardDefinitionId",new Dictionary<string,object>{{"$type","FormulaValueConfig"},{"Formula","([4])"},{"Value",6}}}},
            ["rawStatic"]=new Dictionary<string,object>{{"CardDefinitionId",new Dictionary<string,object>{{"$type","StaticValueConfig"},{"Value",5},{"Formula","([6])"}}}}
        });
        Check(links.GetProperty("cardIds").EnumerateArray().Select(id=>id.GetInt32()).SequenceEqual(new[]{2,3,4,5})&&links.GetProperty("unresolvedCount").GetInt32()==1,"number scraping or dynamic DSL introduced false references");
        var pathological=Extract(new{CardDefinitionIds=new{dsl=new string('(',50000)+"6"}});
        Check(pathological.GetProperty("cardIds").GetArrayLength()==0&&pathological.GetProperty("unresolvedCount").GetInt32()==1,"long malformed DSL not safely disclosed");
        f.ExtraAbilities[3]![0].Effects[0].Parameters["CardDefinitionIds"]=new FormulaValueConfig{Formula="context.owner.hand.select(self.cardId)"};
        Check(Configured(f.Provider,[1],[]).GetProperty("configurationGapCount").GetInt32()>0,"dynamic reference gap hidden from rule input");
    }

    internal static void NativeCatalog(string source,string output)
    {
        var original=GameDataProvider.Instance;
        var snapshot=Path.Combine(Path.GetTempPath(),"ai-native-rule-"+Guid.NewGuid().ToString("N")+".db");
        using(var input=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=source,Mode=SqliteOpenMode.ReadOnly}.ToString()))
        using(var copy=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=snapshot,Mode=SqliteOpenMode.ReadWriteCreate}.ToString()))
        {input.Open();copy.Open();input.BackupDatabase(copy);}
        var connection=new SqliteConnectionStringBuilder{DataSource=snapshot,Mode=SqliteOpenMode.ReadOnly}.ToString();
        using var services=new ServiceCollection().AddLogging(builder=>builder.ClearProviders())
            .AddDbContext<ApplicationDbContext>(options=>options.UseSqlite(connection))
            .AddScoped<ICardRepository,EFCardRepository>().AddScoped<AbilityLoader>().BuildServiceProvider();
        try {
            var provider=new GameDataProvider(services.GetRequiredService<IServiceScopeFactory>(),NullLogger<GameDataProvider>.Instance);
            provider.InitializeAsync().GetAwaiter().GetResult();
            var timer=Stopwatch.StartNew();var first=Read(provider);var firstMs=timer.Elapsed.TotalMilliseconds;
            timer.Restart();var second=Read(provider);var cachedMs=timer.Elapsed.TotalMilliseconds;
            var reloaded=new GameDataProvider(services.GetRequiredService<IServiceScopeFactory>(),NullLogger<GameDataProvider>.Instance);
            reloaded.InitializeAsync().GetAwaiter().GetResult();var restarted=Read(reloaded);
            var restartStable=first.Fingerprint==restarted.Fingerprint&&first.EngineFingerprint==restarted.EngineFingerprint;
            using var scope=services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nativeFacts=new List<object>();
            foreach(var deckId in new[]{"ca342d12-336c-4ab1-9477-89e8bd34245b","c8685f85-405a-49fd-aa26-6fcb3bdb5996","daf8a8fa-c1d2-4aec-b5e7-a284adc22bf2"}) {
                var guid=Guid.Parse(deckId);var ids=db.UserDeckCards.AsNoTracking().Where(card=>card.DeckId==guid).Select(card=>card.CardDefinitionId).Distinct().OrderBy(id=>id).ToArray();
                if(ids.Length==0)continue;
                var definitions=provider.GetAllCardDefinitions();var textReferences=AiRuleContextForNative(ids,definitions);
                var references=Configured(provider,ids,ids.Where(definitions.ContainsKey).Select(id=>definitions[id].Description).ToArray());
                var referenceIds=references.GetProperty("cards").EnumerateArray().Select(card=>card.GetProperty("id").GetInt32()).ToArray();
                var allIds=ids.Concat(referenceIds).Distinct().ToArray();
                var complete=Facts(provider,allIds,int.MaxValue);var bounded=Facts(provider,allIds);
                var rawBytes=System.Text.Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(allIds.Select(id=>provider.GetAbilityConfigs(id)),new JsonSerializerOptions(Json){Encoder=System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),Converters={new System.Text.Json.Serialization.JsonStringEnumConverter()}}));
                nativeFacts.Add(new{deckId,deckCards=ids.Length,requested=allIds.Length,rawBytes,completeBytes=complete.GetProperty("contentBytes").GetInt32(),bounded,
                    references,configOnlyIds=referenceIds.Except(ids).Except(textReferences).Order().ToArray()});
            }
            var configuredHoldouts=new List<object>();
            foreach(var id in new[]{1451402,1471809,1471938,1471940}) {
                var definitions=provider.GetAllCardDefinitions();if(!definitions.TryGetValue(id,out var card))continue;
                var oldIds=AiRuleContextForNative([id],definitions);
                var configured=Configured(provider,[id],[card.Description]);
                var extra=configured.GetProperty("cards").EnumerateArray().Select(row=>row.GetProperty("id").GetInt32()).Except(oldIds).Except(new[]{id}).Order().ToArray();
                var expectedNewDefinitions=id is 1451402 or 1471809;
                configuredHoldouts.Add(new {cardId=id,card.Name,expectedNewDefinitions,extraDefinitionIds=extra,references=configured});
                Check((extra.Length>0)==expectedNewDefinitions,"native ID-reference positive/control differs from current catalog; inspect "+id);
            }
            Check(configuredHoldouts.Count==4,"native ID-reference holdout definitions unavailable; inspect current catalog");
            File.WriteAllText(output,JsonSerializer.Serialize(new {readOnly=true,isolatedSqliteSnapshot=true,first,cachedStable=first==second,
                cachedObjectReused=ReferenceEquals(first,second),restartStable,distinctProviderRevision=provider.DataRevision!=reloaded.DataRevision,firstMs,cachedMs,nativeFacts,configuredHoldouts,
                scope="Native provider loaded from read-only database; no model request or full battle simulation."},new JsonSerializerOptions(Json){WriteIndented=true}));
            Check(first.Available&&first.Cards>0&&first.Abilities>0&&first==second&&ReferenceEquals(first,second)&&restartStable,
                "native catalog fingerprint unavailable or unchanged revision not reused; inspect report");
        }
        finally {
            GameDataProvider.SetInstanceForTesting(original);
            services.Dispose();SqliteConnection.ClearAllPools();
            var resolved=Path.GetFullPath(snapshot);
            if(!resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)
                ||!Path.GetFileName(resolved).StartsWith("ai-native-rule-",StringComparison.Ordinal))throw new InvalidOperationException("Unexpected snapshot cleanup path");
            foreach(var suffix in new[]{"","-wal","-shm"})if(File.Exists(resolved+suffix))File.Delete(resolved+suffix);
        }
    }

    private static int[] AiRuleContextForNative(int[] ids,IReadOnlyDictionary<int,CardDefinition> definitions) {
        var type=typeof(AiStrategyPlanner).Assembly.GetType("ShadowverseBeyond.Server.Services.AiRuleContext")!;
        var catalog=type.GetMethod("Collect",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[ids.Where(definitions.ContainsKey).Select(id=>definitions[id].Description),definitions,40]);
        return JsonSerializer.SerializeToElement(catalog,Json).GetProperty("cards").EnumerateArray().Select(card=>card.GetProperty("id").GetInt32()).ToArray();
    }
    private sealed class Catalog
    {
        internal readonly Dictionary<int,CardDefinition> Cards=new(){[1]=new(){Id=1,Name="Rule card",Cost=1,Type=CardType.Follower,Description="Unchanged card text"},[2]=new(){Id=2,Name="Other card",Cost=2}};
        internal readonly StaticValueConfig Damage=new(){Value=1};
        internal readonly FormulaValueConfig Formula=new(){Formula="context.owner.currentPP+1"};
        internal readonly AbilityConfig Child=new(){Id=102,Conditions=[new(){Expression="true"}]};
        internal readonly AbilityConfig Ability;
        internal readonly BuffDefinition Buff=new(){Id="template",JsonConfig="{\"amount\":1,\"kind\":\"stat\"}"};
        internal readonly Dictionary<int,List<AbilityConfig>> ExtraAbilities=new();
        internal bool ExposeBuffs=true;internal Guid Revision;internal Func<Guid>? RevisionReader;internal int CatalogReads;
        internal IGameDataProvider Provider {get;}
        internal Catalog() {
            Ability=new(){Id=101,CardId=1,Trigger=new(){EventTypes=[EventType.PlayCard],CountLimit=new(){MaxPerTurn=1}},
                Conditions=[new(){Expression="true"}],CounterRegistrations=[new(){Name="counter"}],
                Targets=new(){["target"]=new(){Query=new(){SourcePoolExpression="context.opponent.leader"}}},
                Properties=new(){["calculated"]=Formula},
                Effects=[new(){Type=EffectType.Damage,Parameters=new(){["DamageAmount"]=Damage,["Other"]=new StaticValueConfig{Value=3}},ChainedAbilities=[Child]},
                    new(){Type=EffectType.AddBuff,Parameters=new(){["BuffDefinitionId"]=new StaticValueConfig{Value="template"}}}]};
            Provider=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch {
                "get_DataRevision"=>RevisionReader?.Invoke()??Revision,
                "GetAllCardDefinitions"=>ReadCards(),"GetCardDefinition"=>Cards.GetValueOrDefault((int)args![0]!),
                "GetAbilityConfigs"=>(int)args![0]! ==1?new List<AbilityConfig>{Ability}:ExtraAbilities.GetValueOrDefault((int)args[0]!)??[],
                "GetAllAbilityConfigs"=>Cards.Keys.ToDictionary(id=>id,id=>id==1?new List<AbilityConfig>{Ability}:ExtraAbilities.GetValueOrDefault(id)??[]),
                "GetAllBuffDefinitions"=>ExposeBuffs?new Dictionary<string,BuffDefinition>{{Buff.Id,Buff}}:null,_=>null});
        }
        private object ReadCards(){CatalogReads++;return Cards;}
    }
    private sealed class Host:IHostEnvironment {
        public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="Rule identity fixtures";
        public string ContentRootPath{get;set;}=".";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
    }
    private sealed class Provider:HttpMessageHandler,IHttpClientFactory {
        internal int Calls;internal readonly List<JsonElement> Prefixes=new();internal readonly List<JsonElement> Inputs=new();
        internal string LastTask = "";
        public HttpClient CreateClient(string name)=>new(this,false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) {
            Calls++;
            using(var body=JsonDocument.Parse(request.Content!.ReadAsStringAsync(token).GetAwaiter().GetResult())) {
                var messages=body.RootElement.GetProperty("messages");
                using var prefix=JsonDocument.Parse(messages[1].GetProperty("content").GetString()!);Prefixes.Add(prefix.RootElement.Clone());
                LastTask=messages[messages.GetArrayLength()-2].GetProperty("content").GetString()!;
                using var input=JsonDocument.Parse(messages[messages.GetArrayLength()-1].GetProperty("content").GetString()!);Inputs.Add(input.RootElement.Clone());
            }
            const string content="""{"gamePlan":"fixture","cards":[{"id":1,"role":"tempo"}],"focus":"value","plan":"fixture","prioritize":[],"hold":[],"indexes":[0]}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content}}}}))});
        }
    }
}
