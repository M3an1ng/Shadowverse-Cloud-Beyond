using System.Reflection;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
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
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

var definitions = new Dictionary<int, CardDefinition>();
for (var id = 1; id <= 6; id++)
    definitions[id] = new() { Id = id, Name = $"Test {id}", Type = CardType.Follower, Cost = 1, Attack = 2, Health = 3 };
definitions[5].Type = CardType.Crest;
definitions[6].Type = CardType.Faith;
var data = Proxy<IGameDataProvider>.Make((m, a) => m.Name switch {
    "GetCardDefinition" => definitions.GetValueOrDefault((int)a![0]!),
    "GetAbilityConfigs" => new List<AbilityConfig>(),
    "GetAllCardDefinitions" => definitions,
    "get_Cards" => definitions.Values.ToList(), _ => null
});
GameDataProvider.SetInstanceForTesting(data);
var manager = Proxy<IAbilityManager>.Make((m, a) => m.Name is "GetAbilitiesForCard" or "GetEffectiveAbilitiesForCard" or "CreateAbilitiesForCard" ? new List<IAbility>() : null);
var buffs = new BuffManager(NullLogger<BuffManager>.Instance, manager);
var buffFactory = new BuffFactory(NullLogger<BuffFactory>.Instance, data);
var buffService = new BuffService(buffs,
    new BuffEvaluator(Proxy<IConditionEvaluator>.Make((m, a) => true), NullLogger<BuffEvaluator>.Instance),
    buffFactory, NullLogger<BuffService>.Instance, new BuffStackingEngine(NullLogger<BuffStackingEngine>.Instance));
var factory = new CardFactory(NullLogger<CardFactory>.Instance, buffFactory, buffs);
var accessor = new GameContextAccessor();
GameServiceProvider.ContextAccessor = accessor;
PlayerChoiceRequest? lastChoice = null;
bool cancel = false, invalid = false;
var pending = Proxy<IPendingActionService>.Make((m, a) => {
    if (m.Name != "WaitForPlayerChoiceAsync") return null;
    lastChoice = (PlayerChoiceRequest)a![1]!;
    return Task.FromResult(new PlayerChoiceResult { WasCancelled = cancel,
        ChosenTargetIds = [invalid ? Guid.NewGuid() : lastChoice.SelectableTargetIds[0]] });
});
using var services = new ServiceCollection().AddSingleton(data).AddSingleton<ICardFactory>(factory)
    .AddSingleton<IBuffService>(buffService).AddSingleton(pending)
    .AddSingleton<IEventHistoryService>(Proxy<IEventHistoryService>.Make((m, a) => null)).BuildServiceProvider();
var mapper = new MapperConfiguration(cfg => { }).CreateMapper();
var handler = new DiscoverEffectHandler(NullLogger<DiscoverEffectHandler>.Instance);
var passed = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); }
void Test(string name, Action run) { run(); Console.WriteLine($"PASS {name}"); passed++; }
(GameState Game, PlayerState Own, PlayerState Enemy) Match() {
    var own = new PlayerState(Guid.NewGuid()); var enemy = new PlayerState(Guid.NewGuid());
    return (new GameState("discover-test", own, enemy, own.PlayerId) { AbilityManager = manager, BuffManager = buffs }, own, enemy);
}
List<IGameEvent> Commit(IEnumerable<IGameEvent> events, GameContext context) {
    var committed = new List<IGameEvent>();
    foreach (var e in events) {
        committed.Add(e);
        if (e is not ICommittableEvent c) continue;
        var result = c.Commit(context); Check(result.Success, result.ErrorMessage ?? "commit failed");
        committed.AddRange(Commit(result.GeneratedEvents, context));
    }
    return committed;
}
DiscoverRequestEvent Request(CardInstance card, GameContext context, DiscoverMode mode = DiscoverMode.Instance) {
    var result = handler.Apply(new Effect(context.RequestingPlayerId), context, new() { Mode = mode, CopySourceTargets = new[] { card } });
    Check(result.Success, result.ErrorMessage ?? "handler failed");
    var request = result.GeneratedEvents.OfType<DiscoverRequestEvent>().Single();
    request.ResolveAsync(context).GetAwaiter().GetResult();
    return request;
}
foreach (var enemySource in new[] { false, true }) Test($"original {(enemySource ? "enemy" : "own")} deck card moves without drawing", () => {
    var (game, own, enemy) = Match(); var source = enemySource ? enemy : own;
    var before = factory.CreateAndRegisterNewCard(game, 2, source.PlayerId, CardZone.Deck);
    var card = factory.CreateAndRegisterNewCard(game, 1, source.PlayerId, CardZone.Deck);
    var after = factory.CreateAndRegisterNewCard(game, 3, source.PlayerId, CardZone.Deck);
    card.SetCounter("discover-state", 7); card.Damage = 1;
    using var context = new GameContext(services, mapper, accessor, game, own.PlayerId);
    var request = Request(card, context);
    Check(lastChoice!.SelectableTargetIds.SequenceEqual(new[] { card.InstanceId }), "choice does not expose original instance");
    Check(source.Deck.Contains(card) && card.OwnerPlayerId == source.PlayerId, "selection mutated the source prematurely");
    var result = request.Commit(context); Check(result.Success, "request failed");
    var events = Commit(result.GeneratedEvents, context);
    Check(own.Hand.Single() == card && game.FindCardInstance(card.InstanceId) == card, "original instance was not retained");
    Check(card.OwnerPlayerId == own.PlayerId && !source.Deck.Contains(card), "owner or source deck was not updated");
    Check(source.Deck.SequenceEqual(new[] { before, after }) && before.ZoneIndex == 0 && after.ZoneIndex == 1, "deck order/index changed");
    Check(card.GetCounter("discover-state") == 7 && card.Damage == 1, "instance state was reset");
    Check(card.LastMoveReason == CardMoveReason.AddedToHand && events.All(e => e is not DrawEvent), "discovery counted as draw");
    Check(request.Target.Single().Card == card && events.OfType<DiscoverEvent>().Single().DiscoveredCard == card, "completion did not publish original card");
    Check(events.OfType<DeckCountChangedEvent>().Any(e => e.Source?.Player == source), "source deck count notification missing");
    var move = events.OfType<CardMovedEvent>().Single();
    Check(move.FromZone == CardZone.Deck && move.ToZone == CardZone.Hand, "movement lost deck origin");
});
Test("pool excludes non-deck, unregistered, crest and faith instances", () => {
    var (game, own, enemy) = Match();
    var valid = factory.CreateAndRegisterNewCard(game, 1, enemy.PlayerId, CardZone.Deck);
    var hand = factory.CreateAndRegisterNewCard(game, 2, own.PlayerId, CardZone.Hand);
    var crest = factory.CreateAndRegisterNewCard(game, 5, own.PlayerId, CardZone.Deck);
    var faith = factory.CreateAndRegisterNewCard(game, 6, own.PlayerId, CardZone.Deck);
    var fake = new CardInstance(Guid.NewGuid(), 3, own.PlayerId) { CurrentZone = CardZone.Deck };
    using var context = new GameContext(services, mapper, accessor, game, own.PlayerId);
    var result = handler.Apply(new Effect(own.PlayerId), context, new() { Mode = DiscoverMode.Instance, CopySourceTargets = new[] { valid, valid, hand, crest, faith, fake } });
    var request = result.GeneratedEvents.OfType<DiscoverRequestEvent>().Single(); request.ResolveAsync(context).GetAwaiter().GetResult();
    Check(lastChoice!.SelectableTargetIds.SequenceEqual(new[] { valid.InstanceId }), "invalid or duplicate candidates shown");
    var empty = handler.Apply(new Effect(own.PlayerId), context, new() { Mode = DiscoverMode.Instance, CopySourceTargets = Array.Empty<CardInstance>() });
    Check(empty.Success && empty.GeneratedEvents.Count == 0, "empty pool opened a choice");
});
Test("full own hand burns stolen original under the new owner", () => {
    var (game, own, enemy) = Match();
    for (var i = 0; i < GameConstants.Hand.MaxHandSize; i++) factory.CreateAndRegisterNewCard(game, 2, own.PlayerId, CardZone.Hand);
    var card = factory.CreateAndRegisterNewCard(game, 1, enemy.PlayerId, CardZone.Deck);
    using var context = new GameContext(services, mapper, accessor, game, own.PlayerId);
    Commit(Request(card, context).Commit(context).GeneratedEvents, context);
    Check(card.CurrentZone == CardZone.Graveyard && card.OwnerPlayerId == own.PlayerId && own.Graveyard.Contains(card), "overflow ownership or destination wrong");
    Check(enemy.Deck.Count == 0 && own.Hand.Count == GameConstants.Hand.MaxHandSize, "overflow changed hand or left source card");
});
Test("stale choice cannot materialize a replacement or steal another zone", () => {
    var (game, own, enemy) = Match(); var card = factory.CreateAndRegisterNewCard(game, 1, enemy.PlayerId, CardZone.Deck);
    using var context = new GameContext(services, mapper, accessor, game, own.PlayerId);
    var request = Request(card, context);
    Commit(new[] { new CardMovedEvent(card, CardZone.Deck, CardZone.Hand, CardMoveReason.Drawn) }, context);
    var result = request.Commit(context);
    Check(!result.Success && own.Hand.Count == 0 && enemy.Hand.Contains(card) && card.OwnerPlayerId == enemy.PlayerId, "stale selection moved or copied card");
});
Test("definition and copy modes still create new cards without consuming source", () => {
    foreach (var mode in new[] { DiscoverMode.Definition, DiscoverMode.Copy }) {
        var (game, own, enemy) = Match(); var source = factory.CreateAndRegisterNewCard(game, 1, enemy.PlayerId, CardZone.Deck);
        using var context = new GameContext(services, mapper, accessor, game, own.PlayerId);
        var result = handler.Apply(new Effect(own.PlayerId), context, new() { Mode = mode, CardDefinitionIds = [1], CopySourceTargets = source });
        Check(result.GeneratedEvents.OfType<DiscoverRequestEvent>().Any(), $"configured mode {mode} produced no candidates");
        var request = result.GeneratedEvents.OfType<DiscoverRequestEvent>().Single(); request.ResolveAsync(context).GetAwaiter().GetResult();
        Commit(request.Commit(context).GeneratedEvents, context);
        Check(own.Hand.Single().InstanceId != source.InstanceId && enemy.Deck.Single() == source, "existing mode consumed original");
    }
});
Test("cancel and invalid replies do not mutate decks", () => {
    var (game, own, enemy) = Match(); var card = factory.CreateAndRegisterNewCard(game, 1, enemy.PlayerId, CardZone.Deck);
    using var context = new GameContext(services, mapper, accessor, game, own.PlayerId);
    cancel = true; Check(Request(card, context).Commit(context).GeneratedEvents.Count == 0, "cancel produced cards"); cancel = false;
    invalid = true; var rejected = false;
    try { Request(card, context); } catch (InvalidOperationException) { rejected = true; } finally { invalid = false; }
    Check(rejected && enemy.Deck.Single() == card && own.Hand.Count == 0, "invalid reply changed state");
});
Test("executor binds instance mode and skips the inactive definition formula", () => {
    var unusedFormulaCalls = 0;
    var resolver = Proxy<IValueSourceResolver>.Make((m, a) => {
        if (a![0] is StaticValueConfig value) return value.Value;
        unusedFormulaCalls++; return Array.Empty<int>();
    });
    using var handlers = new ServiceCollection().AddSingleton(handler).BuildServiceProvider();
    var executor = new EffectExecutor(handlers, resolver, NullLogger<EffectExecutor>.Instance,
        new EffectHandlerRegistry(NullLogger<EffectHandlerRegistry>.Instance));
    foreach (var mode in new object[] { "Instance", 2, DiscoverMode.Instance }) {
        var (game, own, enemy) = Match(); var card = factory.CreateAndRegisterNewCard(game, 1, enemy.PlayerId, CardZone.Deck);
        using var context = new GameContext(services, mapper, accessor, game, own.PlayerId);
        var effect = new Effect(own.PlayerId) { EffectConfig = new() { Type = EffectType.Discover,
            Parameters = new() { ["Mode"] = new StaticValueConfig { Value = mode },
                ["CopySourceTargets"] = new StaticValueConfig { Value = new[] { card } },
                ["CardDefinitionIds"] = new FormulaValueConfig { Formula = "unused" } } } };
        var result = executor.ExecuteEffect(effect, context); Check(result.Success, result.ErrorMessage ?? "executor failed");
        var request = result.GeneratedEvents.OfType<DiscoverRequestEvent>().Single(); request.ResolveAsync(context).GetAwaiter().GetResult();
        Commit(request.Commit(context).GeneratedEvents, context);
        Check(own.Hand.Single() == card && enemy.Deck.Count == 0, "configured instance mode was bound as definition/copy");
    }
    Check(unusedFormulaCalls == 0, "inactive definition formula was evaluated");
});
Console.WriteLine($"Discover regression: {passed} passed; external services and player databases unused.");

public class Proxy<T> : DispatchProxy where T : class {
    public Func<MethodInfo, object?[]?, object?> Handler = null!;
    public static T Make(Func<MethodInfo, object?[]?, object?> handler) {
        var proxy = Create<T, Proxy<T>>(); ((Proxy<T>)(object)proxy).Handler = handler; return proxy;
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args) {
        var value = Handler(method!, args); if (value != null) return value;
        return method!.ReturnType != typeof(void) && method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}
