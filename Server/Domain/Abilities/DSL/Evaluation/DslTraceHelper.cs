namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation
{
    public static class DslTraceHelper
    {
        public static T Capture<T>(IDslTraceCollector? collector, string expression, T value)
        {
            if (collector == null || !collector.IsEnabled)
            {
                return value;
            }

            collector.Capture(expression, value);
            return value;
        }
    }
}
