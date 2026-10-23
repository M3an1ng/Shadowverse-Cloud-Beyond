using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Constants;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Buffs;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ShadowverseBeyond.Server.Options;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Dtos.Replay;
using System.Text.Json;

namespace ShadowverseBeyond.Server.Tests
{
    public class TransactionServiceIntegrationTests
    {
        private readonly Mock<ILogger<GameActionTransactionService>> _mockLogger;
        private readonly Mock<IAbilityManager> _mockAbilityManager;
        private readonly Mock<IEventProcessor> _mockEventProcessor;
        private readonly Mock<IPendingActionService> _mockPendingActionService;
        private readonly Mock<IEffectExecutor> _mockEffectExecutor;
        private readonly Mock<IClientMethodMapper> _mockClientMethodMapper;
        private readonly Mock<ICommittedEventJournal> _mockEventJournal;
        private readonly Mock<ISnapshotStore> _mockSnapshotStore;
        private readonly Mock<IMetaProgressionService> _mockMetaProgressionService;
        private readonly Mock<IWinrateStatsService> _mockWinrateStatsService;
        private readonly IOptions<ReplayOptions> _replayOptions;
        private readonly GameActionTransactionService _transactionService;

        public TransactionServiceIntegrationTests()
        {
            _mockLogger = new Mock<ILogger<GameActionTransactionService>>();
            _mockAbilityManager = new Mock<IAbilityManager>();
            _mockEventProcessor = new Mock<IEventProcessor>();
            _mockPendingActionService = new Mock<IPendingActionService>();
            _mockEffectExecutor = new Mock<IEffectExecutor>();
            _mockClientMethodMapper = new Mock<IClientMethodMapper>();
            _mockEventJournal = new Mock<ICommittedEventJournal>();
            _mockSnapshotStore = new Mock<ISnapshotStore>();
            _mockMetaProgressionService = new Mock<IMetaProgressionService>();
            _mockWinrateStatsService = new Mock<IWinrateStatsService>();
            _replayOptions = Microsoft.Extensions.Options.Options.Create(new ReplayOptions());
            var recordedMapper = new JsonRecordedEventMapper();
            var rawEventMapper = new JsonGameEventRawEventMapper();
            _mockEventJournal
                .Setup(j => j.AppendBundleAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<IReadOnlyList<ShadowverseBeyond.Server.Dtos.Replay.CommittedEventWriteDto>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ShadowverseBeyond.Server.Dtos.Replay.CommittedBundleAppendResult(1, 1, 1));
            _mockEventProcessor
                .Setup(p => p.ProcessEventAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>()))
                .ReturnsAsync(new ProcessorResult());
            _mockSnapshotStore
                .Setup(s => s.SaveSnapshotAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<ShadowverseBeyond.Server.Dtos.GameStateDto>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _transactionService = new GameActionTransactionService(
                _mockLogger.Object,
                _mockAbilityManager.Object,
                _mockEventProcessor.Object,
                _mockPendingActionService.Object,
                _mockEffectExecutor.Object,
                _mockClientMethodMapper.Object,
                _mockEventJournal.Object,
                _mockSnapshotStore.Object,
                recordedMapper,
                rawEventMapper,
                _mockMetaProgressionService.Object,
                _mockWinrateStatsService.Object,
                _replayOptions
            );
        }

        [Fact]
        public async Task ProcessTransactionAsync_AccelerateReplacementSpellRequiredTarget_UsesReplacementPreflightAbility()
        {
            const int originalDefinitionId = 91001;
            const int replacementDefinitionId = 91002;

            var harness = CreateAlternatePlayPreflightHarness(
                "alternate-preflight-target",
                originalDefinitionId,
                replacementDefinitionId,
                cancelChoice: false);

            var request = new PlayCardRequestedEvent(
                harness.Player.PlayerId,
                new PlayCardActionDto
                {
                    CardUid = harness.SourceCard.InstanceId,
                    PlayMode = PlayMode.Accelerate
                });

            var result = await _transactionService.ProcessTransactionAsync(
                harness.GameContext,
                new List<IGameEvent> { request });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.False(result.IsCancelled);
            Assert.NotNull(harness.CapturedChoiceRequest);
            Assert.Contains(harness.TargetCard.InstanceId, harness.CapturedChoiceRequest!.SelectableTargetIds);
            Assert.True(harness.ProcessedReplacementAbility);
        }

        [Fact]
        public async Task ProcessTransactionAsync_AccelerateReplacementSpellTargetCancelled_LeavesOriginalCardInHandAndDoesNotSpendPp()
        {
            const int originalDefinitionId = 91011;
            const int replacementDefinitionId = 91012;

            var harness = CreateAlternatePlayPreflightHarness(
                "alternate-preflight-cancel",
                originalDefinitionId,
                replacementDefinitionId,
                cancelChoice: true);

            var startingPp = harness.Player.CurrentPlayPoints;
            var request = new PlayCardRequestedEvent(
                harness.Player.PlayerId,
                new PlayCardActionDto
                {
                    CardUid = harness.SourceCard.InstanceId,
                    PlayMode = PlayMode.Accelerate
                });

            var result = await _transactionService.ProcessTransactionAsync(
                harness.GameContext,
                new List<IGameEvent> { request });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.True(result.IsCancelled);
            Assert.Equal(startingPp, harness.Player.CurrentPlayPoints);
            Assert.Contains(harness.SourceCard, harness.Player.Hand);
            Assert.Equal(originalDefinitionId, harness.SourceCard.CurrentDefinitionId);
            Assert.Equal(CardZone.Hand, harness.SourceCard.CurrentZone);
            Assert.True(harness.ProcessedReplacementAbility);
            var choiceCountAfterCancel = harness.ChoiceRequestCount;

            harness.Player.CurrentPlayPoints = 5;
            var baseRequest = new PlayCardRequestedEvent(
                harness.Player.PlayerId,
                new PlayCardActionDto
                {
                    CardUid = harness.SourceCard.InstanceId,
                    PlayMode = PlayMode.Base
                });

            var baseResult = await _transactionService.ProcessTransactionAsync(
                harness.GameContext,
                new List<IGameEvent> { baseRequest });

            Assert.True(baseResult.IsSuccess, baseResult.ErrorMessage);
            Assert.False(baseResult.IsCancelled);
            Assert.Equal(choiceCountAfterCancel, harness.ChoiceRequestCount);
        }

        private AlternatePlayPreflightHarness CreateAlternatePlayPreflightHarness(
            string gameId,
            int originalDefinitionId,
            int replacementDefinitionId,
            bool cancelChoice)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            var buffService = CreateDefaultBuffService();
            buffService.Setup(s => s.GetEffectiveCost(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => card.BaseCost);
            buffService.Setup(s => s.GetEffectiveAbilityKeywordValue(
                    It.IsAny<CardInstance>(),
                    It.IsAny<AbilityKeyword>(),
                    It.IsAny<int>(),
                    It.IsAny<GameContext>()))
                .Returns((CardInstance _, AbilityKeyword _, int baseValue, GameContext _) => baseValue);
            buffService.Setup(s => s.RemoveAllBuffs(
                    It.IsAny<Guid>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Func<Buff, bool>?>()))
                .Returns(new DomainOperationResult { Success = true });
            services.AddSingleton<IBuffService>(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IValueSourceResolver, NullValueSourceResolver>();

            var queryExecutorMock = new Mock<IQueryExecutor>();
            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                queryExecutorMock.Object,
                _mockPendingActionService.Object);
            services.AddSingleton<IQueryExecutor>(queryExecutorMock.Object);
            var targetFactory = new TargetFactory(targetSelectionService, NullLogger<Target>.Instance);
            var abilityFactory = new TestAbilityFactory(new NullConditionFactory(), targetFactory, targetSelectionService);

            var originalDefinition = new CardDefinition
            {
                Id = originalDefinitionId,
                Name = "Original Alternate Follower",
                Type = CardType.Follower,
                Cost = 5,
                Attack = 3,
                Health = 3
            };
            var replacementDefinition = new CardDefinition
            {
                Id = replacementDefinitionId,
                Name = "Replacement Accelerate Spell",
                Type = CardType.Spell,
                Cost = 2
            };
            var dataProvider = new Mock<IGameDataProvider>();
            dataProvider.Setup(p => p.GetCardDefinition(originalDefinitionId)).Returns(originalDefinition);
            dataProvider.Setup(p => p.GetCardDefinition(replacementDefinitionId)).Returns(replacementDefinition);
            dataProvider.Setup(p => p.GetAbilityConfigs(originalDefinitionId)).Returns(new List<AbilityConfig>());

            var replacementAbilityConfig = new AbilityConfig
            {
                Name = "accelerate-replacement-target",
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
            dataProvider.Setup(p => p.GetAbilityConfigs(replacementDefinitionId))
                .Returns(new List<AbilityConfig> { replacementAbilityConfig });
            services.AddSingleton(dataProvider.Object);
            services.AddSingleton<IAbilityFactory>(abilityFactory);
            var serviceProvider = services.BuildServiceProvider();
            GameDataProvider.SetInstanceForTesting(dataProvider.Object);

            var player = new PlayerState(Guid.NewGuid())
            {
                MaxPlayPoints = 2,
                CurrentPlayPoints = 2
            };
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState(gameId, player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), originalDefinitionId, player.PlayerId)
            {
                CurrentZone = CardZone.Hand
            };
            player.Hand.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var targetCard = new CardInstance(Guid.NewGuid(), 92001, opponent.PlayerId)
            {
                CurrentZone = CardZone.Field
            };
            opponent.Field.Add(targetCard);
            gameState.RegisterCardInstance(targetCard);
            dataProvider.Setup(p => p.GetCardDefinition(92001)).Returns(new CardDefinition
            {
                Id = 92001,
                Name = "Target Follower",
                Type = CardType.Follower,
                Cost = 1,
                Attack = 1,
                Health = 3
            });

            var declarationAbilityConfig = new AbilityConfig
            {
                Name = "accelerate-declaration",
                AbilityKeywords = new List<AbilityKeywordConfig>
                {
                    new() { Keyword = AbilityKeyword.Accelerate, Value = 2 }
                },
                AlternatePlay = new AlternatePlayConfig
                {
                    PlayAsCardDefinitionId = replacementDefinitionId
                }
            };
            var declarationAbility = new Ability(
                declarationAbilityConfig,
                new NullConditionFactory(),
                new NullTargetFactory(),
                targetSelectionService,
                sourceCard);

            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var gameContext = new GameContext(
                serviceProvider,
                Mock.Of<IMapper>(),
                accessor,
                gameState,
                player.PlayerId,
                default,
                sourceCard: sourceCard);
            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            queryExecutorMock
                .Setup(q => q.ExecuteQuery(
                    It.IsAny<QueryConfig>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Dictionary<string, List<TargetInfo>>?>()))
                .Returns(new QueryResult
                {
                    Success = true,
                    Results = new List<object> { targetCard }
                });

            PlayerChoiceRequest? capturedChoiceRequest = null;
            var choiceRequestCount = 0;
            _mockPendingActionService
                .Setup(s => s.WaitForPlayerChoiceAsync(It.IsAny<string>(), It.IsAny<PlayerChoiceRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, PlayerChoiceRequest choiceRequest, CancellationToken __) =>
                {
                    choiceRequestCount++;
                    capturedChoiceRequest = choiceRequest;
                    return new PlayerChoiceResult
                    {
                        WasCancelled = cancelChoice,
                        ChosenTargetIds = cancelChoice
                            ? new List<Guid>()
                            : new List<Guid> { targetCard.InstanceId }
                    };
                });

            var processedReplacementAbility = false;
            _mockAbilityManager
                .Setup(m => m.GetEffectiveAbilitiesForCard(sourceCard.InstanceId, It.IsAny<GameContext>()))
                .Returns(new List<IAbility> { declarationAbility });
            _mockAbilityManager
                .Setup(m => m.GetAbilitiesForCard(sourceCard.InstanceId))
                .Returns(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.CreateAbilitiesForCard(
                    sourceCard,
                    It.IsAny<List<AbilityConfig>>(),
                    null,
                    gameState))
                .Returns(new List<Ability>());
            _mockAbilityManager
                .Setup(m => m.GetAbilityByRuntimeId(declarationAbility.RuntimeId))
                .Returns(declarationAbility);
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(
                    It.Is<IAbility>(ability => ability.Name == "accelerate-replacement-target"),
                    It.IsAny<GameContext>(),
                    true,
                    false,
                    false))
                .Returns<IAbility, GameContext, bool, bool, bool>(async (ability, context, _, _, _) =>
                {
                    processedReplacementAbility = true;
                    var concreteAbility = Assert.IsType<Ability>(ability);
                    var targetResult = await concreteAbility.ResolveTargetsAsync(context, true);
                    if (targetResult.WasCancelled)
                    {
                        return AbilityProcessResult.Cancelled("cancelled");
                    }

                    if (targetResult.Status != ResolutionStatus.Completed)
                    {
                        return AbilityProcessResult.Failure(targetResult.ErrorMessage ?? "target resolution failed");
                    }

                    var effects = await concreteAbility.CreateEffectsAsync(context, targetResult);
                    return AbilityProcessResult.Success(effects);
                });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(
                    It.Is<IAbility>(ability => ability.Name == "accelerate-replacement-target"),
                    It.IsAny<GameContext>(),
                    false,
                    It.IsAny<bool>(),
                    It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>()));
            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult { Success = true });

            return new AlternatePlayPreflightHarness(
                player,
                sourceCard,
                targetCard,
                gameContext,
                () => capturedChoiceRequest,
                () => choiceRequestCount,
                () => processedReplacementAbility);
        }

        private sealed class AlternatePlayPreflightHarness
        {
            private readonly Func<PlayerChoiceRequest?> _getCapturedChoiceRequest;
            private readonly Func<bool> _getProcessedReplacementAbility;

            public AlternatePlayPreflightHarness(
                PlayerState player,
                CardInstance sourceCard,
                CardInstance targetCard,
                GameContext gameContext,
                Func<PlayerChoiceRequest?> getCapturedChoiceRequest,
                Func<int> getChoiceRequestCount,
                Func<bool> getProcessedReplacementAbility)
            {
                Player = player;
                SourceCard = sourceCard;
                TargetCard = targetCard;
                GameContext = gameContext;
                _getCapturedChoiceRequest = getCapturedChoiceRequest;
                _getChoiceRequestCount = getChoiceRequestCount;
                _getProcessedReplacementAbility = getProcessedReplacementAbility;
            }

            public PlayerState Player { get; }
            public CardInstance SourceCard { get; }
            public CardInstance TargetCard { get; }
            public GameContext GameContext { get; }
            public PlayerChoiceRequest? CapturedChoiceRequest => _getCapturedChoiceRequest();
            public int ChoiceRequestCount => _getChoiceRequestCount();
            public bool ProcessedReplacementAbility => _getProcessedReplacementAbility();
            private readonly Func<int> _getChoiceRequestCount;
        }

        private static Mock<IBuffService> CreateDefaultBuffService()
        {
            var buffService = new Mock<IBuffService>();
            buffService.Setup(s => s.GetEffectiveActionUsageLimit(It.IsAny<CardInstance>(), It.IsAny<CardActionUsageType>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, CardActionUsageType actionType, GameContext _) => card.GetBaseActionUsageLimit(actionType));
            buffService.Setup(s => s.GetEffectiveMaxHealth(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(10);
            buffService.Setup(s => s.GetEffectiveAttack(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(1);
            buffService.Setup(s => s.GetEffectiveCost(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(0);
            buffService.Setup(s => s.GetEffectiveKeywords(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(new List<CardKeyword>());
            return buffService;
        }

        private sealed class ImmediateTestEvent : GameEventBase, ICommittableEvent
        {
            public ImmediateTestEvent()
                : base(EventType.PlayCard)
            {
            }

            public DomainOperationResult Commit(GameContext context)
            {
                return new DomainOperationResult { Success = true };
            }
        }

        private sealed class ImmediatePlayerActionTestEvent : GameEventBase, ICommittableEvent, IPlayerActionEvent
        {
            public ImmediatePlayerActionTestEvent(CardInstance? sourceCard = null)
                : base(EventType.PlayCard)
            {
                if (sourceCard != null)
                {
                    Source = TargetInfo.ForCard(sourceCard);
                }
            }

            public DomainOperationResult Commit(GameContext context)
            {
                return new DomainOperationResult { Success = true };
            }
        }

        private sealed class QueueDrainedTestEvent : DelayedEventBase, ICommittableEvent
        {
            public QueueDrainedTestEvent()
                : base(EventType.Draw)
            {
                ConfigureDelay(DelayConfig.QueueDrained());
            }

            public DomainOperationResult Commit(GameContext context)
            {
                return new DomainOperationResult { Success = true };
            }
        }

        private sealed class EffectScopedDelayTestEvent : DelayedEventBase, ICommittableEvent
        {
            public EffectScopedDelayTestEvent(Guid parentEffectId)
                : base(EventType.Destroy)
            {
                ConfigureDelay(DelayConfig.Parent(parentEffectId));
            }

            public DomainOperationResult Commit(GameContext context)
            {
                return new DomainOperationResult { Success = true };
            }
        }

        private sealed class ImmediateDrawTestEvent : GameEventBase, ICommittableEvent
        {
            public ImmediateDrawTestEvent()
                : base(EventType.Draw)
            {
            }

            public DomainOperationResult Commit(GameContext context)
            {
                return new DomainOperationResult { Success = true };
            }
        }

        private sealed class CommitFlagEvent : GameEventBase, ICommittableEvent
        {
            private readonly Action _onCommit;

            public CommitFlagEvent(Action onCommit)
                : base(EventType.Draw)
            {
                _onCommit = onCommit ?? throw new ArgumentNullException(nameof(onCommit));
            }

            public DomainOperationResult Commit(GameContext context)
            {
                _onCommit();
                return new DomainOperationResult { Success = true };
            }
        }

        private sealed class CommitFailureEvent : GameEventBase, ICommittableEvent
        {
            public CommitFailureEvent()
                : base(EventType.Draw)
            {
            }

            public DomainOperationResult Commit(GameContext context)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = "Commit failed."
                };
            }
        }

        private sealed class CommitThrowsEvent : GameEventBase, ICommittableEvent
        {
            public CommitThrowsEvent()
                : base(EventType.Draw)
            {
            }

            public DomainOperationResult Commit(GameContext context)
            {
                throw new InvalidOperationException("Commit crash for test.");
            }
        }

        private sealed class CommitGeneratesEvent : GameEventBase, ICommittableEvent
        {
            private readonly IGameEvent _generatedEvent;

            public CommitGeneratesEvent(IGameEvent generatedEvent)
                : base(EventType.Draw)
            {
                _generatedEvent = generatedEvent ?? throw new ArgumentNullException(nameof(generatedEvent));
            }

            public DomainOperationResult Commit(GameContext context)
            {
                return new DomainOperationResult
                {
                    Success = true,
                    GeneratedEvents = new List<IGameEvent> { _generatedEvent }
                };
            }
        }

        [Fact]
        public async Task ProcessTransactionAsync_HandlesComplexTriggerChain_Correctly()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(new Mock<IBuffService>().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player, opponent, player.PlayerId);
            gameState.AbilityManager = _mockAbilityManager.Object;
            gameState.BuffManager = new Mock<IBuffManager>().Object;

            var attackerCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId);
            var targetCard = new CardInstance(Guid.NewGuid(), 2, opponent.PlayerId);

            var mockInitialEvent = new Mock<PlayCardEvent>(player, attackerCard, PlayMode.Base, 0);
            var mockDamageEvent = new Mock<DamageEvent>(targetCard, 1, attackerCard);
            var mockModifiedDamageEvent = new Mock<DamageEvent>(targetCard, 2, attackerCard);

            var mapper = new Mock<IMapper>();
            var gameContext = new GameContext(serviceProvider, mapper.Object, serviceProvider.GetRequiredService<IGameContextAccessor>(), gameState, player.PlayerId, default, mockInitialEvent.Object, attackerCard);
            GameServiceProvider.ContextAccessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            GameServiceProvider.ContextAccessor.GameContext = gameContext;
            GameServiceProvider.Logger = NullLogger.Instance;

            var mockEffectConfig = new Mock<EffectConfig>().Object;
            var mockDamageEffect = new Effect(mockEffectConfig, Guid.NewGuid(), null, null, Guid.NewGuid(), null);
            var mockModifyDamageEffect = new Effect(mockEffectConfig, Guid.NewGuid(), null, null, Guid.NewGuid(), null);
            var mockDrawCardEffect = new Effect(mockEffectConfig, Guid.NewGuid(), null, null, Guid.NewGuid(), null);

            var mockOnPlayAbility = new Mock<IAbility>();
            var mockBeforeDamageAbility = new Mock<IAbility>();
            var mockAfterDamageAbility = new Mock<IAbility>();

            _mockAbilityManager.Setup(m => m.GetTriggeredAbilitiesAsync(mockInitialEvent.Object, gameContext, TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { mockOnPlayAbility.Object });
            _mockAbilityManager.Setup(m => m.ProcessSingleAbilityAsync(mockOnPlayAbility.Object, gameContext, false, false, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { mockDamageEffect }));

            _mockAbilityManager.Setup(m => m.GetTriggeredAbilitiesAsync(mockDamageEvent.Object, gameContext, TriggerTiming.Before))
                .ReturnsAsync(new List<IAbility> { mockBeforeDamageAbility.Object });
            _mockAbilityManager.Setup(m => m.ProcessSingleAbilityAsync(mockBeforeDamageAbility.Object, gameContext, false, false, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { mockModifyDamageEffect }));

            _mockAbilityManager.Setup(m => m.GetTriggeredAbilitiesAsync(mockModifiedDamageEvent.Object, gameContext, TriggerTiming.After))
                .ReturnsAsync(new List<IAbility> { mockAfterDamageAbility.Object });
            // Default empty for other cases
            _mockAbilityManager.Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager.Setup(m => m.ProcessSingleAbilityAsync(mockAfterDamageAbility.Object, gameContext, false, false, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { mockDrawCardEffect }));

            _mockEffectExecutor.Setup(e => e.ExecuteEffect(mockModifyDamageEffect, gameContext))
                .Returns(new GameStateActionResult { Success = true, GeneratedEvents = new List<IGameEvent> { mockModifiedDamageEvent.Object } });
            _mockEffectExecutor.Setup(e => e.ExecuteEffect(mockDamageEffect, gameContext))
                .Returns(new GameStateActionResult { Success = true });
            _mockEffectExecutor.Setup(e => e.ExecuteEffect(mockDrawCardEffect, gameContext))
                .Returns(new GameStateActionResult { Success = true });

            mockInitialEvent.As<ICommittableEvent>().Setup(e => e.Commit(gameContext))
                .Returns(new DomainOperationResult { Success = true, GeneratedEvents = new List<IGameEvent> { mockDamageEvent.Object } });
            mockDamageEvent.As<ICommittableEvent>().Setup(e => e.Commit(gameContext))
                .Returns(new DomainOperationResult { Success = true });
            mockModifiedDamageEvent.As<ICommittableEvent>().Setup(e => e.Commit(gameContext))
                .Returns(new DomainOperationResult { Success = true });

            // Act
            await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { mockInitialEvent.Object });

            // Assert
            // Ensure pipeline completed without throwing
            Assert.True(true);
        }

        [Fact]
        public async Task ProcessTransactionAsync_SkipsQueuedEffect_WhenSourceAbilityBecomesSuppressed()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("suppressed-queued-effect", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var initialEvent = new ImmediateTestEvent();
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            using var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                initialEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.ContextAccessor.GameContext = gameContext;
            GameServiceProvider.Logger = NullLogger.Instance;

            var ability = new Mock<IAbility>();
            var triggerConfig = new TriggerConfig { RevalidateZoneBeforeExecution = false };
            ability.SetupGet(a => a.RuntimeId).Returns(Guid.NewGuid());
            ability.SetupGet(a => a.SourceCard).Returns(sourceCard);
            ability.SetupGet(a => a.SourceCardId).Returns(sourceCard.InstanceId);
            ability.SetupGet(a => a.TriggerConfig).Returns(triggerConfig);
            ability.SetupGet(a => a.KeywordSummary).Returns("SuppressedOnExecution");

            var effect = new Effect(new EffectConfig { Type = EffectType.Draw }, player.PlayerId, null, ability.Object.RuntimeId, initialEvent.EventId, sourceCard.InstanceId)
            {
                SourceAbility = ability.Object
            };

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { ability.Object });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(ability.Object, It.IsAny<GameContext>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { effect }));
            _mockAbilityManager
                .Setup(m => m.IsAbilitySuppressed(ability.Object, It.IsAny<GameContext>()))
                .Returns(true);

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            _mockEffectExecutor.Verify(e => e.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()), Times.Never);
        }

        [Fact]
        public async Task ProcessTransactionAsync_SkipsQueuedEffect_WhenSourceAbilityStateVersionAdvances()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IQueryExecutor>(new Mock<IQueryExecutor>().Object);
            services.AddSingleton<IPendingActionService>(_mockPendingActionService.Object);
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid()) { TurnNumber = 1 };
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("versioned-queued-effect", player, opponent, player.PlayerId)
            {
                Phase = GamePhase.Main,
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var initialEvent = new ImmediateTestEvent();
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            using var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                initialEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.ContextAccessor.GameContext = gameContext;
            GameServiceProvider.Logger = NullLogger.Instance;

            var conditionFactory = new Mock<IConditionFactory>();
            var targetFactory = new Mock<ITargetFactory>();
            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                serviceProvider.GetRequiredService<IQueryExecutor>(),
                _mockPendingActionService.Object);
            var abilityConfig = new AbilityConfig
            {
                Id = 9101,
                Name = "VersionedAbility",
                Trigger = new TriggerConfig
                {
                    EventTypes = new List<EventType> { initialEvent.EventType },
                    Timing = TriggerTiming.On,
                    RevalidateZoneBeforeExecution = false
                }
            };
            var ability = new Ability(abilityConfig, conditionFactory.Object, targetFactory.Object, targetSelectionService, sourceCard);

            var effect = new Effect(new EffectConfig { Type = EffectType.Draw }, player.PlayerId, null, ability.RuntimeId, initialEvent.EventId, sourceCard.InstanceId)
            {
                SourceAbility = ability,
                SourceAbilityStateVersion = ability.QueuedEffectStateVersion
            };

            ability.InvalidateQueuedEffects("remove_ability");

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { ability });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(ability, It.IsAny<GameContext>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { effect }));
            _mockAbilityManager
                .Setup(m => m.GetAbilityByRuntimeId(ability.RuntimeId))
                .Returns(ability);
            _mockAbilityManager
                .Setup(m => m.IsAbilitySuppressed(ability, It.IsAny<GameContext>()))
                .Returns(false);

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            _mockEffectExecutor.Verify(e => e.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()), Times.Never);
        }

        [Fact]
        public async Task ProcessTransactionAsync_SkipsQueuedEffect_WhenSourceAbilityWasUnregisteredWithCancellingTombstone()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IQueryExecutor>(new Mock<IQueryExecutor>().Object);
            services.AddSingleton<IPendingActionService>(_mockPendingActionService.Object);
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid())
            {
                TurnNumber = 1
            };
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player, opponent, player.PlayerId)
            {
                Phase = GamePhase.Main,
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var initialEvent = new ImmediateTestEvent();
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            using var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                initialEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.ContextAccessor.GameContext = gameContext;
            GameServiceProvider.Logger = NullLogger.Instance;

            var abilityRuntimeId = Guid.NewGuid();
            var ability = new Mock<IAbility>();
            var triggerConfig = new TriggerConfig { RevalidateZoneBeforeExecution = false };
            ability.SetupGet(a => a.RuntimeId).Returns(abilityRuntimeId);
            ability.SetupGet(a => a.SourceCard).Returns(sourceCard);
            ability.SetupGet(a => a.SourceCardId).Returns(sourceCard.InstanceId);
            ability.SetupGet(a => a.TriggerConfig).Returns(triggerConfig);
            ability.SetupGet(a => a.KeywordSummary).Returns("SuppressedThenUnregistered");

            var effect = new Effect(new EffectConfig { Type = EffectType.Draw }, player.PlayerId, null, abilityRuntimeId, initialEvent.EventId, sourceCard.InstanceId)
            {
                SourceAbility = ability.Object,
                SourceAbilityStateVersion = 0
            };

            var tombstone = new AbilitySnapshot(
                abilityRuntimeId,
                sourceCard.InstanceId,
                sourceCard.Name,
                "SuppressedThenUnregistered",
                9001,
                null,
                new[] { AbilityKeyword.Lastword },
                "Lastword",
                "suppressed_cleanup",
                true,
                true,
                false,
                1,
                "remove_ability");

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { ability.Object });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(ability.Object, It.IsAny<GameContext>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { effect }));
            _mockAbilityManager
                .Setup(m => m.GetAbilityByRuntimeId(abilityRuntimeId))
                .Returns((IAbility?)null);
            _mockAbilityManager
                .Setup(m => m.TryGetAbilitySnapshot(abilityRuntimeId, out tombstone))
                .Returns(true);

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            _mockEffectExecutor.Verify(e => e.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()), Times.Never);
        }

        [Fact]
        public async Task ProcessTransactionAsync_SkipsRemainingQueuedEffects_WhenSourceCardTransforms()
        {
            const int transformedDefinitionId = 910001;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();

            var mockDataProvider = new Mock<IGameDataProvider>();
            mockDataProvider.SetupGet(p => p.IsInitialized).Returns(true);
            mockDataProvider.Setup(p => p.GetCardDefinition(transformedDefinitionId))
                .Returns(new CardDefinition
                {
                    Id = transformedDefinitionId,
                    Name = "Transformed Source",
                    Type = CardType.Follower,
                    Attack = 2,
                    Health = 2,
                    Cost = 1,
                    Description = "test"
                });
            mockDataProvider.Setup(p => p.GetAbilityConfigs(transformedDefinitionId))
                .Returns(new List<AbilityConfig>());
            services.AddSingleton(mockDataProvider.Object);

            var serviceProvider = services.BuildServiceProvider();
            GameDataProvider.SetInstanceForTesting(mockDataProvider.Object);

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("transform-queued-effect", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId)
            {
                CurrentZone = CardZone.Field,
                ZoneStayTurn = 1
            };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var initialEvent = new ImmediateTestEvent();
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            using var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                initialEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.ContextAccessor.GameContext = gameContext;
            GameServiceProvider.Logger = NullLogger.Instance;

            var conditionFactory = new Mock<IConditionFactory>();
            var targetFactory = new Mock<ITargetFactory>();
            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                new Mock<IQueryExecutor>().Object,
                _mockPendingActionService.Object);
            var abilityConfig = new AbilityConfig
            {
                Id = 9201,
                Name = "TransformingAbility",
                Trigger = new TriggerConfig
                {
                    EventTypes = new List<EventType> { initialEvent.EventType },
                    Timing = TriggerTiming.On,
                    RevalidateZoneBeforeExecution = false
                }
            };
            var ability = new Ability(abilityConfig, conditionFactory.Object, targetFactory.Object, targetSelectionService, sourceCard);

            var transformEffect = new Effect(
                new EffectConfig { Type = EffectType.Transform },
                player.PlayerId,
                null,
                ability.RuntimeId,
                initialEvent.EventId,
                sourceCard.InstanceId)
            {
                SourceAbility = ability,
                SourceAbilityStateVersion = ability.QueuedEffectStateVersion
            };

            var followUpEffect = new Effect(
                new EffectConfig { Type = EffectType.Draw },
                player.PlayerId,
                null,
                ability.RuntimeId,
                initialEvent.EventId,
                sourceCard.InstanceId)
            {
                SourceAbility = ability,
                SourceAbilityStateVersion = ability.QueuedEffectStateVersion
            };

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { ability });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(ability, It.IsAny<GameContext>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { transformEffect, followUpEffect }));
            _mockAbilityManager
                .Setup(m => m.GetAbilitiesForCard(sourceCard.InstanceId))
                .Returns(new List<IAbility> { ability });
            _mockAbilityManager
                .Setup(m => m.GetEffectiveAbilitiesForCard(sourceCard.InstanceId, It.IsAny<GameContext>()))
                .Returns(new List<IAbility> { ability });
            _mockAbilityManager
                .Setup(m => m.UnregisterCardAbilities(sourceCard.InstanceId, It.IsAny<string>()));
            _mockAbilityManager
                .Setup(m => m.CreateAbilitiesForCard(sourceCard, It.IsAny<List<AbilityConfig>>(), null, gameState))
                .Returns(new List<Ability>());

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(transformEffect, It.IsAny<GameContext>()))
                .Returns((Effect _, GameContext context) =>
                {
                    var transformEvent = new TransformEvent(sourceCard, transformedDefinitionId, sourceCard);
                    return new GameStateActionResult
                    {
                        Success = true,
                        GeneratedEvents = new List<IGameEvent> { transformEvent }
                    };
                });
            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(followUpEffect, It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult { Success = true });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Equal(transformedDefinitionId, sourceCard.BaseDefinitionId);
            _mockEffectExecutor.Verify(e => e.ExecuteEffect(followUpEffect, It.IsAny<GameContext>()), Times.Never);
        }

        [Fact]
        public async Task ProcessTransactionAsync_EvolveTriggerWithBlockExecutionOnly_ShouldNotAbortEvolution()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IAbilityManager>(_mockAbilityManager.Object);
            var buffService = CreateDefaultBuffService();
            buffService
                .Setup(s => s.ApplyStatBuff(
                    It.IsAny<GameContext>(),
                    It.IsAny<CardInstance>(),
                    It.IsAny<List<StatModification>>(),
                    It.IsAny<DurationType>(),
                    It.IsAny<int?>(),
                    It.IsAny<string?>(),
                    It.IsAny<bool>(),
                    It.IsAny<ConditionConfig?>()))
                .Returns(new DomainOperationResult { Success = true });
            services.AddSingleton<IBuffService>(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var queryExecutorMock = new Mock<IQueryExecutor>();
            services.AddSingleton(queryExecutorMock.Object);
            services.AddSingleton<IPendingActionService>(_mockPendingActionService.Object);
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid())
            {
                IsEvolutionUnlocked = true,
                CanEvolve = true,
                MaxEvolutionPoints = 1,
                CurrentEvolutionPoints = 1
            };
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("evolve-skip-execution", player, opponent, player.PlayerId)
            {
                CurrentPlayerId = player.PlayerId
            };

            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                queryExecutorMock.Object,
                _mockPendingActionService.Object);
            var abilityManager = new AbilityManager(
                NullLogger<AbilityManager>.Instance,
                new Mock<IAbilityFactory>().Object,
                new List<IAbility>());
            gameState.AbilityManager = abilityManager;
            gameState.BuffManager = new BuffManager(NullLogger<BuffManager>.Instance, abilityManager);

            var evolvingCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            var opponentCard = new CardInstance(Guid.NewGuid(), 2, opponent.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(evolvingCard);
            opponent.Field.Add(opponentCard);
            gameState.RegisterCardInstance(evolvingCard);
            gameState.RegisterCardInstance(opponentCard);

            queryExecutorMock
                .Setup(q => q.ExecuteQuery(
                    It.IsAny<QueryConfig>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Dictionary<string, List<TargetInfo>>?>()))
                .Returns(new QueryResult
                {
                    Success = true,
                    Results = new List<object> { opponentCard }
                });

            var evolveTriggerAbilityConfig = new AbilityConfig
            {
                Name = "evolve-trigger-skip",
                Trigger = new TriggerConfig
                {
                    EventTypes = new List<EventType> { EventType.Evolve },
                    Timing = TriggerTiming.On,
                    RelationType = TriggerRelationType.Any,
                    IsEnabled = true
                },
                Targets = new Dictionary<string, TargetConfig>
                {
                    ["target"] = new TargetConfig
                    {
                        Query = new QueryConfig { SourcePoolExpression = "stub" },
                        TargetHandling = TargetHandlingStrategy.BlockAbilityExecutionOnly,
                        PlayerChoiceOptions = new PlayerChoiceConfig
                        {
                            IsRequired = true,
                            MinCount = 2,
                            MaxCount = 2
                        }
                    }
                },
                Effects = new List<EffectConfig>
                {
                    new() { Type = EffectType.Destroy }
                }
            };
            var evolveTriggerAbility = new Ability(
                evolveTriggerAbilityConfig,
                new NullConditionFactory(),
                new TargetFactory(targetSelectionService, NullLogger<Target>.Instance),
                targetSelectionService,
                evolvingCard);
            abilityManager.RegisterAbility(evolveTriggerAbility);

            var transactionService = new GameActionTransactionService(
                _mockLogger.Object,
                abilityManager,
                _mockEventProcessor.Object,
                _mockPendingActionService.Object,
                _mockEffectExecutor.Object,
                _mockClientMethodMapper.Object,
                _mockEventJournal.Object,
                _mockSnapshotStore.Object,
                new JsonRecordedEventMapper(),
                new JsonGameEventRawEventMapper(),
                _mockMetaProgressionService.Object,
                _mockWinrateStatsService.Object,
                _replayOptions);

            var actionDto = new EvolveActionDto { UnitUid = evolvingCard.InstanceId.ToString() };
            var evolveRequested = new EvolveRequestedEvent(player.PlayerId, actionDto);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            using var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                evolveRequested,
                evolvingCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.ContextAccessor.GameContext = gameContext;
            GameServiceProvider.Logger = NullLogger.Instance;

            var result = await transactionService.ProcessTransactionAsync(gameContext, new[] { evolveRequested });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.True(evolvingCard.IsEvolved);
            Assert.False(evolvingCard.IsSuperEvolved);
            Assert.Contains(opponentCard, opponent.Field);
            _mockEffectExecutor.Verify(e => e.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()), Times.Never);
        }

        [Fact]
        public async Task ProcessTransactionAsync_PersistsCommittedEventHistory_BeforeAfterTriggers()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);

            var historyServiceMock = new Mock<IEventHistoryService>();
            var historyStored = false;
            historyServiceMock
                .Setup(s => s.StoreEventAsync(It.IsAny<string>(), It.IsAny<IGameEvent>()))
                .Callback(() => historyStored = true)
                .Returns(Task.CompletedTask);
            historyServiceMock
                .Setup(s => s.GetEvents(It.IsAny<string>()))
                .Returns(Array.Empty<IGameEvent>());
            historyServiceMock
                .Setup(s => s.GetEventByIdAsync(It.IsAny<string>(), It.IsAny<Guid>()))
                .ReturnsAsync((IGameEvent?)null);
            historyServiceMock
                .Setup(s => s.ClearHistoryForGameAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            services.AddSingleton(historyServiceMock.Object);

            services.AddSingleton<IConditionFactory, NullConditionFactory>();
            services.AddSingleton<ITargetFactory, NullTargetFactory>();
            services.AddSingleton<IQueryExecutor, NullQueryExecutor>();
            services.AddSingleton<IPendingActionService>(_mockPendingActionService.Object);
            services.AddSingleton<TargetSelectionService>();
            services.AddSingleton<IAbilityFactory, TestAbilityFactory>();
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("history-timing", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var gameContext = new GameContext(serviceProvider, mapper.Object, accessor, gameState, player.PlayerId, default, null, sourceCard);
            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.ContextAccessor.GameContext = gameContext;
            GameServiceProvider.Logger = NullLogger.Instance;

            var initialEvent = new ImmediateTestEvent();

            _mockAbilityManager.Setup(m => m.GetTriggeredAbilitiesAsync(
                    It.IsAny<IGameEvent>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());

            _mockAbilityManager.Setup(m => m.GetTriggeredAbilitiesAsync(
                    It.IsAny<IGameEvent>(),
                    It.IsAny<GameContext>(),
                    TriggerTiming.After))
                .Callback(() => Assert.True(historyStored))
                .ReturnsAsync(new List<IAbility>());

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            historyServiceMock.Verify(
                s => s.StoreEventAsync(gameState.GameId, It.Is<IGameEvent>(e => e.EventId == initialEvent.EventId)),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task ProcessTransactionAsync_CapturesCurrentPlayerSnapshot_OnEventAndRawEventPayload()
        {
            IReadOnlyList<CommittedEventWriteDto>? capturedWrites = null;
            _mockEventJournal
                .Setup(j => j.AppendBundleAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<IReadOnlyList<CommittedEventWriteDto>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, Guid, IReadOnlyList<CommittedEventWriteDto>, CancellationToken>((_, _, writes, _) =>
                {
                    capturedWrites = writes;
                })
                .ReturnsAsync(new CommittedBundleAppendResult(1, 1, 1));

            var replayOptions = Microsoft.Extensions.Options.Options.Create(new ReplayOptions
            {
                EnableJournalWrites = true
            });

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IConditionFactory, NullConditionFactory>();
            services.AddSingleton<ITargetFactory, NullTargetFactory>();
            services.AddSingleton<IQueryExecutor, NullQueryExecutor>();
            services.AddSingleton<IPendingActionService>(_mockPendingActionService.Object);
            services.AddSingleton<TargetSelectionService>();
            services.AddSingleton<IAbilityFactory, TestAbilityFactory>();
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid()) { TurnNumber = 5 };
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("snapshot-capture", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object,
                CurrentPlayerId = player.PlayerId
            };

            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var gameContext = new GameContext(serviceProvider, mapper.Object, accessor, gameState, player.PlayerId);
            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.ContextAccessor.GameContext = gameContext;
            GameServiceProvider.Logger = NullLogger.Instance;

            _mockAbilityManager.Setup(m => m.GetTriggeredAbilitiesAsync(
                    It.IsAny<IGameEvent>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());

            var initialEvent = new ImmediateTestEvent();
            var transactionService = new GameActionTransactionService(
                _mockLogger.Object,
                _mockAbilityManager.Object,
                _mockEventProcessor.Object,
                _mockPendingActionService.Object,
                _mockEffectExecutor.Object,
                _mockClientMethodMapper.Object,
                _mockEventJournal.Object,
                _mockSnapshotStore.Object,
                new JsonRecordedEventMapper(),
                new JsonGameEventRawEventMapper(),
                _mockMetaProgressionService.Object,
                _mockWinrateStatsService.Object,
                replayOptions);
            var result = await transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Equal(player.PlayerId, initialEvent.CurrentPlayerId);
            Assert.Equal(5, initialEvent.CurrentPlayerTurnNumber);

            var recordedEvent = Assert.Single(capturedWrites ?? Array.Empty<CommittedEventWriteDto>());
            Assert.False(string.IsNullOrWhiteSpace(recordedEvent.RawEventJson));

            var rawEnvelope = JsonSerializer.Deserialize<RawEventEnvelopeDto>(recordedEvent.RawEventJson!);
            Assert.NotNull(rawEnvelope);
            Assert.Equal(gameState.InGameIds.Format(ShadowverseBeyond.Server.Domain.Identifiers.InGameIdKind.Player, player.PlayerId), rawEnvelope!.Payload[nameof(GameEventBase.CurrentPlayerId)]!.GetValue<string>());
            Assert.Equal(5, rawEnvelope.Payload[nameof(GameEventBase.CurrentPlayerTurnNumber)]!.GetValue<int>());
        }

        [Fact]
        public async Task ProcessTransactionAsync_ChainedAbilities_RunAfterGeneratedEventCommit()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IConditionFactory, NullConditionFactory>();
            services.AddSingleton<ITargetFactory, NullTargetFactory>();
            services.AddSingleton<IQueryExecutor, NullQueryExecutor>();
            services.AddSingleton<IPendingActionService>(_mockPendingActionService.Object);
            services.AddSingleton<TargetSelectionService>();
            services.AddSingleton<IAbilityFactory, TestAbilityFactory>();
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("chain-after-commit", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var gameContext = new GameContext(serviceProvider, mapper.Object, accessor, gameState, player.PlayerId, default, null, sourceCard);
            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.ContextAccessor.GameContext = gameContext;
            GameServiceProvider.Logger = NullLogger.Instance;

            var initialEvent = new ImmediateTestEvent();

            var committed = false;
            var commitEvent = new CommitFlagEvent(() => committed = true);

            var effectConfig = new EffectConfig
            {
                Type = EffectType.Draw,
                OnFailure = FailureHandling.CompleteWithFailure,
                ChainedAbilities = new List<AbilityConfig>
                {
                    new AbilityConfig { Name = "Chained" }
                }
            };
            var effect = new Effect(effectConfig, player.PlayerId, new List<TargetInfo>(), null, Guid.Empty, sourceCard.InstanceId);

            var completionEvent = new EffectCompletedEvent(
                effectConfig.Type,
                new Dictionary<string, object>(),
                new List<TargetInfo>(),
                new List<IGameEvent> { commitEvent },
                new List<CardInstance>(),
                new List<CardInstanceSnapshot>(),
                new Dictionary<string, object?>(),
                true,
                sourceCard);

            _mockEffectExecutor.Setup(e => e.ExecuteEffect(effect, It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult
                {
                    Success = true,
                    GeneratedEvents = new List<IGameEvent> { commitEvent, completionEvent }
                });

            var primaryAbility = new Mock<IAbility>();
            primaryAbility.SetupGet(a => a.SourceCard).Returns(sourceCard);
            primaryAbility.SetupGet(a => a.KeywordSummary).Returns("Primary");
            effect.SourceAbility = primaryAbility.Object;

            _mockAbilityManager.Setup(m => m.GetTriggeredAbilitiesAsync(
                    It.IsAny<IGameEvent>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());

            _mockAbilityManager.Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, gameContext, TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { primaryAbility.Object });

            _mockAbilityManager.Setup(m => m.ProcessSingleAbilityAsync(primaryAbility.Object, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { effect }));

            var chainSawCommitted = false;
            _mockAbilityManager.Setup(m => m.ProcessSingleAbilityAsync(
                    It.Is<IAbility>(a => a is Ability),
                    It.IsAny<GameContext>(),
                    false,
                    true,
                    It.IsAny<bool>()))
                .Callback(() => chainSawCommitted = committed)
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>()));

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsSuccess);
            Assert.True(chainSawCommitted);
        }

        [Fact]
        public async Task ProcessTransactionAsync_GeneratedEventCommitFailure_RespectsFailureHandling()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IConditionFactory, NullConditionFactory>();
            services.AddSingleton<ITargetFactory, NullTargetFactory>();
            services.AddSingleton<IQueryExecutor, NullQueryExecutor>();
            services.AddSingleton<IPendingActionService>(_mockPendingActionService.Object);
            services.AddSingleton<TargetSelectionService>();
            services.AddSingleton<IAbilityFactory, TestAbilityFactory>();
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("chain-after-commit-failure", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var gameContext = new GameContext(serviceProvider, mapper.Object, accessor, gameState, player.PlayerId, default, null, sourceCard);
            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.ContextAccessor.GameContext = gameContext;
            GameServiceProvider.Logger = NullLogger.Instance;

            var initialEvent = new ImmediateTestEvent();

            var commitEvent = new CommitFailureEvent();

            var effectConfig = new EffectConfig
            {
                Type = EffectType.Draw,
                OnFailure = FailureHandling.SkipEffect,
                ChainedAbilities = new List<AbilityConfig>
                {
                    new AbilityConfig { Name = "Chained" }
                }
            };
            var effect = new Effect(effectConfig, player.PlayerId, new List<TargetInfo>(), null, Guid.Empty, sourceCard.InstanceId);

            var completionEvent = new EffectCompletedEvent(
                effectConfig.Type,
                new Dictionary<string, object>(),
                new List<TargetInfo>(),
                new List<IGameEvent> { commitEvent },
                new List<CardInstance>(),
                new List<CardInstanceSnapshot>(),
                new Dictionary<string, object?>(),
                true,
                sourceCard);

            _mockEffectExecutor.Setup(e => e.ExecuteEffect(effect, It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult
                {
                    Success = true,
                    GeneratedEvents = new List<IGameEvent> { commitEvent, completionEvent }
                });

            var primaryAbility = new Mock<IAbility>();
            primaryAbility.SetupGet(a => a.SourceCard).Returns(sourceCard);
            primaryAbility.SetupGet(a => a.KeywordSummary).Returns("Primary");

            _mockAbilityManager.Setup(m => m.GetTriggeredAbilitiesAsync(
                    It.IsAny<IGameEvent>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());

            _mockAbilityManager.Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, gameContext, TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { primaryAbility.Object });

            _mockAbilityManager.Setup(m => m.ProcessSingleAbilityAsync(primaryAbility.Object, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { effect }));

            var chainExecuted = false;
            _mockAbilityManager.Setup(m => m.ProcessSingleAbilityAsync(
                    It.Is<IAbility>(a => a is Ability),
                    It.IsAny<GameContext>(),
                    false,
                    true,
                    It.IsAny<bool>()))
                .Callback(() => chainExecuted = true)
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>()));

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.False(chainExecuted);
        }

        [Fact]
        public async Task ProcessTransactionAsync_CommitException_BroadcastsAlreadyProcessedEventsOnly()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("partial-broadcast-on-crash", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var gameContext = new GameContext(serviceProvider, mapper.Object, accessor, gameState, player.PlayerId, default, null, sourceCard);
            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.ContextAccessor.GameContext = gameContext;
            GameServiceProvider.Logger = NullLogger.Instance;

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());

            _mockEventProcessor
                .Setup(p => p.ProcessEventAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>()))
                .ReturnsAsync((IGameEvent evt, GameContext _) => new ProcessorResult
                {
                    ResultingDtos = new List<GameEventDto>
                    {
                        new LogEventDto(evt.EventId, evt.EventType.ToString(), "processed", player.PlayerId)
                    }
                });

            var sentDtos = new List<GameEventDto>();
            _mockClientMethodMapper
                .Setup(m => m.BroadcastEventToGroupAllAsync(It.IsAny<string>(), It.IsAny<GameEventDto>()))
                .Callback<string, GameEventDto>((_, dto) => sentDtos.Add(dto))
                .Returns(Task.CompletedTask);

            var crashingEvent = new CommitThrowsEvent();
            var initialEvent = new CommitGeneratesEvent(crashingEvent);

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.False(result.IsSuccess);
            Assert.Contains(sentDtos, dto => dto is LogEventDto log && log.EventId == initialEvent.EventId);
            Assert.DoesNotContain(sentDtos, dto => dto is LogEventDto log && log.EventId == crashingEvent.EventId);
            Assert.Contains(sentDtos, dto => dto is LogEventDto log && log.EventType == "TransactionFailed");
        }

        [Fact]
        public async Task ProcessTransactionAsync_WhenGameEndAlreadyResolved_DoesNotProcessLaterGameEnd()
        {
            const int leaderDefinitionId = 900001;
            const int sourceDefinitionId = 900002;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var mockDataProvider = new Mock<IGameDataProvider>();
            mockDataProvider.SetupGet(provider => provider.IsInitialized).Returns(true);
            mockDataProvider.Setup(provider => provider.GetCardDefinition(It.IsAny<int>()))
                .Returns((int id) => id == leaderDefinitionId
                    ? new CardDefinition
                    {
                        Id = leaderDefinitionId,
                        Name = "Leader",
                        Type = CardType.Leader,
                        Attack = 0,
                        Health = 10
                    }
                    : new CardDefinition
                    {
                        Id = id,
                        Name = id == sourceDefinitionId ? "Source" : $"Card_{id}",
                        Type = CardType.Follower,
                        Attack = 1,
                        Health = 1
                    });
            mockDataProvider.Setup(provider => provider.GetAbilityConfigs(It.IsAny<int>()))
                .Returns(new List<AbilityConfig>());
            GameDataProvider.SetInstanceForTesting(mockDataProvider.Object);

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("double-game-end", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), sourceDefinitionId, player.PlayerId) { CurrentZone = CardZone.Field };
            var playerLeader = new CardInstance(Guid.NewGuid(), leaderDefinitionId, player.PlayerId) { CurrentZone = CardZone.Field, Damage = 9 };
            var opponentLeader = new CardInstance(Guid.NewGuid(), leaderDefinitionId, opponent.PlayerId) { CurrentZone = CardZone.Field, Damage = 9 };
            player.Leader = playerLeader;
            opponent.Leader = opponentLeader;
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);
            gameState.RegisterCardInstance(playerLeader);
            gameState.RegisterCardInstance(opponentLeader);

            var firstLethalDamage = new DamageEvent(opponentLeader, 1, sourceCard, DamageKind.Ability);
            var laterLethalDamage = new DamageEvent(playerLeader, 1, sourceCard, DamageKind.Ability);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            using var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                firstLethalDamage,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.ContextAccessor.GameContext = gameContext;
            GameServiceProvider.Logger = NullLogger.Instance;

            _mockAbilityManager
                .Setup(manager => manager.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());

            var result = await _transactionService.ProcessTransactionAsync(
                gameContext,
                new IGameEvent[] { firstLethalDamage, laterLethalDamage });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            var gameEnd = Assert.Single(result.ProcessedEvents.OfType<GameEndEvent>());
            Assert.Equal(player.PlayerId, gameEnd.WinnerId);
            Assert.Equal(opponent.PlayerId, gameEnd.LoserId);
            Assert.Equal(player.PlayerId, gameState.WinnerId);
        }

        [Fact]
        public async Task ProcessTransactionAsync_EffectCompletedSnapshots_RemainEmptyBeforeRefresh()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();

            var buffService = new Mock<IBuffService>();
            buffService.Setup(s => s.GetEffectiveMaxHealth(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(10);
            buffService.Setup(s => s.GetEffectiveCost(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(0);
            buffService.Setup(s => s.GetEffectiveKeywords(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(new List<CardKeyword>());
            buffService.Setup(s => s.GetEffectiveAttack(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => card.IsEvolved ? 5 : 1);
            buffService.Setup(s => s.RemoveAllBuffs(It.IsAny<Guid>(), It.IsAny<GameContext>(), It.IsAny<Func<Buff, bool>?>()))
                .Returns(new DomainOperationResult { Success = true });
            buffService.Setup(s => s.InvalidateDerivedState(It.IsAny<GameState>(), It.IsAny<Guid>()));
            services.AddSingleton(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("snapshot-pre-commit", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 999999, player.PlayerId) { CurrentZone = CardZone.Field };
            var movedCard = new CardInstance(Guid.NewGuid(), 999999, opponent.PlayerId)
            {
                CurrentZone = CardZone.Field,
                IsEvolved = true
            };
            gameState.RegisterCardInstance(sourceCard);
            gameState.RegisterCardInstance(movedCard);
            player.Field.Add(sourceCard);
            opponent.Field.Add(movedCard);

            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var gameContext = new GameContext(serviceProvider, mapper.Object, accessor, gameState, player.PlayerId, default, null, sourceCard);
            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.ContextAccessor.GameContext = gameContext;
            GameServiceProvider.Logger = NullLogger.Instance;

            var initialEvent = new ImmediateTestEvent();

            var effectConfig = new EffectConfig { Type = EffectType.Draw, OnFailure = FailureHandling.CompleteWithFailure };
            var effect = new Effect(effectConfig, player.PlayerId, new List<TargetInfo>(), null, Guid.Empty, sourceCard.InstanceId);

            _mockAbilityManager.Setup(m => m.GetEffectiveAbilitiesForCard(It.IsAny<Guid>(), It.IsAny<GameContext>()))
                .Returns(new List<IAbility>());

            var moveEvent = new CardMovedEvent(movedCard, CardZone.Field, CardZone.Graveyard, CardMoveReason.Destroyed);
            var generatedCards = new List<CardInstance> { movedCard };
            var generatedSnapshots = new List<CardInstanceSnapshot> { CardInstanceSnapshot.From(movedCard) };
            var completionEvent = new EffectCompletedEvent(
                effectConfig.Type,
                new Dictionary<string, object>(),
                new List<TargetInfo>(),
                new List<IGameEvent> { moveEvent },
                generatedCards,
                generatedSnapshots,
                new Dictionary<string, object?>(),
                true,
                sourceCard);

            _mockEffectExecutor.Setup(e => e.ExecuteEffect(effect, It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult
                {
                    Success = true,
                    GeneratedEvents = new List<IGameEvent> { moveEvent, completionEvent }
                });

            var primaryAbility = new Mock<IAbility>();
            primaryAbility.SetupGet(a => a.SourceCard).Returns(sourceCard);
            primaryAbility.SetupGet(a => a.KeywordSummary).Returns("Primary");

            _mockAbilityManager.Setup(m => m.GetTriggeredAbilitiesAsync(
                    It.IsAny<IGameEvent>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());

            _mockAbilityManager.Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, gameContext, TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { primaryAbility.Object });

            _mockAbilityManager.Setup(m => m.ProcessSingleAbilityAsync(primaryAbility.Object, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { effect }));

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            var completion = result.ProcessedEvents.OfType<EffectCompletedEvent>().Single();
            Assert.Empty(completion.GeneratedCardSnapshots);

            Assert.False(movedCard.IsEvolved);
            Assert.Equal(CardZone.Graveyard, movedCard.CurrentZone);
            Assert.Equal(1, movedCard.CurrentAttack);
        }

        [Fact]
        public async Task ProcessTransactionAsync_RewritesDamageEventInBeforeIntercept()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            var buffService = CreateDefaultBuffService();
            services.AddSingleton(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player, opponent, player.PlayerId);
            gameState.AbilityManager = _mockAbilityManager.Object;
            gameState.BuffManager = new Mock<IBuffManager>().Object;

            var attackerCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            var targetCard = new CardInstance(Guid.NewGuid(), 2, opponent.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(attackerCard);
            opponent.Field.Add(targetCard);

            var damageEvent = new DamageEvent(targetCard, 1, attackerCard, DamageKind.Ability);
            var mapper = new Mock<IMapper>();
            var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                player.PlayerId,
                default,
                damageEvent,
                attackerCard);

            GameServiceProvider.ContextAccessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            GameServiceProvider.Logger = NullLogger.Instance;

            var interceptEffectConfig = new EffectConfig { Type = EffectType.RewriteEventParams };
            var interceptEffect = new Effect(interceptEffectConfig, player.PlayerId, null, null, damageEvent.EventId, attackerCard.InstanceId);

            var interceptAbility = new Mock<IAbility>();
            interceptAbility.SetupGet(a => a.RuntimeId).Returns(Guid.NewGuid());
            interceptAbility.SetupGet(a => a.SourceCard).Returns(attackerCard);

            _mockAbilityManager.Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager.Setup(m => m.GetTriggeredAbilitiesAsync(damageEvent, It.IsAny<GameContext>(), TriggerTiming.Before))
                .ReturnsAsync(new List<IAbility> { interceptAbility.Object });
            _mockAbilityManager.Setup(m => m.ProcessSingleAbilityAsync(interceptAbility.Object, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { interceptEffect }));

            _mockEffectExecutor.Setup(e => e.ExecuteEffect(interceptEffect, It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult
                {
                    Success = true,
                    EventIntercept = EventInterceptResult.RewriteParameter("Damage", 3)
                });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { damageEvent });

            Assert.True(result.IsSuccess);
            var processedDamage = result.ProcessedEvents.OfType<DamageEvent>().Single();
            Assert.Equal(3, processedDamage.Damage);
            Assert.Equal(damageEvent.EventId, processedDamage.EventId);
        }

        [Fact]
        public async Task ProcessTransactionAsync_BeforeInterceptAbilityTriggeredEvent_CanTriggerChainedAbility()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            var buffService = CreateDefaultBuffService();
            services.AddSingleton(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("before-intercept-chain", player, opponent, player.PlayerId);
            gameState.AbilityManager = _mockAbilityManager.Object;
            gameState.BuffManager = new Mock<IBuffManager>().Object;

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            var targetCard = new CardInstance(Guid.NewGuid(), 2, opponent.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            opponent.Field.Add(targetCard);

            var damageEvent = new DamageEvent(targetCard, 1, sourceCard, DamageKind.Ability);

            var mapper = new Mock<IMapper>();
            var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                player.PlayerId,
                default,
                damageEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            GameServiceProvider.Logger = NullLogger.Instance;

            var interceptEffectConfig = new EffectConfig { Type = EffectType.RewriteEventParams };
            var interceptEffect = new Effect(
                interceptEffectConfig,
                player.PlayerId,
                null,
                null,
                damageEvent.EventId,
                sourceCard.InstanceId);

            var chainedEffectConfig = new EffectConfig { Type = EffectType.Draw };
            var chainedEffect = new Effect(
                chainedEffectConfig,
                player.PlayerId,
                null,
                null,
                Guid.Empty,
                sourceCard.InstanceId);

            var interceptAbility = new Mock<IAbility>();
            interceptAbility.SetupGet(a => a.RuntimeId).Returns(Guid.NewGuid());
            interceptAbility.SetupGet(a => a.SourceCard).Returns(sourceCard);
            interceptAbility.SetupGet(a => a.KeywordSummary).Returns("Intercept");

            var chainedAbility = new Mock<IAbility>();
            chainedAbility.SetupGet(a => a.RuntimeId).Returns(Guid.NewGuid());
            chainedAbility.SetupGet(a => a.SourceCard).Returns(sourceCard);
            chainedAbility.SetupGet(a => a.KeywordSummary).Returns("Chain");

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(
                    It.Is<IGameEvent>(e => e.EventId == damageEvent.EventId && e.EventType == EventType.Damage),
                    It.IsAny<GameContext>(),
                    TriggerTiming.Before))
                .ReturnsAsync(new List<IAbility> { interceptAbility.Object });
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(
                    It.Is<IGameEvent>(e => e is AbilityTriggeredEvent),
                    It.IsAny<GameContext>(),
                    TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { chainedAbility.Object });

            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(interceptAbility.Object, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { interceptEffect }));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(chainedAbility.Object, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { chainedEffect }));

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(interceptEffect, It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult
                {
                    Success = true,
                    EventIntercept = EventInterceptResult.RewriteParameter("Damage", 2)
                });

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(chainedEffect, It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult { Success = true });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { damageEvent });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Contains(result.ProcessedEvents, e => e is AbilityTriggeredEvent);
            _mockEffectExecutor.Verify(e => e.ExecuteEffect(chainedEffect, It.IsAny<GameContext>()), Times.Once);
        }

        [Fact]
        public async Task ProcessTransactionAsync_BeforeInterceptAbilityWithBroadcastDisabled_ShouldNotEmitAbilityTriggeredEvent()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            var buffService = CreateDefaultBuffService();
            services.AddSingleton(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("before-intercept-no-broadcast", player, opponent, player.PlayerId);
            gameState.AbilityManager = _mockAbilityManager.Object;
            gameState.BuffManager = new Mock<IBuffManager>().Object;

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            var targetCard = new CardInstance(Guid.NewGuid(), 2, opponent.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            opponent.Field.Add(targetCard);

            var damageEvent = new DamageEvent(targetCard, 1, sourceCard, DamageKind.Ability);

            var mapper = new Mock<IMapper>();
            var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                player.PlayerId,
                default,
                damageEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            GameServiceProvider.Logger = NullLogger.Instance;

            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                new NullQueryExecutor(),
                _mockPendingActionService.Object);
            var conditionFactory = new Mock<IConditionFactory>();
            var targetFactory = new Mock<ITargetFactory>();
            var interceptAbility = new Ability(
                new AbilityConfig
                {
                    Id = 990001,
                    Name = "NoBroadcastIntercept",
                    BroadcastOnTrigger = false,
                    Trigger = new TriggerConfig
                    {
                        EventTypes = new List<EventType> { EventType.Damage },
                        Timing = TriggerTiming.Before,
                        IsEnabled = true
                    }
                },
                conditionFactory.Object,
                targetFactory.Object,
                targetSelectionService,
                sourceCard);

            var interceptEffectConfig = new EffectConfig { Type = EffectType.RewriteEventParams };
            var interceptEffect = new Effect(
                interceptEffectConfig,
                player.PlayerId,
                null,
                null,
                damageEvent.EventId,
                sourceCard.InstanceId);

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(
                    It.Is<IGameEvent>(e => e.EventId == damageEvent.EventId && e.EventType == EventType.Damage),
                    It.IsAny<GameContext>(),
                    TriggerTiming.Before))
                .ReturnsAsync(new List<IAbility> { interceptAbility });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(interceptAbility, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { interceptEffect }));

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(interceptEffect, It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult
                {
                    Success = true,
                    EventIntercept = EventInterceptResult.RewriteParameter("Damage", 2)
                });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { damageEvent });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.DoesNotContain(result.ProcessedEvents, e => e is AbilityTriggeredEvent);
        }

        [Fact]
        public async Task ProcessTransactionAsync_MixedBeforeAbility_ExecutesNonInterceptEffectsAfterRewrite()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IConditionFactory, NullConditionFactory>();
            services.AddSingleton<ITargetFactory, NullTargetFactory>();
            services.AddSingleton<IQueryExecutor, NullQueryExecutor>();
            services.AddSingleton<IPendingActionService>(_mockPendingActionService.Object);
            services.AddSingleton<TargetSelectionService>();
            services.AddSingleton<IAbilityFactory, TestAbilityFactory>();
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("mixed-before-effects", player, opponent, player.PlayerId)
            {
                CurrentPlayerId = player.PlayerId
            };

            var abilityManager = new AbilityManager(
                NullLogger<AbilityManager>.Instance,
                serviceProvider.GetRequiredService<IAbilityFactory>(),
                new List<IAbility>());
            gameState.AbilityManager = abilityManager;
            gameState.BuffManager = new BuffManager(NullLogger<BuffManager>.Instance, abilityManager);

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            var targetCard = new CardInstance(Guid.NewGuid(), 2, opponent.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            opponent.Field.Add(targetCard);
            gameState.RegisterCardInstance(sourceCard);
            gameState.RegisterCardInstance(targetCard);

            var mixedBeforeAbilityConfig = new AbilityConfig
            {
                Id = 991001,
                Name = "mixed-before-ability",
                Type = AbilityType.Triggered,
                Trigger = new TriggerConfig
                {
                    EventTypes = new List<EventType> { EventType.Damage },
                    Timing = TriggerTiming.Before,
                    RelationType = TriggerRelationType.Any,
                    IsEnabled = true
                },
                Effects = new List<EffectConfig>
                {
                    new() { Type = EffectType.RewriteEventParams },
                    new() { Type = EffectType.Draw }
                }
            };
            abilityManager.CreateAbilitiesForCard(sourceCard, new List<AbilityConfig> { mixedBeforeAbilityConfig }, null, gameState);

            var transactionService = new GameActionTransactionService(
                _mockLogger.Object,
                abilityManager,
                _mockEventProcessor.Object,
                _mockPendingActionService.Object,
                _mockEffectExecutor.Object,
                _mockClientMethodMapper.Object,
                _mockEventJournal.Object,
                _mockSnapshotStore.Object,
                new JsonRecordedEventMapper(),
                new JsonGameEventRawEventMapper(),
                _mockMetaProgressionService.Object,
                _mockWinrateStatsService.Object,
                _replayOptions);

            var damageEvent = new DamageEvent(targetCard, 1, sourceCard, DamageKind.Ability);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            using var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                damageEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.ContextAccessor.GameContext = gameContext;
            GameServiceProvider.Logger = NullLogger.Instance;

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(
                    It.Is<Effect>(effect => effect.Type == EffectType.RewriteEventParams),
                    It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult
                {
                    Success = true,
                    EventIntercept = EventInterceptResult.RewriteParameter("Damage", 2)
                });
            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(
                    It.Is<Effect>(effect => effect.Type == EffectType.Draw),
                    It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult { Success = true });

            var result = await transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { damageEvent });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            var processedDamage = Assert.Single(result.ProcessedEvents.OfType<DamageEvent>());
            Assert.Equal(2, processedDamage.Damage);
            _mockEffectExecutor.Verify(
                e => e.ExecuteEffect(
                    It.Is<Effect>(effect => effect.Type == EffectType.Draw),
                    It.IsAny<GameContext>()),
                Times.Once);
        }

        [Fact]
        public async Task ProcessTransactionAsync_RewriteChooseEventSelectionCount_AffectsDecision()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IValueSourceResolver, NullValueSourceResolver>();
            var conditionFactory = new NullConditionFactory();
            var targetFactory = new NullTargetFactory();
            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                new NullQueryExecutor(),
                _mockPendingActionService.Object);
            services.AddSingleton(targetSelectionService);
            services.AddSingleton<IAbilityFactory>(new TestAbilityFactory(conditionFactory, targetFactory, targetSelectionService));
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("choose-rewrite", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var initialEvent = new ImmediateTestEvent();
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                initialEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var branchA = new AbilityConfig { Name = "OptionA" };
            var branchB = new AbilityConfig { Name = "OptionB" };
            var branchC = new AbilityConfig { Name = "OptionC" };

            var chooseEffectConfig = new EffectConfig
            {
                Type = EffectType.Choose,
                Parameters = new Dictionary<string, ValueConfig>
                {
                    ["abilityConfigs"] = new StaticValueConfig
                    {
                        Value = new List<AbilityConfig> { branchA, branchB, branchC }
                    },
                    ["selectionCount"] = new StaticValueConfig { Value = 1 }
                }
            };

            var abilityConfig = new AbilityConfig
            {
                Name = "ChooseAbility",
                Effects = new List<EffectConfig> { chooseEffectConfig },
                Trigger = new TriggerConfig()
            };

            var ability = new Ability(abilityConfig, conditionFactory, targetFactory, targetSelectionService, sourceCard);

            var chooseEffect = new Effect(
                chooseEffectConfig,
                player.PlayerId,
                new List<TargetInfo>(),
                ability.RuntimeId,
                initialEvent.EventId,
                sourceCard.InstanceId)
            {
                SourceAbility = ability,
                EffectIndex = 0
            };

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { ability });

            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(It.IsAny<IAbility>(), It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>()));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(ability, It.IsAny<GameContext>(), true, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { chooseEffect }));

            var interceptAbility = new Mock<IAbility>();
            interceptAbility.SetupGet(a => a.RuntimeId).Returns(Guid.NewGuid());
            interceptAbility.SetupGet(a => a.SourceCard).Returns(sourceCard);
            interceptAbility.SetupGet(a => a.KeywordSummary).Returns("Intercept");

            var interceptEffectConfig = new EffectConfig { Type = EffectType.RewriteEventParams };
            var interceptEffect = new Effect(
                interceptEffectConfig,
                player.PlayerId,
                null,
                interceptAbility.Object.RuntimeId,
                Guid.Empty,
                sourceCard.InstanceId)
            {
                SourceAbility = interceptAbility.Object
            };

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.Is<IGameEvent>(e => e is ChooseEvent), It.IsAny<GameContext>(), TriggerTiming.Before))
                .ReturnsAsync(new List<IAbility> { interceptAbility.Object });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(interceptAbility.Object, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { interceptEffect }));

            _mockPendingActionService
                .Setup(s => s.WaitForPlayerChoiceAsync(It.IsAny<string>(), It.IsAny<PlayerChoiceRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, PlayerChoiceRequest request, CancellationToken __) =>
                {
                    var chosen = request.SelectableTargetIds.Take(request.MinSelectionCount).ToList();
                    return new PlayerChoiceResult
                    {
                        WasCancelled = false,
                        ChosenTargetIds = chosen
                    };
                });

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult { Success = true });
            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(interceptEffect, It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult
                {
                    Success = true,
                    EventIntercept = EventInterceptResult.RewriteParameter("SelectionCount", 2)
                });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsSuccess, result.ErrorMessage);
        }

        [Fact]
        public async Task ProcessTransactionAsync_ChooseSelectionCountExceedsOptions_CapsToAvailableOptions()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IValueSourceResolver, NullValueSourceResolver>();
            var conditionFactory = new NullConditionFactory();
            var targetFactory = new NullTargetFactory();
            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                new NullQueryExecutor(),
                _mockPendingActionService.Object);
            services.AddSingleton(targetSelectionService);
            services.AddSingleton<IAbilityFactory>(new TestAbilityFactory(conditionFactory, targetFactory, targetSelectionService));
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("choose-cap-selection-count", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var initialEvent = new ImmediatePlayerActionTestEvent(sourceCard);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                initialEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var branchA = new AbilityConfig { Name = "OptionA" };
            var branchB = new AbilityConfig { Name = "OptionB" };
            var branchC = new AbilityConfig { Name = "OptionC" };

            var chooseEffectConfig = new EffectConfig
            {
                Type = EffectType.Choose,
                Parameters = new Dictionary<string, ValueConfig>
                {
                    ["abilityConfigs"] = new StaticValueConfig
                    {
                        Value = new List<AbilityConfig> { branchA, branchB, branchC }
                    },
                    ["selectionCount"] = new StaticValueConfig { Value = 5 }
                }
            };

            var abilityConfig = new AbilityConfig
            {
                Name = "ChooseAbility",
                Effects = new List<EffectConfig> { chooseEffectConfig },
                Trigger = new TriggerConfig()
            };

            var ability = new Ability(abilityConfig, conditionFactory, targetFactory, targetSelectionService, sourceCard);
            var chooseEffect = new Effect(
                chooseEffectConfig,
                player.PlayerId,
                new List<TargetInfo>(),
                ability.RuntimeId,
                initialEvent.EventId,
                sourceCard.InstanceId)
            {
                SourceAbility = ability,
                EffectIndex = 0
            };

            PlayerChoiceRequest? capturedRequest = null;

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { ability });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(It.IsAny<IAbility>(), It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>()));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(ability, It.IsAny<GameContext>(), true, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { chooseEffect }));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(ability, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { chooseEffect }));

            _mockPendingActionService
                .Setup(s => s.WaitForPlayerChoiceAsync(It.IsAny<string>(), It.IsAny<PlayerChoiceRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, PlayerChoiceRequest request, CancellationToken __) =>
                {
                    capturedRequest = request;
                    return new PlayerChoiceResult
                    {
                        WasCancelled = false,
                        ChosenTargetIds = request.SelectableTargetIds.Take(request.MaxSelectionCount).ToList()
                    };
                });

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult { Success = true });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.NotNull(capturedRequest);
            Assert.Equal(3, capturedRequest!.MinSelectionCount);
            Assert.Equal(3, capturedRequest.MaxSelectionCount);

            var chooseEvent = Assert.Single(result.ProcessedEvents.OfType<ChooseEvent>());
            Assert.Equal(3, chooseEvent.SelectedOptionIds.Count);
        }

        [Fact]
        public async Task ProcessTransactionAsync_PrimaryChoose_ReusesPreflightDecisionInCommit()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IValueSourceResolver, NullValueSourceResolver>();
            var conditionFactory = new NullConditionFactory();
            var targetFactory = new NullTargetFactory();
            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                new NullQueryExecutor(),
                _mockPendingActionService.Object);
            services.AddSingleton(targetSelectionService);
            services.AddSingleton<IAbilityFactory>(new TestAbilityFactory(conditionFactory, targetFactory, targetSelectionService));
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("choose-preflight-reuse", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var initialEvent = new ImmediatePlayerActionTestEvent(sourceCard);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                initialEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var branchA = new AbilityConfig { Name = "OptionA" };
            var branchB = new AbilityConfig { Name = "OptionB" };

            var chooseEffectConfig = new EffectConfig
            {
                Type = EffectType.Choose,
                Parameters = new Dictionary<string, ValueConfig>
                {
                    ["abilityConfigs"] = new StaticValueConfig
                    {
                        Value = new List<AbilityConfig> { branchA, branchB }
                    },
                    ["selectionCount"] = new StaticValueConfig { Value = 1 }
                }
            };

            var abilityConfig = new AbilityConfig
            {
                Name = "ChooseAbility",
                Effects = new List<EffectConfig> { chooseEffectConfig },
                Trigger = new TriggerConfig()
            };

            var ability = new Ability(abilityConfig, conditionFactory, targetFactory, targetSelectionService, sourceCard);
            var chooseEffect = new Effect(
                chooseEffectConfig,
                player.PlayerId,
                new List<TargetInfo>(),
                ability.RuntimeId,
                initialEvent.EventId,
                sourceCard.InstanceId)
            {
                SourceAbility = ability,
                EffectIndex = 0
            };

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { ability });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(It.IsAny<IAbility>(), It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>()));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(ability, It.IsAny<GameContext>(), true, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { chooseEffect }));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(ability, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { chooseEffect }));

            _mockPendingActionService
                .Setup(s => s.WaitForPlayerChoiceAsync(It.IsAny<string>(), It.IsAny<PlayerChoiceRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, PlayerChoiceRequest request, CancellationToken __) =>
                {
                    return new PlayerChoiceResult
                    {
                        WasCancelled = false,
                        ChosenTargetIds = new List<Guid> { request.SelectableTargetIds.First() }
                    };
                });

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult { Success = true });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            _mockPendingActionService.Verify(
                s => s.WaitForPlayerChoiceAsync(It.IsAny<string>(), It.IsAny<PlayerChoiceRequest>(), It.IsAny<CancellationToken>()),
                Times.Once);
            _mockAbilityManager.Verify(
                m => m.ProcessSingleAbilityAsync(ability, It.IsAny<GameContext>(), true, true, It.IsAny<bool>()),
                Times.Once);
            _mockAbilityManager.Verify(
                m => m.ProcessSingleAbilityAsync(ability, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()),
                Times.Never);
        }

        [Fact]
        public async Task ProcessTransactionAsync_ChainedChooseWithoutPreflightSnapshot_ResolvesInCommit()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IValueSourceResolver, NullValueSourceResolver>();
            var conditionFactory = new NullConditionFactory();
            var targetFactory = new NullTargetFactory();
            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                new NullQueryExecutor(),
                _mockPendingActionService.Object);
            services.AddSingleton(targetSelectionService);
            services.AddSingleton<IAbilityFactory>(new TestAbilityFactory(conditionFactory, targetFactory, targetSelectionService));
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("chained-choose-commit", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var initialEvent = new ImmediatePlayerActionTestEvent(sourceCard);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                initialEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var decisionBranchA = new AbilityConfig { Name = "DecisionA" };
            var decisionBranchB = new AbilityConfig { Name = "DecisionB" };
            var chainedChooseEffectConfig = new EffectConfig
            {
                Type = EffectType.Choose,
                Parameters = new Dictionary<string, ValueConfig>
                {
                    ["abilityConfigs"] = new StaticValueConfig
                    {
                        Value = new List<AbilityConfig> { decisionBranchA, decisionBranchB }
                    },
                    ["selectionCount"] = new StaticValueConfig { Value = 1 }
                }
            };
            var chainedAbilityTemplate = new AbilityConfig
            {
                Name = "ChainChoose",
                Effects = new List<EffectConfig> { chainedChooseEffectConfig },
                Trigger = new TriggerConfig()
            };

            var primaryEffectConfig = new EffectConfig
            {
                Type = EffectType.Shuffle,
                ChainedAbilities = new List<AbilityConfig> { chainedAbilityTemplate }
            };
            var primaryAbilityConfig = new AbilityConfig
            {
                Name = "PrimaryShuffle",
                Effects = new List<EffectConfig> { primaryEffectConfig },
                Trigger = new TriggerConfig()
            };
            var primaryAbility = new Ability(primaryAbilityConfig, conditionFactory, targetFactory, targetSelectionService, sourceCard);
            var shuffleEffect = new Effect(
                primaryEffectConfig,
                player.PlayerId,
                new List<TargetInfo>(),
                primaryAbility.RuntimeId,
                initialEvent.EventId,
                sourceCard.InstanceId)
            {
                SourceAbility = primaryAbility,
                EffectIndex = 0
            };

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { primaryAbility });

            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(It.IsAny<IAbility>(), It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>()));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(primaryAbility, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { shuffleEffect }));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(
                    It.Is<IAbility>(ability => ability is Ability && ((Ability)ability).Config.Name == "ChainChoose"),
                    It.IsAny<GameContext>(),
                    false,
                    true,
                    It.IsAny<bool>()))
                .ReturnsAsync((IAbility ability, GameContext _, bool __, bool ___, bool ____) =>
                {
                    var chainedAbility = (Ability)ability;
                    var chainedChooseEffect = new Effect(
                        chainedChooseEffectConfig,
                        player.PlayerId,
                        new List<TargetInfo>(),
                        chainedAbility.RuntimeId,
                        initialEvent.EventId,
                        sourceCard.InstanceId)
                    {
                        SourceAbility = chainedAbility,
                        EffectIndex = 0
                    };

                    return AbilityProcessResult.Success(new List<Effect> { chainedChooseEffect });
                });

            PlayerChoiceRequest? capturedChoiceRequest = null;
            _mockPendingActionService
                .Setup(s => s.WaitForPlayerChoiceAsync(It.IsAny<string>(), It.IsAny<PlayerChoiceRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, PlayerChoiceRequest request, CancellationToken __) =>
                {
                    capturedChoiceRequest = request;
                    return new PlayerChoiceResult
                    {
                        WasCancelled = false,
                        ChosenTargetIds = new List<Guid> { request.SelectableTargetIds.First() }
                    };
                });

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult { Success = true });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.NotNull(capturedChoiceRequest);
            Assert.False(capturedChoiceRequest!.IsCancellable);
            _mockPendingActionService.Verify(
                s => s.WaitForPlayerChoiceAsync(It.IsAny<string>(), It.IsAny<PlayerChoiceRequest>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ProcessTransactionAsync_ChainedTargetChoice_PreflightsAndReusesChainedAbilitySnapshot()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IValueSourceResolver, NullValueSourceResolver>();
            var conditionFactory = new NullConditionFactory();
            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                new NullQueryExecutor(),
                _mockPendingActionService.Object);
            var targetFactory = new TargetFactory(targetSelectionService, NullLogger<Target>.Instance);
            services.AddSingleton(targetSelectionService);
            services.AddSingleton<IAbilityFactory>(new TestAbilityFactory(conditionFactory, targetFactory, targetSelectionService));
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("chained-target-choice-preflight", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var initialEvent = new ImmediatePlayerActionTestEvent(sourceCard);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                initialEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var chainedAbilityTemplate = new AbilityConfig
            {
                Name = "ChainTarget",
                Trigger = new TriggerConfig(),
                Targets = new Dictionary<string, TargetConfig>
                {
                    ["target"] = new TargetConfig
                    {
                        Query = new QueryConfig(),
                        PlayerChoiceOptions = new PlayerChoiceConfig
                        {
                            IsRequired = true,
                            MinCount = 1,
                            MaxCount = 1
                        }
                    }
                }
            };

            var primaryEffectConfig = new EffectConfig
            {
                Type = EffectType.Shuffle,
                ChainedAbilities = new List<AbilityConfig> { chainedAbilityTemplate }
            };
            var primaryAbilityConfig = new AbilityConfig
            {
                Name = "PrimaryWithChainedTargetChoice",
                Effects = new List<EffectConfig> { primaryEffectConfig },
                Trigger = new TriggerConfig()
            };
            var primaryAbility = new Ability(primaryAbilityConfig, conditionFactory, targetFactory, targetSelectionService, sourceCard);
            var primaryEffect = new Effect(
                primaryEffectConfig,
                player.PlayerId,
                new List<TargetInfo>(),
                primaryAbility.RuntimeId,
                initialEvent.EventId,
                sourceCard.InstanceId)
            {
                SourceAbility = primaryAbility,
                EffectIndex = 0
            };

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { primaryAbility });

            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(It.IsAny<IAbility>(), It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>()));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(primaryAbility, It.IsAny<GameContext>(), true, false, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { primaryEffect }));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(primaryAbility, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { primaryEffect }));

            var chainedCallFlags = new List<(Guid RuntimeId, bool IsCancellable)>();
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(
                    It.Is<IAbility>(ability => ability is Ability && ((Ability)ability).Config.Name == "ChainTarget"),
                    It.IsAny<GameContext>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>()))
                .Callback((IAbility ability, GameContext _, bool isCancellable, bool __, bool ___) =>
                {
                    chainedCallFlags.Add((ability.RuntimeId, isCancellable));
                })
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>()));

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult { Success = true });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Contains(chainedCallFlags, call => call.IsCancellable);
            Assert.Contains(chainedCallFlags, call => !call.IsCancellable);
            var preflightRuntimeId = chainedCallFlags.First(call => call.IsCancellable).RuntimeId;
            var commitRuntimeId = chainedCallFlags.First(call => !call.IsCancellable).RuntimeId;
            Assert.Equal(preflightRuntimeId, commitRuntimeId);
            _mockAbilityManager.Verify(
                m => m.ProcessSingleAbilityAsync(primaryAbility, It.IsAny<GameContext>(), true, false, It.IsAny<bool>()),
                Times.Once);
            _mockAbilityManager.Verify(
                m => m.ProcessSingleAbilityAsync(primaryAbility, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()),
                Times.Never);
        }

        [Fact]
        public async Task ProcessTransactionAsync_ChainedTargetChoiceCancelledInPreflight_DoesNotEnterCommit()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IValueSourceResolver, NullValueSourceResolver>();
            var conditionFactory = new NullConditionFactory();
            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                new NullQueryExecutor(),
                _mockPendingActionService.Object);
            var targetFactory = new TargetFactory(targetSelectionService, NullLogger<Target>.Instance);
            services.AddSingleton(targetSelectionService);
            services.AddSingleton<IAbilityFactory>(new TestAbilityFactory(conditionFactory, targetFactory, targetSelectionService));
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("chained-target-choice-cancelled-preflight", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var initialEvent = new ImmediatePlayerActionTestEvent(sourceCard);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                initialEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var chainedAbilityTemplate = new AbilityConfig
            {
                Name = "ChainTarget",
                Trigger = new TriggerConfig(),
                Targets = new Dictionary<string, TargetConfig>
                {
                    ["target"] = new TargetConfig
                    {
                        Query = new QueryConfig(),
                        PlayerChoiceOptions = new PlayerChoiceConfig
                        {
                            IsRequired = true,
                            MinCount = 1,
                            MaxCount = 1
                        }
                    }
                }
            };

            var primaryEffectConfig = new EffectConfig
            {
                Type = EffectType.Shuffle,
                ChainedAbilities = new List<AbilityConfig> { chainedAbilityTemplate }
            };
            var primaryAbilityConfig = new AbilityConfig
            {
                Name = "PrimaryWithChainedTargetChoice",
                Effects = new List<EffectConfig> { primaryEffectConfig },
                Trigger = new TriggerConfig()
            };
            var primaryAbility = new Ability(primaryAbilityConfig, conditionFactory, targetFactory, targetSelectionService, sourceCard);
            var primaryEffect = new Effect(
                primaryEffectConfig,
                player.PlayerId,
                new List<TargetInfo>(),
                primaryAbility.RuntimeId,
                initialEvent.EventId,
                sourceCard.InstanceId)
            {
                SourceAbility = primaryAbility,
                EffectIndex = 0
            };

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { primaryAbility });

            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(It.IsAny<IAbility>(), It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>()));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(primaryAbility, It.IsAny<GameContext>(), true, false, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { primaryEffect }));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(primaryAbility, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { primaryEffect }));

            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(
                    It.Is<IAbility>(ability => ability is Ability && ((Ability)ability).Config.Name == "ChainTarget"),
                    It.IsAny<GameContext>(),
                    true,
                    false,
                    It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Cancelled("cancelled in preflight"));

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult { Success = true });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsCancelled);
            _mockAbilityManager.Verify(
                m => m.ProcessSingleAbilityAsync(
                    It.Is<IAbility>(ability => ability is Ability && ((Ability)ability).Config.Name == "ChainTarget"),
                    It.IsAny<GameContext>(),
                    false,
                    true,
                    It.IsAny<bool>()),
                Times.Never);
            _mockAbilityManager.Verify(
                m => m.ProcessSingleAbilityAsync(primaryAbility, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()),
                Times.Never);
        }

        [Fact]
        public async Task ProcessTransactionAsync_DecisionBranchTargetChoiceCancelledInPreflight_DoesNotEnterCommit()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IValueSourceResolver, NullValueSourceResolver>();
            var conditionFactory = new NullConditionFactory();
            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                new NullQueryExecutor(),
                _mockPendingActionService.Object);
            var targetFactory = new TargetFactory(targetSelectionService, NullLogger<Target>.Instance);
            services.AddSingleton(targetSelectionService);
            services.AddSingleton<IAbilityFactory>(new TestAbilityFactory(conditionFactory, targetFactory, targetSelectionService));
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("decision-branch-target-cancelled-preflight", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var initialEvent = new ImmediatePlayerActionTestEvent(sourceCard);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                initialEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var branchDecisionEffectConfig = new EffectConfig
            {
                Type = EffectType.Choose,
                Parameters = new Dictionary<string, ValueConfig>
                {
                    ["abilityConfigs"] = new StaticValueConfig
                    {
                        Value = new List<AbilityConfig>()
                    },
                    ["selectionCount"] = new StaticValueConfig { Value = 0 }
                }
            };

            var branchTargetAbilityConfig = new AbilityConfig
            {
                Name = "BranchTarget",
                Trigger = new TriggerConfig(),
                Effects = new List<EffectConfig> { branchDecisionEffectConfig }
            };

            var chooseEffectConfig = new EffectConfig
            {
                Type = EffectType.Choose,
                Parameters = new Dictionary<string, ValueConfig>
                {
                    ["abilityConfigs"] = new StaticValueConfig
                    {
                        Value = new List<AbilityConfig> { branchTargetAbilityConfig }
                    },
                    ["selectionCount"] = new StaticValueConfig { Value = 1 }
                }
            };

            var primaryAbilityConfig = new AbilityConfig
            {
                Name = "PrimaryChoose",
                Effects = new List<EffectConfig> { chooseEffectConfig },
                Trigger = new TriggerConfig()
            };
            var primaryAbility = new Ability(primaryAbilityConfig, conditionFactory, targetFactory, targetSelectionService, sourceCard);
            var chooseEffect = new Effect(
                chooseEffectConfig,
                player.PlayerId,
                new List<TargetInfo>(),
                primaryAbility.RuntimeId,
                initialEvent.EventId,
                sourceCard.InstanceId)
            {
                SourceAbility = primaryAbility,
                EffectIndex = 0
            };

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { primaryAbility });

            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(It.IsAny<IAbility>(), It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>()));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(primaryAbility, It.IsAny<GameContext>(), true, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { chooseEffect }));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(primaryAbility, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { chooseEffect }));

            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(
                    It.Is<IAbility>(ability => ability is Ability && ((Ability)ability).Config.Name == "BranchTarget"),
                    It.IsAny<GameContext>(),
                    true,
                    true,
                    It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Cancelled("cancelled in decision-branch preflight"));

            var cancellableFlags = new List<bool>();
            _mockPendingActionService
                .Setup(s => s.WaitForPlayerChoiceAsync(It.IsAny<string>(), It.IsAny<PlayerChoiceRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, PlayerChoiceRequest request, CancellationToken __) =>
                {
                    cancellableFlags.Add(request.IsCancellable);
                    return new PlayerChoiceResult
                    {
                        WasCancelled = false,
                        ChosenTargetIds = new List<Guid> { request.SelectableTargetIds.First() }
                    };
                });

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult { Success = true });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsCancelled);
            Assert.Empty(result.ProcessedEvents);
            Assert.Single(cancellableFlags);
            Assert.True(cancellableFlags[0]);

            _mockAbilityManager.Verify(
                m => m.ProcessSingleAbilityAsync(
                    It.Is<IAbility>(ability => ability is Ability && ((Ability)ability).Config.Name == "BranchTarget"),
                    It.IsAny<GameContext>(),
                    false,
                    true,
                    It.IsAny<bool>()),
                Times.Never);
        }

        [Fact]
        public async Task ProcessTransactionAsync_ChainedAbilityGeneratedEvents_AreProcessed()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IValueSourceResolver, NullValueSourceResolver>();
            var conditionFactory = new NullConditionFactory();
            var targetFactory = new NullTargetFactory();
            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                new NullQueryExecutor(),
                _mockPendingActionService.Object);
            services.AddSingleton(targetSelectionService);
            services.AddSingleton<IAbilityFactory>(new TestAbilityFactory(conditionFactory, targetFactory, targetSelectionService));
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("chain-generated-events", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var initialEvent = new ImmediatePlayerActionTestEvent(sourceCard);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                initialEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var chainedAbilityTemplate = new AbilityConfig
            {
                Name = "ChainGeneratedOnly",
                Effects = new List<EffectConfig>(),
                Trigger = new TriggerConfig()
            };
            var primaryEffectConfig = new EffectConfig
            {
                Type = EffectType.Shuffle,
                ChainedAbilities = new List<AbilityConfig> { chainedAbilityTemplate }
            };
            var primaryAbilityConfig = new AbilityConfig
            {
                Name = "PrimaryWithChain",
                Effects = new List<EffectConfig> { primaryEffectConfig },
                Trigger = new TriggerConfig()
            };
            var primaryAbility = new Ability(primaryAbilityConfig, conditionFactory, targetFactory, targetSelectionService, sourceCard);
            var primaryEffect = new Effect(
                primaryEffectConfig,
                player.PlayerId,
                new List<TargetInfo>(),
                primaryAbility.RuntimeId,
                initialEvent.EventId,
                sourceCard.InstanceId)
            {
                SourceAbility = primaryAbility,
                EffectIndex = 0
            };

            var generatedCommitProcessed = false;
            var generatedAbilityTriggeredEventId = Guid.Empty;

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { primaryAbility });

            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(primaryAbility, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { primaryEffect }));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(
                    It.Is<IAbility>(ability => ability is Ability && ((Ability)ability).Config.Name == "ChainGeneratedOnly"),
                    It.IsAny<GameContext>(),
                    false,
                    true,
                    It.IsAny<bool>()))
                .ReturnsAsync((IAbility ability, GameContext _, bool __, bool ___, bool ____) =>
                {
                    var commitEvent = new CommitFlagEvent(() => generatedCommitProcessed = true);
                    var triggeredEvent = new AbilityTriggeredEvent(ability, initialEvent);
                    generatedAbilityTriggeredEventId = triggeredEvent.EventId;

                    return AbilityProcessResult.Success(
                        new List<Effect>(),
                        new List<IGameEvent> { commitEvent, triggeredEvent });
                });

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult { Success = true });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.True(generatedCommitProcessed);
            Assert.NotEqual(Guid.Empty, generatedAbilityTriggeredEventId);
            Assert.Contains(result.ProcessedEvents, e => e.EventId == generatedAbilityTriggeredEventId);
        }

        [Fact]
        public async Task ProcessTransactionAsync_PreflightPrimaryAbilityWithFailedConditions_DoesNotEmitAbilityTriggeredEvent()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IValueSourceResolver, NullValueSourceResolver>();
            var conditionFactory = new NullConditionFactory();
            var targetFactory = new NullTargetFactory();
            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                new NullQueryExecutor(),
                _mockPendingActionService.Object);
            services.AddSingleton(targetSelectionService);
            services.AddSingleton<IAbilityFactory>(new TestAbilityFactory(conditionFactory, targetFactory, targetSelectionService));
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("preflight-failed-conditions", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var initialEvent = new ImmediatePlayerActionTestEvent(sourceCard);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                initialEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var chainedAbilityTemplate = new AbilityConfig
            {
                Name = "ChainTarget",
                Trigger = new TriggerConfig(),
                Targets = new Dictionary<string, TargetConfig>
                {
                    ["target"] = new TargetConfig
                    {
                        Query = new QueryConfig(),
                        PlayerChoiceOptions = new PlayerChoiceConfig
                        {
                            IsRequired = true,
                            MinCount = 1,
                            MaxCount = 1
                        }
                    }
                }
            };

            var primaryEffectConfig = new EffectConfig
            {
                Type = EffectType.Shuffle,
                ChainedAbilities = new List<AbilityConfig> { chainedAbilityTemplate }
            };
            var primaryAbilityConfig = new AbilityConfig
            {
                Name = "PrimaryWithFailedPreflight",
                Effects = new List<EffectConfig> { primaryEffectConfig },
                Trigger = new TriggerConfig()
            };
            var primaryAbility = new Ability(primaryAbilityConfig, conditionFactory, targetFactory, targetSelectionService, sourceCard);

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { primaryAbility });

            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(primaryAbility, It.IsAny<GameContext>(), true, false, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>()));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(primaryAbility, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>()));

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult { Success = true });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.DoesNotContain(result.ProcessedEvents, e => e is AbilityTriggeredEvent);
        }

        [Fact]
        public async Task ProcessTransactionAsync_SkipsChainedAbility_WhenSourceCardTransforms()
        {
            const int transformedDefinitionId = 910002;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IValueSourceResolver, NullValueSourceResolver>();

            var mockDataProvider = new Mock<IGameDataProvider>();
            mockDataProvider.SetupGet(p => p.IsInitialized).Returns(true);
            mockDataProvider.Setup(p => p.GetCardDefinition(transformedDefinitionId))
                .Returns(new CardDefinition
                {
                    Id = transformedDefinitionId,
                    Name = "Transformed Source",
                    Type = CardType.Follower,
                    Attack = 2,
                    Health = 2,
                    Cost = 1,
                    Description = "test"
                });
            mockDataProvider.Setup(p => p.GetAbilityConfigs(transformedDefinitionId))
                .Returns(new List<AbilityConfig>());
            services.AddSingleton(mockDataProvider.Object);

            var conditionFactory = new NullConditionFactory();
            var targetFactory = new NullTargetFactory();
            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                new NullQueryExecutor(),
                _mockPendingActionService.Object);
            services.AddSingleton(targetSelectionService);
            services.AddSingleton<IAbilityFactory>(new TestAbilityFactory(conditionFactory, targetFactory, targetSelectionService));

            var serviceProvider = services.BuildServiceProvider();
            GameDataProvider.SetInstanceForTesting(mockDataProvider.Object);

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("transform-chain-effect", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId)
            {
                CurrentZone = CardZone.Field,
                ZoneStayTurn = 1
            };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var initialEvent = new ImmediatePlayerActionTestEvent(sourceCard);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                initialEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.ContextAccessor.GameContext = gameContext;
            GameServiceProvider.Logger = NullLogger.Instance;

            var primaryChainedAbilityTemplate = new AbilityConfig
            {
                Name = "ChainAfterTransform",
                Effects = new List<EffectConfig>(),
                Trigger = new TriggerConfig()
            };
            var primaryEffectConfig = new EffectConfig
            {
                Type = EffectType.Shuffle,
                ChainedAbilities = new List<AbilityConfig> { primaryChainedAbilityTemplate }
            };
            var primaryAbilityConfig = new AbilityConfig
            {
                Name = "TransformingPrimary",
                Effects = new List<EffectConfig> { primaryEffectConfig },
                Trigger = new TriggerConfig()
            };
            var primaryAbility = new Ability(primaryAbilityConfig, conditionFactory, targetFactory, targetSelectionService, sourceCard);
            var primaryEffect = new Effect(
                primaryEffectConfig,
                player.PlayerId,
                new List<TargetInfo>(),
                primaryAbility.RuntimeId,
                initialEvent.EventId,
                sourceCard.InstanceId)
            {
                SourceAbility = primaryAbility,
                SourceAbilityStateVersion = primaryAbility.QueuedEffectStateVersion
            };

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { primaryAbility });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(primaryAbility, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { primaryEffect }));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(
                    It.Is<IAbility>(ability => ability is Ability && ((Ability)ability).Config.Name == "ChainAfterTransform"),
                    It.IsAny<GameContext>(),
                    false,
                    true,
                    It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>()));
            _mockAbilityManager
                .Setup(m => m.GetEffectiveAbilitiesForCard(sourceCard.InstanceId, It.IsAny<GameContext>()))
                .Returns(new List<IAbility> { primaryAbility });
            _mockAbilityManager
                .Setup(m => m.GetAbilitiesForCard(sourceCard.InstanceId))
                .Returns(new List<IAbility> { primaryAbility });
            _mockAbilityManager
                .Setup(m => m.UnregisterCardAbilities(sourceCard.InstanceId, It.IsAny<string>()));
            _mockAbilityManager
                .Setup(m => m.CreateAbilitiesForCard(sourceCard, It.IsAny<List<AbilityConfig>>(), null, gameState))
                .Returns(new List<Ability>());

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(primaryEffect, It.IsAny<GameContext>()))
                .Returns((Effect _, GameContext context) =>
                {
                    var transformEvent = new TransformEvent(sourceCard, transformedDefinitionId, sourceCard);
                    return new GameStateActionResult
                    {
                        Success = true,
                        GeneratedEvents = new List<IGameEvent> { transformEvent }
                    };
                });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Equal(transformedDefinitionId, sourceCard.BaseDefinitionId);
            _mockAbilityManager.Verify(
                m => m.ProcessSingleAbilityAsync(
                    It.Is<IAbility>(ability => ability is Ability && ((Ability)ability).Config.Name == "ChainAfterTransform"),
                    It.IsAny<GameContext>(),
                    false,
                    true,
                    It.IsAny<bool>()),
                Times.Never);
        }

        [Fact]
        public async Task ProcessTransactionAsync_DecisionAbilityGeneratedEvents_AreProcessed()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IValueSourceResolver, NullValueSourceResolver>();
            var conditionFactory = new NullConditionFactory();
            var targetFactory = new NullTargetFactory();
            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                new NullQueryExecutor(),
                _mockPendingActionService.Object);
            services.AddSingleton(targetSelectionService);
            services.AddSingleton<IAbilityFactory>(new TestAbilityFactory(conditionFactory, targetFactory, targetSelectionService));
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("decision-generated-events", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var initialEvent = new ImmediatePlayerActionTestEvent(sourceCard);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                initialEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var decisionAbilityConfig = new AbilityConfig { Name = "DecisionGeneratedOnly" };
            var chooseEffectConfig = new EffectConfig
            {
                Type = EffectType.Choose,
                Parameters = new Dictionary<string, ValueConfig>
                {
                    ["abilityConfigs"] = new StaticValueConfig
                    {
                        Value = new List<AbilityConfig> { decisionAbilityConfig }
                    },
                    ["selectionCount"] = new StaticValueConfig { Value = 1 }
                }
            };
            var primaryAbilityConfig = new AbilityConfig
            {
                Name = "PrimaryChoose",
                Effects = new List<EffectConfig> { chooseEffectConfig },
                Trigger = new TriggerConfig()
            };
            var primaryAbility = new Ability(primaryAbilityConfig, conditionFactory, targetFactory, targetSelectionService, sourceCard);
            var chooseEffect = new Effect(
                chooseEffectConfig,
                player.PlayerId,
                new List<TargetInfo>(),
                primaryAbility.RuntimeId,
                initialEvent.EventId,
                sourceCard.InstanceId)
            {
                SourceAbility = primaryAbility,
                EffectIndex = 0
            };

            var generatedCommitProcessed = false;
            var generatedAbilityTriggeredEventId = Guid.Empty;

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { primaryAbility });

            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(primaryAbility, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { chooseEffect }));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(
                    It.Is<IAbility>(ability => ability is Ability && ((Ability)ability).Config.Name == "DecisionGeneratedOnly"),
                    It.IsAny<GameContext>(),
                    false,
                    true,
                    It.IsAny<bool>()))
                .ReturnsAsync((IAbility ability, GameContext _, bool __, bool ___, bool ____) =>
                {
                    var commitEvent = new CommitFlagEvent(() => generatedCommitProcessed = true);
                    var triggeredEvent = new AbilityTriggeredEvent(ability, initialEvent);
                    generatedAbilityTriggeredEventId = triggeredEvent.EventId;

                    return AbilityProcessResult.Success(
                        new List<Effect>(),
                        new List<IGameEvent> { commitEvent, triggeredEvent });
                });

            _mockPendingActionService
                .Setup(s => s.WaitForPlayerChoiceAsync(It.IsAny<string>(), It.IsAny<PlayerChoiceRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, PlayerChoiceRequest request, CancellationToken __) =>
                {
                    return new PlayerChoiceResult
                    {
                        WasCancelled = false,
                        ChosenTargetIds = new List<Guid> { request.SelectableTargetIds.First() }
                    };
                });

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult { Success = true });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.True(generatedCommitProcessed);
            Assert.NotEqual(Guid.Empty, generatedAbilityTriggeredEventId);
            Assert.Contains(result.ProcessedEvents, e => e.EventId == generatedAbilityTriggeredEventId);
        }

        [Fact]
        public async Task ProcessTransactionAsync_PrimaryChoose_CancelledInPreflight_StopsTransaction()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IValueSourceResolver, NullValueSourceResolver>();
            var conditionFactory = new NullConditionFactory();
            var targetFactory = new NullTargetFactory();
            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                new NullQueryExecutor(),
                _mockPendingActionService.Object);
            services.AddSingleton(targetSelectionService);
            services.AddSingleton<IAbilityFactory>(new TestAbilityFactory(conditionFactory, targetFactory, targetSelectionService));
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("choose-cancel-preflight", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var initialEvent = new ImmediatePlayerActionTestEvent(sourceCard);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                initialEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var branchA = new AbilityConfig { Name = "OptionA" };
            var branchB = new AbilityConfig { Name = "OptionB" };

            var chooseEffectConfig = new EffectConfig
            {
                Type = EffectType.Choose,
                Parameters = new Dictionary<string, ValueConfig>
                {
                    ["abilityConfigs"] = new StaticValueConfig
                    {
                        Value = new List<AbilityConfig> { branchA, branchB }
                    },
                    ["selectionCount"] = new StaticValueConfig { Value = 1 }
                }
            };

            var abilityConfig = new AbilityConfig
            {
                Name = "ChooseAbility",
                Effects = new List<EffectConfig> { chooseEffectConfig },
                Trigger = new TriggerConfig()
            };

            var ability = new Ability(abilityConfig, conditionFactory, targetFactory, targetSelectionService, sourceCard);
            var chooseEffect = new Effect(
                chooseEffectConfig,
                player.PlayerId,
                new List<TargetInfo>(),
                ability.RuntimeId,
                initialEvent.EventId,
                sourceCard.InstanceId)
            {
                SourceAbility = ability,
                EffectIndex = 0
            };

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { ability });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(ability, It.IsAny<GameContext>(), true, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { chooseEffect }));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(It.IsAny<IAbility>(), It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>()));

            _mockPendingActionService
                .Setup(s => s.WaitForPlayerChoiceAsync(It.IsAny<string>(), It.IsAny<PlayerChoiceRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PlayerChoiceResult
                {
                    WasCancelled = true,
                    ChosenTargetIds = new List<Guid>()
                });

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult { Success = true });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsSuccess);
            Assert.True(result.IsCancelled);
            Assert.Empty(result.ProcessedEvents);
            _mockPendingActionService.Verify(
                s => s.WaitForPlayerChoiceAsync(It.IsAny<string>(), It.IsAny<PlayerChoiceRequest>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ProcessTransactionAsync_PrimaryChoose_RetryAfterCancelledPreflight_ReusesAbilityCleanly()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IValueSourceResolver, NullValueSourceResolver>();
            var conditionFactory = new NullConditionFactory();
            var targetFactory = new NullTargetFactory();
            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                new NullQueryExecutor(),
                _mockPendingActionService.Object);
            services.AddSingleton(targetSelectionService);
            services.AddSingleton<IAbilityFactory>(new TestAbilityFactory(conditionFactory, targetFactory, targetSelectionService));
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("choose-cancel-retry", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var firstEvent = new ImmediatePlayerActionTestEvent(sourceCard);
            var secondEvent = new ImmediatePlayerActionTestEvent(sourceCard);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var branchA = new AbilityConfig { Name = "OptionA" };
            var branchB = new AbilityConfig { Name = "OptionB" };

            var chooseEffectConfig = new EffectConfig
            {
                Type = EffectType.Choose,
                Parameters = new Dictionary<string, ValueConfig>
                {
                    ["abilityConfigs"] = new StaticValueConfig
                    {
                        Value = new List<AbilityConfig> { branchA, branchB }
                    },
                    ["selectionCount"] = new StaticValueConfig { Value = 1 }
                }
            };

            var abilityConfig = new AbilityConfig
            {
                Name = "ChooseAbility",
                Effects = new List<EffectConfig> { chooseEffectConfig },
                Trigger = new TriggerConfig()
            };

            var ability = new Ability(abilityConfig, conditionFactory, targetFactory, targetSelectionService, sourceCard);

            Effect BuildChooseEffect(IGameEvent triggeringEvent) => new(
                chooseEffectConfig,
                player.PlayerId,
                new List<TargetInfo>(),
                ability.RuntimeId,
                triggeringEvent.EventId,
                sourceCard.InstanceId)
            {
                SourceAbility = ability,
                EffectIndex = 0
            };

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync((IGameEvent evt, GameContext _, TriggerTiming timing) =>
                {
                    if (timing == TriggerTiming.On
                        && (ReferenceEquals(evt, firstEvent) || ReferenceEquals(evt, secondEvent)))
                    {
                        return new List<IAbility> { ability };
                    }

                    return new List<IAbility>();
                });

            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(ability, It.IsAny<GameContext>(), true, true, It.IsAny<bool>()))
                .ReturnsAsync((IAbility _, GameContext context, bool __, bool ___, bool ____) =>
                    AbilityProcessResult.Success(new List<Effect> { BuildChooseEffect(context.TriggeringEvent!) }));

            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(It.IsAny<IAbility>(), It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>()));

            var choiceResults = new Queue<Func<PlayerChoiceRequest, PlayerChoiceResult>>(new[]
            {
                new Func<PlayerChoiceRequest, PlayerChoiceResult>(_ => new PlayerChoiceResult
                {
                    WasCancelled = true,
                    ChosenTargetIds = new List<Guid>()
                }),
                new Func<PlayerChoiceRequest, PlayerChoiceResult>(request => new PlayerChoiceResult
                {
                    WasCancelled = false,
                    ChosenTargetIds = new List<Guid> { request.SelectableTargetIds.First() }
                })
            });

            _mockPendingActionService
                .Setup(s => s.WaitForPlayerChoiceAsync(It.IsAny<string>(), It.IsAny<PlayerChoiceRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, PlayerChoiceRequest request, CancellationToken __) =>
                {
                    var next = choiceResults.Dequeue();
                    return next(request);
                });

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult { Success = true });

            var firstContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                firstEvent,
                sourceCard);

            var secondContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                secondEvent,
                sourceCard);

            var firstResult = await _transactionService.ProcessTransactionAsync(firstContext, new List<IGameEvent> { firstEvent });
            var secondResult = await _transactionService.ProcessTransactionAsync(secondContext, new List<IGameEvent> { secondEvent });

            Assert.True(firstResult.IsSuccess);
            Assert.True(firstResult.IsCancelled);
            Assert.True(secondResult.IsSuccess, secondResult.ErrorMessage);
            Assert.False(secondResult.IsCancelled);
            Assert.Single(secondResult.ProcessedEvents.OfType<ChooseEvent>());
            _mockPendingActionService.Verify(
                s => s.WaitForPlayerChoiceAsync(It.IsAny<string>(), It.IsAny<PlayerChoiceRequest>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task ProcessTransactionAsync_PrimaryChoose_RelativeRewrite_CommitStartsFromPreparedInput()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IValueSourceResolver, NullValueSourceResolver>();
            var conditionFactory = new NullConditionFactory();
            var targetFactory = new NullTargetFactory();
            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                new NullQueryExecutor(),
                _mockPendingActionService.Object);
            services.AddSingleton(targetSelectionService);
            services.AddSingleton<IAbilityFactory>(new TestAbilityFactory(conditionFactory, targetFactory, targetSelectionService));
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("choose-relative-rewrite", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var initialEvent = new ImmediatePlayerActionTestEvent(sourceCard);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                initialEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var branchA = new AbilityConfig { Name = "OptionA" };
            var branchB = new AbilityConfig { Name = "OptionB" };
            var branchC = new AbilityConfig { Name = "OptionC" };

            var chooseEffectConfig = new EffectConfig
            {
                Type = EffectType.Choose,
                Parameters = new Dictionary<string, ValueConfig>
                {
                    ["abilityConfigs"] = new StaticValueConfig
                    {
                        Value = new List<AbilityConfig> { branchA, branchB, branchC }
                    },
                    ["selectionCount"] = new StaticValueConfig { Value = 1 }
                }
            };

            var abilityConfig = new AbilityConfig
            {
                Name = "ChooseAbility",
                Effects = new List<EffectConfig> { chooseEffectConfig },
                Trigger = new TriggerConfig()
            };

            var ability = new Ability(abilityConfig, conditionFactory, targetFactory, targetSelectionService, sourceCard);
            var chooseEffect = new Effect(
                chooseEffectConfig,
                player.PlayerId,
                new List<TargetInfo>(),
                ability.RuntimeId,
                initialEvent.EventId,
                sourceCard.InstanceId)
            {
                SourceAbility = ability,
                EffectIndex = 0
            };

            var interceptAbility = new Mock<IAbility>();
            interceptAbility.SetupGet(a => a.RuntimeId).Returns(Guid.NewGuid());
            interceptAbility.SetupGet(a => a.SourceCard).Returns(sourceCard);
            interceptAbility.SetupGet(a => a.KeywordSummary).Returns("RelativeIntercept");
            interceptAbility.SetupGet(a => a.TriggerConfig).Returns(new TriggerConfig());

            var interceptEffectConfig = new EffectConfig { Type = EffectType.RewriteEventParams };
            var interceptEffect = new Effect(
                interceptEffectConfig,
                player.PlayerId,
                null,
                interceptAbility.Object.RuntimeId,
                Guid.Empty,
                sourceCard.InstanceId)
            {
                SourceAbility = interceptAbility.Object
            };

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { ability });
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.Is<IGameEvent>(evt => evt is ChooseEvent), It.IsAny<GameContext>(), TriggerTiming.Before))
                .ReturnsAsync(new List<IAbility> { interceptAbility.Object });

            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(ability, It.IsAny<GameContext>(), true, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { chooseEffect }));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(interceptAbility.Object, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { interceptEffect }));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(
                    It.Is<IAbility>(candidate => !ReferenceEquals(candidate, interceptAbility.Object) && !ReferenceEquals(candidate, ability)),
                    It.IsAny<GameContext>(),
                    false,
                    true,
                    It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>()));

            var requestedSelectionCounts = new List<int>();
            _mockPendingActionService
                .Setup(s => s.WaitForPlayerChoiceAsync(It.IsAny<string>(), It.IsAny<PlayerChoiceRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, PlayerChoiceRequest request, CancellationToken __) =>
                {
                    requestedSelectionCounts.Add(request.MinSelectionCount);
                    return new PlayerChoiceResult
                    {
                        WasCancelled = false,
                        ChosenTargetIds = request.SelectableTargetIds.Take(request.MinSelectionCount).ToList()
                    };
                });

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.Is<Effect>(effect => effect.Type != EffectType.RewriteEventParams), It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult { Success = true });
            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.Is<Effect>(effect => effect.Type == EffectType.RewriteEventParams), It.IsAny<GameContext>()))
                .Returns((Effect _, GameContext context) =>
                {
                    var chooseEvent = Assert.IsType<ChooseEvent>(context.TriggeringEvent);
                    return new GameStateActionResult
                    {
                        Success = true,
                        EventIntercept = EventInterceptResult.RewriteParameter("SelectionCount", chooseEvent.SelectionCount + 1)
                    };
                });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.False(result.IsCancelled);
            var chooseEvent = Assert.Single(result.ProcessedEvents.OfType<ChooseEvent>());
            Assert.Equal(2, chooseEvent.SelectedOptionIds.Count);
            Assert.Equal(new[] { 2 }, requestedSelectionCounts);
        }

        [Fact]
        public async Task ProcessTransactionAsync_PrimaryChoose_CancelRetryWithRelativeRewrite_DoesNotLeakPreflightState()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IValueSourceResolver, NullValueSourceResolver>();
            var conditionFactory = new NullConditionFactory();
            var targetFactory = new NullTargetFactory();
            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                new NullQueryExecutor(),
                _mockPendingActionService.Object);
            services.AddSingleton(targetSelectionService);
            services.AddSingleton<IAbilityFactory>(new TestAbilityFactory(conditionFactory, targetFactory, targetSelectionService));
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("choose-relative-rewrite-cancel-retry", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var firstEvent = new ImmediatePlayerActionTestEvent(sourceCard);
            var secondEvent = new ImmediatePlayerActionTestEvent(sourceCard);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var branchA = new AbilityConfig { Name = "OptionA" };
            var branchB = new AbilityConfig { Name = "OptionB" };
            var branchC = new AbilityConfig { Name = "OptionC" };

            var chooseEffectConfig = new EffectConfig
            {
                Type = EffectType.Choose,
                Parameters = new Dictionary<string, ValueConfig>
                {
                    ["abilityConfigs"] = new StaticValueConfig
                    {
                        Value = new List<AbilityConfig> { branchA, branchB, branchC }
                    },
                    ["selectionCount"] = new StaticValueConfig { Value = 1 }
                }
            };

            var abilityConfig = new AbilityConfig
            {
                Name = "ChooseAbility",
                Effects = new List<EffectConfig> { chooseEffectConfig },
                Trigger = new TriggerConfig()
            };

            var ability = new Ability(abilityConfig, conditionFactory, targetFactory, targetSelectionService, sourceCard);

            Effect BuildChooseEffect(IGameEvent triggeringEvent) => new(
                chooseEffectConfig,
                player.PlayerId,
                new List<TargetInfo>(),
                ability.RuntimeId,
                triggeringEvent.EventId,
                sourceCard.InstanceId)
            {
                SourceAbility = ability,
                EffectIndex = 0
            };

            var interceptAbility = new Mock<IAbility>();
            interceptAbility.SetupGet(a => a.RuntimeId).Returns(Guid.NewGuid());
            interceptAbility.SetupGet(a => a.SourceCard).Returns(sourceCard);
            interceptAbility.SetupGet(a => a.KeywordSummary).Returns("RelativeIntercept");
            interceptAbility.SetupGet(a => a.TriggerConfig).Returns(new TriggerConfig());

            var interceptEffectConfig = new EffectConfig { Type = EffectType.RewriteEventParams };
            var interceptEffect = new Effect(
                interceptEffectConfig,
                player.PlayerId,
                null,
                interceptAbility.Object.RuntimeId,
                Guid.Empty,
                sourceCard.InstanceId)
            {
                SourceAbility = interceptAbility.Object
            };

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(firstEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { ability });
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(secondEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { ability });
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.Is<IGameEvent>(evt => evt is ChooseEvent), It.IsAny<GameContext>(), TriggerTiming.Before))
                .ReturnsAsync(new List<IAbility> { interceptAbility.Object });

            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(ability, It.IsAny<GameContext>(), true, true, It.IsAny<bool>()))
                .ReturnsAsync((IAbility _, GameContext context, bool __, bool ___, bool ____) =>
                    AbilityProcessResult.Success(new List<Effect> { BuildChooseEffect(context.TriggeringEvent!) }));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(interceptAbility.Object, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { interceptEffect }));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(
                    It.Is<IAbility>(candidate => !ReferenceEquals(candidate, interceptAbility.Object) && !ReferenceEquals(candidate, ability)),
                    It.IsAny<GameContext>(),
                    false,
                    true,
                    It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>()));

            var requestedSelectionCounts = new List<int>();
            var choiceResults = new Queue<Func<PlayerChoiceRequest, PlayerChoiceResult>>(new[]
            {
                new Func<PlayerChoiceRequest, PlayerChoiceResult>(request =>
                {
                    requestedSelectionCounts.Add(request.MinSelectionCount);
                    return new PlayerChoiceResult
                    {
                        WasCancelled = true,
                        ChosenTargetIds = new List<Guid>()
                    };
                }),
                new Func<PlayerChoiceRequest, PlayerChoiceResult>(request =>
                {
                    requestedSelectionCounts.Add(request.MinSelectionCount);
                    return new PlayerChoiceResult
                    {
                        WasCancelled = false,
                        ChosenTargetIds = request.SelectableTargetIds.Take(request.MinSelectionCount).ToList()
                    };
                })
            });

            _mockPendingActionService
                .Setup(s => s.WaitForPlayerChoiceAsync(It.IsAny<string>(), It.IsAny<PlayerChoiceRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, PlayerChoiceRequest request, CancellationToken __) =>
                {
                    var next = choiceResults.Dequeue();
                    return next(request);
                });

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.Is<Effect>(effect => effect.Type != EffectType.RewriteEventParams), It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult { Success = true });
            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.Is<Effect>(effect => effect.Type == EffectType.RewriteEventParams), It.IsAny<GameContext>()))
                .Returns((Effect _, GameContext context) =>
                {
                    var chooseEvent = Assert.IsType<ChooseEvent>(context.TriggeringEvent);
                    return new GameStateActionResult
                    {
                        Success = true,
                        EventIntercept = EventInterceptResult.RewriteParameter("SelectionCount", chooseEvent.SelectionCount + 1)
                    };
                });

            var firstContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                firstEvent,
                sourceCard);

            var secondContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                secondEvent,
                sourceCard);

            var firstResult = await _transactionService.ProcessTransactionAsync(firstContext, new List<IGameEvent> { firstEvent });
            var secondResult = await _transactionService.ProcessTransactionAsync(secondContext, new List<IGameEvent> { secondEvent });

            Assert.True(firstResult.IsSuccess);
            Assert.True(firstResult.IsCancelled);
            Assert.True(secondResult.IsSuccess, secondResult.ErrorMessage);
            Assert.False(secondResult.IsCancelled);
            Assert.Equal(new[] { 2, 2 }, requestedSelectionCounts);
            Assert.Equal(2, Assert.Single(secondResult.ProcessedEvents.OfType<ChooseEvent>()).SelectedOptionIds.Count);
        }

        [Fact]
        public async Task ProcessTransactionAsync_PrimaryChoose_CommitUsesPreflightInputSnapshot_WhenRuntimeInputDrifts()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IValueSourceResolver>(new FlakySelectionCountResolver(1, 2));
            var conditionFactory = new NullConditionFactory();
            var targetFactory = new NullTargetFactory();
            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                new NullQueryExecutor(),
                _mockPendingActionService.Object);
            services.AddSingleton(targetSelectionService);
            services.AddSingleton<IAbilityFactory>(new TestAbilityFactory(conditionFactory, targetFactory, targetSelectionService));
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("choose-input-drift", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var initialEvent = new ImmediatePlayerActionTestEvent(sourceCard);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                initialEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var branchA = new AbilityConfig { Name = "OptionA" };
            var branchB = new AbilityConfig { Name = "OptionB" };

            var chooseEffectConfig = new EffectConfig
            {
                Type = EffectType.Choose,
                Parameters = new Dictionary<string, ValueConfig>
                {
                    ["abilityConfigs"] = new StaticValueConfig
                    {
                        Value = new List<AbilityConfig> { branchA, branchB }
                    },
                    ["selectionCount"] = new FormulaValueConfig { Formula = "selectionCount" }
                }
            };

            var abilityConfig = new AbilityConfig
            {
                Name = "ChooseAbility",
                Effects = new List<EffectConfig> { chooseEffectConfig },
                Trigger = new TriggerConfig()
            };

            var ability = new Ability(abilityConfig, conditionFactory, targetFactory, targetSelectionService, sourceCard);
            var chooseEffect = new Effect(
                chooseEffectConfig,
                player.PlayerId,
                new List<TargetInfo>(),
                ability.RuntimeId,
                initialEvent.EventId,
                sourceCard.InstanceId)
            {
                SourceAbility = ability,
                EffectIndex = 0
            };

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { ability });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(ability, It.IsAny<GameContext>(), true, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { chooseEffect }));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(It.IsAny<IAbility>(), It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>()));

            _mockPendingActionService
                .Setup(s => s.WaitForPlayerChoiceAsync(It.IsAny<string>(), It.IsAny<PlayerChoiceRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, PlayerChoiceRequest request, CancellationToken __) => new PlayerChoiceResult
                {
                    WasCancelled = false,
                    ChosenTargetIds = new List<Guid> { request.SelectableTargetIds.First() }
                });

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult { Success = true });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.False(result.IsCancelled);

            var chooseEvent = Assert.Single(result.ProcessedEvents.OfType<ChooseEvent>());
            Assert.Single(chooseEvent.SelectedOptionIds);
        }

        [Fact]
        public async Task ProcessTransactionAsync_PrimaryChooseRepeatCount_PreflightsAllExecutionsWhenInputStable()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IValueSourceResolver, NullValueSourceResolver>();
            var conditionFactory = new NullConditionFactory();
            var targetFactory = new NullTargetFactory();
            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                new NullQueryExecutor(),
                _mockPendingActionService.Object);
            services.AddSingleton(targetSelectionService);
            services.AddSingleton<IAbilityFactory>(new TestAbilityFactory(conditionFactory, targetFactory, targetSelectionService));
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("choose-repeat-preflight", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var initialEvent = new ImmediatePlayerActionTestEvent(sourceCard);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                initialEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var branchA = new AbilityConfig { Name = "OptionA" };
            var branchB = new AbilityConfig { Name = "OptionB" };

            var chooseEffectConfig = new EffectConfig
            {
                Type = EffectType.Choose,
                RepeatCount = 2,
                Parameters = new Dictionary<string, ValueConfig>
                {
                    ["abilityConfigs"] = new StaticValueConfig
                    {
                        Value = new List<AbilityConfig> { branchA, branchB }
                    },
                    ["selectionCount"] = new StaticValueConfig { Value = 1 }
                }
            };

            var abilityConfig = new AbilityConfig
            {
                Name = "ChooseAbility",
                Effects = new List<EffectConfig> { chooseEffectConfig },
                Trigger = new TriggerConfig()
            };

            var ability = new Ability(abilityConfig, conditionFactory, targetFactory, targetSelectionService, sourceCard);
            var chooseEffect = new Effect(
                chooseEffectConfig,
                player.PlayerId,
                new List<TargetInfo>(),
                ability.RuntimeId,
                initialEvent.EventId,
                sourceCard.InstanceId)
            {
                SourceAbility = ability,
                EffectIndex = 0
            };

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { ability });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(It.IsAny<IAbility>(), It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>()));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(ability, It.IsAny<GameContext>(), true, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { chooseEffect }));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(ability, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { chooseEffect }));

            var cancellableFlags = new List<bool>();
            _mockPendingActionService
                .Setup(s => s.WaitForPlayerChoiceAsync(It.IsAny<string>(), It.IsAny<PlayerChoiceRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, PlayerChoiceRequest request, CancellationToken __) =>
                {
                    cancellableFlags.Add(request.IsCancellable);
                    return new PlayerChoiceResult
                    {
                        WasCancelled = false,
                        ChosenTargetIds = new List<Guid> { request.SelectableTargetIds.First() }
                    };
                });

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult { Success = true });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Equal(3, cancellableFlags.Count);
            Assert.All(cancellableFlags, Assert.True);
            Assert.Equal(3, result.ProcessedEvents.OfType<ChooseEvent>().Count());
        }

        [Fact]
        public async Task ProcessTransactionAsync_PrimaryChooseRepeatConditionDisabled_PreflightsAllExecutions()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IValueSourceResolver, NullValueSourceResolver>();
            var conditionFactory = new NullConditionFactory();
            var targetFactory = new NullTargetFactory();
            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                new NullQueryExecutor(),
                _mockPendingActionService.Object);
            services.AddSingleton(targetSelectionService);
            services.AddSingleton<IAbilityFactory>(new TestAbilityFactory(conditionFactory, targetFactory, targetSelectionService));
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("choose-repeat-condition-preflight", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var initialEvent = new ImmediatePlayerActionTestEvent(sourceCard);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                initialEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var branchA = new AbilityConfig { Name = "OptionA" };
            var branchB = new AbilityConfig { Name = "OptionB" };

            var chooseEffectConfig = new EffectConfig
            {
                Type = EffectType.Choose,
                RepeatCount = 2,
                RepeatCondition = new ConditionConfig { IsEnabled = false },
                Parameters = new Dictionary<string, ValueConfig>
                {
                    ["abilityConfigs"] = new StaticValueConfig
                    {
                        Value = new List<AbilityConfig> { branchA, branchB }
                    },
                    ["selectionCount"] = new StaticValueConfig { Value = 1 }
                }
            };

            var abilityConfig = new AbilityConfig
            {
                Name = "ChooseAbility",
                Effects = new List<EffectConfig> { chooseEffectConfig },
                Trigger = new TriggerConfig()
            };

            var ability = new Ability(abilityConfig, conditionFactory, targetFactory, targetSelectionService, sourceCard);
            var chooseEffect = new Effect(
                chooseEffectConfig,
                player.PlayerId,
                new List<TargetInfo>(),
                ability.RuntimeId,
                initialEvent.EventId,
                sourceCard.InstanceId)
            {
                SourceAbility = ability,
                EffectIndex = 0
            };

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { ability });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(It.IsAny<IAbility>(), It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>()));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(ability, It.IsAny<GameContext>(), true, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { chooseEffect }));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(ability, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { chooseEffect }));

            var cancellableFlags = new List<bool>();
            _mockPendingActionService
                .Setup(s => s.WaitForPlayerChoiceAsync(It.IsAny<string>(), It.IsAny<PlayerChoiceRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, PlayerChoiceRequest request, CancellationToken __) =>
                {
                    cancellableFlags.Add(request.IsCancellable);
                    return new PlayerChoiceResult
                    {
                        WasCancelled = false,
                        ChosenTargetIds = new List<Guid> { request.SelectableTargetIds.First() }
                    };
                });

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult { Success = true });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Equal(3, cancellableFlags.Count);
            Assert.Equal(3, cancellableFlags.Count(flag => flag));
            Assert.Equal(0, cancellableFlags.Count(flag => !flag));
            Assert.Equal(3, result.ProcessedEvents.OfType<ChooseEvent>().Count());
        }

        [Fact]
        public async Task ProcessTransactionAsync_PrimaryChooseRepeatConditionEnabled_FailsWithPreflightIncomplete()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IValueSourceResolver, NullValueSourceResolver>();
            var conditionFactory = new NullConditionFactory();
            var targetFactory = new NullTargetFactory();
            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                new NullQueryExecutor(),
                _mockPendingActionService.Object);
            services.AddSingleton(targetSelectionService);
            services.AddSingleton<IAbilityFactory>(new TestAbilityFactory(conditionFactory, targetFactory, targetSelectionService));
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("choose-repeat-condition-enabled-preflight", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var initialEvent = new ImmediatePlayerActionTestEvent(sourceCard);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                initialEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var branchA = new AbilityConfig { Name = "OptionA" };
            var branchB = new AbilityConfig { Name = "OptionB" };

            var chooseEffectConfig = new EffectConfig
            {
                Type = EffectType.Choose,
                RepeatCount = 2,
                RepeatCondition = new ConditionConfig { IsEnabled = true },
                Parameters = new Dictionary<string, ValueConfig>
                {
                    ["abilityConfigs"] = new StaticValueConfig
                    {
                        Value = new List<AbilityConfig> { branchA, branchB }
                    },
                    ["selectionCount"] = new StaticValueConfig { Value = 1 }
                }
            };

            var abilityConfig = new AbilityConfig
            {
                Name = "ChooseAbility",
                Effects = new List<EffectConfig> { chooseEffectConfig },
                Trigger = new TriggerConfig()
            };

            var ability = new Ability(abilityConfig, conditionFactory, targetFactory, targetSelectionService, sourceCard);
            var chooseEffect = new Effect(
                chooseEffectConfig,
                player.PlayerId,
                new List<TargetInfo>(),
                ability.RuntimeId,
                initialEvent.EventId,
                sourceCard.InstanceId)
            {
                SourceAbility = ability,
                EffectIndex = 0
            };

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { ability });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(It.IsAny<IAbility>(), It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>()));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(ability, It.IsAny<GameContext>(), true, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { chooseEffect }));

            var cancellableFlags = new List<bool>();
            _mockPendingActionService
                .Setup(s => s.WaitForPlayerChoiceAsync(It.IsAny<string>(), It.IsAny<PlayerChoiceRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, PlayerChoiceRequest request, CancellationToken __) =>
                {
                    cancellableFlags.Add(request.IsCancellable);
                    return new PlayerChoiceResult
                    {
                        WasCancelled = false,
                        ChosenTargetIds = new List<Guid> { request.SelectableTargetIds.First() }
                    };
                });

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult { Success = true });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.False(result.IsSuccess);
            Assert.Contains("PreflightIncomplete", result.ErrorMessage ?? string.Empty);
            Assert.Single(cancellableFlags);
            Assert.All(cancellableFlags, Assert.True);
        }

        [Fact]
        public async Task ProcessTransactionAsync_QueueDrainedDelay_DefersEventUntilQueuesEmpty()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(new Mock<IBuffService>().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player, opponent, player.PlayerId);
            gameState.AbilityManager = _mockAbilityManager.Object;
            gameState.BuffManager = new Mock<IBuffManager>().Object;

            var mapper = new Mock<IMapper>();
            var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                player.PlayerId,
                default);

            GameServiceProvider.ContextAccessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            GameServiceProvider.ContextAccessor.GameContext = gameContext;
            GameServiceProvider.Logger = NullLogger.Instance;

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());

            var processedOrder = new List<EventType>();
            _mockEventProcessor
                .Setup(p => p.ProcessEventAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>()))
                .Callback<IGameEvent, GameContext>((evt, ctx) => processedOrder.Add(evt.EventType))
                .ReturnsAsync(new ProcessorResult());

            var delayedEvent = new QueueDrainedTestEvent();
            var immediateEvent = new ImmediateTestEvent();

            await _transactionService.ProcessTransactionAsync(
                gameContext,
                new List<IGameEvent> { delayedEvent, immediateEvent });

            var filtered = processedOrder.Where(t => t == EventType.PlayCard || t == EventType.Draw).ToList();
            Assert.Equal(new List<EventType> { EventType.PlayCard, EventType.Draw }, filtered);
        }

        [Fact]
        public async Task ProcessTransactionAsync_EndTurnTransition_DefersPlayPointUpdatesUntilTurnEndEffectsComplete()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IAbilityManager>(_mockAbilityManager.Object);
            var buffService = CreateDefaultBuffService();
            buffService
                .Setup(s => s.CollectEndOfTurnExpiryEvents(It.IsAny<GameContext>()))
                .Returns(Array.Empty<BuffRemovedEvent>());
            buffService
                .Setup(s => s.CollectStartOfTurnExpiryEvents(It.IsAny<GameContext>(), It.IsAny<PlayerState>()))
                .Returns(Array.Empty<BuffRemovedEvent>());
            buffService
                .Setup(s => s.RefreshDerivedStateCache(It.IsAny<GameContext>(), It.IsAny<string>()));
            services.AddSingleton<IBuffService>(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };
            gameState.Phase = GamePhase.Main;

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var ability = new Mock<IAbility>();
            ability.SetupGet(a => a.RuntimeId).Returns(Guid.NewGuid());
            ability.SetupGet(a => a.DefinitionId).Returns(1001);
            ability.SetupGet(a => a.SourceCard).Returns(sourceCard);
            ability.SetupGet(a => a.AbilityKeywords).Returns(Array.Empty<AbilityKeywordConfig>());
            ability.SetupGet(a => a.TriggerConfig).Returns(new TriggerConfig { Timing = TriggerTiming.On });

            var effect = new Effect(
                new EffectConfig { Type = EffectType.Damage },
                player.PlayerId,
                new List<TargetInfo>(),
                ability.Object.RuntimeId,
                Guid.Empty,
                sourceCard.InstanceId);

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.Is<IGameEvent>(e => e is TurnEndEvent), It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { ability.Object });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(ability.Object, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { effect }));
            _mockAbilityManager
                .Setup(m => m.GetEffectiveAbilitiesForCard(It.IsAny<Guid>(), It.IsAny<GameContext>()))
                .Returns(new List<IAbility>());

            _mockEffectExecutor
                .Setup(executor => executor.ExecuteEffect(effect, It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult
                {
                    Success = true,
                    GeneratedEvents = new List<IGameEvent> { new ImmediateDrawTestEvent() }
                });

            var processedOrder = new List<EventType>();
            _mockEventProcessor
                .Setup(p => p.ProcessEventAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>()))
                .Callback<IGameEvent, GameContext>((evt, _) => processedOrder.Add(evt.EventType))
                .ReturnsAsync(new ProcessorResult());

            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            using var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var result = await _transactionService.ProcessTransactionAsync(
                gameContext,
                new List<IGameEvent> { new EndTurnRequestedEvent(player.PlayerId) });

            if (!result.IsSuccess)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Transaction failed. Error='{result.ErrorMessage ?? "<null>"}'. Processed={result.ProcessedEvents.Count}.");
            }

            var drawIndex = processedOrder.IndexOf(EventType.Draw);
            var cleanupIndex = processedOrder.IndexOf(EventType.TurnBoundaryCleanup);
            var ppMaxIndex = processedOrder.IndexOf(EventType.PpMaxUpdated);
            var ppIndex = processedOrder.IndexOf(EventType.PpUpdated);
            var turnStartIndex = processedOrder.IndexOf(EventType.TurnStart);

            Assert.True(drawIndex >= 0, "Expected a Draw event generated by turn-end triggered effects.");
            Assert.True(cleanupIndex > drawIndex, "TurnBoundaryCleanup should be processed after turn-end triggered effects.");
            Assert.True(ppMaxIndex > drawIndex, "PpMaxUpdated should be processed after turn-end triggered effects.");
            Assert.True(ppIndex > drawIndex, "PpUpdated should be processed after turn-end triggered effects.");
            Assert.True(ppMaxIndex > cleanupIndex, "PpMaxUpdated should be processed after end-of-turn cleanup.");
            Assert.True(ppIndex > cleanupIndex, "PpUpdated should be processed after end-of-turn cleanup.");
            Assert.True(turnStartIndex > cleanupIndex, "TurnStart should be processed after end-of-turn cleanup.");
        }

        [Fact]
        public async Task ProcessTransactionAsync_TurnEndDeferredTargets_ResolveBeforeComboCleanup()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            var buffService = CreateDefaultBuffService();
            buffService
                .Setup(s => s.CollectEndOfTurnExpiryEvents(It.IsAny<GameContext>()))
                .Returns(Array.Empty<BuffRemovedEvent>());
            buffService
                .Setup(s => s.CollectStartOfTurnExpiryEvents(It.IsAny<GameContext>(), It.IsAny<PlayerState>()))
                .Returns(Array.Empty<BuffRemovedEvent>());
            buffService
                .Setup(s => s.RefreshDerivedStateCache(It.IsAny<GameContext>(), It.IsAny<string>()));
            services.AddSingleton<IBuffService>(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid()) { Combo = 3 };
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };
            gameState.Phase = GamePhase.Main;

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var ability = new Mock<IAbility>();
            var abilityRuntimeId = Guid.NewGuid();
            ability.SetupGet(a => a.RuntimeId).Returns(abilityRuntimeId);
            ability.SetupGet(a => a.DefinitionId).Returns(1002);
            ability.SetupGet(a => a.SourceCard).Returns(sourceCard);
            ability.SetupGet(a => a.AbilityKeywords).Returns(Array.Empty<AbilityKeywordConfig>());
            ability.SetupGet(a => a.Keywords).Returns(Array.Empty<AbilityKeyword>());
            ability.SetupGet(a => a.KeywordSummary).Returns(AbilityKeyword.None.ToString());
            ability.SetupGet(a => a.TriggerConfig).Returns(new TriggerConfig { Timing = TriggerTiming.On });
            ability.SetupGet(a => a.Targets).Returns(new List<Target>());

            var effect = new Effect(
                new EffectConfig { Type = EffectType.Vanish },
                player.PlayerId,
                new List<TargetInfo>(),
                abilityRuntimeId,
                Guid.Empty,
                sourceCard.InstanceId)
            {
                ResolveTargetsOnExecution = true,
                SourceAbility = ability.Object
            };

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.Is<IGameEvent>(e => e is TurnEndEvent), It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { ability.Object });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(ability.Object, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { effect }));
            _mockAbilityManager
                .Setup(m => m.GetEffectiveAbilitiesForCard(It.IsAny<Guid>(), It.IsAny<GameContext>()))
                .Returns(new List<IAbility>());

            var observedComboDuringDeferredResolution = -1;
            ability
                .Setup(a => a.ResolveTargetsAsync(It.IsAny<GameContext>(), false))
                .Callback<GameContext, bool>((ctx, _) => observedComboDuringDeferredResolution = ctx.Owner.Combo)
                .ReturnsAsync(new TargetResolutionResult
                {
                    Status = ResolutionStatus.Completed,
                    ResolvedTargets = new Dictionary<string, List<TargetInfo>>()
                });

            _mockEffectExecutor
                .Setup(executor => executor.ExecuteEffect(effect, It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult { Success = true });

            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            using var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var result = await _transactionService.ProcessTransactionAsync(
                gameContext,
                new List<IGameEvent> { new EndTurnRequestedEvent(player.PlayerId) });

            if (!result.IsSuccess)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Transaction failed. Error='{result.ErrorMessage ?? "<null>"}'. Processed={result.ProcessedEvents.Count}.");
            }

            Assert.Equal(3, observedComboDuringDeferredResolution);
        }

        [Fact]
        public async Task ProcessTransactionAsync_EffectScopedDelay_DefersUntilEffectFinishes()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var initialEvent = new ImmediatePlayerActionTestEvent(sourceCard);
            var ability = new Mock<IAbility>();
            ability.SetupGet(a => a.RuntimeId).Returns(Guid.NewGuid());
            ability.SetupGet(a => a.SourceCard).Returns(sourceCard);

            var effect = new Effect(
                new EffectConfig { Type = EffectType.Damage },
                player.PlayerId,
                new List<TargetInfo>(),
                ability.Object.RuntimeId,
                initialEvent.EventId,
                sourceCard.InstanceId);

            var delayedEvent = new EffectScopedDelayTestEvent(effect.Id);
            var immediateEvent = new ImmediateDrawTestEvent();

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetEffectiveAbilitiesForCard(It.IsAny<Guid>(), It.IsAny<GameContext>()))
                .Returns(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { ability.Object });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(ability.Object, It.IsAny<GameContext>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { effect }));

            _mockEffectExecutor
                .Setup(executor => executor.ExecuteEffect(effect, It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult
                {
                    Success = true,
                    GeneratedEvents = new List<IGameEvent> { delayedEvent, immediateEvent }
                });

            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            using var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                initialEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            if (!result.IsSuccess)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Transaction failed. Error='{result.ErrorMessage ?? "<null>"}'. Processed={result.ProcessedEvents.Count}.");
            }
            var ordered = result.ProcessedEvents.ToList();
            var immediateIndex = ordered.FindIndex(evt => evt.EventId == immediateEvent.EventId);
            var delayedIndex = ordered.FindIndex(evt => evt.EventId == delayedEvent.EventId);
            Assert.True(immediateIndex >= 0);
            Assert.True(delayedIndex >= 0);
            Assert.True(immediateIndex < delayedIndex);
        }

        [Fact]
        public async Task ProcessTransactionAsync_LethalDamageFromSingleEffect_ProcessesAllDamageBeforeDestroy()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            var buffService = CreateDefaultBuffService();
            buffService.Setup(s => s.GetEffectiveMaxHealth(It.IsAny<CardInstance>(), It.IsAny<GameContext>())).Returns(2);
            services.AddSingleton(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            var targetA = new CardInstance(Guid.NewGuid(), 2, opponent.PlayerId) { CurrentZone = CardZone.Field };
            var targetB = new CardInstance(Guid.NewGuid(), 3, opponent.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            opponent.Field.Add(targetA);
            opponent.Field.Add(targetB);
            gameState.RegisterCardInstance(sourceCard);
            gameState.RegisterCardInstance(targetA);
            gameState.RegisterCardInstance(targetB);

            var initialEvent = new ImmediatePlayerActionTestEvent(sourceCard);
            var ability = new Mock<IAbility>();
            ability.SetupGet(a => a.RuntimeId).Returns(Guid.NewGuid());
            ability.SetupGet(a => a.SourceCard).Returns(sourceCard);

            var effect = new Effect(
                new EffectConfig { Type = EffectType.Damage },
                player.PlayerId,
                new List<TargetInfo> { TargetInfo.ForCard(targetA), TargetInfo.ForCard(targetB) },
                ability.Object.RuntimeId,
                initialEvent.EventId,
                sourceCard.InstanceId);

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetEffectiveAbilitiesForCard(It.IsAny<Guid>(), It.IsAny<GameContext>()))
                .Returns(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { ability.Object });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(ability.Object, It.IsAny<GameContext>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { effect }));

            _mockEffectExecutor
                .Setup(executor => executor.ExecuteEffect(effect, It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult
                {
                    Success = true,
                    GeneratedEvents = new List<IGameEvent>
                    {
                        new DamageEvent(targetA, 2, sourceCard, DamageKind.Ability),
                        new DamageEvent(targetB, 2, sourceCard, DamageKind.Ability)
                    }
                });

            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            using var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                initialEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            if (!result.IsSuccess)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Transaction failed. Error='{result.ErrorMessage ?? "<null>"}'. Processed={result.ProcessedEvents.Count}.");
            }

            var importantEvents = result.ProcessedEvents
                .Where(evt => evt is DamageEvent || evt is DestroyEvent)
                .ToList();

            Assert.Equal(4, importantEvents.Count);
            Assert.Equal(2, importantEvents.Count(evt => evt is DamageEvent));
            Assert.Equal(2, importantEvents.Count(evt => evt is DestroyEvent));

            var lastDamageIndex = importantEvents.FindLastIndex(evt => evt is DamageEvent);
            var firstDestroyIndex = importantEvents.FindIndex(evt => evt is DestroyEvent);
            if (lastDamageIndex >= firstDestroyIndex)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Order: {string.Join(" -> ", importantEvents.Select(evt => evt.EventType))}");
            }
        }

        [Fact]
        public async Task ProcessTransactionAsync_LethalDamageFromMultipleSingleTargetEffects_ProcessesAllDamageBeforeDestroy()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            var buffService = CreateDefaultBuffService();
            buffService.Setup(s => s.GetEffectiveMaxHealth(It.IsAny<CardInstance>(), It.IsAny<GameContext>())).Returns(2);
            services.AddSingleton(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            var targetA = new CardInstance(Guid.NewGuid(), 2, opponent.PlayerId) { CurrentZone = CardZone.Field };
            var targetB = new CardInstance(Guid.NewGuid(), 3, opponent.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            opponent.Field.Add(targetA);
            opponent.Field.Add(targetB);
            gameState.RegisterCardInstance(sourceCard);
            gameState.RegisterCardInstance(targetA);
            gameState.RegisterCardInstance(targetB);

            var initialEvent = new ImmediatePlayerActionTestEvent(sourceCard);
            var ability = new Mock<IAbility>();
            ability.SetupGet(a => a.RuntimeId).Returns(Guid.NewGuid());
            ability.SetupGet(a => a.SourceCard).Returns(sourceCard);

            var firstEffect = new Effect(
                new EffectConfig { Type = EffectType.Damage },
                player.PlayerId,
                new List<TargetInfo> { TargetInfo.ForCard(targetA) },
                ability.Object.RuntimeId,
                initialEvent.EventId,
                sourceCard.InstanceId);

            var secondEffect = new Effect(
                new EffectConfig { Type = EffectType.Damage },
                player.PlayerId,
                new List<TargetInfo> { TargetInfo.ForCard(targetB) },
                ability.Object.RuntimeId,
                initialEvent.EventId,
                sourceCard.InstanceId);

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetEffectiveAbilitiesForCard(It.IsAny<Guid>(), It.IsAny<GameContext>()))
                .Returns(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { ability.Object });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(ability.Object, It.IsAny<GameContext>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { firstEffect, secondEffect }));

            _mockEffectExecutor
                .Setup(executor => executor.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()))
                .Returns((Effect executedEffect, GameContext _) =>
                {
                    if (ReferenceEquals(executedEffect, firstEffect))
                    {
                        return new GameStateActionResult
                        {
                            Success = true,
                            GeneratedEvents = new List<IGameEvent> { new DamageEvent(targetA, 2, sourceCard, DamageKind.Ability) }
                        };
                    }

                    if (ReferenceEquals(executedEffect, secondEffect))
                    {
                        return new GameStateActionResult
                        {
                            Success = true,
                            GeneratedEvents = new List<IGameEvent> { new DamageEvent(targetB, 2, sourceCard, DamageKind.Ability) }
                        };
                    }

                    return new GameStateActionResult { Success = true };
                });

            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            using var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                initialEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            if (!result.IsSuccess)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Transaction failed. Error='{result.ErrorMessage ?? "<null>"}'. Processed={result.ProcessedEvents.Count}.");
            }

            var importantEvents = result.ProcessedEvents
                .Where(evt => evt is DamageEvent || evt is DestroyEvent)
                .ToList();

            Assert.Equal(4, importantEvents.Count);
            Assert.IsType<DamageEvent>(importantEvents[0]);
            Assert.IsType<DamageEvent>(importantEvents[1]);
            Assert.IsType<DestroyEvent>(importantEvents[2]);
            Assert.IsType<DestroyEvent>(importantEvents[3]);
        }

        [Fact]
        public async Task ProcessTransactionAsync_DecisionBranches_InsertDestroySettlementBetweenBranches()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            var buffService = CreateDefaultBuffService();
            buffService.Setup(s => s.GetEffectiveMaxHealth(It.IsAny<CardInstance>(), It.IsAny<GameContext>())).Returns(2);
            services.AddSingleton<IBuffService>(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IValueSourceResolver, NullValueSourceResolver>();
            var conditionFactory = new NullConditionFactory();
            var targetFactory = new NullTargetFactory();
            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                new NullQueryExecutor(),
                _mockPendingActionService.Object);
            services.AddSingleton(targetSelectionService);
            services.AddSingleton<IAbilityFactory>(new TestAbilityFactory(conditionFactory, targetFactory, targetSelectionService));
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("decision-branch-destroy-settlement", player, opponent, player.PlayerId)
            {
                AbilityManager = _mockAbilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            var target = new CardInstance(Guid.NewGuid(), 2, opponent.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(sourceCard);
            opponent.Field.Add(target);
            gameState.RegisterCardInstance(sourceCard);
            gameState.RegisterCardInstance(target);

            var initialEvent = new ImmediatePlayerActionTestEvent(sourceCard);
            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            using var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                initialEvent,
                sourceCard);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var branchDamageEffectConfig = new EffectConfig { Type = EffectType.Damage };
            var branchA = new AbilityConfig
            {
                Name = "DecisionDamage",
                Effects = new List<EffectConfig> { branchDamageEffectConfig },
                Trigger = new TriggerConfig()
            };
            var branchB = new AbilityConfig
            {
                Name = "DecisionObserve",
                Trigger = new TriggerConfig()
            };

            var chooseEffectConfig = new EffectConfig
            {
                Type = EffectType.Choose,
                Parameters = new Dictionary<string, ValueConfig>
                {
                    ["abilityConfigs"] = new StaticValueConfig
                    {
                        Value = new List<AbilityConfig> { branchA, branchB }
                    },
                    ["selectionCount"] = new StaticValueConfig { Value = 2 }
                }
            };

            var rootAbilityConfig = new AbilityConfig
            {
                Name = "ChooseAbility",
                Effects = new List<EffectConfig> { chooseEffectConfig },
                Trigger = new TriggerConfig()
            };

            var rootAbility = new Ability(rootAbilityConfig, conditionFactory, targetFactory, targetSelectionService, sourceCard);
            var chooseEffect = new Effect(
                chooseEffectConfig,
                player.PlayerId,
                new List<TargetInfo>(),
                rootAbility.RuntimeId,
                initialEvent.EventId,
                sourceCard.InstanceId)
            {
                SourceAbility = rootAbility,
                EffectIndex = 0
            };

            CardZone? branchBObservedZone = null;

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetEffectiveAbilitiesForCard(It.IsAny<Guid>(), It.IsAny<GameContext>()))
                .Returns(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(initialEvent, It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { rootAbility });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(rootAbility, It.IsAny<GameContext>(), true, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { chooseEffect }));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(rootAbility, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { chooseEffect }));
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(
                    It.Is<IAbility>(candidate => candidate is Ability && ((Ability)candidate).Config.Name == "DecisionDamage"),
                    It.IsAny<GameContext>(),
                    false,
                    true,
                    It.IsAny<bool>()))
                .ReturnsAsync((IAbility candidate, GameContext context, bool _, bool __, bool ___) =>
                {
                    var decisionAbility = (Ability)candidate;
                    var damageEffect = new Effect(
                        branchDamageEffectConfig,
                        player.PlayerId,
                        new List<TargetInfo> { TargetInfo.ForCard(target) },
                        decisionAbility.RuntimeId,
                        context.TriggeringEvent?.EventId ?? Guid.Empty,
                        sourceCard.InstanceId)
                    {
                        SourceAbility = decisionAbility,
                        EffectIndex = 0
                    };

                    return AbilityProcessResult.Success(new List<Effect> { damageEffect });
                });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(
                    It.Is<IAbility>(candidate => candidate is Ability && ((Ability)candidate).Config.Name == "DecisionObserve"),
                    It.IsAny<GameContext>(),
                    false,
                    true,
                    It.IsAny<bool>()))
                .ReturnsAsync((IAbility _, GameContext __, bool ___, bool ____, bool _____) =>
                {
                    branchBObservedZone = target.CurrentZone;
                    return AbilityProcessResult.Success(new List<Effect>());
                });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(
                    It.Is<IAbility>(candidate => candidate is Ability
                        && ((Ability)candidate).Config.Name != rootAbility.Config.Name
                        && ((Ability)candidate).Config.Name != "DecisionDamage"
                        && ((Ability)candidate).Config.Name != "DecisionObserve"),
                    It.IsAny<GameContext>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>()));

            _mockPendingActionService
                .Setup(s => s.WaitForPlayerChoiceAsync(It.IsAny<string>(), It.IsAny<PlayerChoiceRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, PlayerChoiceRequest request, CancellationToken __) =>
                {
                    return new PlayerChoiceResult
                    {
                        WasCancelled = false,
                        ChosenTargetIds = request.SelectableTargetIds.Take(2).ToList()
                    };
                });

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.Is<Effect>(effect => effect.Type == EffectType.Damage), It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult
                {
                    Success = true,
                    GeneratedEvents = new List<IGameEvent> { new DamageEvent(target, 2, sourceCard, DamageKind.Ability) }
                });
            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.Is<Effect>(effect => effect.Type != EffectType.Damage), It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult { Success = true });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { initialEvent });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Equal(CardZone.Graveyard, branchBObservedZone);
        }

        [Fact]
        public async Task ProcessTransactionAsync_EmitsCardInstanceUpdatedForBuffApplied()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            var buffService = CreateDefaultBuffService();
            services.AddSingleton(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player, opponent, player.PlayerId);
            gameState.AbilityManager = _mockAbilityManager.Object;
            gameState.BuffManager = new Mock<IBuffManager>().Object;

            var targetCard = new CardInstance(Guid.NewGuid(), 1, opponent.PlayerId) { CurrentZone = CardZone.Field };
            opponent.Field.Add(targetCard);
            gameState.RegisterCardInstance(targetCard);

            var buff = new Buff(Guid.Empty, targetCard.InstanceId, 1, new List<IBuffEffect>());
            var buffAppliedEvent = new BuffAppliedEvent(buff, null, null);

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());

            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            using var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                buffAppliedEvent,
                null);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { buffAppliedEvent });

            Assert.True(result.IsSuccess);
            var updateEvent = result.ProcessedEvents.OfType<CardInstanceUpdatedEvent>().Single();
            Assert.Contains(updateEvent.UpdatedCards, card => card.InstanceId == targetCard.InstanceId);
        }

        [Fact]
        public async Task ProcessTransactionAsync_ReplacesDestroyEventWithVanish()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            var buffService = CreateDefaultBuffService();
            services.AddSingleton(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player, opponent, player.PlayerId);
            gameState.AbilityManager = _mockAbilityManager.Object;
            gameState.BuffManager = new Mock<IBuffManager>().Object;

            var attackerCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field };
            var targetCard = new CardInstance(Guid.NewGuid(), 2, opponent.PlayerId) { CurrentZone = CardZone.Field };
            player.Field.Add(attackerCard);
            opponent.Field.Add(targetCard);

            var destroyEvent = new DestroyEvent(targetCard, attackerCard, EventReasonSource.Ability);
            var mapper = new Mock<IMapper>();
            var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                player.PlayerId,
                default,
                destroyEvent,
                attackerCard);

            GameServiceProvider.ContextAccessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            GameServiceProvider.Logger = NullLogger.Instance;

            var interceptEffectConfig = new EffectConfig { Type = EffectType.ReplaceEvent };
            var interceptEffect = new Effect(interceptEffectConfig, player.PlayerId, null, null, destroyEvent.EventId, attackerCard.InstanceId);

            var interceptAbility = new Mock<IAbility>();
            interceptAbility.SetupGet(a => a.RuntimeId).Returns(Guid.NewGuid());
            interceptAbility.SetupGet(a => a.SourceCard).Returns(attackerCard);

            _mockAbilityManager.Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager.Setup(m => m.GetTriggeredAbilitiesAsync(destroyEvent, It.IsAny<GameContext>(), TriggerTiming.Before))
                .ReturnsAsync(new List<IAbility> { interceptAbility.Object });
            _mockAbilityManager.Setup(m => m.ProcessSingleAbilityAsync(interceptAbility.Object, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { interceptEffect }));

            _mockEffectExecutor.Setup(e => e.ExecuteEffect(interceptEffect, It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult
                {
                    Success = true,
                    EventIntercept = EventInterceptResult.ReplaceEvent(
                        EventType.Vanish,
                        new Dictionary<string, object?>
                        {
                            ["VanishedCard"] = targetCard,
                            ["OriginalZone"] = CardZone.Field,
                            ["VanishReason"] = "LeaveFieldIntercept"
                        },
                        "LeaveFieldIntercept")
                });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new List<IGameEvent> { destroyEvent });

            Assert.True(result.IsSuccess);
            Assert.DoesNotContain(result.ProcessedEvents, e => e is DestroyEvent);
            var vanishEvent = result.ProcessedEvents.OfType<CardVanishedEvent>().Single();
            Assert.Equal(destroyEvent.EventId, vanishEvent.EventId);
        }

        [Fact]
        public async Task CombatResolution_SkipsDamage_WhenPreCombatEffectRemovesDefender()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            var buffService = CreateDefaultBuffService();
            services.AddSingleton(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player, opponent, player.PlayerId)
            {
                CurrentPlayerId = player.PlayerId
            };
            gameState.AbilityManager = _mockAbilityManager.Object;
            gameState.BuffManager = new Mock<IBuffManager>().Object;

            var attacker = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field, ZoneStayTurn = 1 };
            var defender = new CardInstance(Guid.NewGuid(), 2, opponent.PlayerId) { CurrentZone = CardZone.Field, ZoneStayTurn = 1 };
            player.Field.Add(attacker);
            opponent.Field.Add(defender);
            gameState.RegisterCardInstance(attacker);
            gameState.RegisterCardInstance(defender);

            var dto = new AttackActionDto { AttackerUid = attacker.InstanceId, TargetUid = defender.InstanceId };
            var attackRequest = new AttackRequestedEvent(player.PlayerId, dto);

            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            using var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                attackRequest,
                attacker);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var mockAbility = new Mock<IAbility>();
            mockAbility.SetupGet(a => a.SourceCard).Returns(attacker);

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.Is<IGameEvent>(e => e is AttackEvent), It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { mockAbility.Object });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(mockAbility.Object, It.IsAny<GameContext>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync((IAbility ability, GameContext context, bool _, bool __, bool ___) =>
                {
                    var effectConfig = new EffectConfig { Type = EffectType.Vanish };
                    var effect = new Effect(effectConfig, player.PlayerId, null, null, context.TriggeringEvent?.EventId ?? Guid.Empty, attacker.InstanceId);
                    return AbilityProcessResult.Success(new List<Effect> { effect });
                });

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()))
                .Returns((Effect effect, GameContext context) =>
                {
                    var vanishEvent = new CardVanishedEvent(defender, defender.CurrentZone, EventReasonSource.Ability);
                    return new GameStateActionResult
                    {
                        Success = true,
                        GeneratedEvents = new List<IGameEvent> { vanishEvent }
                    };
                });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new[] { attackRequest });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Empty(result.ProcessedEvents.OfType<DamageEvent>());
        }

        [Fact]
        public async Task CombatResolution_SkipsDamage_WhenPreCombatEffectMovesDefenderOutAndBackToField()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            var buffService = CreateDefaultBuffService();
            services.AddSingleton(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player, opponent, player.PlayerId)
            {
                CurrentPlayerId = player.PlayerId
            };
            gameState.AbilityManager = _mockAbilityManager.Object;
            gameState.BuffManager = new Mock<IBuffManager>().Object;

            var attacker = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field, ZoneStayTurn = 1 };
            var defender = new CardInstance(Guid.NewGuid(), 2, opponent.PlayerId) { CurrentZone = CardZone.Field, ZoneStayTurn = 1 };
            player.Field.Add(attacker);
            opponent.Field.Add(defender);
            gameState.RegisterCardInstance(attacker);
            gameState.RegisterCardInstance(defender);

            var dto = new AttackActionDto { AttackerUid = attacker.InstanceId, TargetUid = defender.InstanceId };
            var attackRequest = new AttackRequestedEvent(player.PlayerId, dto);

            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            using var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                attackRequest,
                attacker);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var mockAbility = new Mock<IAbility>();
            mockAbility.SetupGet(a => a.SourceCard).Returns(attacker);

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.Is<IGameEvent>(e => e is AttackEvent), It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { mockAbility.Object });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(mockAbility.Object, It.IsAny<GameContext>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync((IAbility _, GameContext context, bool __, bool ___, bool ____) =>
                {
                    var effectConfig = new EffectConfig { Type = EffectType.Return };
                    var effect = new Effect(effectConfig, player.PlayerId, null, null, context.TriggeringEvent?.EventId ?? Guid.Empty, attacker.InstanceId);
                    return AbilityProcessResult.Success(new List<Effect> { effect });
                });

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()))
                .Returns((Effect _, GameContext __) =>
                {
                    var moveOut = new CardMovedEvent(defender, CardZone.Field, CardZone.Hand, CardMoveReason.ReturnedToHand);
                    var moveBack = new CardMovedEvent(defender, CardZone.Hand, CardZone.Field, CardMoveReason.Unknown);
                    return new GameStateActionResult
                    {
                        Success = true,
                        GeneratedEvents = new List<IGameEvent> { moveOut, moveBack }
                    };
                });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new[] { attackRequest });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Empty(result.ProcessedEvents.OfType<DamageEvent>());
            Assert.Equal(CardZone.Field, defender.CurrentZone);
        }

        [Fact]
        public async Task CombatResolution_SkipsDamage_WhenPreCombatEffectTransformsDefender()
        {
            const int transformedDefinitionId = 999001;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IAbilityManager>(_mockAbilityManager.Object);
            var buffService = CreateDefaultBuffService();
            services.AddSingleton(buffService.Object);

            var mockDataProvider = new Mock<IGameDataProvider>();
            mockDataProvider.SetupGet(p => p.IsInitialized).Returns(true);
            mockDataProvider.Setup(p => p.GetCardDefinition(transformedDefinitionId))
                .Returns(new CardDefinition
                {
                    Id = transformedDefinitionId,
                    Name = "Transformed Defender",
                    Type = CardType.Follower,
                    Attack = 2,
                    Health = 2,
                    Cost = 1,
                    Description = "test"
                });
            mockDataProvider.Setup(p => p.GetAbilityConfigs(transformedDefinitionId))
                .Returns(new List<AbilityConfig>());
            services.AddSingleton(mockDataProvider.Object);

            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();
            GameDataProvider.SetInstanceForTesting(mockDataProvider.Object);

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player, opponent, player.PlayerId)
            {
                CurrentPlayerId = player.PlayerId
            };
            gameState.AbilityManager = _mockAbilityManager.Object;
            gameState.BuffManager = new Mock<IBuffManager>().Object;

            var attacker = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field, ZoneStayTurn = 1 };
            var defender = new CardInstance(Guid.NewGuid(), 2, opponent.PlayerId) { CurrentZone = CardZone.Field, ZoneStayTurn = 1 };
            player.Field.Add(attacker);
            opponent.Field.Add(defender);
            gameState.RegisterCardInstance(attacker);
            gameState.RegisterCardInstance(defender);

            var dto = new AttackActionDto { AttackerUid = attacker.InstanceId, TargetUid = defender.InstanceId };
            var attackRequest = new AttackRequestedEvent(player.PlayerId, dto);

            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            using var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                attackRequest,
                attacker);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            var mockAbility = new Mock<IAbility>();
            mockAbility.SetupGet(a => a.SourceCard).Returns(attacker);

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.Is<IGameEvent>(e => e is AttackEvent), It.IsAny<GameContext>(), TriggerTiming.On))
                .ReturnsAsync(new List<IAbility> { mockAbility.Object });
            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(mockAbility.Object, It.IsAny<GameContext>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync((IAbility _, GameContext context, bool __, bool ___, bool ____) =>
                {
                    var effectConfig = new EffectConfig { Type = EffectType.Transform };
                    var effect = new Effect(effectConfig, player.PlayerId, null, null, context.TriggeringEvent?.EventId ?? Guid.Empty, attacker.InstanceId);
                    return AbilityProcessResult.Success(new List<Effect> { effect });
                });

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()))
                .Returns((Effect _, GameContext __) =>
                {
                    var transformEvent = new TransformEvent(defender, transformedDefinitionId);
                    return new GameStateActionResult
                    {
                        Success = true,
                        GeneratedEvents = new List<IGameEvent> { transformEvent }
                    };
                });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new[] { attackRequest });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Empty(result.ProcessedEvents.OfType<DamageEvent>());
            Assert.Equal(transformedDefinitionId, defender.BaseDefinitionId);
            Assert.Equal(CardZone.Field, defender.CurrentZone);
        }

        [Fact]
        public async Task CombatResolution_AppliesDamage_WhenParticipantsRemain()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            var buffService = CreateDefaultBuffService();
            services.AddSingleton(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player, opponent, player.PlayerId)
            {
                CurrentPlayerId = player.PlayerId
            };
            gameState.AbilityManager = _mockAbilityManager.Object;
            gameState.BuffManager = new Mock<IBuffManager>().Object;

            var attacker = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field, ZoneStayTurn = 1 };
            var defender = new CardInstance(Guid.NewGuid(), 2, opponent.PlayerId) { CurrentZone = CardZone.Field, ZoneStayTurn = 1 };
            player.Field.Add(attacker);
            opponent.Field.Add(defender);
            gameState.RegisterCardInstance(attacker);
            gameState.RegisterCardInstance(defender);

            var dto = new AttackActionDto { AttackerUid = attacker.InstanceId, TargetUid = defender.InstanceId };
            var attackRequest = new AttackRequestedEvent(player.PlayerId, dto);

            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            using var gameContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player.PlayerId,
                default,
                attackRequest,
                attacker);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new[] { attackRequest });

            Assert.True(result.IsSuccess);
            Assert.Contains(result.ProcessedEvents.OfType<DamageEvent>(), e => e.DamageTarget?.InstanceId == defender.InstanceId);
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

        private sealed class FlakySelectionCountResolver : IValueSourceResolver
        {
            private readonly Queue<int> _selectionCounts;

            public FlakySelectionCountResolver(params int[] selectionCounts)
            {
                _selectionCounts = new Queue<int>(selectionCounts ?? Array.Empty<int>());
            }

            public object? Resolve(ValueConfig valueConfig, GameContext context)
            {
                return valueConfig switch
                {
                    StaticValueConfig staticValue => staticValue.Value,
                    FormulaValueConfig formulaValue when string.Equals(formulaValue.Formula, "selectionCount", StringComparison.Ordinal)
                        => ResolveFormulaValue(formulaValue.Formula, context),
                    _ => null
                };
            }

            public object? ResolveFormulaValue(string formula, GameContext context)
            {
                if (!string.Equals(formula, "selectionCount", StringComparison.Ordinal))
                {
                    return null;
                }

                if (_selectionCounts.Count == 0)
                {
                    return 1;
                }

                return _selectionCounts.Dequeue();
            }

            public object? ResolveFormulaValueWithTrace(string formula, GameContext context, IDslTraceCollector traceCollector)
                => ResolveFormulaValue(formula, context);
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

        private sealed class NullQueryExecutor : IQueryExecutor
        {
            public QueryResult ExecuteQuery(string dsl, GameContext context, Dictionary<string, List<TargetInfo>>? resolvedTargets = null)
                => new() { Success = false, ErrorMessage = "Not used in this test" };

            public QueryResult ExecuteQuery(QueryConfig queryConfig, GameContext context, Dictionary<string, List<TargetInfo>>? resolvedTargets = null)
                => new() { Success = false, ErrorMessage = "Not used in this test" };
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
                => abilityConfigs.Select(cfg => CreateAbility(cfg, cardInstance, sourceBuffId)).ToList();
        }
    }
}
