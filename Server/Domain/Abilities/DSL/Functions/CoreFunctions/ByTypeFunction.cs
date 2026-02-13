using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions
{
    /// <summary>
    /// byType(source, eventType): filters any event enumerable by EventType.
    /// </summary>
    public sealed class ByTypeFunction : BaseDslFunction
    {
        public override string Name => "byType";
        public override Type ReturnType => typeof(IEnumerable<IGameEvent>);
        public override string Description => "按事件类型筛选事件集合。";

        public override IReadOnlyList<DslParameterInfo> Parameters => new List<DslParameterInfo>
        {
            new DslParameterInfo("source", typeof(IEnumerable<IGameEvent>), "事件集合来源。"),
            new DslParameterInfo("eventType", typeof(EventType), "事件类型（例如 Damage / Destroy）。")
        };

        protected override int? GetExpectedArgumentCount() => 2;

        protected override bool ValidateArgumentTypes(List<object?> args)
        {
            if (args.Count != 2)
            {
                return false;
            }

            var source = args[0];
            if (source != null && !IsCollection(source))
            {
                return false;
            }

            return TryResolveEventType(args[1], out _);
        }

        public override object? Execute(List<object?> args, GameContext context)
        {
            if (args.Count != 2)
            {
                return Enumerable.Empty<IGameEvent>();
            }

            if (!TryResolveEventType(args[1], out var eventType))
            {
                return Enumerable.Empty<IGameEvent>();
            }

            var source = args[0];
            if (source == null)
            {
                return Enumerable.Empty<IGameEvent>();
            }

            var filtered = ConvertToEnumerable(source)
                .OfType<IGameEvent>()
                .Where(evt => evt.EventType == eventType)
                .ToList();

            return filtered;
        }

        private static bool TryResolveEventType(object? raw, out EventType eventType)
        {
            eventType = EventType.Unknown;

            if (raw == null)
            {
                return false;
            }

            if (raw is EventType enumValue)
            {
                eventType = enumValue;
                return true;
            }

            if (raw is string text)
            {
                var normalized = text.Trim().Trim('"', '\'');
                if (normalized.StartsWith("EventType.", StringComparison.OrdinalIgnoreCase))
                {
                    normalized = normalized.Substring("EventType.".Length);
                }

                return Enum.TryParse(normalized, true, out eventType);
            }

            return false;
        }
    }
}
