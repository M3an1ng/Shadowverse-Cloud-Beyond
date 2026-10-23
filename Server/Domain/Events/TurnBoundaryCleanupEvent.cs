using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.GameFlow;
using ShadowverseBeyond.Server.Domain.Identifiers;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace ShadowverseBeyond.Server.Domain.Events
{
    [GameEventMetadata(
        "回合边界清理",
        "当回合开始或结束边界进入统一状态清理阶段时触发。",
        EventType.TurnBoundaryCleanup,
        visibility: EventMetadataVisibility.System)]
    public class TurnBoundaryCleanupEvent : DelayedEventBase, ICommittableEvent
    {
        public PlayerState? SubjectPlayer => Source?.Player;

        public TurnBoundaryCleanupTiming Timing { get; }

        public TurnBoundaryCleanupEvent(
            PlayerState subjectPlayer,
            TurnBoundaryCleanupTiming timing,
            DelayConfig? delayConfig = null,
            Guid? eventId = null,
            DateTime? timestamp = null)
            : base(EventType.TurnBoundaryCleanup, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(subjectPlayer);
            Timing = timing;
            ConfigureDelay(delayConfig ?? DelayConfig.QueueDrained());
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var player = SubjectPlayer;
            if (player == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "TurnBoundaryCleanupEvent.Commit: Source player is null." };
            }

            var cleanupService = context.ServiceProvider.GetService<ITurnBoundaryCleanupService>()
                ?? new TurnBoundaryCleanupService(context.BuffService);
            var cleanupEvents = cleanupService.CollectCleanupEvents(context, Timing, player);

            var result = new DomainOperationResult { Success = true };
            result.GeneratedEvents.AddRange(cleanupEvents);
            return result;
        }
    }
}
