using System;
using System.Linq;
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

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.SettleEarthSigil)]
    public class SettleEarthSigilEffectHandler : EffectHandlerBase<SettleEarthSigilEffectParams>
    {
        public SettleEarthSigilEffectHandler(ILogger<SettleEarthSigilEffectHandler> logger)
            : base(logger)
        {
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, SettleEarthSigilEffectParams parameters)
        {
            var enteringCard = effect.SelectedTargets
                .FirstOrDefault(target => target.Type == TargetType.Card && target.Card != null)
                ?.Card;

            if (enteringCard == null)
            {
                return new GameStateActionResult
                {
                    Success = false,
                    ErrorMessage = "SettleEarthSigil effect requires an entering EarthSigil card target."
                };
            }

            var result = new GameStateActionResult { Success = true };
            var owner = context.CurrentGameState.GetPlayerState(enteringCard.OwnerPlayerId);
            if (owner == null)
            {
                result.Success = false;
                result.ErrorMessage = $"SettleEarthSigil effect owner player {enteringCard.OwnerPlayerId} not found.";
                return result;
            }

            if (enteringCard.CurrentZone != CardZone.Field || !enteringCard.HasKeyword(CardKeyword.EarthSigil))
            {
                return result;
            }

            var earthSigils = owner.Field
                .Where(card => card.HasKeyword(CardKeyword.EarthSigil))
                .ToList();

            if (earthSigils.Count == 0)
            {
                return result;
            }

            if (enteringCard.GetCounter(CardInstance.EarthsigilCounterKey) == 0)
            {
                enteringCard.SetCounter(CardInstance.EarthsigilCounterKey, 1);
            }

            if (earthSigils.Count == 1)
            {
                return result;
            }

            var total = earthSigils.Sum(card => card.GetCounter(CardInstance.EarthsigilCounterKey));
            enteringCard.SetCounter(CardInstance.EarthsigilCounterKey, total);

            foreach (var other in earthSigils.Where(card => card.InstanceId != enteringCard.InstanceId))
            {
                result.GeneratedEvents.Add(new LeaveFieldEvent(
                    other,
                    LeaveFieldKind.Vanish,
                    EventReasonSource.System,
                    enteringCard));
            }

            return result;
        }
    }
}
