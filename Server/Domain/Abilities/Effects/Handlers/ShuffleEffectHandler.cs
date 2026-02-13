using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Effects;
using System.Linq;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.Shuffle)]
    public class ShuffleEffectHandler : EffectHandlerBase<ShuffleEffectParams>
    {
        public ShuffleEffectHandler(ILogger<ShuffleEffectHandler> logger)
            : base(logger)
        {
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, ShuffleEffectParams parameters)
        {
            const string effectName = "ShuffleEffect";
            Logger.LogDebug("{EffectName}: Starting.", effectName);

            var targetCards = effect.SelectedTargets
                .Where(target => target.Type == Common.TargetType.Card && target.Card != null)
                .Select(target => target.Card!)
                .Where(card => card.CurrentZone == CardZone.Hand)
                .ToList();

            if (targetCards.Count == 0)
            {
                return new GameStateActionResult
                {
                    Success = false,
                    ErrorMessage = "Shuffle effect has no valid hand targets."
                };
            }

            var result = new GameStateActionResult { Success = true };
            foreach (var group in targetCards.GroupBy(card => card.OwnerPlayerId))
            {
                var owner = context.CurrentGameState.GetPlayerState(group.Key);
                if (owner == null)
                {
                    return new GameStateActionResult
                    {
                        Success = false,
                        ErrorMessage = $"Shuffle effect owner not found for player {group.Key}."
                    };
                }

                result.GeneratedEvents.Add(new ShuffleEvent(owner, group.ToList(), EventReasonSource.Ability));
            }

            return result;
        }
    }
}
