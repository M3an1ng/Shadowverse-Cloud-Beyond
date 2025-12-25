using System;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // DomainOperationResult
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event triggered when a Buff is removed from a target.
    /// As a committable event, it performs the removal via BuffService.
    /// </summary>
    [GameEventMetadata("Buff移除", "当一个Buff从目标身上被移除时触发的事件。", EventType.BuffRemoved)]
    public class BuffRemovedEvent : GameEventBase, ICommittableEvent
    {
        [GameEventParameter("移除的Buff", "被移除的Buff实例。")]
        public Buff RemovedBuff { get; }

        [GameEventParameter("移除原因", "Buff被移除的原因。")]
        public string RemovalReason { get; }

        [GameEventParameter("移除来源", "导致Buff移除的来源卡牌。")]
        public CardInstance? RemovalSource => Source?.Card;

        public BuffRemovedEvent(Buff removedBuff, CardInstance? source, string removalReason = "manual")
            : base(EventType.BuffRemoved)
        {
            RemovedBuff = removedBuff ?? throw new ArgumentNullException(nameof(removedBuff));
            RemovalReason = removalReason ?? "unknown";
            if (source != null)
            {
                Source = TargetInfo.ForCard(source);
            }
        }

        public DomainOperationResult Commit(GameContext context)
        {
            // The commit action is the single, atomic operation of unregistering the buff.
            context.BuffManager.UnregisterBuffInstance(this.RemovedBuff.Id);
            context.CurrentGameState.IncrementBuffRevision();
            context.BuffService.InvalidateDerivedState(context.CurrentGameState, this.RemovedBuff.TargetCardInstanceId);

            // This event does not generate further events.
            return new DomainOperationResult { Success = true };
        }
    }
}
