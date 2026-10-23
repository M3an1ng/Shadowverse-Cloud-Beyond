using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace ShadowverseBeyond.Server.Domain.Events.Metadata
{
    public sealed class EventParameterCache
    {
        private static readonly ConcurrentDictionary<Type, EventParameterDescriptor> Cache = new();

        public static EventParameterDescriptor GetDescriptor(Type eventType)
        {
            if (eventType == null) throw new ArgumentNullException(nameof(eventType));
            return Cache.GetOrAdd(eventType, BuildDescriptor);
        }

        public static bool TryGetBinding(Type eventType, string name, out EventParameterBinding binding)
        {
            binding = null!;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var descriptor = GetDescriptor(eventType);
            return descriptor.Lookup.TryGetValue(name, out binding);
        }

        public static bool TryGetRewritableBinding(
            Type eventType,
            string name,
            out EventParameterBinding binding,
            out string? errorMessage)
        {
            errorMessage = null;
            if (!TryGetBinding(eventType, name, out binding))
            {
                errorMessage = $"RewriteEventParamsEffect: parameter '{name}' is not rewritable.";
                return false;
            }

            if (!(binding.Scope == GameEventParameterScope.Input || binding.Scope == GameEventParameterScope.Both))
            {
                errorMessage = $"RewriteEventParamsEffect: parameter '{binding.PropertyName}' is not rewritable.";
                return false;
            }

            if (binding.Setter == null)
            {
                errorMessage = $"RewriteEventParamsEffect: parameter '{binding.PropertyName}' is not settable.";
                return false;
            }

            return true;
        }

        private static EventParameterDescriptor BuildDescriptor(Type eventType)
        {
            var bindings = new List<EventParameterBinding>();
            var lookup = new Dictionary<string, EventParameterBinding>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in EnumerateParameterProperties(eventType))
            {
                var attribute = property.GetCustomAttribute<GameEventParameterAttribute>();
                if (attribute == null)
                {
                    continue;
                }

                var getter = CreateGetter(property);
                var setter = CreateSetter(property);
                var binding = new EventParameterBinding(
                    property.Name,
                    attribute.DisplayName,
                    attribute.Description,
                    attribute.Scope,
                    attribute.ShowInTriggerTips,
                    attribute.DslDisplayTypeName,
                    property.PropertyType,
                    getter,
                    setter);

                bindings.Add(binding);
                lookup[property.Name] = binding;
                if (!string.IsNullOrWhiteSpace(attribute.DisplayName))
                {
                    lookup[attribute.DisplayName] = binding;
                }
            }

            return new EventParameterDescriptor(bindings, lookup);
        }

        private static IEnumerable<PropertyInfo> EnumerateParameterProperties(Type eventType)
        {
            var hierarchy = new Stack<Type>();
            for (var current = eventType;
                current != null && current != typeof(object) && typeof(IGameEvent).IsAssignableFrom(current);
                current = current.BaseType)
            {
                hierarchy.Push(current);
            }

            while (hierarchy.Count > 0)
            {
                var current = hierarchy.Pop();
                foreach (var property in current.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(p => p.GetIndexParameters().Length == 0))
                {
                    yield return property;
                }
            }
        }

        private static Func<IGameEvent, object?> CreateGetter(PropertyInfo property)
        {
            var instance = Expression.Parameter(typeof(IGameEvent), "instance");
            var castInstance = Expression.Convert(instance, property.DeclaringType!);
            var propertyAccess = Expression.Property(castInstance, property);
            var castResult = Expression.Convert(propertyAccess, typeof(object));
            return Expression.Lambda<Func<IGameEvent, object?>>(castResult, instance).Compile();
        }

        private static Action<IGameEvent, object?>? CreateSetter(PropertyInfo property)
        {
            var setMethod = property.GetSetMethod(true);
            if (setMethod != null)
            {
                return BuildSetter(property.DeclaringType!, property);
            }

            var declaringType = property.DeclaringType!;
            var backingField = declaringType.GetField($"<{property.Name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            if (backingField != null)
            {
                if (backingField.IsInitOnly || backingField.IsLiteral)
                {
                    return (instance, value) => backingField.SetValue(instance, value);
                }

                return BuildSetter(declaringType, backingField);
            }

            return null;
        }

        private static Action<IGameEvent, object?> BuildSetter(Type declaringType, PropertyInfo property)
        {
            var instance = Expression.Parameter(typeof(IGameEvent), "instance");
            var value = Expression.Parameter(typeof(object), "value");
            var castInstance = Expression.Convert(instance, declaringType);
            var castValue = Expression.Convert(value, property.PropertyType);
            var propertyAccess = Expression.Property(castInstance, property);
            var assign = Expression.Assign(propertyAccess, castValue);
            return Expression.Lambda<Action<IGameEvent, object?>>(assign, instance, value).Compile();
        }

        private static Action<IGameEvent, object?> BuildSetter(Type declaringType, FieldInfo field)
        {
            var instance = Expression.Parameter(typeof(IGameEvent), "instance");
            var value = Expression.Parameter(typeof(object), "value");
            var castInstance = Expression.Convert(instance, declaringType);
            var castValue = Expression.Convert(value, field.FieldType);
            var fieldAccess = Expression.Field(castInstance, field);
            var assign = Expression.Assign(fieldAccess, castValue);
            return Expression.Lambda<Action<IGameEvent, object?>>(assign, instance, value).Compile();
        }
    }

    public sealed class EventParameterDescriptor
    {
        public EventParameterDescriptor(
            IReadOnlyList<EventParameterBinding> parameters,
            IReadOnlyDictionary<string, EventParameterBinding> lookup)
        {
            Parameters = parameters;
            Lookup = lookup;
        }

        public IReadOnlyList<EventParameterBinding> Parameters { get; }
        public IReadOnlyDictionary<string, EventParameterBinding> Lookup { get; }
    }

    public sealed class EventParameterBinding
    {
        public EventParameterBinding(
            string propertyName,
            string displayName,
            string description,
            GameEventParameterScope scope,
            bool showInTriggerTips,
            string? dslDisplayTypeName,
            Type propertyType,
            Func<IGameEvent, object?> getter,
            Action<IGameEvent, object?>? setter)
        {
            PropertyName = propertyName;
            DisplayName = displayName;
            Description = description;
            Scope = scope;
            ShowInTriggerTips = showInTriggerTips;
            DslDisplayTypeName = dslDisplayTypeName;
            PropertyType = propertyType;
            Getter = getter;
            Setter = setter;
        }

        public string PropertyName { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public GameEventParameterScope Scope { get; }
        public bool ShowInTriggerTips { get; }
        public string? DslDisplayTypeName { get; }
        public Type PropertyType { get; }
        public Func<IGameEvent, object?> Getter { get; }
        public Action<IGameEvent, object?>? Setter { get; }
    }
}
