using System;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event triggered when an amulet's countdown value decreases.
    /// </summary>
    [GameEventMetadata("倒计时变化", "当护符倒计时减少时触发的事件。", EventType.CountdownTick)]
    public class CountdownTickEvent : DelayedEventBase, ICommittableEvent
    {
        [GameEventParameter("变化量", "倒计时变化量（通常为-1）。")]
        public int ChangeAmount { get; }

        [GameEventParameter("旧倒计时", "变化前的倒计时值。")]
        public int? OldCountdown { get; }

        [GameEventParameter("新倒计时", "变化后的倒计时值。")]
        public int? NewCountdown { get; private set; }

        [GameEventParameter("来源玩家", "触发倒计时变化的玩家。", DslDisplayTypeName = "事件源")]
        public PlayerState? SourcePlayer => Source?.Player;

        [GameEventParameter("目标护符", "倒计时发生变化的护符。", DslDisplayTypeName = "事件目标")]
        public CardInstance? TargetCard => Target.FirstOrDefault()?.Card;

        public CountdownTickEvent(
            PlayerState sourcePlayer,
            CardInstance card,
            int changeAmount,
            DelayConfig? delayConfig = null,
            Guid? eventId = null,
            DateTime? timestamp = null)
            : base(EventType.CountdownTick, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(sourcePlayer ?? throw new ArgumentNullException(nameof(sourcePlayer)));
            Target.Add(TargetInfo.ForCard(card ?? throw new ArgumentNullException(nameof(card))));
            ChangeAmount = changeAmount;
            OldCountdown = card.CurrentCountdown;
            ConfigureDelay(delayConfig ?? DelayConfig.Disabled);
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult { Success = true };
            var card = TargetCard;
            if (card == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "CountdownTickEvent.Commit: target card is null." };
            }

            if (!card.CurrentCountdown.HasValue)
            {
                return result;
            }

            card.ApplyCountdownChange(ChangeAmount);
            NewCountdown = card.CurrentCountdown;

            if (OldCountdown.HasValue && OldCountdown.Value > 0 && NewCountdown == 0)
            {
                result.GeneratedEvents.Add(new LeaveFieldEvent(
                    card,
                    LeaveFieldKind.Destroy,
                    EventReasonSource.System,
                    card));
            }

            return result;
        }
    }
}
