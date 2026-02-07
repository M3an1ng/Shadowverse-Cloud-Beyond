using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Abilities.Common;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.SpellBoost)]
    public class SpellBoostEffectHandler : EffectHandlerBase<SpellBoostEffectParams>
    {
        public SpellBoostEffectHandler(ILogger<SpellBoostEffectHandler> logger)
            : base(logger)
        {
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, SpellBoostEffectParams parameters)
        {
            const string effectName = "SpellBoostEffect";
            Logger.LogDebug("{EffectName}: Starting spellboost effect.", effectName);

            if (context.SourceCard == null)
            {
                return new GameStateActionResult
                {
                    Success = false,
                    ErrorMessage = "SpellBoost effect requires SourceCard in context."
                };
            }

            var targetCards = effect.SelectedTargets
                .Where(target => target.Type == TargetType.Card && target.Card != null)
                .Select(target => target.Card!)
                .ToList();

            if (targetCards.Count == 0)
            {
                return new GameStateActionResult
                {
                    Success = false,
                    ErrorMessage = "SpellBoost effect requires card targets."
                };
            }

            var player = context.CurrentGameState.GetPlayerState(context.SourceCard.OwnerPlayerId);
            if (player == null)
            {
                return new GameStateActionResult
                {
                    Success = false,
                    ErrorMessage = "SpellBoost effect requires source player."
                };
            }

            var result = new GameStateActionResult
            {
                Success = true,
                GeneratedEvents = new List<IGameEvent>
                {
                    new SpellBoostEvent(player, context.SourceCard, targetCards)
                }
            };

            return result;
        }
    }
}
