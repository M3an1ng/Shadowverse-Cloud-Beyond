using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.Replay;
using ShadowverseBeyond.Server.Infrastructure.Services;
using System;
using System.Text.Json.Nodes;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class RawEventGameEventRegistryTests
    {
        [Fact]
        public void Unknown_event_type_is_rejected()
        {
            var registry = new RawEventGameEventRegistry();
            var (gameState, playerId, targetCardId) = CreateGameState(withTargetCard: true);

            var playerToken = gameState.InGameIds.Format(InGameIdKind.Player, playerId);
            var cardToken = gameState.InGameIds.Format(InGameIdKind.Card, targetCardId);
            var raw = new RawEventEnvelopeDto(
                2,
                "NotARealEventType",
                null,
                DateTime.UtcNow,
                playerToken,
                new[] { cardToken },
                new JsonObject());

            Assert.False(registry.TryCreateGameEvent(raw, gameState, playerId, out _, out _));
        }

        [Fact]
        public void Damage_event_requires_allowed_payload_keys()
        {
            var registry = new RawEventGameEventRegistry();
            var (gameState, playerId, targetCardId) = CreateGameState(withTargetCard: true);

            var cardToken = gameState.InGameIds.Format(InGameIdKind.Card, targetCardId);
            var raw = new RawEventEnvelopeDto(
                2,
                EventType.Damage.ToString(),
                null,
                DateTime.UtcNow,
                cardToken,
                new[] { cardToken },
                new JsonObject { ["Damage"] = 1, ["Nope"] = 1 });

            Assert.False(registry.TryCreateGameEvent(raw, gameState, playerId, out _, out _));
        }

        [Fact]
        public void CreateCard_event_creates_event()
        {
            var registry = new RawEventGameEventRegistry();
            var (gameState, playerId, _) = CreateGameState(withTargetCard: false);

            var playerToken = gameState.InGameIds.Format(InGameIdKind.Player, playerId);
            var raw = new RawEventEnvelopeDto(
                2,
                EventType.CreateCard.ToString(),
                null,
                DateTime.UtcNow,
                playerToken,
                Array.Empty<string>(),
                new JsonObject
                {
                    ["CardDefinitionId"] = 1001
                });

            Assert.True(registry.TryCreateGameEvent(raw, gameState, playerId, out var evt, out var err), err);
            var create = Assert.IsType<CreateCardEvent>(evt);
            Assert.Equal(playerId, create.TargetPlayer?.PlayerId);
            Assert.Equal(1001, create.CardDefinitionId);
        }

        private static (GameState gameState, Guid player1Id, Guid targetCardId) CreateGameState(bool withTargetCard)
        {
            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var player1 = new PlayerState(player1Id);
            var player2 = new PlayerState(player2Id);
            var gameState = new GameState("raw-event-test", player1, player2, player1Id)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            var cardId = Guid.NewGuid();
            if (withTargetCard)
            {
                var targetCard = new CardInstance(cardId, 1001, player1Id) { CurrentZone = CardZone.Field };
                player1.Field.Add(targetCard);
                gameState.RegisterCardInstance(targetCard);
                gameState.InGameIds.GetOrCreate(InGameIdKind.Card, cardId);
            }

            return (gameState, player1Id, cardId);
        }
    }
}
