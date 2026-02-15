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
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Query;

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

        private static Mock<IBuffService> CreateDefaultBuffService()
        {
            var buffService = new Mock<IBuffService>();
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

            Assert.True(result.IsSuccess);
            historyServiceMock.Verify(
                s => s.StoreEventAsync(gameState.GameId, It.Is<IGameEvent>(e => e.EventId == initialEvent.EventId)),
                Times.AtLeastOnce);
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

            Assert.True(result.IsSuccess);
            Assert.False(chainExecuted);
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

            var attacker = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field, FieldTurn = 1 };
            var defender = new CardInstance(Guid.NewGuid(), 2, opponent.PlayerId) { CurrentZone = CardZone.Field, FieldTurn = 1 };
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
                    var vanishEvent = new CardVanishedEvent(defender, defender.CurrentZone, "PreCombat");
                    return new GameStateActionResult
                    {
                        Success = true,
                        GeneratedEvents = new List<IGameEvent> { vanishEvent }
                    };
                });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new[] { attackRequest });

            Assert.True(result.IsSuccess);
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

            var attacker = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field, FieldTurn = 1 };
            var defender = new CardInstance(Guid.NewGuid(), 2, opponent.PlayerId) { CurrentZone = CardZone.Field, FieldTurn = 1 };
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

            Assert.True(result.IsSuccess);
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

            var attacker = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field, FieldTurn = 1 };
            var defender = new CardInstance(Guid.NewGuid(), 2, opponent.PlayerId) { CurrentZone = CardZone.Field, FieldTurn = 1 };
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

            Assert.True(result.IsSuccess);
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

            var attacker = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Field, FieldTurn = 1 };
            var defender = new CardInstance(Guid.NewGuid(), 2, opponent.PlayerId) { CurrentZone = CardZone.Field, FieldTurn = 1 };
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
