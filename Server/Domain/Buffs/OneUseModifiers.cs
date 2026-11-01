using System.Linq;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;

namespace ShadowverseBeyond.Server.Domain.Buffs;
public static class OneUseModifiers
{
    public static DomainOperationResult Consume(CardInstance card, AbilityKeyword keyword, GameContext context)
    {
        var result = new DomainOperationResult { Success = true };
        var buffs = context.BuffManager.GetBuffsForCard(card.InstanceId)
            .Where(buff => buff.IsActive && buff.RemoveAfterUse && buff.Effects.OfType<AbilityKeywordBuffEffect>()
                .Any(effect => effect.Modifications.Any(mod => mod.Keyword == keyword))).ToList();
        if (buffs.Count == 0) return result;
        // Keep an inactive lifetime marker so a continuous aura cannot recreate an already consumed use.
        // Normal zone cleanup still removes this marker with its originating aura.
        foreach (var buff in buffs) buff.IsActive = false;
        context.BuffService.InvalidateDerivedState(context.CurrentGameState, card.InstanceId);
        result.GeneratedEvents.Add(new CardInstanceUpdatedEvent(new[] { card }));
        return result;
    }
}
