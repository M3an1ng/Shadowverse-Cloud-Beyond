using System;

namespace ShadowverseBeyond.Server.Domain.Events
{
    public readonly struct DelayConfig
    {
        public static readonly DelayConfig Disabled = new(false, Guid.Empty, DelayMode.ParentEffectsComplete);

        public bool Enabled { get; }
        public Guid ParentEventId { get; }
        public DelayMode Mode { get; }

        private DelayConfig(bool enabled, Guid parentEventId, DelayMode mode)
        {
            Enabled = enabled;
            ParentEventId = parentEventId;
            Mode = mode;
        }

        public static DelayConfig Parent(Guid parentEventId)
        {
            return parentEventId == Guid.Empty
                ? Disabled
                : new DelayConfig(true, parentEventId, DelayMode.ParentEffectsComplete);
        }

        public static DelayConfig QueueDrained()
        {
            return new DelayConfig(true, Guid.Empty, DelayMode.QueueDrained);
        }

        public static DelayConfig Create(Guid parentEventId, DelayMode mode)
        {
            if (mode == DelayMode.QueueDrained)
            {
                return QueueDrained();
            }

            return Parent(parentEventId);
        }

        public DelayConfig Normalize()
        {
            if (!Enabled)
            {
                return Disabled;
            }

            if (Mode == DelayMode.QueueDrained)
            {
                return QueueDrained();
            }

            return Parent(ParentEventId);
        }

        public bool IsActive => Enabled && (Mode == DelayMode.QueueDrained || ParentEventId != Guid.Empty);
    }
}
