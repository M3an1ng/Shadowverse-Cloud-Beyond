using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Hubs;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class GameServiceAlternatePlayPlayabilityTests
    {
        [Fact]
        public async Task GetCardPlayabilityAsync_Accelerate_EvaluatesReplacementPreconditions()
        {
            var harness = CreateHarness(
                "alternate-playability-precondition",
                PlayMode.Accelerate,
                CardType.Spell,
                replacementAbilityConfig: new AbilityConfig
                {
                    Name = "replacement-precondition",
                    Trigger = TriggerConfig.OnEventInAllZones(EventType.PlayCard),
                    PreConditions = new List<ConditionConfig>
                    {
                        ConditionConfig.FromExpression("replacement_precondition")
                    }
                },
                conditionResult: false);

            var result = await harness.GameService.GetCardPlayabilityAsync(
                harness.GameState.GameId,
                harness.Player.PlayerId,
                harness.SourceCard.InstanceId,
                CancellationToken.None);

            Assert.False(result.IsPlayable);
            Assert.Contains("replacement_precondition", result.FailureReason);
        }

        [Fact]
        public async Task GetCardPlayabilityAsync_Accelerate_TargetAvailabilityUsesReplacementAbilities()
        {
            var harness = CreateHarness(
                "alternate-playability-targets",
                PlayMode.Accelerate,
                CardType.Spell,
                replacementAbilityConfig: CreateReplacementTargetAbilityConfig(),
                targetQueryHasResult: true);

            var result = await harness.GameService.GetCardPlayabilityAsync(
                harness.GameState.GameId,
                harness.Player.PlayerId,
                harness.SourceCard.InstanceId,
                CancellationToken.None);

            Assert.True(result.IsPlayable, result.FailureReason);
            Assert.True(result.RequiresTargetSelection);
            Assert.True(result.HasValidTargets);
            Assert.Contains(harness.TargetCard.InstanceId, result.ValidTargetIds);
        }

        [Fact]
        public async Task GetCardPlayabilityAsync_AccelerateSpell_AllowsFullField()
        {
            var harness = CreateHarness(
                "alternate-playability-full-field-spell",
                PlayMode.Accelerate,
                CardType.Spell,
                replacementAbilityConfig: null,
                fillPlayerField: true);

            var result = await harness.GameService.GetCardPlayabilityAsync(
                harness.GameState.GameId,
                harness.Player.PlayerId,
                harness.SourceCard.InstanceId,
                CancellationToken.None);

            Assert.True(result.IsPlayable, result.FailureReason);
            Assert.Equal(PlayMode.Accelerate, result.SelectedPlayMode);
        }

        [Fact]
        public async Task GetCardPlayabilityAsync_CrystalizeAmulet_BlocksFullField()
        {
            var harness = CreateHarness(
                "alternate-playability-full-field-amulet",
                PlayMode.Crystalize,
                CardType.Amulet,
                replacementAbilityConfig: null,
                fillPlayerField: true);

            var result = await harness.GameService.GetCardPlayabilityAsync(
                harness.GameState.GameId,
                harness.Player.PlayerId,
                harness.SourceCard.InstanceId,
                CancellationToken.None);

            Assert.False(result.IsPlayable);
            Assert.Contains("场地已满", result.FailureReason);
            Assert.Equal(PlayMode.Crystalize, result.SelectedPlayMode);
        }

        private static AlternatePlayabilityHarness CreateHarness(
            string gameId,
            PlayMode alternateMode,
            CardType replacementType,
            AbilityConfig? replacementAbilityConfig,
            bool conditionResult = true,
            bool targetQueryHasResult = false,
            bool fillPlayerField = false)
        {
            const int originalDefinitionId = 93001;
            const int replacementDefinitionId = 93002;
            const int targetDefinitionId = 93003;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IValueSourceResolver, NullValueSourceResolver>();

            var buffService = CreateBuffService();
            services.AddSingleton<IBuffService>(buffService.Object);

            var conditionEvaluator = new Mock<IConditionEvaluator>();
            conditionEvaluator
                .Setup(e => e.Evaluate(It.IsAny<ConditionConfig>(), It.IsAny<GameContext>()))
                .Returns(conditionResult);
            services.AddSingleton(conditionEvaluator.Object);

            var queryExecutor = new Mock<IQueryExecutor>();
            services.AddSingleton(queryExecutor.Object);
            var pendingActionService = new Mock<IPendingActionService>();
            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                queryExecutor.Object,
                pendingActionService.Object);
            services.AddSingleton(targetSelectionService);
            var targetFactory = new TargetFactory(targetSelectionService, NullLogger<Target>.Instance);
            services.AddSingleton<IAbilityFactory>(new TestAbilityFactory(new NullConditionFactory(), targetFactory, targetSelectionService));

            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid())
            {
                MaxPlayPoints = 2,
                CurrentPlayPoints = 2
            };
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState(gameId, player, opponent, player.PlayerId)
            {
                BuffManager = new Mock<IBuffManager>().Object
            };

            var abilityManager = new Mock<IAbilityManager>();
            gameState.AbilityManager = abilityManager.Object;

            var originalDefinition = new CardDefinition
            {
                Id = originalDefinitionId,
                Name = "Original Alternate Card",
                Type = CardType.Follower,
                Cost = 5,
                Attack = 3,
                Health = 3
            };
            var replacementDefinition = new CardDefinition
            {
                Id = replacementDefinitionId,
                Name = "Replacement Alternate Card",
                Type = replacementType,
                Cost = 2
            };
            var targetDefinition = new CardDefinition
            {
                Id = targetDefinitionId,
                Name = "Target Follower",
                Type = CardType.Follower,
                Cost = 1,
                Attack = 1,
                Health = 1
            };

            var dataProvider = new Mock<IGameDataProvider>();
            dataProvider.Setup(p => p.GetCardDefinition(originalDefinitionId)).Returns(originalDefinition);
            dataProvider.Setup(p => p.GetCardDefinition(replacementDefinitionId)).Returns(replacementDefinition);
            dataProvider.Setup(p => p.GetCardDefinition(targetDefinitionId)).Returns(targetDefinition);
            dataProvider.Setup(p => p.GetAbilityConfigs(originalDefinitionId)).Returns(new List<AbilityConfig>());
            dataProvider.Setup(p => p.GetAbilityConfigs(replacementDefinitionId))
                .Returns(replacementAbilityConfig == null
                    ? new List<AbilityConfig>()
                    : new List<AbilityConfig> { replacementAbilityConfig });
            GameDataProvider.SetInstanceForTesting(dataProvider.Object);

            var sourceCard = new CardInstance(Guid.NewGuid(), originalDefinitionId, player.PlayerId)
            {
                CurrentZone = CardZone.Hand
            };
            player.Hand.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var targetCard = new CardInstance(Guid.NewGuid(), targetDefinitionId, opponent.PlayerId)
            {
                CurrentZone = CardZone.Field
            };
            opponent.Field.Add(targetCard);
            gameState.RegisterCardInstance(targetCard);

            if (fillPlayerField)
            {
                for (var i = 0; i < GameConstants.Field.MaxFieldSize; i++)
                {
                    var fieldCard = new CardInstance(Guid.NewGuid(), targetDefinitionId, player.PlayerId)
                    {
                        CurrentZone = CardZone.Field
                    };
                    player.Field.Add(fieldCard);
                    gameState.RegisterCardInstance(fieldCard);
                }
            }

            queryExecutor
                .Setup(q => q.ExecuteQuery(
                    It.IsAny<QueryConfig>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Dictionary<string, List<TargetInfo>>?>()))
                .Returns(new QueryResult
                {
                    Success = true,
                    Results = targetQueryHasResult
                        ? new List<object> { targetCard }
                        : new List<object>()
                });

            var declarationAbility = new Ability(
                new AbilityConfig
                {
                    Name = "alternate-declaration",
                    AbilityKeywords = new List<AbilityKeywordConfig>
                    {
                        new()
                        {
                            Keyword = alternateMode == PlayMode.Accelerate
                                ? AbilityKeyword.Accelerate
                                : AbilityKeyword.Crystalize,
                            Value = 2
                        }
                    },
                    AlternatePlay = new AlternatePlayConfig
                    {
                        PlayAsCardDefinitionId = replacementDefinitionId
                    }
                },
                new NullConditionFactory(),
                new NullTargetFactory(),
                targetSelectionService,
                sourceCard);

            abilityManager
                .Setup(m => m.GetEffectiveAbilitiesForCard(sourceCard.InstanceId, It.IsAny<GameContext>()))
                .Returns(new List<IAbility> { declarationAbility });

            var gameService = new GameService(
                Mock.Of<IHubContext<GameHub>>(),
                NullLogger<GameService>.Instance,
                Mock.Of<IMapper>(),
                Mock.Of<IClientMethodMapper>(),
                dataProvider.Object,
                Mock.Of<IMatchService>(),
                Mock.Of<IFirstPlayerSelector>(),
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                pendingActionService.Object);
            AddGameState(gameService, gameState);

            return new AlternatePlayabilityHarness(gameService, gameState, player, sourceCard, targetCard);
        }

        private static AbilityConfig CreateReplacementTargetAbilityConfig()
            => new()
            {
                Name = "replacement-target",
                Trigger = TriggerConfig.OnEventInAllZones(EventType.PlayCard),
                Targets = new Dictionary<string, TargetConfig>
                {
                    ["target"] = new()
                    {
                        Query = new QueryConfig { SourcePoolExpression = "stub" },
                        RequireValidTarget = true,
                        PlayerChoiceOptions = new PlayerChoiceConfig
                        {
                            IsRequired = true,
                            MinCount = 1,
                            MaxCount = 1
                        }
                    }
                },
                Effects = new List<EffectConfig>
                {
                    new() { Type = EffectType.Destroy }
                }
            };

        private static Mock<IBuffService> CreateBuffService()
        {
            var buffService = new Mock<IBuffService>();
            buffService.Setup(s => s.GetEffectiveCost(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => card.BaseCost);
            buffService.Setup(s => s.GetEffectiveAbilityKeywordValue(
                    It.IsAny<CardInstance>(),
                    It.IsAny<AbilityKeyword>(),
                    It.IsAny<int>(),
                    It.IsAny<GameContext>()))
                .Returns((CardInstance _, AbilityKeyword _, int baseValue, GameContext _) => baseValue);
            buffService.Setup(s => s.GetEffectiveMaxHealth(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => card.BaseHealth);
            buffService.Setup(s => s.GetEffectiveAttack(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => card.BaseAttack);
            buffService.Setup(s => s.GetEffectiveKeywords(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(new List<CardKeyword>());
            return buffService;
        }

        private static void AddGameState(GameService gameService, GameState gameState)
        {
            var field = typeof(GameService).GetField("_gameStates", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("GameService game state store field not found.");
            var states = (ConcurrentDictionary<string, GameState>)field.GetValue(gameService)!;
            states[gameState.GameId] = gameState;
        }

        private sealed record AlternatePlayabilityHarness(
            GameService GameService,
            GameState GameState,
            PlayerState Player,
            CardInstance SourceCard,
            CardInstance TargetCard);

        private sealed class NullConditionFactory : IConditionFactory
        {
            public Condition CreateCondition(ConditionConfig config)
                => throw new NotImplementedException("Conditions are not instantiated in these tests.");
        }

        private sealed class NullTargetFactory : ITargetFactory
        {
            public Target Create(string key, TargetConfig config)
                => throw new NotImplementedException("Targets are not used by declaration abilities in these tests.");
        }

        private sealed class NullValueSourceResolver : IValueSourceResolver
        {
            public object? Resolve(ValueConfig valueConfig, GameContext context)
                => null;

            public object? ResolveFormulaValue(string formula, GameContext context)
                => null;

            public object? ResolveFormulaValueWithTrace(string formula, GameContext context, IDslTraceCollector traceCollector)
                => null;
        }

        private sealed class TestAbilityFactory : IAbilityFactory
        {
            private readonly IConditionFactory _conditionFactory;
            private readonly ITargetFactory _targetFactory;
            private readonly TargetSelectionService _targetSelectionService;

            public TestAbilityFactory(
                IConditionFactory conditionFactory,
                ITargetFactory targetFactory,
                TargetSelectionService targetSelectionService)
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
                => abilityConfigs.Select(config => CreateAbility(config, cardInstance, sourceBuffId)).ToList();
        }
    }
}
