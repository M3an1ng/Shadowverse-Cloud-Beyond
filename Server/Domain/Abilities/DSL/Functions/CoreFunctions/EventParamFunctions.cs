using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions
{
    public abstract class EventParamFunctionBase : BaseDslFunction
    {
        protected override int GetMinimumArgumentCount() => 1;
        protected override int? GetMaximumArgumentCount() => 2;

        public override IReadOnlyList<DslParameterInfo> Parameters => new[]
        {
            new DslParameterInfo("name", typeof(string), "事件参数名称（区分大小写不敏感）。"),
            new DslParameterInfo("defaultValue", ReturnType, "参数缺失或类型不匹配时的默认值。", isOptional: true)
        };

        protected static bool TryGetEventParameter(GameContext context, string name, out object? value)
        {
            value = null;
            if (context == null) return false;
            if (context.TriggeringEvent == null) return false;
            if (string.IsNullOrWhiteSpace(name)) return false;

            var ev = context.TriggeringEvent;

            if (ev is IRawEventBackedGameEvent rawBacked)
            {
                if (TryGetJsonValue(rawBacked.OutputPayload, name, out var node))
                {
                    value = node;
                    return true;
                }
            }

            var type = ev.GetType();
            var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null || prop.GetIndexParameters().Length != 0) return false;

            try
            {
                value = prop.GetValue(ev);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetJsonValue(JsonObject obj, string name, out JsonNode? node)
        {
            node = null;
            foreach (var kv in obj)
            {
                if (!string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase)) continue;
                node = kv.Value;
                return true;
            }
            return false;
        }

        protected static int ConvertToIntValue(object? value, int defaultValue)
        {
            if (value == null) return defaultValue;

            if (value is JsonNode node)
            {
                if (node is JsonValue jv)
                {
                    try { return jv.GetValue<int>(); } catch { }
                    try { return (int)jv.GetValue<double>(); } catch { }
                    try
                    {
                        var s = jv.GetValue<string>();
                        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return parsed;
                    }
                    catch { }
                }
                return defaultValue;
            }

            return ConvertToInt(value, defaultValue);
        }

        protected static bool ConvertToBoolValue(object? value, bool defaultValue)
        {
            if (value == null) return defaultValue;

            if (value is JsonNode node)
            {
                if (node is JsonValue jv)
                {
                    try { return jv.GetValue<bool>(); } catch { }
                    try { return jv.GetValue<int>() != 0; } catch { }
                    try
                    {
                        var s = jv.GetValue<string>();
                        if (bool.TryParse(s, out var parsed)) return parsed;
                    }
                    catch { }
                }
                return defaultValue;
            }

            return ConvertToBool(value, defaultValue);
        }

        protected static string ConvertToStringValue(object? value, string defaultValue)
        {
            if (value == null) return defaultValue;

            if (value is JsonNode node)
            {
                if (node is JsonValue jv)
                {
                    try { return jv.GetValue<string>(); } catch { }
                }

                return node.ToJsonString();
            }

            return ConvertToString(value, defaultValue);
        }

        protected static Guid ConvertToGuidValue(object? value, Guid defaultValue)
        {
            if (value == null) return defaultValue;

            if (value is JsonNode node)
            {
                if (node is JsonValue jv)
                {
                    try
                    {
                        var s = jv.GetValue<string>();
                        return Guid.TryParse(s, out var parsed) ? parsed : defaultValue;
                    }
                    catch
                    {
                        return defaultValue;
                    }
                }
                return defaultValue;
            }

            if (value is Guid g) return g;
            if (value is string str && Guid.TryParse(str, out var parsedGuid)) return parsedGuid;
            return defaultValue;
        }
    }

    public sealed class EventIntFunction : EventParamFunctionBase
    {
        public override string Name => "eventInt";
        public override Type ReturnType => typeof(int);
        public override string Description => "从 triggeringEvent 读取一个整型参数（优先 OutputRawEvent，其次反射属性）。";

        public override object? Execute(List<object?> args, GameContext context)
        {
            var name = ConvertToString(args[0], string.Empty);
            var defaultValue = args.Count >= 2 ? ConvertToInt(args[1], 0) : 0;

            if (!TryGetEventParameter(context, name, out var value))
                return defaultValue;

            return ConvertToIntValue(value, defaultValue);
        }
    }

    public sealed class EventBoolFunction : EventParamFunctionBase
    {
        public override string Name => "eventBool";
        public override Type ReturnType => typeof(bool);
        public override string Description => "从 triggeringEvent 读取一个布尔参数（优先 OutputRawEvent，其次反射属性）。";

        public override object? Execute(List<object?> args, GameContext context)
        {
            var name = ConvertToString(args[0], string.Empty);
            var defaultValue = args.Count >= 2 ? ConvertToBool(args[1], false) : false;

            if (!TryGetEventParameter(context, name, out var value))
                return defaultValue;

            return ConvertToBoolValue(value, defaultValue);
        }
    }

    public sealed class EventStringFunction : EventParamFunctionBase
    {
        public override string Name => "eventString";
        public override Type ReturnType => typeof(string);
        public override string Description => "从 triggeringEvent 读取一个字符串参数（优先 OutputRawEvent，其次反射属性）。";

        public override object? Execute(List<object?> args, GameContext context)
        {
            var name = ConvertToString(args[0], string.Empty);
            var defaultValue = args.Count >= 2 ? ConvertToString(args[1], string.Empty) : string.Empty;

            if (!TryGetEventParameter(context, name, out var value))
                return defaultValue;

            return ConvertToStringValue(value, defaultValue);
        }
    }

    public sealed class EventGuidFunction : EventParamFunctionBase
    {
        public override string Name => "eventGuid";
        public override Type ReturnType => typeof(Guid);
        public override string Description => "从 triggeringEvent 读取一个 Guid 参数（优先 OutputRawEvent，其次反射属性）。";

        public override object? Execute(List<object?> args, GameContext context)
        {
            var name = ConvertToString(args[0], string.Empty);
            var defaultValue = args.Count >= 2 ? ConvertToGuidValue(args[1], Guid.Empty) : Guid.Empty;

            if (!TryGetEventParameter(context, name, out var value))
                return defaultValue;

            return ConvertToGuidValue(value, defaultValue);
        }
    }

    public sealed class EventGuidListFunction : EventParamFunctionBase
    {
        public override string Name => "eventGuidList";
        public override Type ReturnType => typeof(List<Guid>);
        public override string Description => "从 triggeringEvent 读取一个 Guid 列表参数（优先 OutputRawEvent，其次反射属性）。";

        public override object? Execute(List<object?> args, GameContext context)
        {
            var name = ConvertToString(args[0], string.Empty);
            var defaultValue = args.Count >= 2 ? ConvertToGuidListValue(args[1], new List<Guid>()) : new List<Guid>();

            if (!TryGetEventParameter(context, name, out var value))
                return defaultValue;

            return ConvertToGuidListValue(value, defaultValue);
        }

        private static List<Guid> ConvertToGuidListValue(object? value, List<Guid> defaultValue)
        {
            if (value == null) return defaultValue;

            if (value is JsonNode node)
            {
                if (node is JsonArray arr)
                {
                    var parsed = new List<Guid>();
                    foreach (var item in arr)
                    {
                        if (item is JsonValue jv)
                        {
                            try
                            {
                                var s = jv.GetValue<string>();
                                if (Guid.TryParse(s, out var g) && g != Guid.Empty) parsed.Add(g);
                                continue;
                            }
                            catch { }
                        }

                        var raw = item?.ToJsonString();
                        if (raw != null)
                        {
                            raw = raw.Trim('"');
                            if (Guid.TryParse(raw, out var g) && g != Guid.Empty) parsed.Add(g);
                        }
                    }
                    return parsed;
                }

                if (node is JsonValue single)
                {
                    try
                    {
                        var s = single.GetValue<string>();
                        if (Guid.TryParse(s, out var g) && g != Guid.Empty) return new List<Guid> { g };
                    }
                    catch { }
                }

                return defaultValue;
            }

            if (value is Guid guid && guid != Guid.Empty)
            {
                return new List<Guid> { guid };
            }

            if (value is IEnumerable<Guid> guidEnumerable)
            {
                return guidEnumerable.Where(g => g != Guid.Empty).ToList();
            }

            if (value is IEnumerable<object> objEnumerable)
            {
                var parsed = new List<Guid>();
                foreach (var item in objEnumerable)
                {
                    if (item is Guid g && g != Guid.Empty)
                    {
                        parsed.Add(g);
                        continue;
                    }

                    if (item is string s && Guid.TryParse(s, out var parsedGuid) && parsedGuid != Guid.Empty)
                    {
                        parsed.Add(parsedGuid);
                    }
                }
                return parsed;
            }

            return defaultValue;
        }
    }
}
