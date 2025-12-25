using System;
using System.Collections.Concurrent;
using System.Threading;

namespace ShadowverseBeyond.Server.Domain.Identifiers
{
    public sealed class InGameIdRegistry
    {
        private int _nextCardId;
        private int _nextBuffId;
        private int _nextAbilityId;
        private int _nextEffectId;
        private int _nextPlayerId;
        private int _nextChoiceId;
        private int _nextEventId;

        private readonly ConcurrentDictionary<Guid, InGameId> _cards = new();
        private readonly ConcurrentDictionary<Guid, InGameId> _buffs = new();
        private readonly ConcurrentDictionary<Guid, InGameId> _abilities = new();
        private readonly ConcurrentDictionary<Guid, InGameId> _effects = new();
        private readonly ConcurrentDictionary<Guid, InGameId> _players = new();
        private readonly ConcurrentDictionary<Guid, InGameId> _choices = new();
        private readonly ConcurrentDictionary<Guid, InGameId> _events = new();

        private readonly ConcurrentDictionary<InGameId, Guid> _reverse = new();

        public InGameId GetOrCreate(InGameIdKind kind, Guid guid)
        {
            if (guid == Guid.Empty) throw new ArgumentException("Guid cannot be empty.", nameof(guid));

            return kind switch
            {
                InGameIdKind.Card => GetOrCreateCore(_cards, InGameIdKind.Card, guid),
                InGameIdKind.Buff => GetOrCreateCore(_buffs, InGameIdKind.Buff, guid),
                InGameIdKind.Ability => GetOrCreateCore(_abilities, InGameIdKind.Ability, guid),
                InGameIdKind.Effect => GetOrCreateCore(_effects, InGameIdKind.Effect, guid),
                InGameIdKind.Player => GetOrCreateCore(_players, InGameIdKind.Player, guid),
                InGameIdKind.Choice => GetOrCreateCore(_choices, InGameIdKind.Choice, guid),
                InGameIdKind.Event => GetOrCreateCore(_events, InGameIdKind.Event, guid),
                _ => throw new NotSupportedException($"Unsupported kind: {kind}")
            };
        }

        public string Format(InGameIdKind kind, Guid guid)
        {
            return GetOrCreate(kind, guid).ToString();
        }

        public bool TryResolveGuid(InGameIdKind kind, string? token, out Guid guid)
        {
            guid = Guid.Empty;
            if (string.IsNullOrWhiteSpace(token)) return false;

            var trimmed = token.Trim();
            if (Guid.TryParse(trimmed, out guid)) return guid != Guid.Empty;

            if (!InGameId.TryParse(trimmed, out var inGameId)) return false;
            if (inGameId.Kind != kind) return false;

            return _reverse.TryGetValue(inGameId, out guid) && guid != Guid.Empty;
        }

        private InGameId GetOrCreateCore(ConcurrentDictionary<Guid, InGameId> map, InGameIdKind kind, Guid guid)
        {
            var id = map.GetOrAdd(guid, static (g, state) =>
            {
                var (k, registry) = state;
                var value = k switch
                {
                    InGameIdKind.Card => Interlocked.Increment(ref registry._nextCardId),
                    InGameIdKind.Buff => Interlocked.Increment(ref registry._nextBuffId),
                    InGameIdKind.Ability => Interlocked.Increment(ref registry._nextAbilityId),
                    InGameIdKind.Effect => Interlocked.Increment(ref registry._nextEffectId),
                    InGameIdKind.Player => Interlocked.Increment(ref registry._nextPlayerId),
                    InGameIdKind.Choice => Interlocked.Increment(ref registry._nextChoiceId),
                    InGameIdKind.Event => Interlocked.Increment(ref registry._nextEventId),
                    _ => throw new NotSupportedException($"Unsupported kind: {k}")
                };
                return new InGameId(k, value);
            }, (kind, this));

            _reverse.TryAdd(id, guid);
            return id;
        }
    }
}
