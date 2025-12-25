using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Services.Abstractions;
using System;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event triggered when a Buff is successfully applied (registered) to a target.
    /// This is now a committable event that handles the application logic.
    /// </summary>
    [GameEventMetadata("Buff应用", "当一个Buff被成功应用到目标时触发的事件。", EventType.BuffApplied)]
    public class BuffAppliedEvent : GameEventBase, ICommittableEvent
    {
        /// <summary>
        /// The Buff instance that was applied.
        /// Contains details about the source, target, duration, and effects.
        /// </summary>
        [GameEventParameter("应用的Buff", "被应用的Buff实例。")]
        public Buff AppliedBuff { get; }

        [GameEventParameter("Buff来源", "应用Buff的来源卡牌。")]
        public CardInstance? BuffSource => Source?.Card;

        public BuffAppliedEvent(Buff appliedBuff, CardInstance? source)
            : base(EventType.BuffApplied)
        {
            AppliedBuff = appliedBuff ?? throw new ArgumentNullException(nameof(appliedBuff));
            if (source != null)
            {
                Source = TargetInfo.ForCard(source);
            }
        }

        public DomainOperationResult Commit(GameContext context)
        {
            // The commit action is now the single, atomic operation of registering the buff.
            // All business logic (stacking, etc.) has already been processed and resulted in this event.
            context.BuffManager.RegisterBuffInstance(this.AppliedBuff, context.CurrentGameState);
            context.CurrentGameState.IncrementBuffRevision();
            context.BuffService.InvalidateDerivedState(context.CurrentGameState, this.AppliedBuff.TargetCardInstanceId);

            // This event does not generate further events.
            return new DomainOperationResult { Success = true };
        }
    }
}
