using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation
{
    public sealed class DslTraceCollector : IDslTraceCollector
    {
        private readonly Dictionary<string, DslTraceValue> _values = new(StringComparer.Ordinal);

        public bool IsEnabled { get; }

        public DslTraceCollector(bool isEnabled = true)
        {
            IsEnabled = isEnabled;
        }

        public IReadOnlyDictionary<string, DslTraceValue> Values => _values;

        public void Capture(string expression, object? value)
        {
            if (!IsEnabled || string.IsNullOrWhiteSpace(expression))
            {
                return;
            }

            _values[expression] = DslTraceValue.From(value);
        }

        public bool TryGetValue(string expression, out DslTraceValue value)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                value = default;
                return false;
            }

            return _values.TryGetValue(expression, out value);
        }
    }
}
