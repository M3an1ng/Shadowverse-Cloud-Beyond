using System;
using System.Collections.Generic;
using System.Linq;

namespace ShadowverseBeyond.Server.Services
{
    public static class TypeNameHelper
    {
        public static string GetCleanTypeName(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
            {
                return GetCleanTypeName(underlying);
            }

            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();
                var genericArgs = type.GetGenericArguments().Select(GetCleanTypeName).ToArray();

                if (genericDef == typeof(List<>))
                {
                    return $"List<{genericArgs[0]}>";
                }

                if (genericDef == typeof(IEnumerable<>))
                {
                    return $"IEnumerable<{genericArgs[0]}>";
                }

                if (genericDef == typeof(IReadOnlyList<>))
                {
                    return $"IReadOnlyList<{genericArgs[0]}>";
                }

                var name = genericDef.Name;
                var tick = name.IndexOf('`');
                if (tick >= 0)
                {
                    name = name.Substring(0, tick);
                }

                return $"{name}<{string.Join(", ", genericArgs)}>";
            }

            return type.Name;
        }

        public static string? TryGetEnumName(Type type)
        {
            if (type == null) return null;

            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
            {
                return TryGetEnumName(underlying);
            }

            return type.IsEnum ? type.Name : null;
        }
    }
}

