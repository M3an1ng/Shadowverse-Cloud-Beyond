using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.GameFlow;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Options;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Dtos.Enums;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class TransactionSameNameTriggerLimitTests
    {
        private readonly Mock<ILogger<GameActionTransactionService>> _mockLogger = new();
        private readonly Mock<IAbilityManager> _mockAbilityManager = new();
        private readonly Mock<IEventProcessor> _mockEventProcessor = new();
        private readonly Mock<IPendingActionService> _mockPendingActionService = new();
        private readonly Mock<IEffectExecutor> _mockEffectExecutor = new();
        private readonly Mock<IClientMethodMapper> _mockClientMethodMapper = new();
        private readonly Mock<ICommittedEventJournal> _mockEventJournal = new();
        private readonly Mock<ISnapshotStore> _mockSnapshotStore = new();
        private readonly Mock<IMetaProgressionService> _mockMetaProgressionService = new();
        private readonly Mock<IWinrateStatsService> _mockWinrateStatsService = new();
        private readonly GameActionTransactionService _transactionService;

        public TransactionSameNameTriggerLimitTests()
        {
            var replayOptions = Microsoft.Extensions.Options.Options.Create(new ReplayOptions());
            var recordedMapper = new JsonRecordedEventMapper();
            var rawEventMapper = new JsonGameEventRawEventMapper();

            _mockEventProcessor
                .Setup(p => p.ProcessEventAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>()))
                .ReturnsAsync(new ProcessorResult());

            _mockEffectExecutor
                .Setup(e => e.ExecuteEffect(It.IsAny<Effect>(), It.IsAny<GameContext>()))
                .Returns(new GameStateActionResult { Success = true });

            _mockEventJournal
                .Setup(j => j.AppendBundleAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<IReadOnlyList<ShadowverseBeyond.Server.Dtos.Replay.CommittedEventWriteDto>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ShadowverseBeyond.Server.Dtos.Replay.CommittedBundleAppendResult(1, 1, 1));

            _mockSnapshotStore
                .Setup(s => s.SaveSnapshotAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<ShadowverseBeyond.Server.Dtos.GameStateDto>(),
                    It.IsAny<CancellationToken>()))
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
                replayOptions);
        }

        [Fact]
        public async Task ProcessTransactionAsync_SameNameLimitEnabled_SameOwnerAndDefinition_TriggersOnce()
        {
            var contextBundle = CreateContextBundle();
            using var gameContext = contextBundle.Context;

            var ability1 = CreateAbilityMock(contextBundle.SourceCard, 7001, true, "A1");
            var ability2 = CreateAbilityMock(contextBundle.SourceCard, 7001, true, "A2");
            var initialEvent = new TransactionTestActionEvent(contextBundle.SourceCard);

            SetupTriggeredAbilities(initialEvent, new List<IAbility> { ability1.Object, ability2.Object });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new[] { initialEvent });

            Assert.True(result.IsSuccess);
            _mockAbilityManager.Verify(
                m => m.ProcessSingleAbilityAsync(ability1.Object, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()),
                Times.Once);
            _mockAbilityManager.Verify(
                m => m.ProcessSingleAbilityAsync(ability2.Object, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()),
                Times.Never);
        }

        [Fact]
        public async Task ProcessTransactionAsync_SameNameLimitDisabled_DoesNotSuppress()
        {
            var contextBundle = CreateContextBundle();
            using var gameContext = contextBundle.Context;

            var ability1 = CreateAbilityMock(contextBundle.SourceCard, 7002, false, "A1");
            var ability2 = CreateAbilityMock(contextBundle.SourceCard, 7002, false, "A2");
            var initialEvent = new TransactionTestActionEvent(contextBundle.SourceCard);

            SetupTriggeredAbilities(initialEvent, new List<IAbility> { ability1.Object, ability2.Object });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new[] { initialEvent });

            Assert.True(result.IsSuccess);
            _mockAbilityManager.Verify(
                m => m.ProcessSingleAbilityAsync(ability1.Object, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()),
                Times.Once);
            _mockAbilityManager.Verify(
                m => m.ProcessSingleAbilityAsync(ability2.Object, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()),
                Times.Once);
        }

        [Fact]
        public async Task ProcessTransactionAsync_SameNameLimitEnabled_DifferentOwners_DoNotSuppressEachOther()
        {
            var contextBundle = CreateContextBundle();
            using var gameContext = contextBundle.Context;

            var opponentCard = new CardInstance(Guid.NewGuid(), 1002, contextBundle.Opponent.PlayerId);
            contextBundle.Opponent.Field.Add(opponentCard);
            contextBundle.GameState.RegisterCardInstance(opponentCard);

            var ownerAbility = CreateAbilityMock(contextBundle.SourceCard, 7003, true, "OwnerAbility");
            var opponentAbility = CreateAbilityMock(opponentCard, 7003, true, "OpponentAbility");
            var initialEvent = new TransactionTestActionEvent(contextBundle.SourceCard);

            SetupTriggeredAbilities(initialEvent, new List<IAbility> { ownerAbility.Object, opponentAbility.Object });

            var result = await _transactionService.ProcessTransactionAsync(gameContext, new[] { initialEvent });

            Assert.True(result.IsSuccess);
            _mockAbilityManager.Verify(
                m => m.ProcessSingleAbilityAsync(ownerAbility.Object, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()),
                Times.Once);
            _mockAbilityManager.Verify(
                m => m.ProcessSingleAbilityAsync(opponentAbility.Object, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()),
                Times.Once);
        }

        [Fact]
        public async Task ProcessTransactionAsync_SameNameLimitState_IsResetBetweenTransactions()
        {
            var contextBundle = CreateContextBundle();
            using var gameContext = contextBundle.Context;

            var ability1 = CreateAbilityMock(contextBundle.SourceCard, 7004, true, "A1");
            var ability2 = CreateAbilityMock(contextBundle.SourceCard, 7004, true, "A2");
            var initialEvent = new TransactionTestActionEvent(contextBundle.SourceCard);

            SetupTriggeredAbilities(initialEvent, new List<IAbility> { ability1.Object, ability2.Object });

            var first = await _transactionService.ProcessTransactionAsync(gameContext, new[] { initialEvent });
            var second = await _transactionService.ProcessTransactionAsync(gameContext, new[] { initialEvent });

            Assert.True(first.IsSuccess);
            Assert.True(second.IsSuccess);
            _mockAbilityManager.Verify(
                m => m.ProcessSingleAbilityAsync(ability1.Object, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()),
                Times.Exactly(2));
            _mockAbilityManager.Verify(
                m => m.ProcessSingleAbilityAsync(ability2.Object, It.IsAny<GameContext>(), false, true, It.IsAny<bool>()),
                Times.Never);
        }

        private void SetupTriggeredAbilities(IGameEvent targetEvent, List<IAbility> abilities)
        {
            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());

            _mockAbilityManager
                .Setup(m => m.GetTriggeredAbilitiesAsync(
                    It.Is<IGameEvent>(e => e.EventId == targetEvent.EventId),
                    It.IsAny<GameContext>(),
                    TriggerTiming.On))
                .ReturnsAsync(abilities);

            _mockAbilityManager
                .Setup(m => m.ProcessSingleAbilityAsync(It.IsAny<IAbility>(), It.IsAny<GameContext>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>()));
        }

        private static Mock<IAbility> CreateAbilityMock(CardInstance sourceCard, long definitionId, bool limitEnabled, string keywordSummary)
        {
            var mock = new Mock<IAbility>();
            mock.SetupGet(a => a.RuntimeId).Returns(Guid.NewGuid());
            mock.SetupGet(a => a.DefinitionId).Returns(definitionId);
            mock.SetupGet(a => a.SourceCard).Returns(sourceCard);
            mock.SetupGet(a => a.SourceCardId).Returns(sourceCard.InstanceId);
            mock.SetupGet(a => a.TriggerConfig).Returns(new TriggerConfig
            {
                Timing = TriggerTiming.On,
                LimitSameNameOncePerTransaction = limitEnabled
            });
            mock.SetupGet(a => a.KeywordSummary).Returns(keywordSummary);
            mock.SetupGet(a => a.Targets).Returns(new List<Target>());
            return mock;
        }

        private (GameContext Context, GameState GameState, PlayerState Owner, PlayerState Opponent, CardInstance SourceCard) CreateContextBundle()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(CreateDefaultBuffService().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var owner = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState($"same-name-limit-{Guid.NewGuid():N}", owner, opponent, owner.PlayerId);
            gameState.AbilityManager = _mockAbilityManager.Object;
            gameState.BuffManager = new Mock<IBuffManager>().Object;

            var sourceCard = new CardInstance(Guid.NewGuid(), 1001, owner.PlayerId)
            {
                CurrentZone = CardZone.Field,
                ZoneStayTurn = 1
            };
            owner.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var mapper = new Mock<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            var context = new GameContext(serviceProvider, mapper.Object, accessor, gameState, owner.PlayerId, default, null, sourceCard);
            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.ContextAccessor.GameContext = context;
            GameServiceProvider.Logger = NullLogger.Instance;
            return (context, gameState, owner, opponent, sourceCard);
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

        private sealed class TransactionTestActionEvent : GameEventBase, ICommittableEvent, IPlayerActionEvent
        {
            public TransactionTestActionEvent(CardInstance sourceCard)
                : base(EventType.PlayCard)
            {
                Source = TargetInfo.ForCard(sourceCard);
            }

            public DomainOperationResult Commit(GameContext context)
            {
                return new DomainOperationResult { Success = true };
            }
        }
    }
}
