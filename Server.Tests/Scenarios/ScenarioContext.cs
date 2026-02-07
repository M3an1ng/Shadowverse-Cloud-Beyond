using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Tests.Scenarios
{
    public sealed class ScenarioContext
    {
        public ScenarioCase Scenario { get; }
        public IServiceProvider Services { get; }
        public GameState GameState { get; }
        public IMapper Mapper { get; }
        public IGameContextAccessor ContextAccessor { get; }
        public IReadOnlyDictionary<string, CardInstance> Cards { get; }
        public ScenarioPendingActionService PendingActionService { get; }
        public Random Random { get; }
        public List<IGameEvent> ProcessedEvents { get; }

        public ScenarioContext(
            ScenarioCase scenario,
            IServiceProvider services,
            GameState gameState,
            IMapper mapper,
            IGameContextAccessor contextAccessor,
            IReadOnlyDictionary<string, CardInstance> cards,
            ScenarioPendingActionService pendingActionService,
            Random random,
            List<IGameEvent> processedEvents)
        {
            Scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            Services = services ?? throw new ArgumentNullException(nameof(services));
            GameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
            Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            ContextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
            Cards = cards ?? throw new ArgumentNullException(nameof(cards));
            PendingActionService = pendingActionService ?? throw new ArgumentNullException(nameof(pendingActionService));
            Random = random ?? throw new ArgumentNullException(nameof(random));
            ProcessedEvents = processedEvents ?? throw new ArgumentNullException(nameof(processedEvents));
        }

        public PlayerState GetPlayer(ScenarioPlayer player)
        {
            return player switch
            {
                ScenarioPlayer.Player1 => GameState.Player1,
                ScenarioPlayer.Player2 => GameState.Player2,
                _ => throw new ArgumentOutOfRangeException(nameof(player), player, "Unknown scenario player.")
            };
        }

        public CardInstance GetCard(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Card key is required.", nameof(key));
            }

            if (!Cards.TryGetValue(key, out var card))
            {
                throw new KeyNotFoundException($"Scenario card key '{key}' not found.");
            }

            return card;
        }

        public IEnumerable<CardInstance> FindCardsById(int cardId, ScenarioPlayer? owner = null, ShadowverseBeyond.Server.Dtos.Enums.CardZone? zone = null)
        {
            IEnumerable<CardInstance> cards = GameState.AllCards.Where(c => c.CurrentDefinitionId == cardId);
            if (owner.HasValue)
            {
                var ownerId = GetPlayer(owner.Value).PlayerId;
                cards = cards.Where(c => c.OwnerPlayerId == ownerId);
            }
            if (zone.HasValue)
            {
                cards = cards.Where(c => c.CurrentZone == zone.Value);
            }

            return cards;
        }

        public int CountCardsInZone(ScenarioPlayer player, ShadowverseBeyond.Server.Dtos.Enums.CardZone zone)
        {
            var state = GetPlayer(player);
            return zone switch
            {
                ShadowverseBeyond.Server.Dtos.Enums.CardZone.Hand => state.Hand.Count,
                ShadowverseBeyond.Server.Dtos.Enums.CardZone.Field => state.Field.Count,
                ShadowverseBeyond.Server.Dtos.Enums.CardZone.Deck => state.Deck.Count,
                ShadowverseBeyond.Server.Dtos.Enums.CardZone.Graveyard => state.Graveyard.Count,
                ShadowverseBeyond.Server.Dtos.Enums.CardZone.Extra => state.Extra.Count,
                ShadowverseBeyond.Server.Dtos.Enums.CardZone.Emblem => state.Emblem.Count,
                _ => 0
            };
        }

        public void RecordEvents(IEnumerable<IGameEvent> events)
        {
            if (events == null)
            {
                return;
            }

            ProcessedEvents.AddRange(events);
        }

        public string? FindCardKey(CardInstance card)
        {
            if (card == null)
            {
                return null;
            }

            foreach (var pair in Cards)
            {
                if (pair.Value.InstanceId == card.InstanceId)
                {
                    return pair.Key;
                }
            }

            return null;
        }
    }
}
