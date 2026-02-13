using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class GrantAbilitySetSetStatusOutputsTests
    {
        [Fact]
        public void GrantAbility_EndOfTurn_RemovesGrantedAbility()
        {
            var env = TestEnvironment.Create();
            var source = env.CreateCard(1001, env.PlayerId);
            var target = env.CreateCard(1002, env.PlayerId);

            using var context = env.CreateContext(source);

            var grantedAbility = new AbilityConfig
            {
                Name = "Granted Ability",
                Type = AbilityType.Triggered,
                Trigger = new TriggerConfig
                {
                    EventTypes = new List<EventType> { EventType.TurnStart },
                    Timing = TriggerTiming.On
                }
            };

            var parameters = new GrantAbilityEffectParams
            {
                AbilityConfigs = new List<AbilityConfig> { grantedAbility },
                DurationType = DurationType.EndOfTurn
            };

            var effectConfig = new EffectConfig { Type = EffectType.GrantAbility };
            var effect = new Effect(
                effectConfig,
                env.PlayerId,
                new List<TargetInfo> { TargetInfo.ForCard(target) },
                parentAbilityId: null,
                triggeredEventId: Guid.Empty,
                sourceCardId: source.InstanceId);

            var handler = new GrantAbilityEffectHandler(
                NullLogger<GrantAbilityEffectHandler>.Instance,
                new TargetProcessingService(NullLogger<TargetProcessingService>.Instance));

            var applyResult = handler.Apply(effect, context, parameters);
            Assert.True(applyResult.Success);

            CommitGeneratedEvents(applyResult.GeneratedEvents, context);

            Assert.Contains(target.Abilities, ability => ability.Name == "Granted Ability");

            var expiryEvents = env.BuffService.CollectEndOfTurnExpiryEvents(context);
            Assert.NotEmpty(expiryEvents);
            CommitGeneratedEvents(expiryEvents, context);

            Assert.DoesNotContain(target.Abilities, ability => ability.Name == "Granted Ability");
        }

        [Fact]
        public void SetSetStatus_SetsStatsUsingSetLayer()
        {
            var env = TestEnvironment.Create();
            var source = env.CreateCard(2001, env.PlayerId);
            var target = env.CreateCard(2002, env.PlayerId);

            using var context = env.CreateContext(source);

            var parameters = new SetStatsEffectParams
            {
                DurationType = DurationType.Permanent,
                AttackModifier = 5,
                HealthModifier = 7,
                CostModifier = 1
            };

            var effectConfig = new EffectConfig { Type = EffectType.GrantAbility };
            var effect = new Effect(
                effectConfig,
                env.PlayerId,
                new List<TargetInfo> { TargetInfo.ForCard(target) },
                parentAbilityId: null,
                triggeredEventId: Guid.Empty,
                sourceCardId: source.InstanceId);

            var handler = new SetStatsEffectHandler(
                NullLogger<SetStatsEffectHandler>.Instance,
                new TargetProcessingService(NullLogger<TargetProcessingService>.Instance));

            var applyResult = handler.Apply(effect, context, parameters);
            Assert.True(applyResult.Success);

            CommitGeneratedEvents(applyResult.GeneratedEvents, context);

            Assert.Equal(5, env.BuffService.GetEffectiveAttack(target, context));
            Assert.Equal(7, env.BuffService.GetEffectiveMaxHealth(target, context));
            Assert.Equal(1, env.BuffService.GetEffectiveCost(target, context));
        }

        [Fact]
        public void SetHealth_ResetsDamageAndCurrentHealthToNewMax()
        {
            var env = TestEnvironment.Create();
            var source = env.CreateCard(2101, env.PlayerId);
            var target = env.CreateCard(2102, env.PlayerId);

            using var context = env.CreateContext(source);

            var growHealth = new StatChangedEvent(
                targetCard: target,
                attackModifier: null,
                healthModifier: 3,
                costModifier: null,
                durationType: DurationType.Permanent,
                durationParameter: 0,
                modifierLayer: ModifierLayer.Additive);
            var growResult = growHealth.Commit(context);
            Assert.True(growResult.Success);
            CommitGeneratedEvents(growResult.GeneratedEvents, context);

            Assert.Equal(4, env.BuffService.GetEffectiveMaxHealth(target, context));
            target.Damage = 2;
            Assert.Equal(2, target.CurrentHealth);

            var setHealth = new StatChangedEvent(
                targetCard: target,
                attackModifier: null,
                healthModifier: 7,
                costModifier: null,
                durationType: DurationType.Permanent,
                durationParameter: 0,
                modifierLayer: ModifierLayer.Set);
            var setResult = setHealth.Commit(context);
            Assert.True(setResult.Success);
            CommitGeneratedEvents(setResult.GeneratedEvents, context);

            Assert.Equal(7, env.BuffService.GetEffectiveMaxHealth(target, context));
            Assert.Equal(0, target.Damage);
            Assert.Equal(7, target.CurrentHealth);
        }

        [Fact]
        public void AdjustMaxHealth_DoesNotHealAndCapsCurrentHealth()
        {
            var env = TestEnvironment.Create();
            var source = env.CreateCard(2201, env.PlayerId);
            var target = env.CreateCard(2202, env.PlayerId);

            using var context = env.CreateContext(source);

            var growHealth = new StatChangedEvent(
                targetCard: target,
                attackModifier: null,
                healthModifier: 3,
                costModifier: null,
                durationType: DurationType.Permanent,
                durationParameter: 0,
                modifierLayer: ModifierLayer.Additive);
            var growResult = growHealth.Commit(context);
            Assert.True(growResult.Success);
            CommitGeneratedEvents(growResult.GeneratedEvents, context);

            Assert.Equal(4, env.BuffService.GetEffectiveMaxHealth(target, context));
            target.Damage = 2;
            Assert.Equal(2, target.CurrentHealth);

            var handler = new ModifyMaxHealthEffectHandler(
                NullLogger<ModifyMaxHealthEffectHandler>.Instance,
                new TargetProcessingService(NullLogger<TargetProcessingService>.Instance));

            var setToSeven = new Effect(
                new EffectConfig { Type = EffectType.AdjustMaxHealth },
                env.PlayerId,
                new List<TargetInfo> { TargetInfo.ForCard(target) },
                parentAbilityId: null,
                triggeredEventId: Guid.Empty,
                sourceCardId: source.InstanceId);
            var increaseParams = new ModifyMaxHealthEffectParams
            {
                DurationType = DurationType.Permanent,
                DurationParameter = 0,
                HealthModifier = 7,
                ModifierLayer = ModifierLayer.Set
            };

            var increaseResult = handler.Apply(setToSeven, context, increaseParams);
            Assert.True(increaseResult.Success);
            CommitGeneratedEvents(increaseResult.GeneratedEvents, context);

            Assert.Equal(7, env.BuffService.GetEffectiveMaxHealth(target, context));
            Assert.Equal(2, target.CurrentHealth);

            var setToOne = new Effect(
                new EffectConfig { Type = EffectType.AdjustMaxHealth },
                env.PlayerId,
                new List<TargetInfo> { TargetInfo.ForCard(target) },
                parentAbilityId: null,
                triggeredEventId: Guid.Empty,
                sourceCardId: source.InstanceId);
            var decreaseParams = new ModifyMaxHealthEffectParams
            {
                DurationType = DurationType.Permanent,
                DurationParameter = 0,
                HealthModifier = 1,
                ModifierLayer = ModifierLayer.Set
            };

            var decreaseResult = handler.Apply(setToOne, context, decreaseParams);
            Assert.True(decreaseResult.Success);
            CommitGeneratedEvents(decreaseResult.GeneratedEvents, context);

            Assert.Equal(1, env.BuffService.GetEffectiveMaxHealth(target, context));
            Assert.Equal(1, target.CurrentHealth);
        }

        [Fact]
        public void EffectCompletedEvent_ExposesGeneratedEvents()
        {
            var env = TestEnvironment.Create();
            var source = env.CreateCard(3001, env.PlayerId);
            var generated = env.CreateCard(3002, env.PlayerId);

            using var effectContext = env.CreateContext(source);

            var effectConfig = new EffectConfig
            {
                Type = EffectType.Summon,
                Parameters = new Dictionary<string, ValueConfig>
                {
                    ["CardSourceMode"] = new StaticValueConfig { Value = CardSourceMode.Instance }
                }
            };

            var effect = new Effect(
                effectConfig,
                env.PlayerId,
                new List<TargetInfo> { TargetInfo.ForCard(generated) },
                parentAbilityId: null,
                triggeredEventId: Guid.Empty,
                sourceCardId: source.InstanceId);

            var executor = env.ServiceProvider.GetRequiredService<IEffectExecutor>();
            var effectResult = executor.ExecuteEffect(effect, effectContext);
            Assert.True(effectResult.Success);

            Assert.Empty(effectResult.GeneratedEvents.OfType<EffectCompletedEvent>());
            Assert.Contains(effectResult.GeneratedEvents, ev =>
                ev is SummonEvent summon && summon.SummonedCard?.InstanceId == generated.InstanceId);
        }

        private static void CommitGeneratedEvents(IEnumerable<IGameEvent>? events, GameContext context)
        {
            if (events == null)
            {
                return;
            }

            foreach (var ev in events.OfType<ICommittableEvent>())
            {
                var result = ev.Commit(context);
                if (result.GeneratedEvents.Count > 0)
                {
                    foreach (var nested in result.GeneratedEvents.OfType<ICommittableEvent>())
                    {
                        nested.Commit(context);
                    }
                }
            }
        }

        private sealed class TestEnvironment
        {
            private readonly Mock<IGameDataProvider> _gameDataProvider = new();
            private readonly Mock<IMapper> _mapper = new();
            private readonly Mock<IConditionEvaluator> _conditionEvaluator = new();
            private readonly Mock<IQueryDslParser> _queryDslParser = new();

            public Guid PlayerId { get; } = Guid.NewGuid();
            public PlayerState Player { get; }
            public GameState GameState { get; }
            public ServiceProvider ServiceProvider { get; }
            public AbilityManager AbilityManager { get; }
            public BuffManager BuffManager { get; }
            public BuffFactory BuffFactory { get; }
            public BuffService BuffService { get; }
            public TargetSelectionService TargetSelectionService { get; }
            public IConditionFactory ConditionFactory { get; }
            public ITargetFactory TargetFactory { get; }

            private TestEnvironment()
            {
                Player = new PlayerState(PlayerId);
                var opponent = new PlayerState(Guid.NewGuid());
                GameState = new GameState("grant-ability-tests", Player, opponent, PlayerId);

                var functionRegistry = new FunctionRegistry(NullLogger<FunctionRegistry>.Instance);
                var memoryCache = new MemoryCache(new MemoryCacheOptions());
                var expressionCache = new DslExpressionCache(memoryCache);
                var dslCompiler = new DslCompiler(NullLogger<DslCompiler>.Instance, functionRegistry, expressionCache, _gameDataProvider.Object);
                var valueSourceResolver = new ValueSourceResolver(NullLogger<ValueSourceResolver>.Instance, dslCompiler);
                var queryExecutor = new QueryExecutor(NullLogger<QueryExecutor>.Instance, dslCompiler, _mapper.Object, _queryDslParser.Object);

                TargetSelectionService = new TargetSelectionService(
                    NullLogger<TargetSelectionService>.Instance,
                    queryExecutor,
                    new NullPendingActionService());

                ConditionFactory = new NullConditionFactory();
                TargetFactory = new TargetFactory(TargetSelectionService, NullLogger<Target>.Instance);
                var abilityFactory = new AbilityFactory(NullLogger<AbilityFactory>.Instance, ConditionFactory, TargetFactory, TargetSelectionService);
                AbilityManager = new AbilityManager(NullLogger<AbilityManager>.Instance, abilityFactory, Array.Empty<IAbility>());

                BuffManager = new BuffManager(NullLogger<BuffManager>.Instance, AbilityManager);
                BuffFactory = new BuffFactory(NullLogger<BuffFactory>.Instance);
                var stackingEngine = new BuffStackingEngine(NullLogger<BuffStackingEngine>.Instance);
                BuffService = new BuffService(
                    BuffManager,
                    new BuffEvaluator(_conditionEvaluator.Object, NullLogger<BuffEvaluator>.Instance),
                    BuffFactory,
                    NullLogger<BuffService>.Instance,
                    stackingEngine);

                GameState.AbilityManager = AbilityManager;
                GameState.BuffManager = BuffManager;

                var services = new ServiceCollection();
                services.AddLogging();
                services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
                services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
                services.AddSingleton(valueSourceResolver);
                services.AddSingleton<IDslCompiler>(dslCompiler);
                services.AddSingleton<IQueryExecutor>(queryExecutor);
                services.AddSingleton<IBuffService>(BuffService);
                services.AddSingleton<ITargetProcessingService>(new TargetProcessingService(NullLogger<TargetProcessingService>.Instance));
                services.AddSingleton<ICardFactory>(Mock.Of<ICardFactory>());
                services.AddSingleton<SummonEffectHandler>();
                services.AddSingleton<IEffectHandlerRegistry>(_ => new EffectHandlerRegistry(NullLogger<EffectHandlerRegistry>.Instance));
                services.AddSingleton<IEffectExecutor>(sp => new EffectExecutor(
                    sp,
                    sp.GetRequiredService<IValueSourceResolver>(),
                    NullLogger<EffectExecutor>.Instance,
                    sp.GetRequiredService<IEffectHandlerRegistry>()));
                services.AddSingleton<IValueSourceResolver>(valueSourceResolver);
                services.AddSingleton(_mapper.Object);

                ServiceProvider = services.BuildServiceProvider();

                GameServiceProvider.ContextAccessor = ServiceProvider.GetRequiredService<IGameContextAccessor>();
                GameServiceProvider.Logger = NullLogger.Instance;

                SetupGameDataProvider();
            }

            public static TestEnvironment Create()
            {
                return new TestEnvironment();
            }

            public GameContext CreateContext(CardInstance source, IGameEvent? triggeringEvent = null)
            {
                return new GameContext(
                    ServiceProvider,
                    _mapper.Object,
                    ServiceProvider.GetRequiredService<IGameContextAccessor>(),
                    GameState,
                    PlayerId,
                    default,
                    triggeringEvent,
                    source);
            }

            public CardInstance CreateCard(int definitionId, Guid ownerId)
            {
                var instance = new CardInstance(Guid.NewGuid(), definitionId, ownerId)
                {
                    CurrentZone = CardZone.Field
                };
                GameState.RegisterCardInstance(instance);
                GameState.GetPlayerState(ownerId)?.Field.Add(instance);
                return instance;
            }

            private void SetupGameDataProvider()
            {
                _gameDataProvider.Setup(provider => provider.GetCardDefinition(It.IsAny<int>()))
                    .Returns((int id) => new CardDefinition
                    {
                        Id = id,
                        Attack = 1,
                        Health = 1,
                        Cost = 1,
                        Type = CardType.Follower,
                        Keywords = new List<string>()
                    });

                var field = typeof(GameDataProvider).GetField("<Instance>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                field?.SetValue(null, _gameDataProvider.Object);
            }
        }

        private sealed class NullPendingActionService : IPendingActionService
        {
            public Task<PlayerChoiceResult> WaitForPlayerChoiceAsync(string gameId, PlayerChoiceRequest request, System.Threading.CancellationToken cancellationToken)
                => throw new InvalidOperationException("Player choice should not be required in this test.");

            public void CompletePlayerChoice(Guid choiceId, Dtos.SignalR.Actions.SubmitTargetChoiceActionDto dto)
                => throw new NotImplementedException();

            public PendingChoiceContext? GetPendingChoiceForPlayer(string gameId, Guid playerId)
                => null;

            public void ForceResolveChoice(Guid choiceId, PlayerChoiceResult forcedResult)
                => throw new NotImplementedException();
        }

        private sealed class NullConditionFactory : IConditionFactory
        {
            public Condition CreateCondition(ConditionConfig config)
                => throw new InvalidOperationException("Condition factory should not be used in this test.");
        }
    }
}
