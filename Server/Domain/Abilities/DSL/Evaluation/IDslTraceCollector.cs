using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation
{
    public interface IDslTraceCollector
    {
        bool IsEnabled { get; }
        void Capture(string expression, object? value);
        bool TryGetValue(string expression, out DslTraceValue value);
        IReadOnlyDictionary<string, DslTraceValue> Values { get; }
    }
}
