using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Domain.Events
{
    public enum EventInterceptKind
    {
        RewriteParameters = 0,
        ReplaceEvent = 1
    }

    public sealed class EventInterceptResult
    {
        public EventInterceptKind Kind { get; }
        public string? ParameterName { get; }
        public object? ParameterValue { get; }
        public EventType? ReplacementEventType { get; }
        public string? ReplacementReason { get; }
        public IReadOnlyDictionary<string, object?>? ReplacementParameters { get; }

        private EventInterceptResult(
            EventInterceptKind kind,
            string? parameterName,
            object? parameterValue,
            EventType? replacementEventType,
            string? replacementReason,
            IReadOnlyDictionary<string, object?>? replacementParameters)
        {
            Kind = kind;
            ParameterName = parameterName;
            ParameterValue = parameterValue;
            ReplacementEventType = replacementEventType;
            ReplacementReason = replacementReason;
            ReplacementParameters = replacementParameters;
        }

        public static EventInterceptResult RewriteParameter(string parameterName, object? parameterValue)
        {
            return new EventInterceptResult(EventInterceptKind.RewriteParameters, parameterName, parameterValue, null, null, null);
        }

        public static EventInterceptResult ReplaceEvent(EventType replacementEventType, string? replacementReason = null)
        {
            return new EventInterceptResult(EventInterceptKind.ReplaceEvent, null, null, replacementEventType, replacementReason, null);
        }

        public static EventInterceptResult ReplaceEvent(
            EventType replacementEventType,
            IReadOnlyDictionary<string, object?>? replacementParameters,
            string? replacementReason = null)
        {
            return new EventInterceptResult(EventInterceptKind.ReplaceEvent, null, null, replacementEventType, replacementReason, replacementParameters);
        }
    }
}
