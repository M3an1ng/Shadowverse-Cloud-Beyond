using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

internal static class AiRuntimeAbilityFactsTests
{
    private static readonly Type Facts=typeof(AiTurnExecutor).Assembly.GetType("ShadowverseBeyond.Server.Services.AiRuntimeAbilityFacts")!;
    private static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
    private static readonly BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Static;
    private static JsonElement Pack(object value)=>JsonSerializer.SerializeToElement(value,Json);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    internal static object Sample()
    {
        using var f=new Fixture();f.Grant();
        f.Game.AbilityManager.UnregisterAbility(f.Base,"fixture removal");
        return f.Read();
    }
    internal static void RuntimeChanges()
    {
        using var f=new Fixture();
        Check(Pack(f.Read()).GetProperty("instances").GetArrayLength()==0,"unchanged definition abilities duplicated into dynamic input");
        var grant=f.Grant();grant.Trigger.RecordTrigger();
        f.Game.AbilityManager.UnregisterAbility(f.Base,"fixture removal");
        var previous=Pack(f.Read());var row=previous.GetProperty("instances").EnumerateArray().Single();
        Check(row.GetProperty("instance").GetProperty("zone").GetString()=="hand"&&row.GetProperty("missingBase")[0].GetProperty("definitionId").GetInt64()==101,"removed original ability not associated with the concrete hand instance");
        var change=row.GetProperty("changes")[0];
        Check(change.GetProperty("kind").GetString()=="added"&&change.GetProperty("config").GetProperty("effects")[0].GetProperty("params").GetProperty("DamageAmount").GetProperty("value").GetInt32()==4,"actual grant configuration missing");
        Check(change.GetProperty("triggerCounts").GetProperty("turn").GetInt32()==1&&grant.Trigger.CurrentTurnTriggerCount==1&&grant.Trigger.TriggeredThisEvent,"reading consumed/reset runtime trigger state");
        Check(previous.GetProperty("referencedCardIds").EnumerateArray().Select(id=>id.GetInt32()).SequenceEqual(new[]{5}),"static card dependency in runtime grant was lost or confused with ability ID");
        grant.Config.Effects[0].Parameters["DamageAmount"]=new StaticValueConfig{Value=7};
        var after=Pack(f.Read());
        Check(after.GetRawText().Contains("\"value\":7")&&!previous.GetRawText().Contains("\"value\":7"),"runtime projection reused stale mutable data");
        f.Game.AbilityManager.UnregisterAbility(grant,"grant removed");
        Check(Pack(f.Read()).GetProperty("instances")[0].GetProperty("changes").GetArrayLength()==0,"removed grant still shown as present");
        var normal=f.Game.AbilityManager.CreateAbilitiesForCard(f.Card,[f.Definitions[2]],null,f.Game).Single();
        normal.Trigger.IsEnabled=false;
        Check(Pack(f.Read()).GetProperty("instances")[0].GetProperty("changes")[0].GetProperty("disabled").GetBoolean(),"runtime-disabled trigger treated as usable");
    }
    internal static void PrivacyAndBudget()
    {
        using var f=new Fixture();f.Grant();
        var hidden=f.Factory.CreateAndRegisterNewCard(f.Game,4,f.Enemy.PlayerId,CardZone.Hand);
        f.Game.AbilityManager.CreateAbilitiesForCard(hidden,[new(){Id=404,Name="PRIVATE_ENEMY_HAND_ABILITY"}],null,f.Game);
        var ownDeck=f.Factory.CreateAndRegisterNewCard(f.Game,4,f.Own.PlayerId,CardZone.Deck);
        var foreignDeck=f.Factory.CreateAndRegisterNewCard(f.Game,4,f.Enemy.PlayerId,CardZone.Deck);
        var real=f.Game.AbilityManager;var lookedUp=new List<Guid>();
        f.Game.AbilityManager=Proxy<IAbilityManager>.Make((method,args)=> {
            if(method.Name is "GetAbilitiesForCard" or "GetEffectiveAbilitiesForCard")lookedUp.Add((Guid)args![0]!);
            return method.Invoke(real,args);
        });
        using var context=f.Context();var snapshot=GameStateProjection.Create(f.Game,context);
        lookedUp.Clear(); // Scope the spy to the collector; the internal complete snapshot projection is not a model payload.
        object Read(int limit=8192,IEnumerable<CardInstanceDto>? choices=null)=>Facts.GetMethod("Read",Hidden)!.Invoke(null,[context,snapshot,f.Own.PlayerId,choices,limit])!;
        var clean=Pack(Read());
        Check(!lookedUp.Contains(hidden.InstanceId)&&!lookedUp.Contains(ownDeck.InstanceId)&&!lookedUp.Contains(foreignDeck.InstanceId),"runtime collector inspected a hidden-zone ability");
        Check(!clean.GetRawText().Contains("PRIVATE_")&&!clean.GetRawText().Contains(f.Own.PlayerId.ToString()),"hidden identity escaped runtime input");
        var limited=Pack(Read(0));Check(limited.GetProperty("instances").GetArrayLength()==0&&limited.GetProperty("omitted").GetArrayLength()==1,"byte limit clipped or silently removed a whole-instance omission");
        var opaque=f.Grant(new(){Id=202,CardId=3,Name="Opaque grant",Properties=new(){["captured"]=new StaticValueConfig{Value=hidden.InstanceId.ToString()}}});
        var guarded=Pack(Read());
        Check(!guarded.GetRawText().Contains(hidden.InstanceId.ToString())&&guarded.GetProperty("instances")[0].GetProperty("changes").EnumerateArray().Any(row=>row.GetProperty("configUnavailable").ValueKind==JsonValueKind.String),"captured private instance identifier was sent");
        f.Grant(new(){Id=204,CardId=3,Name="Captured entity",Properties=new(){["captured"]=new StaticValueConfig{Value=hidden}}});
        lookedUp.Clear();var entityGuarded=Pack(Read());
        Check(!lookedUp.Contains(hidden.InstanceId)&&!entityGuarded.GetRawText().Contains(hidden.InstanceId.ToString()),"captured hidden entity was inspected before serialization rejection");
        var stale=snapshot with {GameId="different-game"};
        var unavailable=Pack(Facts.GetMethod("Read",Hidden)!.Invoke(null,[context,stale,f.Own.PlayerId,null,8192])!);
        Check(!unavailable.GetProperty("available").GetBoolean(),"stale snapshot was mixed with newer runtime data");
        f.Game.AbilityManager=real;
    }
    internal static void DuplicatesAndSuppression()
    {
        using var f=new Fixture();f.Grant();f.Grant();
        var copy=f.Factory.CreateAndRegisterNewCard(f.Game,2,f.Own.PlayerId,CardZone.Hand);
        var packed=Pack(f.Read());
        Check(packed.GetProperty("instances").GetArrayLength()==1&&packed.GetProperty("instances")[0].GetProperty("changes").GetArrayLength()==2,"separate grants merged or affected a same-ID copy");
        var ordinals=packed.GetProperty("instances")[0].GetProperty("changes").EnumerateArray().Select(row=>row.GetProperty("ordinal").GetInt32()).ToArray();
        Check(ordinals.Distinct().Count()==2,"duplicate ability definitions lost their separate instances");
        var suppression=new Buff(Guid.Empty,f.Card.InstanceId,0,[new AbilitySuppressionBuffEffect(RemoveAbilityMode.All)]);
        f.Game.BuffManager.RegisterBuffInstance(suppression,f.Game);
        var suppressed=Pack(f.Read()).GetProperty("instances")[0].GetProperty("changes");
        Check(suppressed.EnumerateArray().Any(row=>row.GetProperty("kind").GetString()=="base"&&row.GetProperty("suppressed").GetBoolean()),"effective-ability suppression was ignored");
        typeof(CardInstance).GetProperty("IsSilenced")!.SetValue(copy,true);
        var silenced=Pack(f.Read()).GetProperty("instances").EnumerateArray().Single(row=>row.GetProperty("instance").GetProperty("index").GetInt32()==1);
        Check(silenced.GetProperty("silenced").GetBoolean(),"silence not associated with the second same-ID instance");
    }
    internal static void OwnerRelativePerspective()
    {
        using var f=new Fixture();f.Grant();
        var enemyCard=f.Factory.CreateAndRegisterNewCard(f.Game,2,f.Enemy.PlayerId,CardZone.Field);
        f.Game.AbilityManager.CreateAbilitiesForCard(enemyCard,[new(){Id=303,CardId=2,Name="Opponent-owned rule"}],Guid.NewGuid(),f.Game);
        var current=Pack(f.Read());
        var own=current.GetProperty("instances").EnumerateArray().Single(row=>row.GetProperty("instance").GetProperty("side").GetString()=="own");
        var enemy=current.GetProperty("instances").EnumerateArray().Single(row=>row.GetProperty("instance").GetProperty("side").GetString()=="enemy");
        Check(own.GetProperty("perspective").GetProperty("dslOwnerSide").GetString()=="own"&&own.GetProperty("perspective").GetProperty("dslOpponentSide").GetString()=="enemy","own rule perspective reversed");
        Check(enemy.GetProperty("perspective").GetProperty("dslOwnerSide").GetString()=="enemy"&&enemy.GetProperty("perspective").GetProperty("dslOpponentSide").GetString()=="own","opponent rule perspective was interpreted relative to the planner");
        using var context=f.Context();
        var flipped=Pack(Facts.GetMethod("Read",Hidden)!.Invoke(null,[context,GameStateProjection.Create(f.Game,context),f.Enemy.PlayerId,null,8192])!);
        var holder=flipped.GetProperty("instances").EnumerateArray().Single(row=>row.GetProperty("instance").GetProperty("zone").GetString()=="field");
        Check(holder.GetProperty("perspective").GetProperty("dslOwnerSide").GetString()=="own"&&holder.GetProperty("perspective").GetProperty("dslOpponentSide").GetString()=="enemy","owner side was cached across a different planning player");
        Check(!current.GetRawText().Contains(f.Own.PlayerId.ToString())&&!flipped.GetRawText().Contains(f.Enemy.PlayerId.ToString()),"perspective leaked player identifiers");
    }
    internal static void WithChoiceContext(GameState game,IGameDataProvider data,Guid owner,Action<List<CardInstanceDto>> action)
    {
        var previous=GameServiceProvider.ContextAccessor;var manager=game.AbilityManager;var buffs=game.BuffManager;
        var accessor=new GameContextAccessor();GameServiceProvider.ContextAccessor=accessor;
        using var services=AiEndTurnEngineTests.Services(data,accessor);using var scope=services.CreateScope();var sp=scope.ServiceProvider;
        try {
            game.AbilityManager=sp.GetRequiredService<IAbilityManager>();game.BuffManager=sp.GetRequiredService<IBuffManager>();
            var factory=sp.GetRequiredService<ShadowverseBeyond.Server.Domain.Factories.ICardFactory>();
            var first=factory.CreateAndRegisterNewCard(game,2,owner,CardZone.Staging);
            var second=factory.CreateAndRegisterNewCard(game,3,owner,CardZone.Staging);
            game.AbilityManager.CreateAbilitiesForCard(second,[new(){Id=203,CardId=3,Name="Copied choice addition",Effects=[new(){Type=EffectType.Damage,
                Parameters=new(){["DamageAmount"]=new StaticValueConfig{Value=4}}}]}],Guid.NewGuid(),game);
            using var context=new GameContext(sp,sp.GetRequiredService<AutoMapper.IMapper>(),accessor,game,owner);
            action([CardInstanceProjection.Create(first,context),CardInstanceProjection.Create(second,context)]);
        } finally {game.AbilityManager=manager;game.BuffManager=buffs;GameServiceProvider.ContextAccessor=previous;}
    }

    private sealed class Fixture:IDisposable
    {
        private readonly IGameDataProvider original=GameDataProvider.Instance;
        private readonly IGameContextAccessor? previous=GameServiceProvider.ContextAccessor;
        private readonly ServiceProvider services;private readonly IServiceScope scope;
        internal readonly GameContextAccessor Accessor=new();
        internal readonly Dictionary<int,AbilityConfig> Definitions;
        internal readonly PlayerState Own=new(Guid.NewGuid()),Enemy=new(Guid.NewGuid());
        internal readonly GameState Game;internal readonly CardInstance Card;internal readonly Ability Base;
        internal readonly ShadowverseBeyond.Server.Domain.Factories.ICardFactory Factory;
        internal Fixture() {
            Definitions=new(){[2]=new(){Id=101,CardId=2,Name="Original",Trigger=new(){EventTypes=[EventType.TurnEnd],ValidZones=[CardZone.Field]},
                Effects=[new(){Type=EffectType.Damage,Parameters=new(){["DamageAmount"]=new StaticValueConfig{Value=1}}}]}};
            var cards=Enumerable.Range(1,5).ToDictionary(id=>id,id=>new CardDefinition{Id=id,Name="Public "+id,Health=4,Type=id==1?CardType.Leader:CardType.Follower});
            var data=Proxy<IGameDataProvider>.Make((method,args)=>method.Name switch {
                "GetCardDefinition"=>cards.GetValueOrDefault((int)args![0]!),"GetAllCardDefinitions"=>cards,
                "GetAbilityConfigs"=>Definitions.TryGetValue((int)args![0]!,out var config)?new List<AbilityConfig>{config}:new List<AbilityConfig>(),_=>null});
            GameDataProvider.SetInstanceForTesting(data);GameServiceProvider.ContextAccessor=Accessor;
            services=AiEndTurnEngineTests.Services(data,Accessor);scope=services.CreateScope();var sp=scope.ServiceProvider;
            Game=new("runtime-facts",Own,Enemy,Own.PlayerId){Status=GameStatus.InProgress,Phase=GamePhase.Main,AbilityManager=sp.GetRequiredService<IAbilityManager>(),BuffManager=sp.GetRequiredService<IBuffManager>()};
            Factory=sp.GetRequiredService<ShadowverseBeyond.Server.Domain.Factories.ICardFactory>();
            Own.Leader=Factory.CreateAndRegisterNewCard(Game,1,Own.PlayerId,CardZone.Field);Enemy.Leader=Factory.CreateAndRegisterNewCard(Game,1,Enemy.PlayerId,CardZone.Field);
            Card=Factory.CreateAndRegisterNewCard(Game,2,Own.PlayerId,CardZone.Hand);
            Base=Game.AbilityManager.GetAbilitiesForCard(Card.InstanceId).OfType<Ability>().Single();
        }
        internal GameContext Context()=>new(scope.ServiceProvider,scope.ServiceProvider.GetRequiredService<AutoMapper.IMapper>(),Accessor,Game,Own.PlayerId);
        internal object Read(){using var context=Context();return Facts.GetMethod("Read",Hidden)!.Invoke(null,[context,GameStateProjection.Create(Game,context),Own.PlayerId,null,8192])!;}
        internal Ability Grant(AbilityConfig? config=null)=>Game.AbilityManager.CreateAbilitiesForCard(Card,[config??new(){Id=201,CardId=3,Name="Granted",Trigger=new(){EventTypes=[EventType.TurnEnd],ValidZones=[CardZone.Hand]},
            Effects=[new(){Type=EffectType.Damage,Parameters=new(){["DamageAmount"]=new StaticValueConfig{Value=4},["CardDefinitionId"]=new StaticValueConfig{Value=5}}}]}],Guid.NewGuid(),Game).Single();
        public void Dispose(){scope.Dispose();services.Dispose();GameDataProvider.SetInstanceForTesting(original);GameServiceProvider.ContextAccessor=previous;}
    }
}
