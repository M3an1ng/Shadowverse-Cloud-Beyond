using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers;

[EffectHandler(EffectType.ModifyEvidence)]
public class ModifyEvidenceEffectHandler(ILogger<ModifyEvidenceEffectHandler> logger, ITargetProcessingService targets)
    : EffectHandlerBase<ModifyEvidenceEffectParams>(logger)
{
    public override GameStateActionResult Apply(Effect effect, GameContext context, ModifyEvidenceEffectParams parameters)
    {
        if (parameters.Amount < 0)
            return new() { Success = false, ErrorMessage = "证据数不能为负数。" };
        var players = PlayerScopeResolver.ResolvePlayerTargets(context, parameters.PlayerScope);
        return targets.ProcessTargets(context, players, (target, _) =>
        {
            var card = target.Player?.Field.FirstOrDefault(IsEvidence);
            if (card == null)
                return new() { Success = false, ErrorMessage = "没有可修改的证据护符。" };
            var old = card.Evidence;
            int value;
            switch (parameters.Operation)
            {
                case CounterOperation.Add:
                    value = card.SetCounter(CardInstance.EvidenceCounterKey, (int)Math.Min(int.MaxValue, (long)old + parameters.Amount));
                    break;
                case CounterOperation.Set:
                    value = card.SetCounter(CardInstance.EvidenceCounterKey, parameters.Amount);
                    break;
                case CounterOperation.Consume:
                    if (!card.TryConsumeCounter(CardInstance.EvidenceCounterKey, parameters.Amount))
                        return new() { Success = false, ErrorMessage = "证据数不足，无法消耗。" };
                    value = card.Evidence;
                    break;
                default:
                    return new() { Success = false, ErrorMessage = "不支持的证据数操作。" };
            }
            var result = new DomainOperationResult { Success = true };
            if (value != old || value == 0)
                result.GeneratedEvents.Add(new CounterChangedEvent(card, CardInstance.EvidenceCounterKey, old, value));
            return result;
        }, "证据数修改");
    }

    private static bool IsEvidence(CardInstance card) => card.CurrentZone == CardZone.Field
        && card.CardType == CardType.Amulet && card.HasKeyword(CardKeyword.Evidence);
}
