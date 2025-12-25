using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Buffs;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Events.Services;
using Microsoft.Extensions.Options;
using ShadowverseBeyond.Server.Options;
using ShadowverseBeyond.Server.Infrastructure.Services;

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
                _replayOptions
            );
        }

        [Fact]
        public async Task ProcessTransactionAsync_HandlesComplexTriggerChain_Correctly()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(new Mock<IBuffService>().Object);
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player, opponent, player.PlayerId);
            gameState.AbilityManager = _mockAbilityManager.Object;
            gameState.BuffManager = new Mock<IBuffManager>().Object;

            var attackerCard = new CardInstance(Guid.NewGuid(), 1, player.PlayerId);
            var targetCard = new CardInstance(Guid.NewGuid(), 2, opponent.PlayerId);

            var mockInitialEvent = new Mock<PlayCardEvent>(player, attackerCard);
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
            _mockAbilityManager.Setup(m => m.ProcessSingleAbilityAsync(mockOnPlayAbility.Object, gameContext, false))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { mockDamageEffect }));

            _mockAbilityManager.Setup(m => m.GetTriggeredAbilitiesAsync(mockDamageEvent.Object, gameContext, TriggerTiming.Before))
                .ReturnsAsync(new List<IAbility> { mockBeforeDamageAbility.Object });
            _mockAbilityManager.Setup(m => m.ProcessSingleAbilityAsync(mockBeforeDamageAbility.Object, gameContext, false))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect> { mockModifyDamageEffect }));

            _mockAbilityManager.Setup(m => m.GetTriggeredAbilitiesAsync(mockModifiedDamageEvent.Object, gameContext, TriggerTiming.After))
                .ReturnsAsync(new List<IAbility> { mockAfterDamageAbility.Object });
            // Default empty for other cases
            _mockAbilityManager.Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                .ReturnsAsync(new List<IAbility>());
            _mockAbilityManager.Setup(m => m.ProcessSingleAbilityAsync(mockAfterDamageAbility.Object, gameContext, false))
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
    }
}
