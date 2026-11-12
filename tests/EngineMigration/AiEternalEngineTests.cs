using System.Reflection;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using AutoMapper;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Persistence;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Models;
using System.Net;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class AiEternalEngineTests
{
    private static readonly Type Policy=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiEternalEngine")!;
    private static readonly BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
    private static object? Call(string name,params object?[] args)=>Policy.GetMethod(name,Hidden)!.Invoke(null,args);
    private static void Check(bool yes,string message){if(!yes)throw new InvalidOperationException(message);}
    private static IDisposable Scope(GameState game,Guid actor)=>(IDisposable)Call("Use",game,actor)!;
    private static CardInstanceDto Card(int id,int cost,CardType type=CardType.Follower)=>new(){CardId=id,InstanceId=Guid.NewGuid(),Cost=cost,CurrentCost=cost,Type=type,Attack=1,Health=1,CurrentAttack=1,CurrentHealth=1};
    internal static void CorePolicy()
    {
        var original=GameDataProvider.Instance;
        var reiss=new CardDefinition{Id=1474120,Type=CardType.Follower,Cost=2};var emblem=new CardDefinition{Id=1474276,Type=CardType.Crest};
        var grant=new AbilityConfig{Trigger=new(){EventTypes=[EventType.PlayCard],RelationType=TriggerRelationType.TargetSelf},
            Effects=[new(){Type=EffectType.AddEmblem,Parameters=new(){["CardDefinitionId"]=new StaticValueConfig{Value=new[]{1474276}},["PlayerScope"]=new StaticValueConfig{Value="self"}}}]};
        var refill=new AbilityConfig{Trigger=new(){EventTypes=[EventType.TurnEnd],ValidZones=[CardZone.Emblem]},
            Conditions=[new(){IsEnabled=true,Expression="count(context.owner.hand)==0"},new(){IsEnabled=true,Expression="context.triggeringEvent.EndingPlayer.playerId==context.owner.playerId"}],
            Effects=[new(){Type=EffectType.Draw,Parameters=new(){["DrawAmount"]=new StaticValueConfig{Value=2},["PlayerScope"]=new StaticValueConfig{Value="self"}}}]};
        var defs=new Dictionary<int,CardDefinition>{{1474120,reiss},{1474276,emblem},{1474113,new(){Id=1474113,Type=CardType.Spell,Cost=1}}};
        var configs=new Dictionary<int,List<AbilityConfig>>{{1474120,[grant]},{1474276,[refill]},{1474113,[]}};
        var data=Proxy<IGameDataProvider>.Make((m,a)=>m.Name switch {"get_DataRevision"=>Guid.Empty,"GetCardDefinition"=>defs.GetValueOrDefault((int)a![0]!),"GetAllCardDefinitions"=>defs,
            "GetAbilityConfigs"=>configs.GetValueOrDefault((int)a![0]!)??[],"GetAllAbilityConfigs"=>configs,"GetAllBuffDefinitions"=>new Dictionary<string,BuffDefinition>(),_=>null});
        GameDataProvider.SetInstanceForTesting(data);
        try
        {
            var own=new PlayerState(Guid.NewGuid()){InitialDeckDefinitionIds=[1474120,1474113]};var enemy=new PlayerState(Guid.NewGuid());var game=new GameState("eternal-policy",own,enemy,own.PlayerId);
            var view=new PlayerStateDto{PlayerId=own.PlayerId,CurrentPP=1,MaxPP=1};var core=Card(1474120,2);var search=Card(1474113,1,CardType.Spell);var tempo=Card(1,1);var spare=Card(2,2);var late=Card(3,6);
            Check(Call("ForModel",view)==null,"deck context leaked before scope");
            using(Scope(game,own.PlayerId))
            {
                view.Hand=[core,search,tempo,late];var replace=(List<string>)Call("Mulligan",view)!;
                Check(replace.SequenceEqual(new[]{late.InstanceId.ToString()}),"mulligan threw away a core access card or cheap tempo");
                view.Hand=[tempo,spare,late];replace=(List<string>)Call("Mulligan",view)!;
                Check(!replace.Contains(tempo.InstanceId.ToString())&&replace.Contains(spare.InstanceId.ToString()),"no-core hand did not search harder while retaining one early body");
                view.Hand=[core,search,spare];Check((double)Call("Retention",view,core)!>=6,"unaffordable first Reiss was not protected");
                view.Hand=[core,search];Check((double)Call("PlayBias",view,search,1)!< -5,"early search discarded the only guaranteed direct core");
                view.Hand=[core,search,spare];
                var duplicate=core with{InstanceId=Guid.NewGuid()};view.Hand=[core,duplicate,spare];
                Check((double)Call("Retention",view,core)!>0&&(double)Call("Retention",view,duplicate)! ==0,"batch material selection did not preserve one designated core instance");
                view.Hand=[core,search,spare];
                Check((double)Call("Retention",view,search)! ==0,"search was protected equally after direct core access was held");
                view.Hand=[search,tempo,spare];Check((double)Call("Retention",view,search)!>4,"only remaining search access was ordinary discard fodder");
                Check((double)Call("PlayBias",view,search,1)!<0,"one-PP turn rigidly used search before a playable body");
                view.CurrentPP=3;Check((double)Call("PlayBias",view,search,1)!>0,"same-turn search plus two-cost engine had no setup priority");
                var fragile=core with{EffectDescriptions=["于手牌中发动。当自己使用其他卡片时，舍弃这张卡片"]};view.Hand=[fragile,tempo];
                Check((double)Call("PlayBias",view,tempo,1)!< -5,"a normal follow-up discarded the searched core before it was played");
                view.Emblem=[Card(1474276,0,CardType.Crest)];Check((double)Call("Retention",view,fragile)! ==0&&(double)Call("PlayBias",view,tempo,1)! ==0,"setup restrictions remained after the emblem existed");
                using(Scope(game,enemy.PlayerId))Check(Call("ForModel",view)==null,"another actor inherited engine scope");
                Check(Call("ForModel",view)!=null,"nested scope did not restore owner");
                own.InitialDeckDefinitionIds=[1474120];using(Scope(game,own.PlayerId))Check(Call("ForModel",view)==null,"another deck with only incidental Reiss inherited the Eternal archetype");
                own.InitialDeckDefinitionIds=[1474120,1474113];
                var legacy=typeof(GameState).GetProperty("LegacyEternalEnginePlayerId",BindingFlags.Instance|BindingFlags.NonPublic)!;
                legacy.SetValue(game,own.PlayerId);using(Scope(game,own.PlayerId))Check(Call("ForModel",view)==null,"comparison control retained the resource-core policy");
                legacy.SetValue(game,null);using(Scope(game,own.PlayerId))Check(Call("ForModel",view)!=null,"comparison switch disabled both policies");
                EvaluationObservations(game,own,enemy);
                view.Emblem.Clear();VerifyModelInput(game,view);
                view.Emblem.Clear();grant.Effects.Clear();Check(!(bool)Call("Active",view)!&&Call("Mulligan",view)==null,"changed rules retained stale role-specific behavior");
            }
            Check(Call("ForModel",view)==null,"resource role leaked after disposal");
            Console.WriteLine("PASS Eternal core role: access-first mulligan, unaffordable retention, early tempo versus same-turn setup, transient core order, established-engine release and actor/rule isolation; provider calls=0");
        }
        finally{GameDataProvider.SetInstanceForTesting(original);}
    }
    internal static void SummonModeRules()
    {
        var tactics=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiGeneratedTokenTactics")!;
        var read=tactics.GetMethod("ReadSummonAttack",Hidden)!;
        var bat=new CardDefinition{Id=1,Type=CardType.Follower,Attack=1,Health=1,Keywords=[]};
        var ghost=new CardDefinition{Id=2,Type=CardType.Follower,Attack=1,Health=1,Keywords=["Storm"]};
        var configs=new List<AbilityConfig>();
        var data=Proxy<IGameDataProvider>.Make((m,a)=>m.Name switch{"GetCardDefinition"=>(int)a![0]! ==1?bat:ghost,"GetAbilityConfigs"=>configs,_=>null});
        var effect=new ShadowverseBeyond.Server.Domain.Abilities.Effects.EffectConfig{Type=ShadowverseBeyond.Server.Domain.Effects.EffectType.Summon,
            Parameters=new(){["CardDefinitionId"]=new StaticValueConfig{Value=new[]{1,2}},["SummonCount"]=new StaticValueConfig{Value=1},["PlayerScope"]=new StaticValueConfig{Value="self"},["CardSourceMode"]=new StaticValueConfig{Value="definition"}}};
        var branch=new AbilityConfig{Name="misleading name",Effects=[effect]};
        object? Read()=>read.Invoke(null,[data,branch]);
        var hit=Read()!;Check((int)hit.GetType().GetProperty("Slots")!.GetValue(hit)! ==2&&(long)hit.GetType().GetProperty("Damage")!.GetValue(hit)! ==1,"summon reader counted non-Storm damage or lost occupied slots");
        branch.Name="search two cards";effect.Parameters["CardDefinitionId"]=new StaticValueConfig{Value=JsonSerializer.SerializeToElement(new[]{2,1})};
        Check(Read()!=null,"mode name/order was used instead of actual definitions");
        effect.Parameters["SummonCount"]=new FormulaValueConfig{Formula="1"};Check(Read()==null,"dynamic count entered static lethal");effect.Parameters["SummonCount"]=new StaticValueConfig{Value=1};
        effect.Parameters["PlayerScope"]=new StaticValueConfig{Value="opponent"};Check(Read()==null,"enemy summons counted as our attack");effect.Parameters["PlayerScope"]=new StaticValueConfig{Value="self"};
        effect.Parameters["CardSourceMode"]=new StaticValueConfig{Value="copy"};Check(Read()==null,"instance/copy state was ignored");effect.Parameters["CardSourceMode"]=new StaticValueConfig{Value="definition"};
        effect.RepeatCount=1;Check(Read()==null,"repeated summon entered a single-mode proof");effect.RepeatCount=0;
        effect.ChainedAbilities.Add(new());Check(Read()==null,"post-summon continuation ignored");effect.ChainedAbilities.Clear();
        branch.Conditions.Add(new(){IsEnabled=true,Expression="false"});Check(Read()==null,"conditional summon treated as guaranteed");branch.Conditions.Clear();
        effect.Parameters["SummonCount"]=new StaticValueConfig{Value=3};Check(Read()==null,"six bodies entered a five-slot proof");effect.Parameters["SummonCount"]=new StaticValueConfig{Value=1};
        ghost.Keywords=["Storm","CannotAttackLeader"];Check(Read()==null,"face-blocked Storm counted");ghost.Keywords=["Storm","Shackle"];Check(Read()==null,"shackled Storm counted");ghost.Keywords=["Rush"];Check(Read()==null,"Rush mistaken for Storm");ghost.Keywords=["Storm"];
        configs.Add(new(){Trigger=new(){EventTypes=[EventType.Attack]},Effects=[]});Check(Read()==null,"unknown token attack response ignored");configs.Clear();
        var choose=new ShadowverseBeyond.Server.Domain.Abilities.Effects.EffectConfig{Type=ShadowverseBeyond.Server.Domain.Effects.EffectType.Choose,
            Parameters=new(){["AbilityConfigs"]=new StaticValueConfig{Value=new[]{branch}},["SelectionCount"]=new StaticValueConfig{Value=1}}};
        var parent=new AbilityConfig{Trigger=new(){EventTypes=[EventType.PlayCard],RelationType=TriggerRelationType.TargetSelf},Effects=[choose]};
        bool Parent()=>(bool)tactics.GetMethod("SimpleSummonDecision",Hidden)!.Invoke(null,[parent])!;
        Check(Parent(),"simple final native choice rejected");
        parent.Effects.Add(effect);Check(!Parent(),"parent continuation ignored");parent.Effects.RemoveAt(1);
        choose.RepeatCount=1;Check(!Parent(),"repeated parent ignored");choose.RepeatCount=0;
        choose.ChainedAbilities.Add(new());Check(!Parent(),"chained parent ignored");choose.ChainedAbilities.Clear();
        parent.Effects.Insert(0,new(){Type=ShadowverseBeyond.Server.Domain.Effects.EffectType.Damage});Check(!Parent(),"unsettled preflight damage accepted");parent.Effects.RemoveAt(0);
        parent.AbilityKeywords.Add(new(){Keyword=AbilityKeyword.Intromezzo});Check(!Parent(),"alternating parent treated as guaranteed");parent.AbilityKeywords.Clear();
        Check(Parent(),"parent guard check leaked mutation");
        Console.WriteLine("PASS summon-mode facts: actual IDs/slots/Storm, renamed and reordered branches; dynamic, copy, conditional, continuation, capacity and combat guards; provider calls=0");
    }
    private static object SourceState(GameState game)=>typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiSummonSourceState")!
        .GetMethod("For",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[game])!;
    private static void NativeSourceGuards(GameState game,GameStateDto snapshot,CardInstance source)
    {
        var method=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiGeneratedTokenTactics")!.GetMethod("ReadSummonLethals",Hidden)!;
        var context=GameServiceProvider.CurrentGameContext!;var player=snapshot.Players.Single(p=>p.PlayerId==source.OwnerPlayerId);var enemy=snapshot.Players.Single(p=>p.PlayerId!=source.OwnerPlayerId);
        int Lines()=>((Array)method.Invoke(null,[context,player,enemy,source])!).Length;
        Check(Lines()==1,"source-card static finish unavailable before native execution");
        var hand=player.Hand;player.Hand=hand.Where(c=>c.InstanceId==source.InstanceId).ToList();Check(Lines()==0,"source route ignored missing discard material");player.Hand=hand;
        var actor=game.CurrentPlayerId;game.CurrentPlayerId=enemy.PlayerId;Check(Lines()==0,"manual turn route used on opponent turn");game.CurrentPlayerId=actor;
        typeof(CardInstance).GetProperty("IsSilenced")!.SetValue(source,true);Check(Lines()==0,"silenced source retained a summon finish");typeof(CardInstance).GetProperty("IsSilenced")!.SetValue(source,false);
        var state=SourceState(game);var comparison=state.GetType().GetField("DisabledPlayer",BindingFlags.Instance|BindingFlags.NonPublic)!;
        comparison.SetValue(state,enemy.PlayerId);Check(Lines()==1,"comparison disabled the other actor's source route");
        comparison.SetValue(state,player.PlayerId);Check(Lines()==0,"comparison control retained the new source proposal");comparison.SetValue(state,null);
        var other=new GameState("isolated-source",new(Guid.NewGuid()),new(Guid.NewGuid()),Guid.NewGuid());
        Check(!ReferenceEquals(state,SourceState(other)),"source comparison leaked across native games");
        Check(Lines()==1,"source-card guard checks leaked mutation");
    }
    private static void NativeModeGuards(GameState game,PlayerChoiceRequest request,GameStateDto snapshot)
    {
        var tactics=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiGeneratedTokenTactics")!;
        var method=tactics.GetMethod("PreferSummonLethal",Hidden)!;var player=snapshot.Players.Single(p=>p.PlayerId==request.RequestingPlayerId);
        var enemy=snapshot.Players.Single(p=>p.PlayerId!=request.RequestingPlayerId);
        var source=(CardInstance)typeof(PlayerChoiceRequest).GetProperty("OptionSourceCard",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(request)!;
        var root=((Ability)game.AbilityManager!.GetAbilityByRuntimeId(request.SourceAbilityRuntimeId)!).Config;
        object? Pick(GameState? match=null)=>method.Invoke(null,[match??game,player,enemy,request,source]);
        Check(Pick()!=null,"native matching context did not expose summon-plus-board lethal");
        enemy.LeaderCard!.Keywords.Add(CardKeyword.Shield);Check(Pick()==null,"shield did not block mode proof");enemy.LeaderCard.Keywords.Remove(CardKeyword.Shield);
        var ready=player.Field.Single();ready.CanAttack=false;Check(Pick()==null,"unready field counted");ready.CanAttack=true;
        ready.AttacksThisTurn=ready.MaxAttacksPerTurn;Check(Pick()==null,"exhausted attacker counted");ready.AttacksThisTurn=0;
        ready.ZoneStayTurn=0;Check(Pick()==null,"new non-Storm attacker counted");ready.ZoneStayTurn=1;
        enemy.Field.Add(Card(1,0));Check(Pick()==null,"unknown defending field ignored");enemy.Field.Clear();
        request.MaxSelectionCount=2;Check(Pick()==null,"multi-pick decision forced as one mode");request.MaxSelectionCount=1;
        var own=game.GetPlayerState(player.PlayerId)!;var extras=Enumerable.Range(0,3).Select(_=>new CardInstance(Guid.NewGuid(),2000000402,own.PlayerId)).ToArray();
        own.Field.AddRange(extras);Check(Pick()==null,"actual full-field slots were ignored");foreach(var extra in extras)own.Field.Remove(extra);
        root.Effects.Add(new(){Type=ShadowverseBeyond.Server.Domain.Effects.EffectType.Damage});Check(Pick()==null,"unresolved parent continuation ignored");root.Effects.RemoveAt(root.Effects.Count-1);
        root.Effects[^1].RepeatCount=1;Check(Pick()==null,"repeated parent decision called a complete lethal");root.Effects[^1].RepeatCount=0;
        Check(Pick(new GameState("unrelated",new(Guid.NewGuid()),new(Guid.NewGuid()),Guid.NewGuid()))==null,"unrelated ambient context used");
        Check(Pick()!=null,"native guard checks leaked state");
    }
    private static void EvaluationObservations(GameState game,PlayerState own,PlayerState enemy)
    {
        var type=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiEternalEvaluation")!;
        own.Hand=[new(Guid.NewGuid(),1474120,own.PlayerId)];
        var collector=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,[game,own.PlayerId],null)!;
        void Observe(string name,params object?[] args)=>type.GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(collector,args);
        own.Hand=[new(Guid.NewGuid(),1474113,own.PlayerId)];Observe("AfterMulligan",own.PlayerId);
        game.CurrentPlayerId=enemy.PlayerId;Observe("AfterOwnTurn",game,own.PlayerId,3);
        own.Hand.Clear();Observe("AfterOwnTurn",game,own.PlayerId,4);
        own.Emblem=[new(Guid.NewGuid(),1474276,own.PlayerId)];Observe("AfterOwnTurn",game,own.PlayerId,5);
        game.CurrentPlayerId=own.PlayerId;Observe("AfterOwnTurn",game,own.PlayerId,6);
        var rows=(AiEternalMatchFacts[])type.GetMethod("Snapshot",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(collector,null)!;
        var row=rows.Single();Check(row.Candidate&&row.PolicyEnabled&&row.CoreBeforeMulligan&&!row.SearchBeforeMulligan
            &&!row.CoreAfterMulligan&&row.SearchAfterMulligan&&row.FirstEmblemObservedOwnTurn==5
            &&row.CompletedOwnTurns==3&&row.EmptyAfterOwnTurnWithoutEmblem==1&&row.EmblemPresentAtEnd,
            "read-only comparison observations lost policy/access scope or counted a midturn return as completed");
        Check(own.Hand.Count==0&&own.Emblem.Count==1,"benchmark observer mutated native cards");
        own.Emblem.Clear();
    }
    private static void VerifyModelInput(GameState game,PlayerStateDto view)
    {
        var folder=Path.Combine(Path.GetTempPath(),"ai-eternal-input-"+Guid.NewGuid().ToString("N"));
        using var handler=new ModelCapture();var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["AiStrategy:Enabled"]="true",["AiStrategy:Endpoint"]="https://example.invalid/chat/completions",["AiStrategy:Model"]="fake",
            ["AiStrategy:GuideCacheEnabled"]="false",["AiStrategy:MemoryPath"]=folder}).Build();
        try
        {
            using var planner=new AiStrategyPlanner(handler,config,new Host(),NullLogger<AiStrategyPlanner>.Instance);
            game.Player1.TurnNumber=3;view.TurnNumber=3;
            Check(planner.WarmDeckAsync(game,view.PlayerId,"Neutral")!.GetAwaiter().GetResult()!=null,"core guide fixture failed");
            var snapshot=new GameStateDto{CurrentPlayerId=view.PlayerId,Players=[view,new(){PlayerId=game.Player2.PlayerId}]};
            Check(planner.PlanAsync(game,snapshot,view.PlayerId,default,(_,_)=>Task.FromResult(new PlayabilityResultDto())).GetAwaiter().GetResult()!=null,"core turn fixture failed");
            var turn=handler.Inputs.Last();Check(handler.Inputs.All(p=>p.GetProperty("deckPrinciples").EnumerateArray().Any(t=>t.GetString()!.Contains("首要资源核心")))
                &&turn.GetProperty("resourceEngine").GetProperty("emblemEstablished").GetBoolean()==false
                &&turn.GetProperty("resourceEngine").GetProperty("coreHandIndexes").GetArrayLength()==1,"core priority or actual missing-emblem state was absent from model input");
            Check(handler.Prefixes.Distinct().Count()==1&&handler.Prefixes.All(p=>p.Contains("deckPrinciples")),"static core principles were repeated in a changing prefix");
        }
        finally
        {
            var path=Path.GetFullPath(folder);Check(path.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(path).StartsWith("ai-eternal-input-"),"unexpected cleanup target");
            if(Directory.Exists(path))Directory.Delete(path,true);
        }
    }
    private sealed class ModelCapture:HttpMessageHandler,IHttpClientFactory
    {
        internal readonly List<JsonElement> Inputs=[];internal readonly List<string> Prefixes=[];private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
        public HttpClient CreateClient(string name)=>new(this,false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));var fields=new Dictionary<string,JsonElement>();
            Prefixes.Add(body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
            foreach(var msg in body.RootElement.GetProperty("messages").EnumerateArray().Where(m=>m.GetProperty("role").GetString()=="user"))
            {using var part=JsonDocument.Parse(msg.GetProperty("content").GetString()!);foreach(var field in part.RootElement.EnumerateObject())fields[field.Name]=field.Value.Clone();}
            var input=JsonSerializer.SerializeToElement(fields,Json);Inputs.Add(input);
            object answer=input.TryGetProperty("legalPlays",out _)?new{focus="setup",plan="fixture",prioritize=Array.Empty<int>(),hold=Array.Empty<int>()}
                :new{gamePlan="fixture",cards=input.GetProperty("deck").EnumerateArray().Select(c=>new{id=c.GetProperty("id").GetInt32(),role="fixture"}).ToArray()};
            return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=JsonSerializer.Serialize(answer)}}}}))};
        }
    }
    internal static void Native(string source,string output,string? modelPath=null,bool modeLethal=false,bool sourceTurn=false)
    {
        var previous=GameDataProvider.Instance;var oldAccessor=GameServiceProvider.ContextAccessor;
        var copy=Path.Combine(Path.GetTempPath(),"ai-eternal-engine-"+Guid.NewGuid().ToString("N")+".db");
        using(var input=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=source,Mode=SqliteOpenMode.ReadOnly}.ToString()))
        using(var backup=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=copy}.ToString())){input.Open();backup.Open();input.BackupDatabase(backup);}
        try
        {
            using var catalog=new ServiceCollection().AddLogging(l=>l.ClearProviders()).AddDbContext<ApplicationDbContext>(o=>o.UseSqlite(new SqliteConnectionStringBuilder{DataSource=copy,Mode=SqliteOpenMode.ReadOnly}.ToString()))
                .AddScoped<ICardRepository,EFCardRepository>().AddScoped<AbilityLoader>().BuildServiceProvider();
            var native=new GameDataProvider(catalog.GetRequiredService<IServiceScopeFactory>(),NullLogger<GameDataProvider>.Instance);native.InitializeAsync().GetAwaiter().GetResult();
            Check((bool)Call("RulesValid")!,"real loaded Reiss/emblem rules failed role gate");
            var rows=sourceTurn?SourceCases(native).ToList():modeLethal?ModeCases(native).ToList():new List<object>{Run(native,3),Run(native,4),Run(native,2),Run(native,1)};string? trainedVersion=null;
            if(modelPath!=null)
            {
                var trained=new AiLearningModel(new Host(),new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["AiModel:Path"]=Path.GetFullPath(modelPath)}).Build(),NullLogger<AiLearningModel>.Instance);
                Check(trained.Current!=null,"requested frozen trained model did not load");trainedVersion=trained.Current!.Version;
                var deck=new List<int>();using var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=copy,Mode=SqliteOpenMode.ReadOnly}.ToString());db.Open();
                using var cmd=db.CreateCommand();cmd.CommandText="SELECT CardDefinitionId,Count FROM UserDeckCards WHERE lower(DeckId)=$deck";cmd.Parameters.AddWithValue("$deck","ca342d12-336c-4ab1-9477-89e8bd34245b");
                using var cards=cmd.ExecuteReader();while(cards.Read())deck.AddRange(Enumerable.Repeat(cards.GetInt32(0),cards.GetInt32(1)));
                Check(deck.Count==40&&deck.Contains(1474120)&&deck.Contains(1474113),"real Eternal specialist deck fixture unavailable");
                rows.AddRange(sourceTurn?SourceCases(native,trained,deck):modeLethal?ModeCases(native,trained,deck):new[]{Run(native,3,trained,deck),Run(native,4,trained,deck),Run(native,2,trained,deck),Run(native,1,trained,deck)});
            }
            File.WriteAllText(output,JsonSerializer.Serialize(new{providerCalls=0,readOnlySource=true,trainedVersion,cases=rows,
                scope="Actual search, discard, grant, play and turn-end native transactions; synthetic leader, deterministic two-definition search pool and playability adapter. Not a broad win-rate evaluation."},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("PASS native Eternal "+(sourceTurn?"complete source-card turn":modeLethal?"summon-mode lethal":"resource core")+": "+rows.Count+" cases; provider calls=0");
        }
        finally
        {
            GameDataProvider.SetInstanceForTesting(previous);GameServiceProvider.ContextAccessor=oldAccessor;SqliteConnection.ClearAllPools();
            if(!Path.GetFullPath(copy).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(copy).StartsWith("ai-eternal-engine-"))throw new InvalidOperationException("Unexpected cleanup target");
            foreach(var suffix in new[]{"","-wal","-shm"})if(File.Exists(copy+suffix))File.Delete(copy+suffix);
        }
    }
    private static object[] SourceCases(IGameDataProvider native,AiLearningModel? model=null,List<int>? deck=null)=>[
        Run(native,1,model,deck,3,"ep",true),Run(native,1,model,deck,1,"none",true),
        Run(native,1,model,deck,3,"empty",true),Run(native,1,model,deck,3,"locked",true),
        Run(native,1,model,deck,3,"used",true),Run(native,0,model,deck,3,"ep",true),
        Run(native,1,model,deck,4,"sep",true),Run(native,1,model,deck,3,"renamed",true),
        Run(native,1,model,deck,3,"disabled-control",true),Run(native,1,model,deck,3,"rejected-playability",true),
        Run(native,1,model,deck,3,"unavailable-base",true),Run(native,1,model,deck,3,"raised-cost",true)];
    private static object[] ModeCases(IGameDataProvider native,AiLearningModel? model=null,List<int>? deck=null)=>[
        Run(native,1,model,deck,2),Run(native,1,model,deck,3),
        Run(native,1,model,deck,3,"ep"),Run(native,1,model,deck,5,"sep"),
        Run(native,1,model,deck,3,"evolve-ghost"),Run(native,1,model,deck,3,"evolve-board"),
        Run(native,1,model,deck,3,"both"),Run(native,1,model,deck,6,"both"),Run(native,1,model,deck,3,"locked"),
        Run(native,1,model,deck,3,"used"),Run(native,1,model,deck,3,"empty"),
        Run(native,1,model,deck,3,"spent-board"),Run(native,1,model,deck,4,"spent-board")];
    private static object Run(IGameDataProvider native,int pp,AiLearningModel? frozen=null,List<int>? initialDeck=null,int? lethalHp=null,string evolution="none",bool beginAtTurn=false)
    {
        const int leader=2000000400,junk=2000000401,zero=2000000402;
        var defs=native.GetAllCardDefinitions().ToDictionary(p=>p.Key,p=>p.Value);defs[leader]=new(){Id=leader,Type=CardType.Leader,Health=20};
        defs[junk]=new(){Id=junk,Type=CardType.Spell,Cost=9};defs[zero]=new(){Id=zero,Type=CardType.Follower,Cost=0,Attack=1,Health=1,Keywords=evolution=="evolve-ghost"?["BanEvolve"]:[]};
        if(evolution=="evolve-board"){var token=defs[1050403];defs[1050403]=new(){Id=token.Id,Name=token.Name,Type=token.Type,Class=token.Class,Cost=token.Cost,Attack=token.Attack,Health=token.Health,Keywords=(token.Keywords??[]).Append("BanEvolve").ToList()};}
        var data=Proxy<IGameDataProvider>.Make((m,a)=>m.Name switch{"get_DataRevision"=>Guid.Empty,"GetCardDefinition"=>defs.GetValueOrDefault((int)a![0]!),"GetAllCardDefinitions"=>defs,
            "GetAbilityConfigs"=>(int)a![0]! is leader or junk or zero?new List<AbilityConfig>():native.GetAbilityConfigs((int)a[0]!),"GetAllBuffDefinitions"=>native.GetAllBuffDefinitions(),_=>null});
        GameDataProvider.SetInstanceForTesting(data);var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
        ServiceProvider services=null!;GameState game=null!;AiService ai=null!;var selectedModes=new List<bool>();
        Func<IGameEvent,Task> nativeSubmit=null!;var submitted=new List<string>();
        GameStateDto Snapshot(){using var sc=services.CreateScope();using var cx=new GameContext(sc.ServiceProvider,sc.ServiceProvider.GetRequiredService<IMapper>(),accessor,game,game.Player1.PlayerId);return GameStateProjection.Create(game,cx);}
        var pending=Proxy<IPendingActionService>.Make((m,a)=>{if(m.ReturnType==typeof(bool))return false;if(m.Name!="WaitForPlayerChoiceAsync")return null;
            var request=(PlayerChoiceRequest)a![1]!;
            if(beginAtTurn&&evolution=="renamed"&&request.TargetKey=="decision"){request.SelectableTargetIds.Reverse();request.SelectableCardInstances=request.SelectableCardInstances.Select(c=>c with{Name="由牌堆抽取两张牌"}).Reverse().ToList();}
            var snapshot=Snapshot();if(!beginAtTurn && lethalHp==2 && evolution=="none" && request.TargetKey=="decision")NativeModeGuards(game,request,snapshot);var answer=ai.DecideTargetChoiceAsync(request,snapshot).GetAwaiter().GetResult();
            if(request.TargetKey=="decision"){var map=(IReadOnlyDictionary<Guid,AbilityConfig>)typeof(PlayerChoiceRequest).GetProperty("DecisionOptions",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(request)!;
                selectedModes.Add((bool)Call("IsSearchBranch",map[answer.ChosenTargetIds.Single()])!);}
            return Task.FromResult(answer);});
        async Task<bool> Enqueue(PlayerActionWrapper action)
        {
            submitted.Add(action.ActionType);
            await nativeSubmit(action.ActionData switch{PlayCardActionDto play=>new PlayCardRequestedEvent(action.PlayerId,play),AttackActionDto hit=>new AttackRequestedEvent(action.PlayerId,hit),EvolveActionDto evolve=>new EvolveRequestedEvent(action.PlayerId,evolve),SuperEvolveActionDto super=>new SuperEvolveRequestedEvent(action.PlayerId,super),_=>new TurnEndEvent(game.Player1)});
            action.MarkCompleted();return true;
        }
        var games=Proxy<IGameService>.Make((m,a)=>m.Name switch{"GetGameState"=>game,"GetCardPlayabilityAsync"=>Playable((Guid)a![2]!),"GetCardPlayabilityForModeAsync"=>Playable((Guid)a![2]!),"EnqueuePlayerActionAsync"=>Enqueue((PlayerActionWrapper)a![1]!),_=>null});
        Task<PlayabilityResultDto> Playable(Guid uid){var card=game.FindCardInstance(uid)!;return Task.FromResult(new PlayabilityResultDto{IsPlayable=evolution!="rejected-playability"&&card.CurrentCost<=game.Player1.CurrentPlayPoints,
            SelectedPlayMode=PlayMode.Base,PlayModes=[new(){Mode=PlayMode.Base,EffectiveCost=evolution=="raised-cost"?2:card.CurrentCost,IsAvailable=evolution!="unavailable-base"}]});}
        using(services=AiEndTurnEngineTests.Services(data,accessor,pending,games))
        {
            using var sc=services.CreateScope();var sp=sc.ServiceProvider;var own=new PlayerState(Guid.NewGuid()){IsAI=true,Class=CardClass.Abysscraft,TurnNumber=pp,CurrentPlayPoints=pp,MaxPlayPoints=pp,InitialDeckDefinitionIds=initialDeck??[1474120,1474120,1474113]};
            var enemy=new PlayerState(Guid.NewGuid());game=new("eternal-"+pp,own,enemy,own.PlayerId){IsLocalAiTraining=true,Phase=GamePhase.Main,Status=GameStatus.InProgress,AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<ShadowverseBeyond.Server.Domain.Buffs.IBuffManager>()};
            var factory=sp.GetRequiredService<ICardFactory>();own.Leader=factory.CreateAndRegisterNewCard(game,leader,own.PlayerId,CardZone.Field);enemy.Leader=factory.CreateAndRegisterNewCard(game,leader,enemy.PlayerId,CardZone.Field);
            for(var n=0;n<3;n++)factory.CreateAndRegisterNewCard(game,junk,enemy.PlayerId,CardZone.Deck);
            var search=factory.CreateAndRegisterNewCard(game,1474113,own.PlayerId,CardZone.Hand);factory.CreateAndRegisterNewCard(game,beginAtTurn||pp==1&&lethalHp==null?1474120:junk,own.PlayerId,CardZone.Hand);
            factory.CreateAndRegisterNewCard(game,1474120,own.PlayerId,CardZone.Deck);factory.CreateAndRegisterNewCard(game,1451404,own.PlayerId,CardZone.Deck);
            var model=frozen??new AiLearningModel(new Host(),new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["AiModel:Path"]=Path.Combine(Path.GetTempPath(),"absent-eternal-"+Guid.NewGuid()+".json")}).Build(),NullLogger<AiLearningModel>.Instance);
            ai=new(NullLogger<AiService>.Instance,services.GetRequiredService<IServiceScopeFactory>(),model);
            if(lethalHp!=null){enemy.Leader.Damage=20-lethalHp.Value;CardInstance? ready=null;if(!beginAtTurn){ready=factory.CreateAndRegisterNewCard(game,zero,own.PlayerId,CardZone.Field);ready.ZoneStayTurn=1;}
                if(evolution!="none"){
                    own.IsEvolutionUnlocked=evolution!="locked";own.IsSuperEvolutionUnlocked=evolution is "sep" or "both";
                    own.MaxEvolutionPoints=1;own.CurrentEvolutionPoints=evolution is "sep" or "empty"?0:1;
                    own.MaxSuperEvolutionPoints=1;own.CurrentSuperEvolutionPoints=evolution is "sep" or "both"?1:0;
                    if(evolution=="used")own.CanEvolve=false;
                    if(evolution=="spent-board")ready!.AttacksThisTurn=1;
                }
            }
            var mulligan=ai.DecideMulliganAsync(Snapshot(),own.PlayerId).GetAwaiter().GetResult();
            Check(!mulligan.Contains(search.InstanceId.ToString())&&mulligan.Count==(beginAtTurn||pp==1&&lethalHp==null?0:1),"actual AI mulligan did not retain the search entrance");
            async Task Submit(IGameEvent ev){using var cx=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId);var r=await sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(cx,[ev]);Check(r.IsSuccess&&!r.IsCancelled,r.ErrorMessage??"native transaction failed");}
            nativeSubmit=Submit;
            if(beginAtTurn)
            {
                var expected=pp==1&&(lethalHp==1||evolution is "ep" or "renamed" or "sep");
                if(evolution=="disabled-control")SourceState(game).GetType().GetField("DisabledPlayer",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(SourceState(game),own.PlayerId);
                if(expected){using var cx=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId);NativeSourceGuards(game,Snapshot(),search);}
                var executor=new AiTurnExecutor(games,ai,NullLogger<AiTurnExecutor>.Instance,sp.GetRequiredService<IMapper>(),services,model);
                executor.ExecuteTurnAsync(game.GameId,own.PlayerId).GetAwaiter().GetResult();
                Console.WriteLine($"Full native source turn: pp={pp}, enemyHp={lethalHp}, evolution={evolution}, winner={game.WinnerId==own.PlayerId}, actions={string.Join(',',submitted)}");
                if(expected)Check(game.WinnerId==own.PlayerId&&submitted.Contains("PlayCard")&&selectedModes.SequenceEqual(new[]{false}),"resource reservation missed a complete source/summon/evolution lethal");
                else Check(game.WinnerId!=own.PlayerId&&!submitted.Contains("PlayCard")&&own.Hand.Any(c=>c.InstanceId==search.InstanceId)&&own.Hand.Any(c=>c.BaseDefinitionId==1474120),"unavailable source route discarded the last guaranteed engine");
                var uses=(long)SourceState(game).GetType().GetField("Uses",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(SourceState(game))!;
                Check(uses==(expected?1:0),"source tactic usage counted a proposal or did not count the completed source action");
                return new{pp,enemyHp=lethalHp,evolution,expectedLethal=expected,trained=frozen!=null,winner=game.WinnerId==own.PlayerId,remainingEnemyHp=enemy.Leader.CurrentHealth,actions=submitted.ToArray(),tacticUses=uses,heldReiss=own.Hand.Any(c=>c.BaseDefinitionId==1474120),passed=true};
            }
            if(pp==1&&lethalHp==null)
            {
                var executor=new AiTurnExecutor(games,ai,NullLogger<AiTurnExecutor>.Instance,sp.GetRequiredService<IMapper>(),services,model);
                executor.ExecuteTurnAsync(game.GameId,own.PlayerId).GetAwaiter().GetResult();
                Check(submitted.SequenceEqual(new[]{"EndTurn"})&&own.Hand.Any(c=>c.BaseDefinitionId==1474120)&&own.Hand.Any(c=>c.BaseDefinitionId==1474113),"one-PP search sacrificed direct core access when simply waiting was valid");
                return new{pp,trained=frozen!=null,searchModes=0,heldReiss=true,heldSearch=true,actions=submitted.ToArray(),passed=true};
            }
            Submit(new PlayCardRequestedEvent(own.PlayerId,new(){CardUid=search.InstanceId,PlayMode=PlayMode.Base,PaidCost=1})).GetAwaiter().GetResult();
            if(lethalHp!=null)
            {
                var choseSearch=selectedModes.Single();Console.WriteLine($"Native night-sorrow mode: enemyHp={lethalHp}, evolution={evolution}, choseSearch={choseSearch}, trained={frozen!=null}");
                var lethal=lethalHp==2||evolution is "ep" or "sep" or "evolve-ghost" or "evolve-board"||evolution=="spent-board"&&lethalHp==3||evolution=="both"&&lethalHp==3;
                if(lethal)
                {
                    Check(!choseSearch,"core setup displaced native summon-plus-existing-board lethal");
                    var executor=new AiTurnExecutor(games,ai,NullLogger<AiTurnExecutor>.Instance,sp.GetRequiredService<IMapper>(),services,model);
                    executor.ExecuteTurnAsync(game.GameId,own.PlayerId).GetAwaiter().GetResult();
                    var attacks=submitted.Count(x=>x=="PerformAttack");
                    Console.WriteLine($"Resolved native lethal: winner={game.WinnerId==own.PlayerId}, enemyHp={enemy.Leader.CurrentHealth}, actions={string.Join(',',submitted)}");
                    Check(game.WinnerId==own.PlayerId&&attacks>=1&&attacks<=2&&(evolution!="none"||attacks==2),"native mode choice did not complete a legal face lethal");
                }
                else Check(choseSearch&&own.Hand.Any(c=>c.BaseDefinitionId==1474120),"insufficient summon damage displaced core setup");
                if(lethal&&evolution!="none")Check(submitted.Count(x=>x is "PerformEvolve" or "PerformSuperEvolve")==1,"lethal route did not perform exactly one legal manual evolution");
                return new{enemyHp=lethalHp,evolution,expectedLethal=lethal,trained=frozen!=null,choseSearch,winner=game.WinnerId==own.PlayerId,remainingEnemyHp=enemy.Leader.CurrentHealth,actions=submitted.ToArray(),passed=true};
            }
            Check(selectedModes.Count>0&&selectedModes.All(x=>x)&&own.Hand.Any(c=>c.BaseDefinitionId==1474120),"search chose tokens or failed to access Reiss");
            if(pp==2)
            {
                factory.CreateAndRegisterNewCard(game,zero,own.PlayerId,CardZone.Hand);using var scope=Scope(game,own.PlayerId);
                var view=Snapshot().Players.Single(p=>p.PlayerId==own.PlayerId);Check((double)Call("PlayBias",view,view.Hand.Single(c=>c.CardId==zero),0)!< -5,"temporary Reiss was sacrificed to a zero-cost follow-up");
                var executor=new AiTurnExecutor(games,ai,NullLogger<AiTurnExecutor>.Instance,sp.GetRequiredService<IMapper>(),services,model);
                executor.ExecuteTurnAsync(game.GameId,own.PlayerId).GetAwaiter().GetResult();
                Check(submitted.SequenceEqual(new[]{"EndTurn"})&&own.Hand.Any(c=>c.BaseDefinitionId==1474120),"full executor discarded the transient core instead of holding until affordable");
                return new{pp,trained=frozen!=null,searchModes=selectedModes.Count,emblem=false,heldReiss=true,actions=submitted.ToArray(),remainingPP=own.CurrentPlayPoints,passed=true};
            }
            var play=ai.DecidePlayCardAsync(Snapshot(),own.PlayerId).GetAwaiter().GetResult();Check(play!=null&&game.FindCardInstance(play.CardUid)!.BaseDefinitionId==1474120,"local selection played another card and lost the searched core");
            Submit(new PlayCardRequestedEvent(own.PlayerId,play!)).GetAwaiter().GetResult();Check(own.Emblem.Any(c=>c.BaseDefinitionId==1474276)&&own.Hand.Count==0,"Reiss failed to establish the empty-hand resource engine");
            Submit(new TurnEndEvent(own)).GetAwaiter().GetResult();Check(own.Hand.Count>=2,"established Reiss emblem did not refill at actual turn end");
            return new{pp,trained=frozen!=null,searchModes=selectedModes.Count,emblem=true,refilled=own.Hand.Count,remainingPP=own.CurrentPlayPoints,passed=true};
        }
    }
    private sealed class Host:IWebHostEnvironment {public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="offline";public string ContentRootPath{get;set;}=Path.GetTempPath();public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();public string WebRootPath{get;set;}=Path.GetTempPath();public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();}
}
