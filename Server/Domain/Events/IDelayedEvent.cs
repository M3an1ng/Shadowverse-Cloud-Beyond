using System;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Marker interface for events that should be delayed until a parent event's
    /// direct effects have finished executing.
    /// </summary>
    public interface IDelayedEvent
    {
        /// <summary>
        /// Delay configuration for this event instance.
        /// </summary>
        DelayConfig DelayConfig { get; }

        /// <summary>
        /// Configures delay behavior before the event is enqueued.
        /// </summary>
        void ConfigureDelay(DelayConfig config);

        /// <summary>
        /// Prevents further delay configuration changes.
        /// </summary>
        void LockDelayConfig();
    }
}
