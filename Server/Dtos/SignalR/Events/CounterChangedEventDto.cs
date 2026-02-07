using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    public class CounterChangedEventDto : GameEventDto
    {
        public Guid? TargetCardInstanceId { get; }
        public Guid? TargetPlayerId { get; }
        public string CounterName { get; }
        public int OldValue { get; }
        public int NewValue { get; }

        public CounterChangedEventDto(Guid sourcePlayerId, Guid? targetCardInstanceId, Guid? targetPlayerId, string counterName, int oldValue, int newValue)
            : base(sourcePlayerId, "CounterChanged")
        {
            TargetCardInstanceId = targetCardInstanceId;
            TargetPlayerId = targetPlayerId;
            CounterName = counterName ?? string.Empty;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}
