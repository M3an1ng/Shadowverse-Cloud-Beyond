using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers;

[EffectHandler(EffectType.SettleEvidence)]
public class SettleEvidenceEffectHandler(ILogger<SettleEvidenceEffectHandler> logger)
    : EffectHandlerBase<SettleEvidenceEffectParams>(logger)
{
    public override GameStateActionResult Apply(Effect effect, GameContext context, SettleEvidenceEffectParams parameters)
    {
        var card = effect.SelectedTargets.FirstOrDefault(t => t.Type == TargetType.Card)?.Card;
        var result = new GameStateActionResult { Success = true };
        if (card == null || card.CurrentZone != CardZone.Field || card.CardType != CardType.Amulet || !card.HasKeyword(CardKeyword.Evidence))
            return result;
        var owner = context.CurrentGameState.GetPlayerState(card.OwnerPlayerId);
        if (owner == null) return new() { Success = false, ErrorMessage = "找不到证据护符的所属玩家。" };
        var others = owner.Field.Where(c => c.InstanceId != card.InstanceId && c.CardType == CardType.Amulet && c.HasKeyword(CardKeyword.Evidence)).ToList();
        var old = card.Evidence;
        var total = 1L + others.Sum(c => (long)c.Evidence);
        card.SetCounter(CardInstance.EvidenceCounterKey, (int)Math.Min(int.MaxValue, total));
        if (old != card.Evidence)
            result.GeneratedEvents.Add(new CounterChangedEvent(card, CardInstance.EvidenceCounterKey, old, card.Evidence));
        foreach (var other in others)
            result.GeneratedEvents.Add(new LeaveFieldEvent(other, LeaveFieldKind.Vanish, EventReasonSource.System, card));
        return result;
    }
}
