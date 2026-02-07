using System;
using System.Collections;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation
{
    public readonly record struct DslTraceValue(string Display, bool IsScalar)
    {
        private const int MaxStringLength = 128;

        public static DslTraceValue From(object? value)
        {
            if (value == null)
            {
                return new DslTraceValue("null", true);
            }

            switch (value)
            {
                case string text:
                    return new DslTraceValue(Truncate(text), true);
                case char ch:
                    return new DslTraceValue(ch.ToString(), true);
                case bool b:
                    return new DslTraceValue(b ? "true" : "false", true);
                case Enum e:
                    return new DslTraceValue(e.ToString(), true);
                case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                    return new DslTraceValue(global::System.Convert.ToString(value, global::System.Globalization.CultureInfo.InvariantCulture) ?? "0", true);
                case DateTime dt:
                    return new DslTraceValue(dt.ToString("O"), true);
                case Guid guid:
                    return new DslTraceValue(guid.ToString(), true);
                case ICollection collection:
                    return new DslTraceValue($"{value.GetType().Name}(Count={collection.Count})", false);
                default:
                    if (value is IEnumerable)
                    {
                        return new DslTraceValue(value.GetType().Name, false);
                    }
                    return new DslTraceValue(value.GetType().Name, false);
            }
        }

        private static string Truncate(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= MaxStringLength)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, MaxStringLength) + "...";
        }
    }
}
