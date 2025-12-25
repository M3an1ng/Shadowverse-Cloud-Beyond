using System;
using System.Collections.Generic;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class DebugCommandParserTests
    {
        [Fact]
        public void Damage_missing_args_returns_failure()
        {
            var (gameState, player1Id, _) = CreateGameState();
            var parser = new DebugCommandParser(
                new RawEventValidator(),
                new RawEventGameEventRegistry());

            var dto = new DebugCommandDto { Command = "damage" };
            var result = parser.Parse(gameState, player1Id, dto);

            Assert.False(result.Success);
            Assert.Empty(result.GeneratedEvents);
        }

        [Fact]
        public void Damage_valid_args_returns_damage_event()
        {
            var (gameState, player1Id, targetCardId) = CreateGameState(withTargetCard: true);
            var parser = new DebugCommandParser(
                new RawEventValidator(),
                new RawEventGameEventRegistry());

            var dto = new DebugCommandDto
            {
                Command = "damage",
                Args = new Dictionary<string, string>
                {
                    ["target"] = gameState.InGameIds.Format(InGameIdKind.Card, targetCardId),
                    ["amount"] = "2",
                    ["source"] = gameState.InGameIds.Format(InGameIdKind.Card, targetCardId)
                }
            };

            var result = parser.Parse(gameState, player1Id, dto);

            Assert.True(result.Success, result.ErrorMessage ?? "<null>");
            Assert.Contains(result.GeneratedEvents, e => e is DamageEvent);
        }

        private static (GameState gameState, Guid player1Id, Guid targetCardId) CreateGameState(bool withTargetCard = false)
        {
            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var player1 = new PlayerState(player1Id);
            var player2 = new PlayerState(player2Id);
            var gameState = new GameState("game-1", player1, player2, player1Id)
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
            }

            return (gameState, player1Id, cardId);
        }
    }
}
