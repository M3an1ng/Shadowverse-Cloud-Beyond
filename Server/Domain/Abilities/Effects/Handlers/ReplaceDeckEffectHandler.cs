using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.ReplaceDeck)]
    public class ReplaceDeckEffectHandler : EffectHandlerBase<ReplaceDeckEffectParams>
    {
        private readonly ICardFactory _cardFactory;

        public ReplaceDeckEffectHandler(
            ILogger<ReplaceDeckEffectHandler> logger,
            ICardFactory cardFactory)
            : base(logger)
        {
            _cardFactory = cardFactory ?? throw new ArgumentNullException(nameof(cardFactory));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, ReplaceDeckEffectParams parameters)
        {
            const string effectName = "ReplaceDeckEffect";
            var cardDefinitionIds = parameters.CardDefinitionIds?.Where(id => id > 0).ToList() ?? new List<int>();
            if (cardDefinitionIds.Count == 0)
            {
                return new GameStateActionResult
                {
                    Success = false,
                    ErrorMessage = "ReplaceDeck requires a non-empty CardDefinitionIds list."
                };
            }

            foreach (var cardDefinitionId in cardDefinitionIds)
            {
                if (GameDataProvider.Instance.GetCardDefinition(cardDefinitionId) == null)
                {
                    return new GameStateActionResult
                    {
                        Success = false,
                        ErrorMessage = $"ReplaceDeck could not resolve card definition {cardDefinitionId}."
                    };
                }
            }

            var targetPlayers = PlayerScopeResolver.ResolvePlayers(context, parameters.PlayerScope);
            if (targetPlayers.Count == 0)
            {
                return new GameStateActionResult
                {
                    Success = false,
                    ErrorMessage = "ReplaceDeck could not resolve any target players."
                };
            }

            Logger.LogDebug(
                "{EffectName}: Replacing deck for {PlayerCount} players with definitions [{DefinitionIds}].",
                effectName,
                targetPlayers.Count,
                string.Join(",", cardDefinitionIds));

            var result = new GameStateActionResult { Success = true };
            foreach (var player in targetPlayers)
            {
                foreach (var oldCard in player.Deck.ToList())
                {
                    result.GeneratedEvents.Add(new CardMovedEvent(
                        oldCard,
                        CardZone.Deck,
                        CardZone.Removed,
                        CardMoveReason.DeckReplaced));
                }

                for (int i = 0; i < cardDefinitionIds.Count; i++)
                {
                    var newCard = _cardFactory.CreateAndRegisterNewCard(
                        context.CurrentGameState,
                        cardDefinitionIds[i],
                        player.PlayerId,
                        CardZone.Staging);

                    result.GeneratedEvents.Add(new CardMovedEvent(
                        newCard,
                        CardZone.Staging,
                        CardZone.Deck,
                        CardMoveReason.AddedToDeck,
                        destinationIndex: i));
                }
            }

            return result;
        }
    }
}
