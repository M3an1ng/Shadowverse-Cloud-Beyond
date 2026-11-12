using System.Reflection;
using System.Text.Json;
using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Persistence;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class AiCompositeTargetTests
{
    const int Leader=2000001520,Small=2000001521,Junk=2000001522;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static object Call(string name,params object[] args)=>typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiTargetScoring")!.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,args)!;
    static EffectConfig Draw()=>new(){Type=EffectType.Draw,Parameters=new(){["PlayerScope"]=new StaticValueConfig{Value="self"},["DrawAmount"]=new StaticValueConfig{Value=1},["UseSearchMode"]=new StaticValueConfig{Value=false}}};
    internal static void Boundaries()
    {
        var damage=new EffectConfig{Type=EffectType.Damage,Parameters=new(){["DamageAmount"]=new StaticValueConfig{Value=1}}};
        int Count(EffectConfig draw,bool current=true)=>((System.Collections.ICollection)Call("FindEffectsForChoice",new[]{damage,draw},"target1",current)).Count;
        Check(Count(Draw())==1&&Count(Draw(),false)==0,"target-independent draw still hides damage score or control no longer reproduces fallback");
        var formula=Draw();formula.Parameters["DrawAmount"]=new FormulaValueConfig{Formula="context.resolvedTargetCards.target1.currentHealth"};Check(Count(formula)==0,"target-dependent formula was dropped");
        var chain=Draw();chain.ChainedAbilities.Add(new());Check(Count(chain)==0,"unknown draw chain was hidden");
        var search=Draw();search.Parameters["UseSearchMode"]=new StaticValueConfig{Value=true};Check(Count(search)==0,"search dependency was hidden");
        var crest=new EffectConfig{Type=EffectType.AddEmblem,Parameters=new(){["PlayerScope"]=new StaticValueConfig{Value="self"},["CardSourceMode"]=new StaticValueConfig{Value="definition"},["CardDefinitionId"]=new StaticValueConfig{Value=new[]{1474276}}}};
        var discard=new EffectConfig{Type=EffectType.Discard};
        Check(((System.Collections.ICollection)Call("FindEffectsForChoice",new[]{discard,crest},"target1",true)).Count==1,"definition crest hides discard material scoring");
        crest.Parameters["CardSourceMode"]=new StaticValueConfig{Value="copy"};Check(((System.Collections.ICollection)Call("FindEffectsForChoice",new[]{discard,crest},"target1",true)).Count==0,"copy-dependent crest incorrectly ignored");
    }
    internal static void Run(string db,string output,string weights)
    {
        Boundaries();var previous=GameDataProvider.Instance;var previousAccessor=GameServiceProvider.ContextAccessor;
        using var catalog=new ServiceCollection().AddLogging(l=>l.ClearProviders()).AddDbContext<ApplicationDbContext>(o=>o.UseSqlite(new SqliteConnectionStringBuilder{DataSource=db,Mode=SqliteOpenMode.ReadOnly}.ToString()))
            .AddScoped<ICardRepository,EFCardRepository>().AddScoped<AbilityLoader>().BuildServiceProvider();
        var cases=new List<object>();
        try{
            var native=new GameDataProvider(catalog.GetRequiredService<IServiceScopeFactory>(),NullLogger<GameDataProvider>.Instance);native.InitializeAsync().GetAwaiter().GetResult();
            foreach(var current in new[]{false,true})foreach(var hp in new[]{20,1}){
                try{cases.Add(Scene(native,current,hp,weights));}catch(Exception e){cases.Add(new{current,enemyHp=hp,passed=false,error=e.GetBaseException().ToString()});}
            }
            File.WriteAllText(output,JsonSerializer.Serialize(new{providerCalls=0,readOnlyCatalog=true,cases,scope="Real 宿星审判 configuration, native targeting/transactions, fixed model. Controlled public 2/1 plus two 1/1 bodies, not a replay of missing historical state."},new JsonSerializerOptions{WriteIndented=true}));
            Check(JsonSerializer.SerializeToElement(cases).EnumerateArray().All(r=>r.GetProperty("passed").GetBoolean()),"Composite target native cases failed: "+output);
            Console.WriteLine("PASS native composite target controls: "+cases.Count+"; provider requests=0");
        }finally{GameDataProvider.SetInstanceForTesting(previous);GameServiceProvider.ContextAccessor=previousAccessor;}
    }
    static object Scene(IGameDataProvider native,bool current,int enemyHp,string weights)
    {
        var defs=native.GetAllCardDefinitions().ToDictionary(p=>p.Key,p=>p.Value);
        defs[Leader]=new(){Id=Leader,Name="public audit leader",Type=CardType.Leader,Health=20};defs[Small]=new(){Id=Small,Name="public 1/1 fixture",Type=CardType.Follower,Attack=1,Health=1,Keywords=["Rush"]};defs[Junk]=new(){Id=Junk,Name="inert private draw",Type=CardType.Spell,Cost=20};
        var data=Proxy<IGameDataProvider>.Make((m,a)=>m.Name switch{"GetCardDefinition"=>defs.GetValueOrDefault((int)a![0]!),"GetAllCardDefinitions"=>defs,"GetAbilityConfigs"=>(int)a![0]!>=Leader?new List<AbilityConfig>():native.GetAbilityConfigs((int)a[0]!),_=>m.Invoke(native,a)});
        GameDataProvider.SetInstanceForTesting(data);var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["AiStrategy:Enabled"]="false",["AiStrategy:IsolateTargetEffects"]=current.ToString(),["AiModel:Path"]=Path.GetFullPath(weights)}).Build();
        var model=new AiLearningModel(new Host(),config,NullLogger<AiLearningModel>.Instance);GameState game=null!;ServiceProvider services=null!;AiService ai=null!;var selected=new List<int>();
        GameStateDto View(){using var s=services.CreateScope();using var c=new GameContext(s.ServiceProvider,s.ServiceProvider.GetRequiredService<IMapper>(),accessor,game,game.Player1.PlayerId);return GameStateProjection.Create(game,c);}
        var pending=Proxy<IPendingActionService>.Make((m,a)=>{if(m.ReturnType==typeof(bool))return false;if(m.Name!="WaitForPlayerChoiceAsync")return null;var req=(PlayerChoiceRequest)a![1]!;
            Check(req.SelectableTargetIds.Count==4,"native pool did not include 2/1, both 1/1s and leader");var choice=ai.DecideTargetChoiceAsync(req,View()).GetAwaiter().GetResult();selected.AddRange(choice.ChosenTargetIds.Select(id=>game.FindCardInstance(id)?.CurrentDefinitionId??0));return Task.FromResult(choice);});
        var games=Proxy<IGameService>.Make((m,a)=>m.Name=="GetGameState"?game:null);
        using(services=AiEndTurnEngineTests.Services(data,accessor,pending,games,s=>s.AddSingleton<IConfiguration>(config))){
            using var scope=services.CreateScope();var sp=scope.ServiceProvider;var factory=sp.GetRequiredService<ICardFactory>();
            var own=new PlayerState(Guid.NewGuid()){IsAI=true,TurnNumber=3,CurrentPlayPoints=3,MaxPlayPoints=3};var enemy=new PlayerState(Guid.NewGuid()){TurnNumber=3};
            game=new("composite-target",own,enemy,own.PlayerId){Status=GameStatus.InProgress,Phase=GamePhase.Main,AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<IBuffManager>()};
            own.Leader=factory.CreateAndRegisterNewCard(game,Leader,own.PlayerId,CardZone.Field);own.Leader.Damage=2;enemy.Leader=factory.CreateAndRegisterNewCard(game,Leader,enemy.PlayerId,CardZone.Field);enemy.Leader.Damage=20-enemyHp;
            var pegasus=factory.CreateAndRegisterNewCard(game,1471971,enemy.PlayerId,CardZone.Field);Check(pegasus.CurrentAttack==2&&pegasus.CurrentHealth==1,"2/1 actual Pegasus changed");
            for(var i=0;i<2;i++)factory.CreateAndRegisterNewCard(game,Small,enemy.PlayerId,CardZone.Field);
            var spell=factory.CreateAndRegisterNewCard(game,1472979,own.PlayerId,CardZone.Hand);factory.CreateAndRegisterNewCard(game,Junk,own.PlayerId,CardZone.Deck);
            ai=new(NullLogger<AiService>.Instance,services.GetRequiredService<IServiceScopeFactory>(),model,config);
            var action=new PlayCardActionDto{CardUid=spell.InstanceId,PlayMode=PlayMode.Base,PaidCost=2};var root=new PlayCardRequestedEvent(own.PlayerId,action);
            using(var c=new GameContext(sp,sp.GetRequiredService<IMapper>(),accessor,game,own.PlayerId,triggeringEvent:root)){
                var tx=sp.GetRequiredService<IGameActionTransactionService>().ProcessTransactionAsync(c,[root]).GetAwaiter().GetResult();Check(tx.IsSuccess&&!tx.IsCancelled,tx.ErrorMessage??"native spell failed");
            }
            if(current)Check(enemyHp==1?enemy.Leader.CurrentHealth==0:pegasus.CurrentZone==CardZone.Graveyard&&enemy.Leader.CurrentHealth==20,"native scoring failed to kill 2/1 or take immediate lethal");
            // The native engine stops the remaining spell effects when leader damage ends the game.
            Check(own.Hand.Count==(enemy.Leader.CurrentHealth==0?0:1)&&own.CurrentPlayPoints==1,"native draw/payment changed");
            return new{current,enemyHp,selected,remainingEnemyHp=enemy.Leader.CurrentHealth,pegasusZone=pegasus.CurrentZone.ToString(),hand=own.Hand.Count,pp=own.CurrentPlayPoints,passed=true};
        }
    }
    sealed class Host:IWebHostEnvironment{public string ApplicationName{get;set;}="target audit";public string EnvironmentName{get;set;}="Test";public string WebRootPath{get;set;}=Path.GetTempPath();public string ContentRootPath{get;set;}=Path.GetTempPath();public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
}
