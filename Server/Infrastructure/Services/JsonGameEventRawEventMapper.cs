using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Dtos.Replay;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ShadowverseBeyond.Server.Infrastructure.Services
{
    public sealed class JsonGameEventRawEventMapper : IGameEventRawEventMapper
    {
        private static readonly ConcurrentDictionary<Type, IReadOnlyList<ParameterAccessor>> AccessorCache = new();

        public string MapToOutputRawEventJson(GameState gameState, IGameEvent gameEvent)
        {
            if (gameEvent == null) throw new ArgumentNullException(nameof(gameEvent));
            if (gameState == null) throw new ArgumentNullException(nameof(gameState));

            // Output RawEvent is the authoritative fact record. It may include both input and output parameters.
            var payload = BuildPayload(gameState, gameEvent, scopeFilter: GameEventParameterScope.Both);

            var envelope = new RawEventEnvelopeDto(
                SchemaVersion: 2,
                EventType: gameEvent.EventType.ToString(),
                EventId: gameState.InGameIds.Format(InGameIdKind.Event, gameEvent.EventId),
                TimestampUtc: gameEvent.Timestamp,
                SourceId: TryFormatKnownGuid(gameState, gameEvent.Source?.InstanceId, out var src) ? src : null,
                TargetIds: gameEvent.Target?.Select(t =>
                    TryFormatKnownGuid(gameState, t.InstanceId, out var tid) ? tid : null)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Cast<string>()
                    .ToArray() ?? Array.Empty<string>(),
                Payload: payload);

            return JsonSerializer.Serialize(envelope, new JsonSerializerOptions
            {
                WriteIndented = false
            });
        }

        private static JsonObject BuildPayload(GameState gameState, IGameEvent gameEvent, GameEventParameterScope scopeFilter)
        {
            var accessors = AccessorCache.GetOrAdd(gameEvent.GetType(), BuildAccessors);
            var obj = new JsonObject();

            foreach (var acc in accessors)
            {
                if (!ScopeMatches(scopeFilter, acc.Scope)) continue;

                object? rawValue;
                try
                {
                    rawValue = acc.Getter(gameEvent);
                }
                catch
                {
                    continue;
                }

                if (!TryConvertToStableJson(gameState, acc.PropertyName, rawValue, out var node)) continue;
                obj[acc.PropertyName] = node;
            }

            return obj;
        }

        private static bool ScopeMatches(GameEventParameterScope filter, GameEventParameterScope paramScope)
        {
            if (filter == GameEventParameterScope.Both) return true;
            if (paramScope == GameEventParameterScope.Both) return true;
            return filter == paramScope;
        }

        private static IReadOnlyList<ParameterAccessor> BuildAccessors(Type type)
        {
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(p => (Property: p, Attr: p.GetCustomAttribute<GameEventParameterAttribute>()))
                .Where(x => x.Attr != null && x.Property.GetIndexParameters().Length == 0)
                .ToList();

            var result = new List<ParameterAccessor>(props.Count);
            foreach (var item in props)
            {
                var getter = CompileGetter(type, item.Property);
                result.Add(new ParameterAccessor(
                    PropertyName: item.Property.Name,
                    Scope: item.Attr!.Scope,
                    Getter: getter));
            }

            return result;
        }

        private static Func<IGameEvent, object?> CompileGetter(Type declaringType, PropertyInfo property)
        {
            var param = Expression.Parameter(typeof(IGameEvent), "e");
            var casted = Expression.Convert(param, declaringType);
            var access = Expression.Property(casted, property);
            var boxed = Expression.Convert(access, typeof(object));
            return Expression.Lambda<Func<IGameEvent, object?>>(boxed, param).Compile();
        }

        private static bool TryConvertToStableJson(GameState gameState, string propertyName, object? value, out JsonNode? node)
        {
            node = null;

            if (value == null)
            {
                node = null;
                return true;
            }

            switch (value)
            {
                case string s:
                    node = JsonValue.Create(s);
                    return true;
                case bool b:
                    node = JsonValue.Create(b);
                    return true;
                case int i:
                    node = JsonValue.Create(i);
                    return true;
                case long l:
                    node = JsonValue.Create(l);
                    return true;
                case float f:
                    node = JsonValue.Create(f);
                    return true;
                case double d:
                    node = JsonValue.Create(d);
                    return true;
                case decimal dec:
                    node = JsonValue.Create(dec);
                    return true;
                case Guid g:
                    if (TryFormatGuid(gameState, propertyName, g, out var stableId))
                    {
                        node = JsonValue.Create(stableId);
                        return true;
                    }
                    return false;
                case DateTime dt:
                    node = JsonValue.Create(dt);
                    return true;
                case Enum e:
                    node = JsonValue.Create(e.ToString());
                    return true;
                case PlayerState player:
                    node = JsonValue.Create(gameState.InGameIds.Format(InGameIdKind.Player, player.PlayerId));
                    return true;
                case CardInstance card:
                    node = JsonValue.Create(gameState.InGameIds.Format(InGameIdKind.Card, card.InstanceId));
                    return true;
                case ShadowverseBeyond.Server.Domain.Buffs.Buff buff:
                    node = JsonValue.Create(gameState.InGameIds.Format(InGameIdKind.Buff, buff.Id));
                    return true;
            }

            if (value is IEnumerable enumerable && value is not string)
            {
                var arr = new JsonArray();
                foreach (var item in enumerable.Cast<object?>())
                {
                    if (!TryConvertToStableJson(gameState, propertyName, item, out var child)) continue;
                    arr.Add(child);
                }

                node = arr;
                return true;
            }

            return false;
        }

        private static bool TryFormatGuid(GameState gameState, string propertyName, Guid guid, out string stableId)
        {
            stableId = string.Empty;
            if (guid == Guid.Empty) return false;

            if (TryFormatKnownGuid(gameState, guid, out stableId)) return true;

            if (propertyName.Contains("Player", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("Winner", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("Loser", StringComparison.OrdinalIgnoreCase))
            {
                stableId = gameState.InGameIds.Format(InGameIdKind.Player, guid);
                return true;
            }

            if (propertyName.Contains("Buff", StringComparison.OrdinalIgnoreCase))
            {
                stableId = gameState.InGameIds.Format(InGameIdKind.Buff, guid);
                return true;
            }

            if (propertyName.Contains("Card", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("Unit", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("Uid", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("Uids", StringComparison.OrdinalIgnoreCase))
            {
                stableId = gameState.InGameIds.Format(InGameIdKind.Card, guid);
                return true;
            }

            if (propertyName.Contains("Event", StringComparison.OrdinalIgnoreCase))
            {
                stableId = gameState.InGameIds.Format(InGameIdKind.Event, guid);
                return true;
            }

            return false;
        }

        private static bool TryFormatKnownGuid(GameState gameState, Guid? guid, out string stableId)
        {
            stableId = string.Empty;
            if (!guid.HasValue || guid.Value == Guid.Empty) return false;
            var g = guid.Value;

            if (g == gameState.Player1.PlayerId || g == gameState.Player2.PlayerId)
            {
                stableId = gameState.InGameIds.Format(InGameIdKind.Player, g);
                return true;
            }

            if (gameState.FindCardInstance(g) != null)
            {
                stableId = gameState.InGameIds.Format(InGameIdKind.Card, g);
                return true;
            }

            if (gameState.BuffManager?.FindBuffInstance(g) != null)
            {
                stableId = gameState.InGameIds.Format(InGameIdKind.Buff, g);
                return true;
            }

            return false;
        }

        private sealed record ParameterAccessor(
            string PropertyName,
            GameEventParameterScope Scope,
            Func<IGameEvent, object?> Getter);
    }
}
