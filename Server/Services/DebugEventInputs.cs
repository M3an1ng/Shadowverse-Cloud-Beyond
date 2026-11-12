using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

public sealed record DebugEventRequest(string EventType, Dictionary<string, JsonElement> Input);
public sealed record EventInputField(string Name, string Type, bool Required, string[]? Values = null, int? Minimum = null);

/// <summary>Input intent construction is deliberately separate from the legacy historical-output reader.</summary>
public static class DebugEventInputs
{
    private sealed record Factory(EventInputField[] Fields, Func<GameState, Dictionary<string, JsonElement>, IGameEvent> Create);
    private static EventInputField R(string name, string type, int? minimum = null) => new(name, type, true, Minimum: minimum);
    private static EventInputField O(string name, string type) => new(name, type, false);
    private static EventInputField E<T>(string name) where T : struct, Enum => new(name, "enum", true, Enum.GetNames<T>());
    private static readonly IReadOnlyDictionary<string, Factory> Factories = Build();

    public static Guid ResolveId(GameState game, string reference)
    {
        if (Guid.TryParse(reference, out var guid)) return guid;
        if (!InGameId.TryParse(reference, out var id) || !game.InGameIds.TryResolveGuid(id.Kind, reference, out guid))
            throw new ArgumentException("unknown_reference: " + reference);
        return guid;
    }
    public static CardInstance Card(GameState game, string reference) => game.FindCardInstance(ResolveId(game, reference))
        ?? throw new ArgumentException("missing_card: " + reference);
    public static PlayerState Player(GameState game, string reference) => game.GetPlayerState(ResolveId(game, reference))
        ?? throw new ArgumentException("missing_player: " + reference);
    private static string S(Dictionary<string, JsonElement> input, string key) => input[key].GetString()!;
    private static int N(Dictionary<string, JsonElement> input, string key) => input[key].GetInt32();
    private static T EnumValue<T>(Dictionary<string, JsonElement> input, string key) where T : struct, Enum => Enum.Parse<T>(S(input, key));
    private static List<CardInstance> Cards(GameState game, Dictionary<string, JsonElement> input) => input["cards"].EnumerateArray().Select(v => Card(game, v.GetString()!)).ToList();

    private static IReadOnlyDictionary<string, Factory> Build()
    {
        var factories = new Dictionary<string, Factory>(StringComparer.Ordinal)
        {
            ["Damage"] = new([R("source", "card"), R("target", "card"), R("amount", "integer", 1)],
                (g, p) => new DamageEvent(Card(g, S(p,"target")), N(p,"amount"), Card(g,S(p,"source")))),
            ["Heal"] = new([R("source", "card"), R("target", "card"), R("amount", "integer", 1)],
                (g, p) => new HealEvent(Card(g,S(p,"target")), N(p,"amount"), Card(g,S(p,"source")))),
            ["Draw"] = new([R("player", "player"), R("count", "integer", 1)], (g,p) => new DrawEvent(Player(g,S(p,"player")),N(p,"count"))),
            ["CreateCard"] = new([R("player", "player"), R("cardDefinitionId", "cardDefinition", 0)],
                (g,p) => new CreateCardEvent(Player(g,S(p,"player")),N(p,"cardDefinitionId"))),
            ["CardMoved"] = new([R("target", "card"), E<CardZone>("zone")],
                (g,p) => new CardMovedEvent(Card(g,S(p,"target")), Card(g,S(p,"target")).CurrentZone, EnumValue<CardZone>(p,"zone"))),
            ["Summon"] = new([R("player", "player"), R("target", "card")],
                (g,p) => new SummonEvent(Player(g,S(p,"player")), Card(g,S(p,"target")))),
            ["AddToHand"] = new([R("player", "player"), R("cards", "cards")], (g,p)=>new AddCardToHandEvent(Player(g,S(p,"player")),Cards(g,p))),
            ["AddEmblem"] = new([R("player", "player"), R("cards", "cards")], (g,p)=>new AddEmblemEvent(Player(g,S(p,"player")),Cards(g,p))),
            ["CardRevealed"] = new([R("player", "player"), R("target", "card")], (g,p)=>new CardRevealedEvent(Player(g,S(p,"player")),Card(g,S(p,"target")))),
            ["SpellBoost"] = new([R("player", "player"), R("source", "card"), R("cards", "cards")],
                (g,p)=>new SpellBoostEvent(Player(g,S(p,"player")),Card(g,S(p,"source")),Cards(g,p))),
            ["AddToDeck"] = new([R("player", "player"), R("cards", "cards"), E<DeckInsertMode>("insertMode")],
                (g,p)=>new AddCardToDeckEvent(Player(g,S(p,"player")),Cards(g,p),EnumValue<DeckInsertMode>(p,"insertMode"))),
            ["Shuffle"] = new([R("player", "player"), R("cards", "cards"), E<DeckInsertMode>("insertMode")],
                (g,p)=>new ShuffleEvent(Player(g,S(p,"player")),Cards(g,p),insertMode:EnumValue<DeckInsertMode>(p,"insertMode"))),
            ["Discard"] = new([R("player", "player"), R("target", "card")],
                (g,p)=>new CardDiscardedEvent(Player(g,S(p,"player")),Card(g,S(p,"target")),EventReasonSource.System)),
            ["Transform"] = new([R("target", "card"),R("cardDefinitionId","cardDefinition",0)],
                (g,p)=>new TransformEvent(Card(g,S(p,"target")),N(p,"cardDefinitionId"))),
            ["HealthChanged"] = new([R("target","card"),R("newHealth","integer",0)], (g,p)=>
            {
                var card=Card(g,S(p,"target")); return new HealthChangedEvent(card,card.CurrentHealth,N(p,"newHealth"),N(p,"newHealth")-card.CurrentHealth);
            }),
            ["StatChanged"] = new([R("target","card"),O("attack","integer"),O("health","integer"),O("cost","integer"),E<ModifierLayer>("layer")],
                (g,p)=>new StatChangedEvent(Card(g,S(p,"target")),p.TryGetValue("attack",out var a)?a.GetInt32():null,
                    p.TryGetValue("health",out var h)?h.GetInt32():null,p.TryGetValue("cost",out var c)?c.GetInt32():null,modifierLayer:EnumValue<ModifierLayer>(p,"layer"))),
            ["KeywordApplied"] = new([R("target","card"),E<CardKeyword>("keyword")],
                (g,p)=>new KeywordAddedEvent(EnumValue<CardKeyword>(p,"keyword"),Card(g,S(p,"target")))),
            ["KeywordRemoved"] = new([R("target","card"),E<CardKeyword>("keyword")],
                (g,p)=>new KeywordRemovedEvent(EnumValue<CardKeyword>(p,"keyword"),Card(g,S(p,"target")))),
            ["BuffRemoved"] = new([R("target","card"),R("buff","buff")], (g,p)=>
            {
                var buff=g.BuffManager.FindBuffInstance(ResolveId(g,S(p,"buff"))) ?? throw new ArgumentException("missing_buff");
                return new BuffRemovedEvent(buff,Card(g,S(p,"target")),null);
            }),
            ["BuffApplied"] = new([R("target","card"),E<StatType>("stat"),R("value","integer"),E<ModifierLayer>("layer"),O("durationType","string")],
                (g,p)=>new DebugStatBuffIntent(Card(g,S(p,"target")),EnumValue<StatType>(p,"stat"),N(p,"value"),EnumValue<ModifierLayer>(p,"layer"),
                    p.TryGetValue("durationType",out var dt) && dt.ValueKind == JsonValueKind.String ? Enum.Parse<DurationType>(dt.GetString()!) : DurationType.Permanent)),
            ["MirageAddRecruitedCard"] = new([R("player","player"),R("cardDefinitionId","cardDefinition",0)],
                (g,p)=>new DebugMirageAddRecruitedCardIntent(Player(g,S(p,"player")),N(p,"cardDefinitionId"))),
            ["MirageSetShopOffer"] = new([R("player","player"),R("cardDefinitionId","cardDefinition",0)],
                (g,p)=>new DebugMirageSetShopOfferIntent(Player(g,S(p,"player")),N(p,"cardDefinitionId"))),
            ["MirageGrantSpecialDiscoverTicket"] = new([R("player","player"),R("targetTier","integer",1)],
                (g,p)=>new DebugMirageGrantSpecialDiscoverTicketIntent(Player(g,S(p,"player")),N(p,"targetTier"))),
            ["CounterSet"] = new([R("target","entity"),R("key","string"),R("value","integer",0)], (g,p)=>new DebugCounterIntent(g,S(p,"target"),S(p,"key"),N(p,"value"))),
            ["TurnStart"] = new([R("player","player")], (g,p)=>new TurnStartEvent(Player(g,S(p,"player")))),
            ["TurnEnd"] = new([R("player","player")], (g,p)=>new TurnEndEvent(Player(g,S(p,"player")))),
            ["GameEnd"] = new([R("winner","player"),R("loser","player"),R("reason","string")],
                (g,p)=>new GameEndEvent(Player(g,S(p,"winner")).PlayerId,Player(g,S(p,"loser")).PlayerId,S(p,"reason")))
        };
        foreach(var (name,kind) in new[]{("Destroy",LeaveFieldKind.Destroy),("Vanish",LeaveFieldKind.Vanish),("Return",LeaveFieldKind.Return),("Tuck",LeaveFieldKind.Tuck)})
            factories[name]=new([R("target","card")],(g,p)=>new LeaveFieldEvent(Card(g,S(p,"target")),kind,EventReasonSource.System));
        factories["PpUpdated"]=new([R("player","player"),R("value","integer",0)],(g,p)=>new PpUpdateEvent(Player(g,S(p,"player")),N(p,"value")));
        factories["PpMaxUpdated"]=new([R("player","player"),R("value","integer",0)],(g,p)=>new PpMaxUpdateEvent(Player(g,S(p,"player")),N(p,"value")));
        factories["EpUpdated"]=new([R("player","player"),R("value","integer",0)],(g,p)=>new EpUpdateEvent(Player(g,S(p,"player")),N(p,"value")));
        factories["EpMaxUpdated"]=new([R("player","player"),R("value","integer",0)],(g,p)=>new EpMaxUpdateEvent(Player(g,S(p,"player")),N(p,"value")));
        factories["SepUpdated"]=new([R("player","player"),R("value","integer",0)],(g,p)=>new SepUpdateEvent(Player(g,S(p,"player")),N(p,"value")));
        factories["SepMaxUpdated"]=new([R("player","player"),R("value","integer",0)],(g,p)=>new SepMaxUpdateEvent(Player(g,S(p,"player")),N(p,"value")));
        return factories;
    }

    public static object Describe(string name)
    {
        if (!Factories.ContainsKey(name) && !Enum.GetNames<EventType>().Contains(name)) throw new ArgumentException("unknown_event: " + name);
        var types=typeof(IGameEvent).Assembly.GetTypes().Where(t=>t.IsClass&&!t.IsAbstract&&typeof(IGameEvent).IsAssignableFrom(t))
            .Where(t=>t.GetCustomAttribute<GameEventMetadataAttribute>()?.EventType.ToString()==name || t.Name == name + "Event").ToArray();
        Factories.TryGetValue(name,out var factory);
        return new
        {
            EventType=name, Status=factory!=null?"submittable":"observed_only",
            Reason=factory!=null?null:types.Length == 0 ? "Declared type without mapped runtime metadata; no input support is claimed." :
                "Derived or context-owned event. Use a listed upstream intent or the formal player action.",
            RuntimeTypes = types.Select(type => type.Name).ToArray(),
            Input=factory?.Fields ?? [],
            Parameters=types.SelectMany(t=>EventParameterCache.GetDescriptor(t).Parameters)
                .Select(p=>new { Name=p.PropertyName, Type=p.PropertyType.Name, Scope=p.Scope.ToString(), p.Description }).ToArray(),
            UpstreamIntents=name switch
            {
                "AddCardToHand" => new[]{"AddToHand"}, "AddDeck" => new[]{"AddToDeck"}, "CounterChanged" => new[]{"CounterSet"},
                "BuffApplied" => new[]{"StatChanged","KeywordApplied"},
                "Act" or "ActRequested" => new[]{"action.act-ability"},
                "Fusion" or "FusionRequested" or "CardFused" => new[]{"action.fusion"},
                "PlayCard" or "PlayCardRequested" or "Enhance" or "Accelerate" or "Crystalize" => new[]{"action.play-card"},
                "Attack" or "AttackRequested" or "CombatResolution" or "CombatDestroyResolution" => new[]{"action.attack"},
                "Evolve" or "EvolveRequested" => new[]{"action.evolve"}, "SuperEvolve" or "SuperEvolveRequested" => new[]{"action.super-evolve"},
                "MulliganSubmitted" or "MulliganCompleted" => new[]{"action.mulligan"},
                "PpBoostRequested" or "PpBoostStateUpdated" => new[]{"action.pp-boost"},
                "TurnBoundaryCleanup" or "EndTurnRequested" => new[]{"action.end-turn"},
                _ => Array.Empty<string>()
            }
        };
    }
    public static IEnumerable<object> List() => Enum.GetNames<EventType>().Concat(Factories.Keys).Distinct().OrderBy(n=>n).Select(Describe);

    public static void Validate(GameState game, DebugEventRequest request)
    {
        if (!Factories.TryGetValue(request.EventType,out var factory)) throw new ArgumentException("event_not_submittable: "+request.EventType);
        if (request.Input is null) throw new ArgumentException("input is required");
        foreach(var key in request.Input.Keys)
            if (!factory.Fields.Any(f=>f.Name==key)) throw new ArgumentException("unknown_input_field: input."+key);
        foreach(var field in factory.Fields)
        {
            if (!request.Input.TryGetValue(field.Name,out var value))
            {
                if(field.Required) throw new ArgumentException("missing_input_field: input."+field.Name);
                continue;
            }
            switch(field.Type)
            {
                case "integer": case "cardDefinition":
                    if(!value.TryGetInt32(out var number)||field.Minimum is int min&&number<min) throw new ArgumentException("invalid_integer: input."+field.Name);
                    if(field.Type=="cardDefinition"&&GameDataProvider.Instance?.GetCardDefinition(number)==null)throw new ArgumentException("missing_card_definition: "+number);
                    break;
                case "cards":
                    if(value.ValueKind!=JsonValueKind.Array)throw new ArgumentException("expected_array: input."+field.Name);
                    foreach(var reference in value.EnumerateArray()) Card(game,reference.GetString()!);
                    break;
                case "card": Card(game,value.GetString()!); break;
                case "player": Player(game,value.GetString()!); break;
                case "buff":
                    if(game.BuffManager.FindBuffInstance(ResolveId(game,value.GetString()!))==null)throw new ArgumentException("missing_buff");
                    break;
                case "entity":
                    var id=ResolveId(game,value.GetString()!);
                    if(game.FindCardInstance(id)==null&&game.GetPlayerState(id)==null)throw new ArgumentException("missing_entity");
                    break;
                case "enum":
                    if(value.ValueKind!=JsonValueKind.String||!field.Values!.Contains(value.GetString()))throw new ArgumentException("invalid_enum: input."+field.Name);
                    break;
                case "string":
                    if(value.ValueKind!=JsonValueKind.String||string.IsNullOrWhiteSpace(value.GetString()))throw new ArgumentException("expected_string: input."+field.Name);
                    break;
            }
        }
        if(request.EventType=="StatChanged"&&!new[]{"attack","health","cost"}.Any(request.Input.ContainsKey))throw new ArgumentException("at least one stat modifier is required");
    }
    public static IGameEvent Create(GameState game, DebugEventRequest request)
    {
        Validate(game,request);
        return Factories[request.EventType].Create(game,request.Input);
    }

    private sealed class DebugCounterIntent : GameEventBase, ICommittableEvent
    {
        private readonly Guid _target;
        [GameEventParameter("计数键","要设置的计数键")] public string Key {get;}
        [GameEventParameter("新值","新计数值")] public int Value {get;}
        public DebugCounterIntent(GameState game,string target,string key,int value):base(EventType.DebugIntent)
        { _target=ResolveId(game,target);Key=key;Value=value; }
        public DomainOperationResult Commit(GameContext context)
        {
            var result=new DomainOperationResult{Success=true};
            if(context.CurrentGameState.GetPlayerState(_target) is {} player)
            {
                var old=player.CounterMap.GetValueOrDefault(Key);player.SetCounter(Key,Value);
                result.GeneratedEvents.Add(new CounterChangedEvent(player,Key,old,Value));
            }
            else
            {
                var card=context.CurrentGameState.FindCardInstance(_target)!;var old=card.CounterMap.GetValueOrDefault(Key);card.SetCounter(Key,Value);
                result.GeneratedEvents.Add(new CounterChangedEvent(card,Key,old,Value));
            }
            return result;
        }
    }

    private sealed class DebugStatBuffIntent(CardInstance target, StatType stat, int value, ModifierLayer layer, DurationType durationType = DurationType.Permanent)
        : GameEventBase(EventType.DebugIntent), ICommittableEvent
    {
        [GameEventParameter("目标", "实际目标卡牌")] public CardInstance TargetCard => target;
        public DomainOperationResult Commit(GameContext context)
        {
            var turn = context.CurrentGameState.GetPlayerState(target.OwnerPlayerId)!.TurnNumber;
            var buff = context.ServiceProvider.GetRequiredService<IBuffFactory>().CreateSystemStatBuff(target,
                [new StatModification(stat, value, layer)], durationType, durationType == DurationType.Permanent ? null : 1, turn);
            var outcome = new DomainOperationResult { Success = true };
            outcome.GeneratedEvents.Add(new BuffAppliedEvent(buff, target, null));
            return outcome;
        }
    }

    private sealed class DebugMirageAddRecruitedCardIntent(PlayerState player, int cardDefinitionId)
        : GameEventBase(EventType.DebugIntent), ICommittableEvent
    {
        [GameEventParameter("玩家", "目标玩家")] public PlayerState TargetPlayer => player;
        [GameEventParameter("卡牌定义ID", "卡牌定义ID")] public int CardDefinitionId => cardDefinitionId;
        public DomainOperationResult Commit(GameContext context)
        {
            if (context.CurrentGameState.Mode != GameMode.Mirage || player.Mirage == null)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = "Mirage recruited card debug intent is only allowed in Mirage mode."
                };
            }

            var def = GameDataProvider.Instance?.GetCardDefinition(cardDefinitionId);
            if (def == null || !def.IsMirageCard)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Card definition {cardDefinitionId} does not belong to the Mirage card pool."
                };
            }

            var shopService = context.ServiceProvider.GetService<IMirageShopService>();
            var mirage = context.CurrentGameState.Mirage;
            var reservedFromPool = false;
            if (shopService != null && mirage != null && shopService.IsManagedPoolCard(mirage, cardDefinitionId))
            {
                if (!shopService.TryReserveFromPool(mirage, cardDefinitionId, 1))
                {
                    return new DomainOperationResult
                    {
                        Success = false,
                        ErrorMessage = $"Shared pool copies for card {cardDefinitionId} are exhausted."
                    };
                }
                reservedFromPool = true;
            }

            var cardFactory = context.ServiceProvider.GetRequiredService<ShadowverseBeyond.Server.Domain.Factories.ICardFactory>();
            var card = cardFactory.CreateAndRegisterNewCard(context.CurrentGameState, cardDefinitionId, player.PlayerId, CardZone.Hand);
            card.IsMirageRecruited = true;
            if (reservedFromPool)
            {
                card.AddMiragePoolHolding(cardDefinitionId, 1);
            }
            card.CostOverride = 0;
            var outcome = new DomainOperationResult { Success = true };
            outcome.GeneratedEvents.Add(new HandUpdatedEvent(player, player.Hand.ToList()));
            var tripleService = context.ServiceProvider.GetService<IMirageTripleService>();
            if (tripleService != null)
            {
                outcome.GeneratedEvents.AddRange(tripleService.CreatePendingTripleEvents(player, context.CurrentGameState.Mirage?.RoundNumber));
            }
            return outcome;
        }
    }

    private sealed class DebugMirageSetShopOfferIntent(PlayerState player, int cardDefinitionId)
        : GameEventBase(EventType.DebugIntent), ICommittableEvent
    {
        [GameEventParameter("玩家", "目标玩家")] public PlayerState TargetPlayer => player;
        [GameEventParameter("卡牌定义ID", "商店报价卡牌定义ID")] public int CardDefinitionId => cardDefinitionId;
        public DomainOperationResult Commit(GameContext context)
        {
            if (context.CurrentGameState.Mode != GameMode.Mirage || player.Mirage == null || context.CurrentGameState.Mirage == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Target player does not have Mirage state." };
            }

            var shopService = context.ServiceProvider.GetService<IMirageShopService>();
            var entry = shopService?.GetCatalogEntry(cardDefinitionId);
            if (entry == null || shopService == null)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Card definition {cardDefinitionId} does not belong to the Mirage shop pool."
                };
            }

            var mirage = context.CurrentGameState.Mirage;
            if (!shopService.TryReserveFromPool(mirage, cardDefinitionId, 1))
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Shared pool copies for card {cardDefinitionId} are exhausted."
                };
            }

            var rules = mirage.Rules;
            var def = GameDataProvider.Instance?.GetCardDefinition(cardDefinitionId);
            var resolvedBuyCost = def != null
                ? MirageShopService.ResolveBuyCost(def, rules)
                : (rules.BuyCost > 0 ? rules.BuyCost : MirageRuleConfig.DefaultBuyCost);
            player.Mirage.ShopOffers.Insert(0, new MirageShopOffer
            {
                OfferId = Guid.NewGuid(),
                CardDefinitionId = cardDefinitionId,
                TavernTier = entry.TavernTier,
                BuyCost = resolvedBuyCost
            });

            return new DomainOperationResult { Success = true };
        }
    }

    private sealed class DebugMirageGrantSpecialDiscoverTicketIntent(PlayerState player, int targetTier)
        : GameEventBase(EventType.DebugIntent), ICommittableEvent
    {
        [GameEventParameter("玩家", "目标玩家")] public PlayerState TargetPlayer => player;
        [GameEventParameter("目标星级", "特殊奖励发现凭据的目标星级")] public int TargetTier => targetTier;
        public DomainOperationResult Commit(GameContext context)
        {
            if (context.CurrentGameState.Mode != GameMode.Mirage || player.Mirage == null || context.CurrentGameState.Mirage == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Target player does not have Mirage state." };
            }

            var tripleService = context.ServiceProvider.GetService<IMirageTripleService>();
            if (tripleService == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "IMirageTripleService is not available." };
            }

            var mirage = context.CurrentGameState.Mirage;
            var ticket = tripleService.CreateDiscoverTicket(mirage, Math.Max(1, targetTier), mirage.Rules, allowAboveMaxTier: true);
            if (ticket == null)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = $"No available Mirage pool candidates for special tier {targetTier}."
                };
            }

            player.Mirage.PendingTickets.Add(ticket);
            return new DomainOperationResult { Success = true };
        }
    }
}
