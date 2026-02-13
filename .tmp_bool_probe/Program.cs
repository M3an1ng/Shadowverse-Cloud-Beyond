using AutoMapper;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddMemoryCache();
services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
services.AddSingleton<IBuffService>(_ => new Mock<IBuffService>().Object);
services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();

var sp = services.BuildServiceProvider();
var mapper = new MapperConfiguration(cfg => { }).CreateMapper();
var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

var functionRegistry = new FunctionRegistry(loggerFactory.CreateLogger<FunctionRegistry>());
var cache = new DslExpressionCache(sp.GetRequiredService<IMemoryCache>());
var compiler = new DslCompiler(
    loggerFactory.CreateLogger<DslCompiler>(),
    functionRegistry,
    cache,
    new DummyGameDataProvider());

var p1 = new PlayerState(Guid.NewGuid());
var p2 = new PlayerState(Guid.NewGuid());
var gameState = new GameState("probe-game", p1, p2, p1.PlayerId);

var triggeringEvent = new ProbeEvent(new[] { AbilityKeyword.EarthRitual });

var ctx = new GameContext(
    sp,
    mapper,
    sp.GetRequiredService<IGameContextAccessor>(),
    gameState,
    p1.PlayerId,
    triggeringEvent: triggeringEvent);

var tsc = new ShadowverseBeyond.Server.Domain.Abilities.Targets.TargetSelectionContext(ctx, null);

var e1 = "contains(context.triggeringEvent.AbilityKeywords, (\"EarthRitual\"))";
var e2 = "contains(context.triggeringEvent.AbilityKeywords, (\"EarthRitual\")) == true";
var e3 = "contains(context.triggeringEvent.AbilityKeywords, (\"EarthRite\"))";

var pE1 = compiler.CompilePredicateRuntime(e1, tsc, null, IDslCompiler.SelfResolutionMode.ContextOnly);
var pE2 = compiler.CompilePredicateRuntime(e2, tsc, null, IDslCompiler.SelfResolutionMode.ContextOnly);
var pE3 = compiler.CompilePredicateRuntime(e3, tsc, null, IDslCompiler.SelfResolutionMode.ContextOnly);

Console.WriteLine($"event keywords: {string.Join(",", triggeringEvent.AbilityKeywords)}");
Console.WriteLine($"{e1} => {pE1(null!)}");
Console.WriteLine($"{e2} => {pE2(null!)}");
Console.WriteLine($"{e3} => {pE3(null!)}");

public sealed class ProbeEvent : GameEventBase
{
    public IReadOnlyList<AbilityKeyword> AbilityKeywords { get; }

    public ProbeEvent(IReadOnlyList<AbilityKeyword> abilityKeywords)
        : base(EventType.AbilityTriggered)
    {
        AbilityKeywords = abilityKeywords;
    }
}

public sealed class DummyGameDataProvider : IGameDataProvider
{
    public IReadOnlyCollection<CardDefinition> Cards => Array.Empty<CardDefinition>();
    public bool IsInitialized => true;
    public Guid DataRevision => Guid.Empty;

    public Task InitializeAsync() => Task.CompletedTask;
    public CardDefinition? GetCardDefinition(int cardId) => null;
    public IReadOnlyDictionary<int, CardDefinition> GetAllCardDefinitions() => new Dictionary<int, CardDefinition>();
    public List<AbilityConfig> GetAbilityConfigs(int cardId) => new List<AbilityConfig>();
    public AbilityConfig? GetAbilityConfig(long abilityId) => null;
    public IReadOnlyDictionary<int, List<AbilityConfig>> GetAllAbilityConfigs() => new Dictionary<int, List<AbilityConfig>>();
    public BuffDefinition? GetBuffDefinition(string buffId) => null;
    public Task RefreshCacheAsync() => Task.CompletedTask;
    public void ClearCache() { }
    public CacheStatistics GetCacheStatistics() => new CacheStatistics();
}
