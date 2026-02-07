using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation
{
    internal sealed class DslCollectionCoercionException : InvalidOperationException
    {
        public DslCollectionCoercionException(string message)
            : base(message)
        {
        }
    }

    internal static class DslCollectionCoercion
    {
        public static bool IsCollectionType(Type type)
        {
            return TryGetEnumerableElementType(type, out _);
        }

        public static bool IsCollectionValue(object? value)
        {
            return value is IEnumerable && value is not string;
        }

        public static bool TryGetEnumerableElementType(Type type, out Type elementType)
        {
            elementType = typeof(object);
            if (type == typeof(string))
            {
                return false;
            }

            if (type.IsArray)
            {
                elementType = type.GetElementType() ?? typeof(object);
                return true;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }

            var enumerableInterface = type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            if (enumerableInterface != null)
            {
                elementType = enumerableInterface.GetGenericArguments()[0];
                return true;
            }

            if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                elementType = typeof(object);
                return true;
            }

            return false;
        }

        public static bool TryCoerceToObjectEnumerable(object? value, out IEnumerable<object?> enumerable)
        {
            if (value == null)
            {
                enumerable = Enumerable.Empty<object?>();
                return true;
            }

            if (value is string)
            {
                enumerable = Enumerable.Empty<object?>();
                return false;
            }

            if (value is IEnumerable sequence)
            {
                enumerable = sequence.Cast<object?>();
                return true;
            }

            enumerable = new[] { value };
            return true;
        }

        public static IEnumerable<object?> CoerceToObjectEnumerableOrSingle(object? value)
        {
            if (value == null)
            {
                return Enumerable.Empty<object?>();
            }

            if (value is string)
            {
                return new[] { value };
            }

            if (value is IEnumerable sequence)
            {
                return sequence.Cast<object?>();
            }

            return new[] { value };
        }
    }
}
