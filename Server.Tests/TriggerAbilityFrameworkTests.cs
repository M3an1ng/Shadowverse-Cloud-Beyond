using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Options;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class TriggerAbilityFrameworkTests
    {
        [Fact]
        public async Task ResolveTargetsForAbilityAsync_AutoRandomSelection_CompletesWithoutPrompt()
        {
            var queryExecutor = new Mock<IQueryExecutor>();
            var pendingActionService = new Mock<IPendingActionService>(MockBehavior.Strict);
            var mapper = new Mock<IMapper>();
            var accessor = new GameContextAccessor();
            var buffService = new Mock<IBuffService>();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor>(accessor);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton(queryExecutor.Object);
            services.AddSingleton(buffService.Object);
            var serviceProvider = services.BuildServiceProvider();
            GameServiceProvider.ContextAccessor = accessor;

            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player1, player2, player1.PlayerId)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1001, player1.PlayerId);
            var candidateA = new CardInstance(Guid.NewGuid(), 2001, player1.PlayerId);
            var candidateB = new CardInstance(Guid.NewGuid(), 2002, player1.PlayerId);
            player1.Field.Add(sourceCard);
            player1.Field.Add(candidateA);
            player1.Field.Add(candidateB);
            gameState.RegisterCardInstance(sourceCard);
            gameState.RegisterCardInstance(candidateA);
            gameState.RegisterCardInstance(candidateB);

            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                queryExecutor.Object,
                pendingActionService.Object);

            var conditionFactory = new Mock<IConditionFactory>();
            var targetFactory = new Mock<ITargetFactory>();
            targetFactory
                .Setup(factory => factory.Create(It.IsAny<string>(), It.IsAny<TargetConfig>()))
                .Returns((string key, TargetConfig config) =>
                    new Target(key, config, targetSelectionService, NullLogger<Target>.Instance));

            var targetConfig = new TargetConfig
            {
                Query = new QueryConfig(),
                PlayerChoiceOptions = new PlayerChoiceConfig
                {
                    IsRequired = true,
                    MinCount = 1,
                    MaxCount = 2
                }
            };

            queryExecutor
                .Setup(executor => executor.ExecuteQuery(
                    It.IsAny<QueryConfig>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Dictionary<string, List<TargetInfo>>?>()))
                .Returns(new QueryResult
                {
                    Success = true,
                    Results = new object[] { candidateA, candidateB }
                });

            var abilityConfig = new AbilityConfig
            {
                Id = 3001,
                Name = "RandomTargetAbility",
                Targets = new Dictionary<string, TargetConfig>
                {
                    ["target"] = targetConfig
                }
            };

            var ability = new Ability(
                abilityConfig,
                conditionFactory.Object,
                targetFactory.Object,
                targetSelectionService,
                sourceCard);

            using var baseContext = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player1.PlayerId,
                CancellationToken.None,
                sourceCard: sourceCard);

            var context = baseContext with
            {
                SourceCard = sourceCard,
                InvocationOptions = new AbilityInvocationOptions
                {
                    AutoRandomTargetSelection = true
                }
            };

            var result = await targetSelectionService.ResolveTargetsForAbilityAsync(ability, context, isCancellable: false);

            Assert.Equal(ResolutionStatus.Completed, result.Status);
            Assert.True(result.ResolvedTargets.TryGetValue("target", out var resolvedTargets));
            Assert.Equal(2, resolvedTargets.Count);
            Assert.Contains(candidateA.InstanceId, resolvedTargets.Select(target => target.InstanceId));
            Assert.Contains(candidateB.InstanceId, resolvedTargets.Select(target => target.InstanceId));
            pendingActionService.Verify(service => service.WaitForPlayerChoiceAsync(
                It.IsAny<string>(),
                It.IsAny<PlayerChoiceRequest>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteTriggeredAbilitiesAsync_PassesInvocationOptionsToTriggeredAbility()
        {
            var abilityManager = new Mock<IAbilityManager>();
            var eventProcessor = new Mock<IEventProcessor>();
            var pendingActionService = new Mock<IPendingActionService>();
            var effectExecutor = new Mock<IEffectExecutor>();
            var clientMethodMapper = new Mock<IClientMethodMapper>();
            var eventJournal = new Mock<ICommittedEventJournal>();
            var snapshotStore = new Mock<ISnapshotStore>();
            var recordedEventMapper = new Mock<IRecordedEventMapper>();
            var rawEventMapper = new Mock<IGameEventRawEventMapper>();
            var metaProgressionService = new Mock<IMetaProgressionService>();
            var winrateStatsService = new Mock<IWinrateStatsService>();
            var mapper = new Mock<IMapper>();
            var accessor = new GameContextAccessor();
            var buffService = new Mock<IBuffService>();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor>(accessor);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton(buffService.Object);
            var serviceProvider = services.BuildServiceProvider();
            GameServiceProvider.ContextAccessor = accessor;

            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player1, player2, player1.PlayerId)
            {
                AbilityManager = abilityManager.Object,
                BuffManager = Mock.Of<IBuffManager>()
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 4001, player1.PlayerId);
            var targetCard = new CardInstance(Guid.NewGuid(), 4002, player2.PlayerId);
            gameState.RegisterCardInstance(sourceCard);
            gameState.RegisterCardInstance(targetCard);

            var matchedAbility = new Mock<IAbility>();
            matchedAbility.SetupGet(ability => ability.RuntimeId).Returns(Guid.NewGuid());
            matchedAbility.SetupGet(ability => ability.SourceCard).Returns(targetCard);
            matchedAbility.SetupGet(ability => ability.SourceCardId).Returns(targetCard.InstanceId);
            matchedAbility.SetupGet(ability => ability.KeywordSummary).Returns("Fanfare");
            matchedAbility.Setup(ability => ability.HasKeyword(AbilityKeyword.Fanfare)).Returns(true);

            var nonMatchedAbility = new Mock<IAbility>();
            nonMatchedAbility.SetupGet(ability => ability.RuntimeId).Returns(Guid.NewGuid());
            nonMatchedAbility.Setup(ability => ability.HasKeyword(AbilityKeyword.Fanfare)).Returns(false);

            abilityManager
                .Setup(manager => manager.GetEffectiveAbilitiesForCard(targetCard.InstanceId, It.IsAny<GameContext>()))
                .Returns(new List<IAbility> { nonMatchedAbility.Object, matchedAbility.Object });

            GameContext? invokedContext = null;
            abilityManager
                .Setup(manager => manager.ProcessSingleAbilityAsync(
                    matchedAbility.Object,
                    It.IsAny<GameContext>(),
                    false,
                    true,
                    true))
                .Callback<IAbility, GameContext, bool, bool, bool>((_, context, _, _, _) => invokedContext = context)
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>(), new List<IGameEvent>()));

            var service = new GameActionTransactionService(
                NullLogger<GameActionTransactionService>.Instance,
                abilityManager.Object,
                eventProcessor.Object,
                pendingActionService.Object,
                effectExecutor.Object,
                clientMethodMapper.Object,
                eventJournal.Object,
                snapshotStore.Object,
                recordedEventMapper.Object,
                rawEventMapper.Object,
                metaProgressionService.Object,
                winrateStatsService.Object,
                Microsoft.Extensions.Options.Options.Create(new ReplayOptions()));

            var effect = new Effect(
                new EffectConfig { Type = EffectType.TriggerAbility },
                player1.PlayerId,
                new List<TargetInfo> { TargetInfo.ForCard(targetCard) },
                null,
                Guid.NewGuid(),
                sourceCard.InstanceId)
            {
                ResolvedTargets = new Dictionary<string, List<TargetInfo>>()
            };

            var triggerParams = new TriggerAbilityEffectParams
            {
                TriggerMode = TriggerAbilityMode.ByKeyword,
                AbilityKeyword = AbilityKeyword.Fanfare,
                AutoRandomTargetSelection = true
            };

            var effectCompletedEvent = new EffectCompletedEvent(
                EffectType.TriggerAbility,
                new Dictionary<string, object>(),
                effect.SelectedTargets.AsReadOnly(),
                Array.Empty<IGameEvent>(),
                Array.Empty<CardInstance>(),
                Array.Empty<CardInstanceSnapshot>(),
                new Dictionary<string, object?>(),
                true,
                sourceCard);

            using var context = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                player1.PlayerId,
                CancellationToken.None,
                sourceCard: sourceCard,
                currentEffect: effect);

            var method = typeof(GameActionTransactionService).GetMethod(
                "ExecuteTriggeredAbilitiesAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);

            var task = (Task<PausableTransactionResult>)method!.Invoke(service, new object[]
            {
                effect,
                context,
                triggerParams,
                effectCompletedEvent,
                new List<IGameEvent>(),
                new Dictionary<string, int>(),
                new Queue<IGameEvent>(),
                new Dictionary<Guid, Queue<IGameEvent>>(),
                new Queue<IGameEvent>(),
                new HashSet<Guid>(),
                new Dictionary<Guid, AttackFlowContext>(),
                0
            })!;

            var result = await task;

            Assert.True(result.IsSuccess);
            Assert.NotNull(invokedContext);
            Assert.Same(effectCompletedEvent, invokedContext!.TriggeringEvent);
            Assert.Same(targetCard, invokedContext.SourceCard);
            Assert.True(invokedContext.InvocationOptions.AutoRandomTargetSelection);
            abilityManager.Verify(manager => manager.ProcessSingleAbilityAsync(
                matchedAbility.Object,
                It.IsAny<GameContext>(),
                false,
                true,
                true), Times.Once);
            abilityManager.Verify(manager => manager.ProcessSingleAbilityAsync(
                nonMatchedAbility.Object,
                It.IsAny<GameContext>(),
                false,
                true,
                true), Times.Never);
        }

        [Fact]
        public async Task ExecuteTriggeredAbilitiesAsync_MatchesAbilityByDefinitionId()
        {
            var service = CreateTransactionServiceForTriggeredAbilityTests(
                out var abilityManager,
                out var serviceProvider,
                out var mapper,
                out var accessor,
                out var gameState,
                out var player1,
                out var sourceCard,
                out var targetCard);

            var matchedAbility = new Mock<IAbility>();
            matchedAbility.SetupGet(ability => ability.RuntimeId).Returns(Guid.NewGuid());
            matchedAbility.SetupGet(ability => ability.SourceCard).Returns(targetCard);
            matchedAbility.SetupGet(ability => ability.SourceCardId).Returns(targetCard.InstanceId);
            matchedAbility.SetupGet(ability => ability.DefinitionId).Returns(9001L);
            matchedAbility.SetupGet(ability => ability.KeywordSummary).Returns("DefinitionMatch");

            var nonMatchedAbility = new Mock<IAbility>();
            nonMatchedAbility.SetupGet(ability => ability.RuntimeId).Returns(Guid.NewGuid());
            nonMatchedAbility.SetupGet(ability => ability.SourceCard).Returns(targetCard);
            nonMatchedAbility.SetupGet(ability => ability.SourceCardId).Returns(targetCard.InstanceId);
            nonMatchedAbility.SetupGet(ability => ability.DefinitionId).Returns(9002L);

            abilityManager
                .Setup(manager => manager.GetEffectiveAbilitiesForCard(targetCard.InstanceId, It.IsAny<GameContext>()))
                .Returns(new List<IAbility> { nonMatchedAbility.Object, matchedAbility.Object });
            abilityManager
                .Setup(manager => manager.ProcessSingleAbilityAsync(
                    matchedAbility.Object,
                    It.IsAny<GameContext>(),
                    false,
                    true,
                    true))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>(), new List<IGameEvent>()));

            var triggerParams = new TriggerAbilityEffectParams
            {
                TriggerMode = TriggerAbilityMode.ByAbilityDefinitionId,
                AbilityDefinitionIds = new List<long> { 9001L }
            };

            var result = await InvokeExecuteTriggeredAbilitiesAsync(
                service,
                serviceProvider,
                mapper,
                accessor,
                gameState,
                player1.PlayerId,
                sourceCard,
                targetCard,
                triggerParams);

            Assert.True(result.IsSuccess);
            abilityManager.Verify(manager => manager.ProcessSingleAbilityAsync(
                matchedAbility.Object,
                It.IsAny<GameContext>(),
                false,
                true,
                true), Times.Once);
            abilityManager.Verify(manager => manager.ProcessSingleAbilityAsync(
                nonMatchedAbility.Object,
                It.IsAny<GameContext>(),
                false,
                true,
                true), Times.Never);
        }

        [Fact]
        public async Task ExecuteTriggeredAbilitiesAsync_MatchesAbilityByTriggerEventType()
        {
            var service = CreateTransactionServiceForTriggeredAbilityTests(
                out var abilityManager,
                out var serviceProvider,
                out var mapper,
                out var accessor,
                out var gameState,
                out var player1,
                out var sourceCard,
                out var targetCard);

            var matchedAbility = new Mock<IAbility>();
            matchedAbility.SetupGet(ability => ability.RuntimeId).Returns(Guid.NewGuid());
            matchedAbility.SetupGet(ability => ability.SourceCard).Returns(targetCard);
            matchedAbility.SetupGet(ability => ability.SourceCardId).Returns(targetCard.InstanceId);
            matchedAbility.SetupGet(ability => ability.TriggerConfig).Returns(new TriggerConfig
            {
                EventTypes = new List<EventType> { EventType.Summon }
            });
            matchedAbility.SetupGet(ability => ability.KeywordSummary).Returns("SummonMatch");

            var nonMatchedAbility = new Mock<IAbility>();
            nonMatchedAbility.SetupGet(ability => ability.RuntimeId).Returns(Guid.NewGuid());
            nonMatchedAbility.SetupGet(ability => ability.SourceCard).Returns(targetCard);
            nonMatchedAbility.SetupGet(ability => ability.SourceCardId).Returns(targetCard.InstanceId);
            nonMatchedAbility.SetupGet(ability => ability.TriggerConfig).Returns(new TriggerConfig
            {
                EventTypes = new List<EventType> { EventType.Destroy }
            });

            abilityManager
                .Setup(manager => manager.GetEffectiveAbilitiesForCard(targetCard.InstanceId, It.IsAny<GameContext>()))
                .Returns(new List<IAbility> { matchedAbility.Object, nonMatchedAbility.Object });
            abilityManager
                .Setup(manager => manager.ProcessSingleAbilityAsync(
                    matchedAbility.Object,
                    It.IsAny<GameContext>(),
                    false,
                    true,
                    true))
                .ReturnsAsync(AbilityProcessResult.Success(new List<Effect>(), new List<IGameEvent>()));

            var triggerParams = new TriggerAbilityEffectParams
            {
                TriggerMode = TriggerAbilityMode.ByEventType,
                TriggerEventType = EventType.Summon
            };

            var result = await InvokeExecuteTriggeredAbilitiesAsync(
                service,
                serviceProvider,
                mapper,
                accessor,
                gameState,
                player1.PlayerId,
                sourceCard,
                targetCard,
                triggerParams);

            Assert.True(result.IsSuccess);
            abilityManager.Verify(manager => manager.ProcessSingleAbilityAsync(
                matchedAbility.Object,
                It.IsAny<GameContext>(),
                false,
                true,
                true), Times.Once);
            abilityManager.Verify(manager => manager.ProcessSingleAbilityAsync(
                nonMatchedAbility.Object,
                It.IsAny<GameContext>(),
                false,
                true,
                true), Times.Never);
        }

        private static GameActionTransactionService CreateTransactionServiceForTriggeredAbilityTests(
            out Mock<IAbilityManager> abilityManager,
            out IServiceProvider serviceProvider,
            out Mock<IMapper> mapper,
            out GameContextAccessor accessor,
            out GameState gameState,
            out PlayerState player1,
            out CardInstance sourceCard,
            out CardInstance targetCard)
        {
            abilityManager = new Mock<IAbilityManager>();
            var eventProcessor = new Mock<IEventProcessor>();
            var pendingActionService = new Mock<IPendingActionService>();
            var effectExecutor = new Mock<IEffectExecutor>();
            var clientMethodMapper = new Mock<IClientMethodMapper>();
            var eventJournal = new Mock<ICommittedEventJournal>();
            var snapshotStore = new Mock<ISnapshotStore>();
            var recordedEventMapper = new Mock<IRecordedEventMapper>();
            var rawEventMapper = new Mock<IGameEventRawEventMapper>();
            var metaProgressionService = new Mock<IMetaProgressionService>();
            var winrateStatsService = new Mock<IWinrateStatsService>();
            mapper = new Mock<IMapper>();
            accessor = new GameContextAccessor();
            var buffService = new Mock<IBuffService>();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor>(accessor);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton(buffService.Object);
            serviceProvider = services.BuildServiceProvider();
            GameServiceProvider.ContextAccessor = accessor;

            player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            gameState = new GameState("test-game", player1, player2, player1.PlayerId)
            {
                AbilityManager = abilityManager.Object,
                BuffManager = Mock.Of<IBuffManager>()
            };

            sourceCard = new CardInstance(Guid.NewGuid(), 4001, player1.PlayerId);
            targetCard = new CardInstance(Guid.NewGuid(), 4002, player2.PlayerId);
            gameState.RegisterCardInstance(sourceCard);
            gameState.RegisterCardInstance(targetCard);

            return new GameActionTransactionService(
                NullLogger<GameActionTransactionService>.Instance,
                abilityManager.Object,
                eventProcessor.Object,
                pendingActionService.Object,
                effectExecutor.Object,
                clientMethodMapper.Object,
                eventJournal.Object,
                snapshotStore.Object,
                recordedEventMapper.Object,
                rawEventMapper.Object,
                metaProgressionService.Object,
                winrateStatsService.Object,
                Microsoft.Extensions.Options.Options.Create(new ReplayOptions()));
        }

        private static async Task<PausableTransactionResult> InvokeExecuteTriggeredAbilitiesAsync(
            GameActionTransactionService service,
            IServiceProvider serviceProvider,
            Mock<IMapper> mapper,
            GameContextAccessor accessor,
            GameState gameState,
            Guid requestingPlayerId,
            CardInstance sourceCard,
            CardInstance targetCard,
            TriggerAbilityEffectParams triggerParams)
        {
            var effect = new Effect(
                new EffectConfig { Type = EffectType.TriggerAbility },
                requestingPlayerId,
                new List<TargetInfo> { TargetInfo.ForCard(targetCard) },
                null,
                Guid.NewGuid(),
                sourceCard.InstanceId)
            {
                ResolvedTargets = new Dictionary<string, List<TargetInfo>>()
            };

            var effectCompletedEvent = new EffectCompletedEvent(
                EffectType.TriggerAbility,
                new Dictionary<string, object>(),
                effect.SelectedTargets.AsReadOnly(),
                Array.Empty<IGameEvent>(),
                Array.Empty<CardInstance>(),
                Array.Empty<CardInstanceSnapshot>(),
                new Dictionary<string, object?>(),
                true,
                sourceCard);

            using var context = new GameContext(
                serviceProvider,
                mapper.Object,
                accessor,
                gameState,
                requestingPlayerId,
                CancellationToken.None,
                sourceCard: sourceCard,
                currentEffect: effect);

            var method = typeof(GameActionTransactionService).GetMethod(
                "ExecuteTriggeredAbilitiesAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);

            var task = (Task<PausableTransactionResult>)method!.Invoke(service, new object[]
            {
                effect,
                context,
                triggerParams,
                effectCompletedEvent,
                new List<IGameEvent>(),
                new Dictionary<string, int>(),
                new Queue<IGameEvent>(),
                new Dictionary<Guid, Queue<IGameEvent>>(),
                new Queue<IGameEvent>(),
                new HashSet<Guid>(),
                new Dictionary<Guid, AttackFlowContext>(),
                0
            })!;

            return await task;
        }
    }
}
