using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Dtos.Enums;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public class DecisionEffectOutputTests
{
    [Fact]
    public void RandomEvent_CommitsDecisionOutputs()
    {
        var services = BuildServices(new FixedChoicePendingActionService());
        var serviceProvider = services.BuildServiceProvider();
        var context = BuildContext(serviceProvider);

        var optionIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var randomEvent = new RandomEvent(
            optionIds,
            selectionCount: 1,
            allowRepeat: false,
            weights: new List<double> { 1, 0 });

        randomEvent.SetSelectedOptionIndexes(new List<int> { 0 });
        randomEvent.Commit(context);

        Assert.Equal(new List<int> { 0 }, randomEvent.SelectedOptionIds.ToList());
    }

    [Fact]
    public void ChooseEvent_CommitsDecisionOutputs()
    {
        var services = BuildServices(new FixedChoicePendingActionService());
        var serviceProvider = services.BuildServiceProvider();
        var context = BuildContext(serviceProvider);

        var optionIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var chooseEvent = new ChooseEvent(
            optionIds,
            selectionCount: 1);

        chooseEvent.SetSelectedOptionIndexes(new List<int> { 1 });
        chooseEvent.Commit(context);

        Assert.Equal(new List<int> { 1 }, chooseEvent.SelectedOptionIds.ToList());
    }

    private static GameContext BuildContext(ServiceProvider serviceProvider)
    {
        var playerId = Guid.NewGuid();
        var opponentId = Guid.NewGuid();
        var gameState = new GameState("decision-output-test", new PlayerState(playerId), new PlayerState(opponentId), playerId)
        {
            AbilityManager = new Mock<IAbilityManager>().Object,
            BuffManager = new Mock<IBuffManager>().Object
        };

        var mapper = serviceProvider.GetRequiredService<IMapper>();
        var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();

        var sourceCard = new CardInstance(Guid.NewGuid(), 0, playerId) { CurrentZone = CardZone.Field };
        var triggeringEvent = new TestGameEvent(Guid.NewGuid());

        var context = new GameContext(
            serviceProvider,
            mapper,
            accessor,
            gameState,
            playerId,
            CancellationToken.None,
            triggeringEvent,
            sourceCard,
            null,
            null,
            isCancellable: false);
        return context;
    }

    private static ServiceCollection BuildServices(IPendingActionService pendingActionService)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
        services.AddSingleton<IMapper>(new MapperConfiguration(_ => { }).CreateMapper());
        services.AddSingleton<IBuffService>(new Mock<IBuffService>().Object);
        services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
        services.AddSingleton<IPendingActionService>(pendingActionService);
        services.AddSingleton<IValueSourceResolver, NullValueSourceResolver>();
        services.AddSingleton<RandomEffectHandler>();
        services.AddSingleton<ChooseEffectHandler>();

        var conditionFactory = new NullConditionFactory();
        var targetFactory = new NullTargetFactory();
        var targetSelectionService = new TargetSelectionService(
            NullLogger<TargetSelectionService>.Instance,
            new NullQueryExecutor(),
            pendingActionService);

        services.AddSingleton<IAbilityFactory>(new FakeAbilityFactory(conditionFactory, targetFactory, targetSelectionService));

        return services;
    }

    private sealed class TestGameEvent : IGameEvent
    {
        public EventType EventType => EventType.PlayCardRequested;
        public Guid EventId { get; }
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public TargetInfo? Source => null;
        public List<TargetInfo> Target { get; } = new();

        public TestGameEvent(Guid eventId)
        {
            EventId = eventId;
        }
    }

    private sealed class FixedChoicePendingActionService : IPendingActionService
    {
        public int PreferredIndex { get; set; }

        public Task<PlayerChoiceResult> WaitForPlayerChoiceAsync(string gameId, PlayerChoiceRequest request, CancellationToken cancellationToken)
        {
            var chosen = request.SelectableTargetIds.Count > PreferredIndex
                ? request.SelectableTargetIds[PreferredIndex]
                : request.SelectableTargetIds.Last();

            return Task.FromResult(new PlayerChoiceResult
            {
                WasCancelled = false,
                ChosenTargetIds = new List<Guid> { chosen }
            });
        }

        public void CompletePlayerChoice(Guid choiceId, Dtos.SignalR.Actions.SubmitTargetChoiceActionDto dto) => throw new NotImplementedException();
        public PendingChoiceContext? GetPendingChoiceForPlayer(string gameId, Guid playerId) => throw new NotImplementedException();
        public void ForceResolveChoice(Guid choiceId, PlayerChoiceResult forcedResult) => throw new NotImplementedException();
    }

    private sealed class FakeAbilityFactory : IAbilityFactory
    {
        private readonly IConditionFactory _conditionFactory;
        private readonly ITargetFactory _targetFactory;
        private readonly TargetSelectionService _targetSelectionService;

        public FakeAbilityFactory(IConditionFactory conditionFactory, ITargetFactory targetFactory, TargetSelectionService targetSelectionService)
        {
            _conditionFactory = conditionFactory;
            _targetFactory = targetFactory;
            _targetSelectionService = targetSelectionService;
        }

        public Ability CreateAbility(AbilityConfig config, CardInstance owner, Guid? sourceBuffId = null)
            => new(config, _conditionFactory, _targetFactory, _targetSelectionService, owner, sourceBuffId);

        public Ability CreateAbilityCopy(Ability ability)
            => new(ability.Config, _conditionFactory, _targetFactory, _targetSelectionService, ability.SourceCard, ability.SourceBuffId);

        public List<Ability> CreateAbilities(CardInstance cardInstance, IEnumerable<AbilityConfig> abilityConfigs, Guid? sourceBuffId = null)
            => abilityConfigs.Select(cfg => CreateAbility(cfg, cardInstance, sourceBuffId)).ToList();
    }

    private sealed class NullValueSourceResolver : IValueSourceResolver
    {
        public object? Resolve(ValueConfig valueConfig, GameContext context)
            => valueConfig is StaticValueConfig staticValue ? staticValue.Value : null;

        public object? ResolveFormulaValue(string formula, GameContext context)
            => null;

        public object? ResolveFormulaValueWithTrace(string formula, GameContext context, IDslTraceCollector traceCollector)
            => ResolveFormulaValue(formula, context);
    }

    private sealed class NullQueryExecutor : IQueryExecutor
    {
        public QueryResult ExecuteQuery(string dsl, GameContext context, Dictionary<string, List<TargetInfo>>? resolvedTargets = null)
            => new() { Success = false, ErrorMessage = "Not used in this test" };

        public QueryResult ExecuteQuery(QueryConfig queryConfig, GameContext context, Dictionary<string, List<TargetInfo>>? resolvedTargets = null)
            => new() { Success = false, ErrorMessage = "Not used in this test" };
    }

    private sealed class NullConditionFactory : IConditionFactory
    {
        public ShadowverseBeyond.Server.Domain.Abilities.Runtime.Condition CreateCondition(ConditionConfig config)
            => throw new NotImplementedException("Conditions are not used in this test.");
    }

    private sealed class NullTargetFactory : ITargetFactory
    {
        public ShadowverseBeyond.Server.Domain.Abilities.Runtime.Target Create(string key, TargetConfig config)
            => throw new NotImplementedException("Targets are not used in this test.");
    }
}
