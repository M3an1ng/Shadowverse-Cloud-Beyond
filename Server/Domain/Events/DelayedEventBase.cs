using System;

namespace ShadowverseBeyond.Server.Domain.Events
{
    public abstract class DelayedEventBase : GameEventBase, IDelayedEvent
    {
        private bool _delayLocked;

        public DelayConfig DelayConfig { get; private set; } = DelayConfig.Disabled;

        protected DelayedEventBase(EventType eventType, Guid? eventId = null, DateTime? timestamp = null)
            : base(eventType, eventId, timestamp)
        {
        }

        public void ConfigureDelay(DelayConfig config)
        {
            if (_delayLocked)
            {
                throw new InvalidOperationException("Delay configuration is locked for this event.");
            }

            DelayConfig = config.Normalize();
        }

        public void LockDelayConfig()
        {
            _delayLocked = true;
        }
    }
}
