using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class ChooseAbilityIntegrationTests
    {
        [Fact]
        public async Task Choose_BranchSelection_IsReusedBetweenCancellableAndCommittedPhases()
        {
            var requestingPlayerId = Guid.NewGuid();
            var opponentPlayerId = Guid.NewGuid();
            var gameId = "test-game";

            var gameState = new GameState(
                gameId,
                new PlayerState(requestingPlayerId),
                new PlayerState(opponentPlayerId),
                requestingPlayerId);

            gameState.AbilityManager = new NullAbilityManager();
            gameState.BuffManager = new NullBuffManager();

            var fakePending = new FakePendingActionService();
            var accessor = new GameContextAccessor();
            var mapper = new MapperConfiguration(_ => { }).CreateMapper();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor>(accessor);
            services.AddSingleton<IMapper>(mapper);
            services.AddSingleton<IBuffService, DummyBuffService>();
            services.AddSingleton<IPendingActionService>(fakePending);
            services.AddSingleton<IValueSourceResolver, NullValueSourceResolver>();
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();

            var conditionFactory = new NullConditionFactory();
            var targetFactory = new NullTargetFactory();
            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                new NullQueryExecutor(),
                fakePending);

            services.AddSingleton<IAbilityFactory>(new FakeAbilityFactory(conditionFactory, targetFactory, targetSelectionService));

            var serviceProvider = services.BuildServiceProvider();

            var sourceCard = new CardInstance(Guid.NewGuid(), 0, requestingPlayerId) { CurrentZone = CardZone.Field };

            var branchA = new AbilityConfig { Name = "Branch A", Description = "A", Effects = new List<EffectConfig>() };
            var branchB = new AbilityConfig { Name = "Branch B", Description = "B", Effects = new List<EffectConfig>() };

            var parentConfig = new AbilityConfig
            {
                Effects = new List<EffectConfig>
                {
                    new EffectConfig
                    {
                        Type = EffectType.Choose,
                        ChainedAbilities = new List<AbilityConfig> { branchA, branchB }
                    }
                }
            };

            var ability = new Ability(parentConfig, conditionFactory, targetFactory, targetSelectionService, sourceCard);

            var triggerEventId = Guid.NewGuid();
            var triggeringEvent = new TestGameEvent(triggerEventId);

            var targetResult = new TargetResolutionResult
            {
                Status = ResolutionStatus.Completed,
                ResolvedTargets = new Dictionary<string, List<TargetInfo>>()
            };

            // Choose selection is now resolved by transaction decision events, not by CreateEffectsAsync.
            using (var cancellableContext = new GameContext(
                       serviceProvider,
                       mapper,
                       accessor,
                       gameState,
                       requestingPlayerId,
                       CancellationToken.None,
                       triggeringEvent,
                       sourceCard,
                       null,
                       null,
                       isCancellable: true))
            {
                var effects = await ability.CreateEffectsAsync(cancellableContext, targetResult);
                Assert.Single(effects);
                Assert.Equal(EffectType.Choose, effects[0].Type);
                Assert.Equal(2, effects[0].EffectConfig.ChainedAbilities.Count);
                Assert.Contains(effects[0].EffectConfig.ChainedAbilities, a => a.Name == "Branch A");
                Assert.Contains(effects[0].EffectConfig.ChainedAbilities, a => a.Name == "Branch B");
            }

            // Commit pass should keep the same option set without triggering player prompt.
            using (var committedContext = new GameContext(
                       serviceProvider,
                       mapper,
                       accessor,
                       gameState,
                       requestingPlayerId,
                       CancellationToken.None,
                       triggeringEvent,
                       sourceCard,
                       null,
                       null,
                       isCancellable: false))
            {
                var effects = await ability.CreateEffectsAsync(committedContext, targetResult);
                Assert.Single(effects);
                Assert.Equal(EffectType.Choose, effects[0].Type);
                Assert.Equal(2, effects[0].EffectConfig.ChainedAbilities.Count);
                Assert.Contains(effects[0].EffectConfig.ChainedAbilities, a => a.Name == "Branch A");
                Assert.Contains(effects[0].EffectConfig.ChainedAbilities, a => a.Name == "Branch B");
            }

            Assert.Equal(0, fakePending.WaitCallCount);
            Assert.Null(fakePending.LastRequest);
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

        private sealed class FakePendingActionService : IPendingActionService
        {
            public int WaitCallCount { get; private set; }
            public PlayerChoiceRequest? LastRequest { get; private set; }

            public Task<PlayerChoiceResult> WaitForPlayerChoiceAsync(string gameId, PlayerChoiceRequest request, CancellationToken cancellationToken)
            {
                WaitCallCount++;
                LastRequest = request;

                var preferred = request.SelectableCardInstances.FirstOrDefault(c => c.Name == "Branch A")?.InstanceId;
                var chosen = preferred ?? request.SelectableTargetIds.First();
                return Task.FromResult(new PlayerChoiceResult { WasCancelled = false, ChosenTargetIds = new List<Guid> { chosen } });
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

        private sealed class NullAbilityManager : IAbilityManager
        {
            public List<Ability> CreateAbilitiesForCard(
                CardInstance cardInstance,
                List<AbilityConfig> abilityConfigs,
                Guid? sourceBuffId = null) => new();
            public List<Ability> CreateAbilitiesForCard(
                CardInstance cardInstance,
                List<AbilityConfig> abilityConfigs,
                Guid? sourceBuffId,
                GameState? gameState) => new();
            public void RegisterAbility(IAbility ability) { }
            public void UnregisterAbility(IAbility ability, string removalReason = "unregistered") { }
            public void UnregisterCardAbilities(Guid cardInstanceId, string removalReason = "card_unregistered") { }
            public void UnregisterAbilitiesFromBuff(Guid buffId, string removalReason = "buff_removed") { }
            public IReadOnlyList<Guid> GetAbilityRuntimeIdsForBuff(Guid buffId) => Array.Empty<Guid>();
            public void ClearAllAbilities() { }
            public List<IAbility> GetAbilitiesForCard(Guid cardInstanceId) => new();
            public List<IAbility> GetEffectiveAbilitiesForCard(Guid cardInstanceId, GameContext context) => new();
            public bool IsAbilitySuppressed(IAbility ability, GameContext context) => false;
            public IAbility? GetAbilityByRuntimeId(Guid runtimeId) => null;
            public bool TryGetAbilitySnapshot(Guid runtimeId, out AbilitySnapshot snapshot)
            {
                snapshot = null!;
                return false;
            }
            public void ClearAbilityTombstones() { }
            public T? GetSystemAbility<T>() where T : class, IAbility => null;
            public Task<List<IAbility>> GetTriggeredAbilitiesAsync(IGameEvent gameEvent, GameContext context, TriggerTiming timing) => Task.FromResult(new List<IAbility>());
            public Task<AbilityProcessResult> ProcessSingleAbilityAsync(IAbility ability, GameContext context, bool isCancellable, bool deferTargetResolution, bool recordTrigger = true)
                => Task.FromResult(AbilityProcessResult.Success(new List<Effect>()));
            public DomainOperationResult RefreshAuraForAbility(IAbility ability, GameContext context) => new() { Success = true };
        }

        private sealed class NullBuffManager : IBuffManager
        {
            public List<Buff> GetBuffsForCard(Guid cardInstanceId) => new();
            public Buff? FindBuffInstance(Guid buffId) => null;
            public void RegisterBuffInstance(Buff buff, GameState gameState) { }
            public Buff? UnregisterBuffInstance(Guid buffId) => null;
        }

        private sealed class DummyBuffService : IBuffService
        {
            public int GetEffectiveAttack(CardInstance card, GameContext gameContext) => throw new NotImplementedException();
            public int GetEffectiveMaxHealth(CardInstance card, GameContext gameContext) => throw new NotImplementedException();
            public int GetRawEffectiveMaxHealth(CardInstance card, GameContext gameContext) => throw new NotImplementedException();
            public int GetEffectiveCost(CardInstance card, GameContext gameContext) => throw new NotImplementedException();
            public List<CardKeyword> GetEffectiveKeywords(CardInstance card, GameContext gameContext) => throw new NotImplementedException();
            public int GetEffectiveAbilityKeywordValue(CardInstance card, AbilityKeyword keyword, int baseValue, GameContext gameContext) => baseValue;
            public int GetEffectiveActionUsageLimit(CardInstance card, CardActionUsageType actionType, GameContext gameContext) => card.GetBaseActionUsageLimit(actionType);
            public DomainOperationResult ApplyKeywordBuff(GameContext context, CardInstance target, List<CardKeyword> keywords, DurationType durationType, int? durationParameter, string? effectKey = null, bool canStack = true, ConditionConfig? removeCondition = null) => throw new NotImplementedException();
            public DomainOperationResult ApplyRemoveKeywordBuff(GameContext context, CardInstance target, List<CardKeyword> keywordsToRemove, DurationType durationType, int? durationParameter, string? effectKey = null, bool canStack = true, ConditionConfig? removeCondition = null) => throw new NotImplementedException();
            public DomainOperationResult ApplyStatBuff(GameContext context, CardInstance target, List<StatModification> modifications, DurationType durationType, int? durationParameter, string? effectKey = null, bool canStack = true, ConditionConfig? removeCondition = null) => throw new NotImplementedException();
            public DomainOperationResult ApplyTribeBuff(GameContext context, CardInstance target, List<string> tribes, DurationType durationType, int? durationParameter, string? effectKey = null, bool canStack = true, ConditionConfig? removeCondition = null) => throw new NotImplementedException();
            public DomainOperationResult ApplyActionUsageLimitBuff(GameContext context, CardInstance target, List<ActionUsageLimitModification> modifications, DurationType durationType, int? durationParameter, string? effectKey = null, bool canStack = true, ConditionConfig? removeCondition = null) => throw new NotImplementedException();
            public DomainOperationResult ApplyBuffProcess(Buff buff, GameContext context, bool canStack = true) => throw new NotImplementedException();
            public DomainOperationResult RemoveBuffProcess(Guid buffId, GameContext context) => throw new NotImplementedException();
            public DomainOperationResult RemoveBuffsFromSource(long sourceAbilityId, GameContext context) => throw new NotImplementedException();
            public DomainOperationResult RemoveAllBuffs(Guid cardInstanceId, GameContext context, Func<Buff, bool>? predicate = null) => throw new NotImplementedException();
            public void RefreshDerivedStateCache(GameContext context, string reason) => throw new NotImplementedException();
            public void InvalidateDerivedState(GameState gameState, Guid cardInstanceId) => throw new NotImplementedException();
            public IReadOnlyList<BuffRemovedEvent> CollectEndOfTurnExpiryEvents(GameContext context) => throw new NotImplementedException();
            public IReadOnlyList<BuffRemovedEvent> CollectStartOfTurnExpiryEvents(GameContext context, PlayerState startingPlayer) => throw new NotImplementedException();
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
}
