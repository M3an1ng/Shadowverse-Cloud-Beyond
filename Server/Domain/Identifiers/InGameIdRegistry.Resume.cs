using System.Collections.Concurrent;

namespace ShadowverseBeyond.Server.Domain.Identifiers;

public sealed partial class InGameIdRegistry
{
    internal Dictionary<string, Guid> CaptureResumeState() => _reverse.ToDictionary(p => p.Key.ToString(),p => p.Value);

    internal void RestoreResumeState(Dictionary<string, Guid> entries)
    {
        _cards.Clear();_buffs.Clear();_abilities.Clear();_effects.Clear();_players.Clear();_choices.Clear();_events.Clear();_reverse.Clear();
        foreach (var (token,guid) in entries)
        {
            if (!InGameId.TryParse(token,out var id)) throw new InvalidDataException("invalid_resume_reference");
            var map = id.Kind switch
            {
                InGameIdKind.Card => _cards, InGameIdKind.Buff => _buffs, InGameIdKind.Ability => _abilities,
                InGameIdKind.Effect => _effects, InGameIdKind.Player => _players, InGameIdKind.Choice => _choices,
                InGameIdKind.Event => _events, _ => throw new InvalidDataException("invalid_resume_reference_kind")
            };
            map[guid] = id;
            _reverse[id] = guid;
        }
        _nextCardId = _cards.Values.Select(v => v.Value).DefaultIfEmpty().Max();
        _nextBuffId = _buffs.Values.Select(v => v.Value).DefaultIfEmpty().Max();
        _nextAbilityId = _abilities.Values.Select(v => v.Value).DefaultIfEmpty().Max();
        _nextEffectId = _effects.Values.Select(v => v.Value).DefaultIfEmpty().Max();
        _nextPlayerId = _players.Values.Select(v => v.Value).DefaultIfEmpty().Max();
        _nextChoiceId = _choices.Values.Select(v => v.Value).DefaultIfEmpty().Max();
        _nextEventId = _events.Values.Select(v => v.Value).DefaultIfEmpty().Max();
    }

    internal void ReplaceStateFrom(InGameIdRegistry source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        static void CopyMap(ConcurrentDictionary<Guid, InGameId> target, ConcurrentDictionary<Guid, InGameId> src)
        {
            target.Clear();
            foreach (var (k, v) in src)
            {
                target[k] = v;
            }
        }

        CopyMap(_cards, source._cards);
        CopyMap(_buffs, source._buffs);
        CopyMap(_abilities, source._abilities);
        CopyMap(_effects, source._effects);
        CopyMap(_players, source._players);
        CopyMap(_choices, source._choices);
        CopyMap(_events, source._events);

        _reverse.Clear();
        foreach (var (k, v) in source._reverse)
        {
            _reverse[k] = v;
        }

        _nextCardId = source._nextCardId;
        _nextBuffId = source._nextBuffId;
        _nextAbilityId = source._nextAbilityId;
        _nextEffectId = source._nextEffectId;
        _nextPlayerId = source._nextPlayerId;
        _nextChoiceId = source._nextChoiceId;
        _nextEventId = source._nextEventId;
    }
}
