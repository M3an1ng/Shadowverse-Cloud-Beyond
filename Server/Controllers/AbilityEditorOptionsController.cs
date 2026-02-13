using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Services;

namespace ShadowverseBeyond.Server.Controllers
{
    [ApiController]
    [Route("api/abilityEditorOptions")]
    public class AbilityEditorOptionsController : ControllerBase
    {
        private static readonly ConcurrentDictionary<EventType, Type?> ReplaceEventTypeCache = new();
        private static readonly ConcurrentDictionary<EventType, IReadOnlyList<EffectParameterMetadataDto>> ReplaceEventParamCache = new();

        [HttpGet("{key}")]
        public ActionResult<Dictionary<string, string>> Get(string key)
        {
            // Placeholder implementation: no option providers are registered yet.
            // Returning an empty map allows the frontend to render a dropdown without hardcoding.
            return Ok(new Dictionary<string, string>());
        }

        [HttpGet("replaceEventParameters/{eventType}")]
        public ActionResult<IReadOnlyList<EffectParameterMetadataDto>> GetReplaceEventParameters(string eventType)
        {
            if (!Enum.TryParse<EventType>(eventType, true, out var replacementEventType))
            {
                return BadRequest($"Unknown event type '{eventType}'.");
            }

            var result = ReplaceEventParamCache.GetOrAdd(replacementEventType, BuildReplaceEventParameters);
            return Ok(result);
        }

        private static IReadOnlyList<EffectParameterMetadataDto> BuildReplaceEventParameters(EventType eventType)
        {
            var eventClrType = ResolveEventType(eventType);
            if (eventClrType == null)
            {
                return Array.Empty<EffectParameterMetadataDto>();
            }

            var descriptor = EventParameterCache.GetDescriptor(eventClrType);
            return descriptor.Parameters
                .Where(p => p.Scope == GameEventParameterScope.Input || p.Scope == GameEventParameterScope.Both)
                .Select(binding =>
                {
                    var controlType = InferControlType(binding.PropertyType);
                    var enumName = controlType == UiControlType.Dropdown
                        ? TypeNameHelper.TryGetEnumName(binding.PropertyType)
                        : null;

                    return new EffectParameterMetadataDto
                    {
                        PropertyName = binding.PropertyName,
                        DisplayName = string.IsNullOrWhiteSpace(binding.DisplayName) ? binding.PropertyName : binding.DisplayName,
                        Description = binding.Description ?? string.Empty,
                        TypeName = TypeNameHelper.GetCleanTypeName(binding.PropertyType),
                        ControlType = controlType,
                        EnumName = enumName,
                        AllowDsl = true,
                        DslType = string.IsNullOrWhiteSpace(binding.DslDisplayTypeName)
                            ? TypeNameHelper.GetDslTypeName(binding.PropertyType)
                            : binding.DslDisplayTypeName
                    };
                })
                .ToList();
        }

        private static UiControlType InferControlType(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            if (underlying.IsEnum)
            {
                return UiControlType.Dropdown;
            }

            if (underlying == typeof(bool))
            {
                return UiControlType.Checkbox;
            }

            if (IsNumericType(underlying))
            {
                return UiControlType.NumberStepper;
            }

            if (IsIntListType(type))
            {
                return UiControlType.TextInput;
            }

            return UiControlType.TextInput;
        }

        private static bool IsNumericType(Type type)
        {
            return type == typeof(byte)
                || type == typeof(sbyte)
                || type == typeof(short)
                || type == typeof(ushort)
                || type == typeof(int)
                || type == typeof(uint)
                || type == typeof(long)
                || type == typeof(ulong)
                || type == typeof(float)
                || type == typeof(double)
                || type == typeof(decimal);
        }

        private static bool IsIntListType(Type type)
        {
            if (!type.IsGenericType)
            {
                return false;
            }

            var genericType = type.GetGenericTypeDefinition();
            if (genericType != typeof(List<>)
                && genericType != typeof(IReadOnlyList<>)
                && genericType != typeof(IEnumerable<>))
            {
                return false;
            }

            var elementType = Nullable.GetUnderlyingType(type.GetGenericArguments()[0]) ?? type.GetGenericArguments()[0];
            return elementType == typeof(int);
        }

        private static Type? ResolveEventType(EventType eventType)
        {
            return ReplaceEventTypeCache.GetOrAdd(eventType, static targetEventType =>
            {
                var eventInterfaceType = typeof(IGameEvent);

                static IEnumerable<Type> SafeGetTypes(Assembly assembly)
                {
                    try
                    {
                        return assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        return ex.Types.Where(t => t != null)!;
                    }
                    catch
                    {
                        return Array.Empty<Type>();
                    }
                }

                var candidates = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic)
                    .SelectMany(SafeGetTypes)
                    .Where(t => t.IsClass && !t.IsAbstract && eventInterfaceType.IsAssignableFrom(t))
                    .Select(t => new { Type = t, Metadata = t.GetCustomAttribute<GameEventMetadataAttribute>() })
                    .Where(x => x.Metadata != null && x.Metadata.EventType == targetEventType)
                    .ToList();

                return candidates
                    .OrderByDescending(x => GetVisibilityRank(x.Metadata!.Visibility))
                    .ThenBy(x => x.Type.Name, StringComparer.Ordinal)
                    .Select(x => x.Type)
                    .FirstOrDefault();
            });
        }

        private static int GetVisibilityRank(EventMetadataVisibility visibility)
        {
            return visibility switch
            {
                EventMetadataVisibility.Visible => 2,
                EventMetadataVisibility.System => 1,
                EventMetadataVisibility.Hidden => 0,
                _ => 0
            };
        }
    }
}
