using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.Reveal)]
    public class RevealEffectHandler : EffectHandlerBase<RevealEffectParams>
    {
        public RevealEffectHandler(ILogger<RevealEffectHandler> logger)
            : base(logger)
        {
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, RevealEffectParams parameters)
        {
            const string effectName = "RevealEffect";

            var player = context.CurrentGameState.GetPlayerState(context.RequestingPlayerId);
            if (player == null)
            {
                return new GameStateActionResult { Success = false, ErrorMessage = $"{effectName}: Requesting player not found." };
            }

            if (effect.SelectedTargets == null || effect.SelectedTargets.Count == 0)
            {
                return new GameStateActionResult { Success = false, ErrorMessage = $"{effectName}: No targets selected." };
            }

            var cards = effect.SelectedTargets
                .Where(t => t.Type == Common.TargetType.Card && t.Card != null)
                .Select(t => t.Card!)
                .ToList();

            if (cards.Count != effect.SelectedTargets.Count)
            {
                return new GameStateActionResult { Success = false, ErrorMessage = $"{effectName}: Reveal can only target cards." };
            }

            var events = new List<IGameEvent>(cards.Count);
            foreach (var card in cards)
            {
                events.Add(new CardRevealedEvent(player, card));
            }

            Logger.LogDebug("{EffectName}: Revealing {Count} card(s).", effectName, cards.Count);

            return new GameStateActionResult { Success = true, GeneratedEvents = events };
        }
    }
}

