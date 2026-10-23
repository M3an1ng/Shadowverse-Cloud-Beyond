using System;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Entities;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class PlayerScopeResolverTests
    {
        private static GameState BuildGameState(Guid player1Id, Guid player2Id)
        {
            var player1 = new PlayerState(player1Id);
            var player2 = new PlayerState(player2Id);
            return new GameState("test-game", player1, player2, player1Id);
        }

        [Fact]
        public void ResolvePlayers_Self_ReturnsRequestingPlayer()
        {
            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var gameState = BuildGameState(player1Id, player2Id);

            var players = PlayerScopeResolver.ResolvePlayers(gameState, player1Id, PlayerScope.Self);

            Assert.Single(players);
            Assert.Equal(player1Id, players[0].PlayerId);
        }

        [Fact]
        public void ResolvePlayers_Opponent_ReturnsOpponentPlayer()
        {
            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var gameState = BuildGameState(player1Id, player2Id);

            var players = PlayerScopeResolver.ResolvePlayers(gameState, player1Id, PlayerScope.Opponent);

            Assert.Single(players);
            Assert.Equal(player2Id, players[0].PlayerId);
        }

        [Fact]
        public void ResolvePlayerTargets_Both_ReturnsBothPlayers()
        {
            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var gameState = BuildGameState(player1Id, player2Id);

            var targets = PlayerScopeResolver.ResolvePlayerTargets(gameState, player1Id, PlayerScope.Both);

            Assert.Equal(2, targets.Count);
            Assert.All(targets, t => Assert.Equal(TargetType.Player, t.Type));
            Assert.Contains(targets, t => t.Player?.PlayerId == player1Id);
            Assert.Contains(targets, t => t.Player?.PlayerId == player2Id);
        }
    }
}
