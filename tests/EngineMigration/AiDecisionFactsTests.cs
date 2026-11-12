using System.Collections;
using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class AiDecisionFactsTests
{
    private static readonly BindingFlags Hidden=BindingFlags.Static|BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly Type Facts=typeof(AiStrategyPlanner).Assembly.GetType("ShadowverseBeyond.Server.Services.AiDecisionFacts")!;
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static object Raw(PlayerChoiceRequest request,int budget=16384)=>Facts.GetMethod("Read",Hidden)!.Invoke(null,[request,budget])!;
    private static JsonElement Packed(PlayerChoiceRequest request,int budget=16384)=>JsonSerializer.SerializeToElement(Raw(request,budget),Json);
    private static AbilityConfig Rule(int amount)=>new(){Name="同名选项",Description="显示文本一样",Effects=[new(){Type=EffectType.Damage,
        Parameters=new(){["DamageAmount"]=new StaticValueConfig{Value=amount}}}]};
    private static PlayerChoiceRequest Choice(Guid player,AbilityConfig first,AbilityConfig second)
    {
        var ids=new[]{Guid.NewGuid(),Guid.NewGuid()};var request=new PlayerChoiceRequest{RequestingPlayerId=player,TargetKey="decision",
            MinSelectionCount=1,MaxSelectionCount=1,SelectableTargetIds=ids.ToList(),
            SelectableCardInstances=ids.Select(id=>new CardInstanceDto{InstanceId=id,Name="同名选项",Description="显示文本一样"}).ToList()};
        Bind(request,new(){{ids[0],first},{ids[1],second}});return request;
    }
    private static void Bind(PlayerChoiceRequest request,Dictionary<Guid,AbilityConfig> bindings)=>
        typeof(PlayerChoiceRequest).GetProperty("DecisionOptions",Hidden)!.SetValue(request,bindings);
    internal static void ExactBindingAndBudgets()
    {
        var own=Guid.NewGuid();var first=Rule(1);var second=Rule(9);var request=Choice(own,first,second);
        var packed=Packed(request);Check(packed.GetProperty("available").GetBoolean()&&packed.GetProperty("rules").GetArrayLength()==2,"same labels collapsed different native rules");
        var again=Packed(Choice(own,first,second));Check(packed.GetRawText()==again.GetRawText(),"new pending GUIDs changed rule identity");
        second.Effects[0].Parameters["DamageAmount"]=new StaticValueConfig{Value=4};
        Check(Packed(request).GetProperty("fingerprint").GetString()!=packed.GetProperty("fingerprint").GetString(),"unchanged labels hid a real configuration change");
        var same=Packed(Choice(own,first,first));Check(same.GetProperty("rules").GetArrayLength()==1&&same.GetProperty("bindings").EnumerateArray().All(b=>b.GetProperty("rule").GetInt32()==0),"identical branches were sent twice or lost an index binding");
        var tiny=Packed(request,8);Check(!tiny.GetProperty("available").GetBoolean()&&tiny.GetProperty("contentBytes").GetInt32()==0
            &&tiny.GetProperty("omitted").GetArrayLength()==2&&tiny.GetProperty("fingerprint").ValueKind==JsonValueKind.Null,"oversized rules were clipped or claimed complete");
        var invalid=Choice(own,first,second);invalid.SelectableTargetIds[1]=invalid.SelectableTargetIds[0];
        Check(!Packed(invalid).GetProperty("available").GetBoolean(),"duplicate legal identities were accepted");
        invalid=Choice(own,first,second);invalid.SelectableTargetIds[0]=Guid.Empty;
        Check(!Packed(invalid).GetProperty("available").GetBoolean(),"empty native identity was accepted");
        invalid=Choice(own,first,second);invalid.TargetKey="discover";
        Check(!Packed(invalid).GetProperty("available").GetBoolean(),"discovery cards were misrepresented as native mode branches");
        Bind(request,new(){{request.SelectableTargetIds[0],first},{Guid.NewGuid(),new(){Name="PRIVATE_UNSELECTABLE_OPTION"}}});
        var missing=Packed(request);Check(!missing.GetProperty("available").GetBoolean()&&!missing.GetRawText().Contains("PRIVATE_"),"missing native binding was inferred, or an unselectable branch leaked");
        var cycle=Rule(2);cycle.Effects[0].ChainedAbilities.Add(cycle);var unreadable=Packed(Choice(own,first,cycle));Check(!unreadable.GetProperty("available").GetBoolean()&&unreadable.GetProperty("omitted").GetArrayLength()==1,"cyclic rule was passed as a complete branch");
        var captured=Rule(3);captured.Properties["captured"]=new StaticValueConfig{Value=new Trap()};
        Check(!Packed(Choice(own,first,captured)).GetProperty("available").GetBoolean()&&Trap.Reads==0,"captured entity getters were evaluated or sent");
        captured.Properties["captured"]=new StaticValueConfig{Value=new Dictionary<string,object>{{"apiKey","PRIVATE_CONFIGURATION_SECRET"}}};
        var privateRule=Packed(Choice(own,first,captured));Check(!privateRule.GetProperty("available").GetBoolean()&&!privateRule.GetRawText().Contains("PRIVATE_CONFIGURATION_SECRET"),"private option fields leaked");
        captured.Properties["captured"]=new StaticValueConfig{Value=Guid.NewGuid()};
        Check(!Packed(Choice(own,first,captured)).GetProperty("available").GetBoolean(),"instance/player identifier was serialized as native rules");
        var text=new string('字',1700)+"TAIL_REAL_RULE";var formula=Rule(0);formula.Description=text;formula.Effects[0].Parameters["DamageAmount"]=new FormulaValueConfig{Formula="context.owner.currentPP+1"};
        formula.Effects.Add(new(){Type=EffectType.GetToken,Parameters=new(){["CardDefinitionId"]=new StaticValueConfig{Value=new[]{99}}}});
        var literal=Packed(Choice(own,formula,first));Check(literal.GetProperty("available").GetBoolean(),"complete literal rules unexpectedly exceeded the shared budget");
        var literalRule=literal.GetProperty("rules")[0];Check(literalRule.GetProperty("text").GetString()==text
            &&literalRule.GetProperty("effects")[0].GetProperty("params").GetProperty("DamageAmount").GetProperty("dsl").GetString()=="context.owner.currentPP+1","literal rule or formula was silently shortened/evaluated");
        var refs=(int[])Raw(Choice(own,formula,first)).GetType().GetProperty("ReferencedCardIds",Hidden)!.GetValue(Raw(Choice(own,formula,first)))!;
        Check(refs.SequenceEqual(new[]{99}),"definition referenced only in resolved mode was omitted");
        Console.WriteLine("PASS exact native option facts: same-name disambiguation, GUID-independent digest, dedup, whole-rule budget, missing/cyclic rejection, formulas and generated references; provider calls=0");
    }

    internal static void ModelAndExperience()
    {
        var original=GameDataProvider.Instance;var root=Path.Combine(Path.GetTempPath(),"ai-decision-facts-"+Guid.NewGuid().ToString("N"));
        var defs=new Dictionary<int,CardDefinition>{[1]=new(){Id=1,Name="Root",Type=CardType.Spell,Cost=1},[99]=new(){Id=99,Name="Resolved-only token",Type=CardType.Follower,Cost=0,Attack=1,Health=1}};
        var revision=Guid.NewGuid();var data=Proxy<IGameDataProvider>.Make((m,a)=>m.Name switch {"get_DataRevision"=>revision,
            "GetCardDefinition"=>defs.GetValueOrDefault((int)a![0]!),"GetAllCardDefinitions"=>defs,"GetAbilityConfigs"=>new List<AbilityConfig>(),
            "GetAllAbilityConfigs"=>defs.Keys.ToDictionary(id=>id,id=>new List<AbilityConfig>()),"GetAllBuffDefinitions"=>new Dictionary<string,BuffDefinition>(),_=>null});
        GameDataProvider.SetInstanceForTesting(data);
        try
        {
            using var http=new Provider();var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
                ["AiStrategy:Enabled"]="true",["AiStrategy:ApiKey"]="offline-fake",["AiStrategy:Endpoint"]="https://example.invalid/chat/completions",["AiStrategy:Model"]="fake",["AiStrategy:MemoryPath"]=root}).Build();
            using var planner=new AiStrategyPlanner(http,config,new Host(),NullLogger<AiStrategyPlanner>.Instance);
            var own=new PlayerState(Guid.NewGuid()){IsAI=true,TurnNumber=3,InitialDeckDefinitionIds=[1]};var enemy=new PlayerState(Guid.NewGuid());var game=new GameState("exact-decision",own,enemy,own.PlayerId);
            var snapshot=new GameStateDto{CurrentPlayerId=own.PlayerId,Players=[new(){PlayerId=own.PlayerId,TurnNumber=3,CurrentPP=3,Hand=[new(){CardId=1,InstanceId=Guid.NewGuid(),Name="Root"}]},
                new(){PlayerId=enemy.PlayerId,Hand=[new(){CardId=404,Name="PRIVATE_ENEMY_HAND"}]}]};
            planner.WarmDeckAsync(game,own.PlayerId,"Neutral")!.GetAwaiter().GetResult();
            Check(planner.PlanAsync(game,snapshot,own.PlayerId,default,(_,_)=>Task.FromResult(new PlayabilityResultDto())).GetAwaiter().GetResult()!=null,"fixture match was not registered through normal planning");
            var choose=typeof(AiStrategyPlanner).GetMethod("ChooseFromOptionsAsync",Hidden)!;var source=new CardInstance(Guid.NewGuid(),1,own.PlayerId);
            List<Guid>? Select(PlayerChoiceRequest req)=>((Task<List<Guid>?>)choose.Invoke(planner,[game,snapshot,req,source,null,CancellationToken.None])!).GetAwaiter().GetResult();
            var small=Rule(1);var large=Rule(9);large.Effects.Add(new(){Type=EffectType.GetToken,Parameters=new(){["CardDefinitionId"]=new StaticValueConfig{Value=new[]{99}}}});
            var first=Choice(own.PlayerId,small,large);Check(Select(first)?.Single()==first.SelectableTargetIds[1],"actual stronger same-name branch was not mapped to selected index");
            var calls=http.Inputs.Count;var repeat=Choice(own.PlayerId,small,large);Check(Select(repeat)?.Single()==repeat.SelectableTargetIds[1]&&http.Inputs.Count==calls,"identical native rules were re-requested instead of remapping cached indices");
            var frame=http.Inputs.Last();var catalog=frame.GetProperty("decisionRules");
            Check(frame.GetProperty("currentAbilityRules").GetProperty("cards").EnumerateArray().Any(c=>c.GetProperty("cardId").GetInt32()==99),"resolved-only generated card lacked actual metadata/rules");
            Check(catalog.GetProperty("contentBytes").GetInt32()+frame.GetProperty("currentAbilityRules").GetProperty("contentBytes").GetInt32()+frame.GetProperty("currentRuntimeRules").GetProperty("contentBytes").GetInt32()<=16384,"mode rules exceeded shared current-rule allowance");
            var hashA=catalog.GetProperty("fingerprint").GetString()!;
            large.Effects[0].Parameters["DamageAmount"]=new StaticValueConfig{Value=0};var changed=Choice(own.PlayerId,small,large);
            Check(Select(changed)?.Single()==changed.SelectableTargetIds[0]&&http.Inputs.Count==calls+1,"same display labels reused a stale rule decision");
            var hashB=http.Inputs.Last().GetProperty("decisionRules").GetProperty("fingerprint").GetString()!;Check(hashA!=hashB,"rule variation did not change fingerprint");
            calls=http.Inputs.Count;var missing=Choice(own.PlayerId,small,large);Bind(missing,new());Check(Select(missing)==null&&http.Inputs.Count==calls,"missing native branches triggered a paid-capable request");
            bool Forbidden(JsonElement value,string? field=null){
                if(value.ValueKind==JsonValueKind.Object)return value.EnumerateObject().Any(item=>Forbidden(item.Value,item.Name));
                if(value.ValueKind==JsonValueKind.Array)return value.EnumerateArray().Any(item=>Forbidden(item,field));
                var idField=field!=null&&(field.Equals("id",StringComparison.OrdinalIgnoreCase)||field.EndsWith("CardId",StringComparison.OrdinalIgnoreCase)
                    ||field.EndsWith("DefinitionId",StringComparison.OrdinalIgnoreCase)||new[]{"played","cardIds","referencedCardIds","unresolvedIds","omittedIds","sourceCardIds"}.Contains(field,StringComparer.OrdinalIgnoreCase));
                return value.ValueKind==JsonValueKind.String&&(value.GetString()!.Contains("PRIVATE_",StringComparison.Ordinal)||idField&&value.GetString()=="404")
                    ||idField&&value.ValueKind==JsonValueKind.Number&&value.TryGetInt32(out var number)&&number==404;
            }
            Check(Forbidden(JsonSerializer.SerializeToElement(new{cardId=404}))&&Forbidden(JsonSerializer.SerializeToElement(new{referencedCardIds=new[]{404}}))
                &&Forbidden(JsonSerializer.SerializeToElement(new{name="PRIVATE_HIDDEN_CANARY"})),"privacy guard missed hidden card IDs or marker values");
            Check(!Forbidden(JsonSerializer.SerializeToElement(new{explanation="各能力",contentBytes=404,score=.404})),"privacy guard confused Unicode escapes or ordinary numbers with a card ID");
            Check(http.Prefixes.Distinct().Count()==1,"shared deck prefix changed between planning and choices");
            Check(!http.Inputs.Any(input=>Forbidden(input)),"hidden card data appeared in native option inputs");
            // Only a committed mode creates experience; a proposed response is not an observed action.
            var committed=new ChooseEvent(changed.SelectableTargetIds,1,source);typeof(ChooseEvent).GetMethod("SetSelectedOptionIndexes",Hidden)!.Invoke(committed,[new[]{0}]);
            planner.ObserveCommittedEvents(game,[committed,new GameEndEvent(own.PlayerId,enemy.PlayerId,"fixture")]);
            var history=Directory.GetFiles(root,"*.json").Where(file=>Path.GetFileNameWithoutExtension(file).Length==64)
                .SelectMany(file=>JsonSerializer.Deserialize<AiStrategyPlanner.Lesson[]>(File.ReadAllText(file),Json)??[]).ToArray();
            Check(history.Length==1&&history[0].Choices?.Single().Position?.NativeOptionRules==hashB,"committed choice lost its actual branch identity");
            ModeStats(planner,data,hashA,hashB);
            Console.WriteLine("PASS fake-provider mode pipeline: exact changed branches/references, repeat cache, constant prefix, missing-binding abstention, hidden-data exclusion and committed experience; real provider calls=0");
        }
        finally
        {
            GameDataProvider.SetInstanceForTesting(original);var path=Path.GetFullPath(root);var temp=Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))+Path.DirectorySeparatorChar;
            if(!path.StartsWith(temp,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(path).StartsWith("ai-decision-facts-"))throw new InvalidOperationException("Unexpected cleanup target");
            if(Directory.Exists(path))Directory.Delete(path,true);
        }
    }
    private static void ModeStats(AiStrategyPlanner planner,IGameDataProvider provider,string a,string b)
    {
        var identity=typeof(AiStrategyPlanner).Assembly.GetType("ShadowverseBeyond.Server.Services.AiDefinitionRules")!;
        var stamp=(AiDefinitionStamp)identity.GetMethod("Read",Hidden)!.Invoke(null,[provider])!;
        AiStrategyPlanner.Lesson Lesson(bool won,string? hash,int second)=>new("Neutral",true,won,8,"fixture",[],[],DateTimeOffset.UtcNow.AddSeconds(second),
            Choices:[new(3,1,"mode",[1],Position:hash==null?null:new(20,20,3,1,0,0,0,0,0){NativeOptionRules=hash})],DefinitionRules:stamp);
        var all=new[]{Lesson(true,a,1),Lesson(false,a,2),Lesson(true,b,3),Lesson(true,null,4)};
        var method=typeof(AiStrategyPlanner).GetMethod("SummarizeExperience",Hidden)!;var packed=JsonSerializer.SerializeToElement(method.Invoke(planner,[all,"Neutral",new string('a',64)]),Json);
        var modes=packed.GetProperty("modes").EnumerateArray().ToArray();
        Check(packed.GetProperty("allGames").GetInt32()==4&&packed.GetProperty("modesExcludedUnbound").GetInt32()==1&&modes.Length==2,"unbound mode history disappeared or inflated known-rule outcomes");
        Check(modes.Single(m=>m.GetProperty("rulesFingerprint").GetString()==a).GetProperty("games").GetInt32()==2
            &&modes.Single(m=>m.GetProperty("rulesFingerprint").GetString()==a).GetProperty("wins").GetInt32()==1
            &&modes.Single(m=>m.GetProperty("rulesFingerprint").GetString()==b).GetProperty("games").GetInt32()==1,"different runtime branches at the same mode index were merged");
    }
    private sealed class Host:IHostEnvironment
    {
        public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="offline decision facts";public string ContentRootPath{get;set;}=Path.GetTempPath();public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
    }
    private sealed class Provider:HttpMessageHandler,IHttpClientFactory
    {
        internal List<JsonElement> Inputs=new();internal List<string> Prefixes=new();
        public HttpClient CreateClient(string name)=>new(this,false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            using var envelope=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));var messages=envelope.RootElement.GetProperty("messages").EnumerateArray().ToArray();
            Prefixes.Add(string.Join("\n",messages.Take(2).Select(m=>m.GetRawText())));var fields=new Dictionary<string,JsonElement>();
            foreach(var message in messages.Where(m=>m.GetProperty("role").GetString()=="user")){using var part=JsonDocument.Parse(message.GetProperty("content").GetString()!);foreach(var p in part.RootElement.EnumerateObject())fields[p.Name]=p.Value.Clone();}
            var input=JsonSerializer.SerializeToElement(fields,Json);Inputs.Add(input);object answer;
            if(input.TryGetProperty("decisionRules",out var rules)&&rules.ValueKind!=JsonValueKind.Null)
            {
                var options=input.GetProperty("options").EnumerateArray().ToArray();var index=options.OrderByDescending(o=>
                    rules.GetProperty("rules")[o.GetProperty("rule").GetInt32()].GetProperty("effects")[0].GetProperty("params").GetProperty("DamageAmount").GetProperty("value").GetDouble()).First().GetProperty("index").GetInt32();
                answer=new{indexes=new[]{index}};
            }
            else if(input.TryGetProperty("legalPlays",out _))answer=new{focus="pressure",plan="fixture",prioritize=Array.Empty<int>(),hold=Array.Empty<int>()};
            else answer=new{gamePlan="fixture",cards=new[]{new{id=1,role="fixture"}}};
            return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=JsonSerializer.Serialize(answer)}}}}))};
        }
    }
    private sealed class Trap
    {
        internal static int Reads;
        public string Hidden {get {Reads++;throw new InvalidOperationException("Hidden entity getter must never be called.");}}
    }
}
