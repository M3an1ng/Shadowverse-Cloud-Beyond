using System.Text.Json;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Services;

namespace ShadowverseBeyond.Server.Domain.Abilities.Auras;

public partial class AuraManager
{
    internal JsonElement CaptureResumeState() => JsonSerializer.SerializeToElement(_aurasById.Values.Select(a => new
        { a.AuraId,a.AuraKey,a.SourceAbilityId,a.SourceCardId,a.AuraIndex,a.BuffConfigs,a.ActiveTargets }), ReplayExecutionState.Json);

    internal void RestoreResumeState(JsonElement state)
    {
        foreach (var item in state.EnumerateArray())
        {
            var aura = new AuraInstance(item.GetProperty("auraId").GetGuid(),item.GetProperty("auraKey").GetString()!,
                item.GetProperty("sourceAbilityId").GetGuid(),item.GetProperty("sourceCardId").Deserialize<Guid?>(),
                item.GetProperty("auraIndex").GetInt32(),item.GetProperty("buffConfigs").Deserialize<List<BuffConfig>>(ReplayExecutionState.Json)!);
            aura.ActiveTargets.UnionWith(item.GetProperty("activeTargets").Deserialize<Guid[]>()!);
            _aurasById.Add(aura.AuraId,aura);
            _auraIdsByKey.Add(aura.AuraKey,aura.AuraId);
            if (!_auraIdsBySourceAbilityId.TryGetValue(aura.SourceAbilityId,out var abilities))
                _auraIdsBySourceAbilityId.Add(aura.SourceAbilityId,abilities = []);
            abilities.Add(aura.AuraId);
            if (aura.SourceCardId is { } card)
            {
                if (!_auraIdsBySourceCardId.TryGetValue(card,out var cards)) _auraIdsBySourceCardId.Add(card,cards = []);
                cards.Add(aura.AuraId);
            }
        }
    }

    internal void ReplaceStateFrom(AuraManager source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        _aurasById.Clear();
        _auraIdsByKey.Clear();
        _auraIdsBySourceAbilityId.Clear();
        _auraIdsBySourceCardId.Clear();

        foreach (var (auraId, inst) in source._aurasById)
        {
            var copy = new AuraInstance(
                inst.AuraId,
                inst.AuraKey,
                inst.SourceAbilityId,
                inst.SourceCardId,
                inst.AuraIndex,
                inst.BuffConfigs.ToList());
            copy.ActiveTargets.UnionWith(inst.ActiveTargets);
            _aurasById[auraId] = copy;
        }

        foreach (var (key, auraId) in source._auraIdsByKey)
        {
            _auraIdsByKey[key] = auraId;
        }

        foreach (var (abilityId, set) in source._auraIdsBySourceAbilityId)
        {
            _auraIdsBySourceAbilityId[abilityId] = new HashSet<Guid>(set);
        }

        foreach (var (cardId, set) in source._auraIdsBySourceCardId)
        {
            _auraIdsBySourceCardId[cardId] = new HashSet<Guid>(set);
        }
    }
}
