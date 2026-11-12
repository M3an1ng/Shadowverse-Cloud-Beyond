using System.Text.Json;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

internal static class AiTargetScoring
{
    internal sealed record Effect(EffectType Type, double? Amount);

    private static bool IndependentOfCardTarget(EffectConfig effect)
    {
        // The verified Sacrifice branch changes the player's resources and targets their leader.
        if (AiPpRefundFacts.TryReadOwnDamageRefund(effect, out _, out _, out _)) return true;
        // These native handlers resolve a fixed player scope, not the chosen battlefield/hand card.
        // Dynamic parameters or follow-up chains can depend on the selected card, so keep fallback.
        if(effect.RepeatCount!=0||effect.RepeatCountValue!=null||effect.RepeatCondition!=null
            ||effect.ChainedAbilities.Count>0||effect.Outputs?.Count is >0
            ||effect.Parameters.Values.Any(v=>v is not StaticValueConfig))return false;
        string? Text(string key)=>effect.Parameters.GetValueOrDefault(key) is StaticValueConfig value
            ?value.Value is JsonElement {ValueKind:JsonValueKind.String} json?json.GetString():value.Value as string:null;
        bool FalseOrMissing(string key)=>!effect.Parameters.TryGetValue(key,out var value)
            ||value is StaticValueConfig s&&(s.Value is false||s.Value is JsonElement {ValueKind:JsonValueKind.False});
        if(Text("PlayerScope") is not ("self" or "opponent" or "both"))return false;
        return effect.Type==EffectType.Draw&&FalseOrMissing("UseSearchMode")
                &&effect.Parameters.Keys.All(k=>k is "DrawAmount" or "PlayerScope" or "UseSearchMode")
            ||effect.Type==EffectType.AddEmblem&&string.Equals(Text("CardSourceMode"),"definition",StringComparison.OrdinalIgnoreCase)
                &&effect.Parameters.Keys.All(k=>k is "CardDefinitionId" or "Amount" or "PlayerScope" or "CardSourceMode" or "PreventDuplicate");
    }
    internal static IReadOnlyList<Effect> FindEffects(IEnumerable<EffectConfig> effects,string key)=>FindEffectsForChoice(effects,key,true);
    internal static IReadOnlyList<Effect> FindEffectsForChoice(IEnumerable<EffectConfig> effects, string key,bool isolateIndependentEffects)
    {
        if (string.IsNullOrWhiteSpace(key)) return Array.Empty<Effect>();
        var targets = new Dictionary<string, List<TargetInfo>>(StringComparer.OrdinalIgnoreCase) { [key] = new() };
        // Choose opens a separate mode request; it does not apply its parent card targets.
        var matching = effects.Where(effect => effect.IsEnabled && (!AiRecoveryPolicy.Enabled || effect.Type != EffectType.Choose)
            && (!isolateIndependentEffects || !IndependentOfCardTarget(effect))
            && EffectTargetSelector.TrySelectTargets(effect.TargetSelector, targets, out _)).OrderBy(effect => effect.ExecutionOrder).ToArray();

        // Discard choices use hand resource valuation at the caller.
        if (matching.Length > 0 && matching.All(effect => effect.Type == EffectType.Discard))
            return matching.Select(effect => new Effect(effect.Type, null)).ToArray();

        // Composite and scripted effects keep the existing model ranking.
        if (matching.Any(effect => effect.Type is not (EffectType.Damage or EffectType.Destroy or EffectType.Vanish or EffectType.Heal)))
            return Array.Empty<Effect>();

        return matching.Select(effect => new Effect(effect.Type, ReadAmount(effect))).ToArray();
    }

    internal static double Score(CardInstanceDto card, Guid actor, IReadOnlyList<Effect> effects)
    {
        var friendly = card.OwnerPlayerId == actor;
        var leader = card.Type == CardType.Leader;
        var threat = 2 + card.CurrentAttack * 1.15 + card.CurrentHealth * .35;
        var health = Math.Max(0d, card.CurrentHealth);
        var damage = 0d;
        var healing = 0d;
        var removed = false;
        var shield = card.Keywords.Contains(CardKeyword.Shield);
        foreach (var effect in effects)
        {
            if (removed) break;
            switch (effect.Type)
            {
                case EffectType.Damage:
                    if (shield)
                    {
                        shield = false;
                        damage += .5;
                        break;
                    }
                    var hit = Math.Min(health, effect.Amount ?? 1);
                    damage += hit;
                    if (effect.Amount.HasValue) health -= hit;
                    // An unknown formula gives chip-damage credit, without assuming lethal.
                    removed = effect.Amount.HasValue && health <= 0;
                    break;
                case EffectType.Destroy:
                    removed = !leader && !card.Keywords.Contains(CardKeyword.DestroyResist) && !card.Keywords.Contains(CardKeyword.EffectImmune);
                    break;
                case EffectType.Vanish:
                    removed = !leader && !card.Keywords.Contains(CardKeyword.EffectImmune);
                    break;
                case EffectType.Heal:
                    var restored = Math.Min(Math.Max(0, card.MaxHealth - health), effect.Amount ?? 1);
                    health += restored;
                    healing += restored;
                    break;
            }
        }

        var harm = removed ? (leader ? 100 : threat) : damage * (leader ? .8 : .55);
        var recovery = healing * (leader ? 1.2 : .75);
        return friendly ? recovery - harm : harm - recovery;
    }

    internal static double? ReadAmount(EffectConfig effect)
    {
        var key = effect.Type == EffectType.Damage ? "DamageAmount" : "HealAmount";
        if (!effect.Parameters.TryGetValue(key, out var config) || config is not StaticValueConfig value)
            return null;
        return value.Value switch
        {
            int number => Math.Max(0, number),
            long number => Math.Max(0, number),
            double number when double.IsFinite(number) => Math.Max(0, number),
            JsonElement element when element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var number)
                && double.IsFinite(number) => Math.Max(0, number),
            _ => null
        };
    }
}
