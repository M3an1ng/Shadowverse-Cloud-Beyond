using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Dtos.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.Fusion)]
    public class FusionEffectHandler : EffectHandlerBase<FusionEffectParams>
    {
        public FusionEffectHandler(ILogger<FusionEffectHandler> logger)
            : base(logger)
        {
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, FusionEffectParams parameters)
        {
            const string effectName = "FusionEffect";

            if (effect.SelectedTargets == null || effect.SelectedTargets.Count == 0)
            {
                return new GameStateActionResult { Success = false, ErrorMessage = $"{effectName}: No materials selected." };
            }

            var cards = new List<CardInstance>();
            foreach (var target in effect.SelectedTargets)
            {
                if (target.Type != Common.TargetType.Card || target.Card == null)
                {
                    return new GameStateActionResult { Success = false, ErrorMessage = $"{effectName}: Target {target.InstanceId} is not a card." };
                }
                cards.Add(target.Card);
            }

            foreach (var card in cards)
            {
                if (card.OwnerPlayerId != context.RequestingPlayerId)
                {
                    return new GameStateActionResult { Success = false, ErrorMessage = $"{effectName}: Card {card.InstanceId} is not owned by requesting player." };
                }
                if (card.CurrentZone != CardZone.Hand)
                {
                    return new GameStateActionResult { Success = false, ErrorMessage = $"{effectName}: Card {card.InstanceId} is not in hand." };
                }
            }

            var generatedEvents = new List<IGameEvent>();

            var sourceCard = effect.SourceCardId.HasValue
                ? context.CurrentGameState.FindCardInstance(effect.SourceCardId.Value)
                : null;
            if (sourceCard == null)
            {
                return new GameStateActionResult { Success = false, ErrorMessage = $"{effectName}: Source card not found for fusion." };
            }

            generatedEvents.Add(new FusionEvent(sourceCard, cards));

            Logger.LogDebug("{EffectName}: Fused {Count} materials.", effectName, cards.Count);

            return new GameStateActionResult
            {
                Success = true,
                GeneratedEvents = generatedEvents
            };
        }
    }
}
