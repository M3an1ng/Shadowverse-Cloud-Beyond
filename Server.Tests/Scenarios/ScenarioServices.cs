using System;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Options;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Mappings;
using Scrutor;

namespace ShadowverseBeyond.Server.Tests.Scenarios
{
    public static class ScenarioServices
    {
        public static IServiceProvider BuildServiceProvider(
            TestGameDataProvider dataProvider,
            ScenarioPendingActionService pendingActionService,
            int randomSeed)
        {
            var services = new ServiceCollection();
            GameDataProvider.SetInstanceForTesting(dataProvider);
            services.AddLogging();
            services.AddAutoMapper(typeof(PlayerStateProfile).Assembly);
            services.AddMemoryCache();
            services.AddDslFunctionSystem();

            services.AddSingleton<IGameDataProvider>(dataProvider);
            services.AddSingleton<IDslExpressionCache, DslExpressionCache>();
            services.AddSingleton<IDslCompiler, DslCompiler>();
            services.AddSingleton<IQueryDslParser, QueryDslParser>();
            var deterministicRandom = new Random(randomSeed);
            DslQueryOperations.RandomOverride = deterministicRandom;
            services.AddSingleton<IQueryExecutor>(sp =>
                new DeterministicQueryExecutor(
                    sp.GetRequiredService<ILogger<DeterministicQueryExecutor>>(),
                    sp.GetRequiredService<IDslCompiler>(),
                    sp.GetRequiredService<IMapper>(),
                    sp.GetRequiredService<IQueryDslParser>(),
                    deterministicRandom));

            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddScoped<IValueSourceResolver, ValueSourceResolver>();
            services.AddScoped<IConditionEvaluator, DslConditionEvaluator>();
            services.AddScoped<IConditionFactory, ConditionFactory>();
            services.AddScoped<TargetSelectionService>();
            services.AddScoped<ITargetFactory, TargetFactory>();
            services.AddScoped<ITargetProcessingService, TargetProcessingService>();

            services.AddSingleton<IAbilityFactory, AbilityFactory>();
            services.AddSingleton<AbilityManager>();
            services.AddSingleton<IAbilityManager>(sp => sp.GetRequiredService<AbilityManager>());

            services.AddSingleton<IBuffEvaluator, BuffEvaluator>();
            services.AddSingleton<IBuffFactory, BuffFactory>();
            services.AddSingleton<IBuffManager, BuffManager>();
            services.AddSingleton<BuffStackingEngine>();
            services.AddSingleton<IBuffService, BuffService>();
            services.AddScoped<ICardFactory, CardFactory>();

            services.AddSingleton<IEffectHandlerRegistry>(sp =>
                new EffectHandlerRegistry(NullLogger<EffectHandlerRegistry>.Instance));
            services.Scan(scan => scan
                .FromAssemblyOf<EffectHandlerRegistry>()
                .AddClasses(classes => classes.AssignableTo(typeof(EffectHandlerBase<>)))
                .AsSelf()
                .WithScopedLifetime());

            services.AddScoped<IEffectExecutor, EffectExecutor>();

            services.AddSingleton<IPendingActionService>(pendingActionService);

            return services.BuildServiceProvider();
        }

        public static GameActionTransactionService BuildTransactionService(
            IServiceProvider services,
            IAbilityManager abilityManager,
            IPendingActionService pendingActionService,
            IEffectExecutor effectExecutor)
        {
            var eventProcessor = new NoopEventProcessor();
            var eventJournal = new NoopEventJournal();
            var snapshotStore = new NoopSnapshotStore();
            var clientMapper = new NoopClientMethodMapper();
            var recordedMapper = new JsonRecordedEventMapper();
            var rawMapper = new JsonGameEventRawEventMapper();
            var metaProgression = new NoopMetaProgressionService();
            var options = Microsoft.Extensions.Options.Options.Create(new ReplayOptions());

            return new GameActionTransactionService(
                NullLogger<GameActionTransactionService>.Instance,
                abilityManager,
                eventProcessor,
                pendingActionService,
                effectExecutor,
                clientMapper,
                eventJournal,
                snapshotStore,
                recordedMapper,
                rawMapper,
                metaProgression,
                options);
        }

        private sealed class NoopEventProcessor : Domain.Events.IEventProcessor
        {
            public Task<Domain.Events.ProcessorResult> ProcessEventAsync(Domain.Events.IGameEvent initialEvent, Domain.Events.GameContext context)
            {
                return Task.FromResult(new Domain.Events.ProcessorResult());
            }
        }

        private sealed class NoopClientMethodMapper : IClientMethodMapper
        {
            public Task BroadcastEventToClientAsync(string connectionId, Dtos.SignalR.Events.GameEventDto eventDto) => Task.CompletedTask;
            public Task BroadcastEventToGroupAsync(string groupName, Dtos.SignalR.Events.GameEventDto eventDto) => Task.CompletedTask;
            public Task BroadcastEventToGroupAllAsync(string groupName, Dtos.SignalR.Events.GameEventDto eventDto) => Task.CompletedTask;
            public Task BroadcastEventToGroupExceptAsync(string groupName, string connectionIdToExclude, Dtos.SignalR.Events.GameEventDto eventDto) => Task.CompletedTask;
        }

        private sealed class NoopEventJournal : ICommittedEventJournal
        {
            public Task<Dtos.Replay.CommittedBundleAppendResult> AppendBundleAsync(
                string gameId,
                Guid bundleId,
                IReadOnlyList<Dtos.Replay.CommittedEventWriteDto> events,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(new Dtos.Replay.CommittedBundleAppendResult(1, 1, events.Count));
            }

            public Task<IReadOnlyList<Dtos.Replay.RecordedCommittedEventDto>> ReadRangeAsync(
                string gameId,
                long fromSeqInclusive,
                int limit,
                CancellationToken cancellationToken)
            {
                return Task.FromResult<IReadOnlyList<Dtos.Replay.RecordedCommittedEventDto>>(Array.Empty<Dtos.Replay.RecordedCommittedEventDto>());
            }

            public Task<long> GetHeadSeqAsync(string gameId, CancellationToken cancellationToken)
            {
                return Task.FromResult(0L);
            }

            public Task<Dtos.Replay.RecordedCommittedEventDto?> FindByEventIdAsync(string gameId, Guid eventId, CancellationToken cancellationToken)
            {
                return Task.FromResult<Dtos.Replay.RecordedCommittedEventDto?>(null);
            }
        }

        private sealed class NoopSnapshotStore : ISnapshotStore
        {
            public Task SaveSnapshotAsync(string gameId, long sequence, Dtos.GameStateDto snapshot, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task<Dtos.Replay.RecordedSnapshotDto?> GetLatestAsync(string gameId, CancellationToken cancellationToken)
            {
                return Task.FromResult<Dtos.Replay.RecordedSnapshotDto?>(null);
            }

            public Task<Dtos.Replay.RecordedSnapshotDto?> GetAtOrBeforeAsync(string gameId, long atSeq, CancellationToken cancellationToken)
            {
                return Task.FromResult<Dtos.Replay.RecordedSnapshotDto?>(null);
            }

            public Task DeleteForGameAsync(string gameId, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class NoopMetaProgressionService : IMetaProgressionService
        {
            public Task ApplyPostCommitAsync(Domain.Events.GameContext context, IReadOnlyList<Domain.Events.IGameEvent> committedEvents, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }
    }
}
