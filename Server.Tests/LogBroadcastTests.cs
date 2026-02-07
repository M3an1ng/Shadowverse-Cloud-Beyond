using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;
using Microsoft.Extensions.Options;
using ShadowverseBeyond.Server.Options;
using ShadowverseBeyond.Server.Infrastructure.Services;

namespace ShadowverseBeyond.Server.Tests
{
    /// <summary>
    /// 后端事件 -> DTO/LogEvent 播报链路验证（无前端依赖）
    /// </summary>
    public class LogBroadcastTests
    {
        private class CaptureClientMethodMapper : IClientMethodMapper
        {
            public List<GameEventDto> Sent { get; } = new();

            public Task BroadcastEventToClientAsync(string connectionId, GameEventDto eventDto)
            {
                Sent.Add(eventDto);
                return Task.CompletedTask;
            }

            public Task BroadcastEventToGroupAsync(string groupName, GameEventDto eventDto)
            {
                Sent.Add(eventDto);
                return Task.CompletedTask;
            }

            public Task BroadcastEventToGroupAllAsync(string groupName, GameEventDto eventDto)
            {
                Sent.Add(eventDto);
                return Task.CompletedTask;
            }

            public Task BroadcastEventToGroupExceptAsync(string groupName, string connectionIdToExclude, GameEventDto eventDto)
            {
                Sent.Add(eventDto);
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task ProcessTransactionAsync_ShouldEmitLogEvent_WhenEventHasNoDto()
        {
            // DI for GameContext
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(new Mock<IBuffService>().Object);
            services.AddSingleton<IBuffManager>(new Mock<IBuffManager>().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var sp = services.BuildServiceProvider();

            var abilityManager = new Mock<IAbilityManager>();
            abilityManager.Setup(m => m.GetTriggeredAbilitiesAsync(It.IsAny<IGameEvent>(), It.IsAny<GameContext>(), It.IsAny<TriggerTiming>()))
                          .ReturnsAsync(new List<IAbility>());

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("log-test-game", player, opponent, player.PlayerId)
            {
                AbilityManager = abilityManager.Object,
                BuffManager = sp.GetRequiredService<IBuffManager>()
            };

            var mapper = new Mock<IMapper>();
            using var context = new GameContext(
                sp,
                mapper.Object,
                sp.GetRequiredService<IGameContextAccessor>(),
                gameState,
                player.PlayerId,
                CancellationToken.None,
                null,
                null);

            var eventProcessor = new Mock<IEventProcessor>();
            // Force no DTOs -> should fallback to LogEventDto
            eventProcessor.Setup(p => p.ProcessEventAsync(It.IsAny<IGameEvent>(), context))
                          .ReturnsAsync(new ProcessorResult());

            var pending = new Mock<IPendingActionService>();
            var effectExecutor = new Mock<IEffectExecutor>();
            var clientMapper = new CaptureClientMethodMapper();
            var eventJournal = new Mock<ICommittedEventJournal>();
            var snapshotStore = new Mock<ISnapshotStore>();
            var options = Microsoft.Extensions.Options.Options.Create(new ReplayOptions());
            var recordedMapper = new JsonRecordedEventMapper();
            var rawEventMapper = new JsonGameEventRawEventMapper();
            var metaProgression = new Mock<IMetaProgressionService>();
            eventJournal
                .Setup(j => j.AppendBundleAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<IReadOnlyList<ShadowverseBeyond.Server.Dtos.Replay.CommittedEventWriteDto>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ShadowverseBeyond.Server.Dtos.Replay.CommittedBundleAppendResult(1, 1, 1));
            snapshotStore
                .Setup(s => s.SaveSnapshotAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<ShadowverseBeyond.Server.Dtos.GameStateDto>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = new GameActionTransactionService(
                NullLogger<GameActionTransactionService>.Instance,
                abilityManager.Object,
                eventProcessor.Object,
                pending.Object,
                effectExecutor.Object,
                clientMapper,
                eventJournal.Object,
                snapshotStore.Object,
                recordedMapper,
                rawEventMapper,
                metaProgression.Object,
                options);

            var ppUpdate = new PpUpdateEvent(player, 5);

            await service.ProcessTransactionAsync(context, new List<IGameEvent> { ppUpdate });

            Assert.Contains(clientMapper.Sent, dto => dto is LogEventDto log && log.EventId == ppUpdate.EventId);
        }
    }
}
