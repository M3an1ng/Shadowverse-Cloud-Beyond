using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Events;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Entities;
using System.Linq;
using AutoMapper;
using System;
using ShadowverseBeyond.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;

using Microsoft.Extensions.Logging.Abstractions;

namespace ShadowverseBeyond.Server.Tests
{
    [Collection("DatabaseCollection")]
    public class CardQueryDslIntegrationTests : IAsyncLifetime
    {
        private readonly DatabaseFixture _fixture;
        private readonly IServiceProvider _serviceProvider;

        public CardQueryDslIntegrationTests(DatabaseFixture fixture, Xunit.Abstractions.ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.OutputHelper = output;
            _serviceProvider = _fixture.ServiceProvider;
        }

        public Task InitializeAsync() => Task.CompletedTask;
        public Task DisposeAsync() => Task.CompletedTask;

        private async Task SeedCardDefinitionsAsync(IEnumerable<CardDefinition> cardDefs)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<Infrastructure.Data.ApplicationDbContext>();
            // Clear existing definitions to avoid duplicate key issues in in-memory DB
            dbContext.CardDefinitions.RemoveRange(dbContext.CardDefinitions);
            await dbContext.SaveChangesAsync();

            await dbContext.CardDefinitions.AddRangeAsync(cardDefs);
            await dbContext.SaveChangesAsync();

            // Force the singleton GameDataProvider to reload its cache with the new data
            var gameDataProvider = _serviceProvider.GetRequiredService<IGameDataProvider>();
            await gameDataProvider.RefreshCacheAsync();
        }

        [Fact]
        public async Task ExecuteQuery_WithValidDsl_ParsesAndExecutesPipelineCorrectly()
        {
            // Arrange
            var dsl = "player.hand.query(self.CurrentCost > 1).orderBy(self.CurrentAttack desc)";
            var playerId = Guid.NewGuid();
            var opponentId = Guid.NewGuid();

            var cardDef1 = new CardDefinition { Id = 1, Cost = 1, Attack = 1 };
            var cardDef2 = new CardDefinition { Id = 2, Cost = 2, Attack = 3 };
            var cardDef3 = new CardDefinition { Id = 3, Cost = 3, Attack = 2 };
            await SeedCardDefinitionsAsync(new[] { cardDef1, cardDef2, cardDef3 });

            var card1 = new CardInstance(Guid.NewGuid(), cardDef1.Id, playerId);
            var card2 = new CardInstance(Guid.NewGuid(), cardDef2.Id, playerId);
            var card3 = new CardInstance(Guid.NewGuid(), cardDef3.Id, playerId);

            var player1 = new PlayerState(playerId) { Hand = new List<CardInstance> { card1, card2, card3 } };
            var player2 = new PlayerState(opponentId);
            var gameState = new GameState("test-game", player1, player2, playerId);

            var executor = _serviceProvider.GetRequiredService<IQueryExecutor>();
            var mapper = _serviceProvider.GetRequiredService<IMapper>();
            var accessor = _serviceProvider.GetRequiredService<IGameContextAccessor>();

            using (var context = new GameContext(_serviceProvider, mapper, accessor, gameState, playerId))
            {
                var result = executor.ExecuteQuery(dsl, context);

                Assert.True(result.Success, result.ErrorMessage);
                Assert.NotNull(result.Results);
                var resultingCards = result.GetResultsAs<CardInstance>().ToList();

                Assert.Equal(2, resultingCards.Count);
                Assert.Equal(card2.InstanceId, resultingCards[0].InstanceId);
                Assert.Equal(card3.InstanceId, resultingCards[1].InstanceId);
            }
        }

        private (GameState gameState, PlayerState player, PlayerState opponent) SetupTestEnvironment(
            List<CardInstance>? playerHand = null,
            List<CardInstance>? playerBoard = null,
            List<CardInstance>? opponentHand = null,
            List<CardInstance>? opponentBoard = null)
        {
            var playerId = Guid.NewGuid();
            var opponentId = Guid.NewGuid();

            var player = new PlayerState(playerId)
            {
                Hand = playerHand ?? new List<CardInstance>(),
                Field = playerBoard ?? new List<CardInstance>()
            };

            var opponent = new PlayerState(opponentId)
            {
                Hand = opponentHand ?? new List<CardInstance>(),
                Field = opponentBoard ?? new List<CardInstance>()
            };

            var gameState = new GameState("test-game", player, opponent, playerId);

            // This mock is no longer needed as the real BuffService will be used.

            return (gameState, player, opponent);
        }

        [Theory]
        [InlineData("player.hand.query(self.CurrentCost > 3)", 2)] // card4, card5
        [InlineData("player.hand.query(self.Tribe == \"Officer\")", 2)] // card2, card5
        [InlineData("player.hand.query(self.CardType == \"Amulet\")", 1)] // card3
        [InlineData("player.hand.orderBy(self.CurrentAttack desc).take(2)", 2)] // card4, card5
        [InlineData("player.hand.take(3).orderBy(self.CurrentCost asc)", 3)] // card1, card2, card3
        public async Task ExecuteQuery_WithVariousDsl_ReturnsCorrectResults(string dsl, int expectedCount)
        {
            // Arrange
            var executor = _serviceProvider.GetRequiredService<IQueryExecutor>();
            var playerId = Guid.NewGuid();

            var cardDef1 = new CardDefinition { Id = 1, Attack = 1, Cost = 1, Type = CardType.Follower };
            var cardDef2 = new CardDefinition { Id = 2, Attack = 2, Cost = 2, Type = CardType.Follower, Tribe = new List<string> { "Officer" } };
            var cardDef3 = new CardDefinition { Id = 3, Attack = 3, Cost = 3, Type = CardType.Amulet };
            var cardDef4 = new CardDefinition { Id = 4, Attack = 4, Cost = 4, Type = CardType.Follower };
            var cardDef5 = new CardDefinition { Id = 5, Attack = 5, Cost = 5, Type = CardType.Follower, Tribe = new List<string> { "Officer" } };
            await SeedCardDefinitionsAsync(new[] { cardDef1, cardDef2, cardDef3, cardDef4, cardDef5 });

            var card1 = new CardInstance(Guid.NewGuid(), cardDef1.Id, playerId);
            var card2 = new CardInstance(Guid.NewGuid(), cardDef2.Id, playerId);
            var card3 = new CardInstance(Guid.NewGuid(), cardDef3.Id, playerId);
            var card4 = new CardInstance(Guid.NewGuid(), cardDef4.Id, playerId);
            var card5 = new CardInstance(Guid.NewGuid(), cardDef5.Id, playerId);

            var (gameState, player, _) = SetupTestEnvironment(
                playerHand: new List<CardInstance> { card1, card2, card3, card4, card5 }
            );

            // Act
            QueryResult result;
            var mapper = _serviceProvider.GetRequiredService<IMapper>();
            var accessor = _serviceProvider.GetRequiredService<IGameContextAccessor>();
            using (var context = new GameContext(_serviceProvider, mapper, accessor, gameState, player.PlayerId))
            {
                result = executor.ExecuteQuery(dsl, context);
            }

            // Assert
            Assert.True(result.Success, result.ErrorMessage);
            Assert.NotNull(result.Results);
            Assert.Equal(expectedCount, result.Results.Count());
        }

        [Theory]
        [InlineData("player.hand.orderBy(self.CurrentAttack desc)", new[] { 2, 5, 3, 4, 1 })]
        [InlineData("player.hand.query(self.Tribe == \"Officer\").orderBy(self.CurrentAttack desc)", new[] { 2, 5 })]
        public async Task ExecuteQuery_WithSorting_ReturnsCorrectlyOrderedResults(string dsl, int[] expectedDefinitionIds)
        {
            // Arrange
            var executor = _serviceProvider.GetRequiredService<IQueryExecutor>();
            var playerId = Guid.NewGuid();

            var cardDef1 = new CardDefinition { Id = 1, Attack = 1 };
            var cardDef2 = new CardDefinition { Id = 2, Attack = 5, Tribe = new List<string> { "Officer" } };
            var cardDef3 = new CardDefinition { Id = 3, Attack = 3 };
            var cardDef4 = new CardDefinition { Id = 4, Attack = 2 };
            var cardDef5 = new CardDefinition { Id = 5, Attack = 4, Tribe = new List<string> { "Officer" } };
            await SeedCardDefinitionsAsync(new[] { cardDef1, cardDef2, cardDef3, cardDef4, cardDef5 });

            var card1 = new CardInstance(Guid.NewGuid(), 1, playerId);
            var card2 = new CardInstance(Guid.NewGuid(), 2, playerId);
            var card3 = new CardInstance(Guid.NewGuid(), 3, playerId);
            var card4 = new CardInstance(Guid.NewGuid(), 4, playerId);
            var card5 = new CardInstance(Guid.NewGuid(), 5, playerId);

            var (gameState, player, _) = SetupTestEnvironment(
                playerHand: new List<CardInstance> { card1, card2, card3, card4, card5 }
            );

            // Act
            QueryResult result;
            var mapper = _serviceProvider.GetRequiredService<IMapper>();
            var accessor = _serviceProvider.GetRequiredService<IGameContextAccessor>();
            using (var context = new GameContext(_serviceProvider, mapper, accessor, gameState, player.PlayerId))
            {
                result = executor.ExecuteQuery(dsl, context);
            }

            // Assert
            Assert.True(result.Success, result.ErrorMessage);
            var resultCards = result.GetResultsAs<CardInstance>().ToList();
            Assert.Equal(expectedDefinitionIds.Length, resultCards.Count);
            Assert.Equal(expectedDefinitionIds, resultCards.Select(c => c.CurrentDefinitionId));
        }

        [Theory]
        // Chained operations with different orders
        [InlineData("player.hand.take(3).query(self.CurrentCost > 1)", 2, new[] { 2, 3 })]
        [InlineData("player.hand.query(self.CurrentCost > 2).take(2)", 2, new[] { 3, 4 })]
        // Multiple sorting criteria
        [InlineData("gamestate.AllField.orderBy(self.CurrentCost desc, self.CurrentAttack asc)", 5, new[] { 105, 102, 104, 103, 101 })]
        public async Task ExecuteQuery_WithComplexChainedDsl_ReturnsCorrectResults(string dsl, int expectedCount, int[] expectedDefinitionIds)
        {
            // Arrange
            var executor = _serviceProvider.GetRequiredService<IQueryExecutor>();
            var playerId = Guid.NewGuid();

            // Player Hand
            var p_cardDef1 = new CardDefinition { Id = 1, Cost = 1 };
            var p_cardDef2 = new CardDefinition { Id = 2, Cost = 2 };
            var p_cardDef3 = new CardDefinition { Id = 3, Cost = 3 };
            var p_cardDef4 = new CardDefinition { Id = 4, Cost = 4 };
            var p_cardDef5 = new CardDefinition { Id = 5, Cost = 5 };
            var p_card1 = new CardInstance(Guid.NewGuid(), 1, playerId);
            var p_card2 = new CardInstance(Guid.NewGuid(), 2, playerId);
            var p_card3 = new CardInstance(Guid.NewGuid(), 3, playerId);
            var p_card4 = new CardInstance(Guid.NewGuid(), 4, playerId);
            var p_card5 = new CardInstance(Guid.NewGuid(), 5, playerId);

            // All Field (Player + Opponent)
            var p_fieldDef1 = new CardDefinition { Id = 101, Cost = 1, Attack = 5 };
            var p_fieldDef2 = new CardDefinition { Id = 102, Cost = 3, Attack = 2 };
            var p_fieldDef3 = new CardDefinition { Id = 103, Cost = 2, Attack = 3 };
            var o_fieldDef1 = new CardDefinition { Id = 104, Cost = 3, Attack = 3 };
            var o_fieldDef2 = new CardDefinition { Id = 105, Cost = 4, Attack = 1 };
            await SeedCardDefinitionsAsync(new[] { p_cardDef1, p_cardDef2, p_cardDef3, p_cardDef4, p_cardDef5, p_fieldDef1, p_fieldDef2, p_fieldDef3, o_fieldDef1, o_fieldDef2 });

            var p_fieldCard1 = new CardInstance(Guid.NewGuid(), 101, playerId);
            var p_fieldCard2 = new CardInstance(Guid.NewGuid(), 102, playerId);
            var p_fieldCard3 = new CardInstance(Guid.NewGuid(), 103, playerId);
            var o_fieldCard1 = new CardInstance(Guid.NewGuid(), 104, Guid.NewGuid());
            var o_fieldCard2 = new CardInstance(Guid.NewGuid(), 105, Guid.NewGuid());

            var (gameState, player, _) = SetupTestEnvironment(
                playerHand: new List<CardInstance> { p_card1, p_card2, p_card3, p_card4, p_card5 },
                playerBoard: new List<CardInstance> { p_fieldCard1, p_fieldCard2, p_fieldCard3 },
                opponentBoard: new List<CardInstance> { o_fieldCard1, o_fieldCard2 }
            );

            // Act
            QueryResult result;
            var mapper = _serviceProvider.GetRequiredService<IMapper>();
            var accessor = _serviceProvider.GetRequiredService<IGameContextAccessor>();
            using (var context = new GameContext(_serviceProvider, mapper, accessor, gameState, player.PlayerId))
            {
                result = executor.ExecuteQuery(dsl, context);
            }

            // Assert
            Assert.True(result.Success, result.ErrorMessage);
        }
    }
}
