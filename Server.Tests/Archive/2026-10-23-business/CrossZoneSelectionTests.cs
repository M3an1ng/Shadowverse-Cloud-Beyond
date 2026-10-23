using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class CrossZoneSelectionTests
    {
        [Fact]
        public async Task PlayerChoiceRequest_includes_renderable_instances_for_graveyard_targets()
        {
            var (result, pending) = await RunChoiceScenarioAsync(
                sourcePoolExpression: "graveyard",
                createCards: (gameState, p1) =>
                {
                    var c = new CardInstance(Guid.NewGuid(), 1001, p1.PlayerId) { CurrentZone = CardZone.Graveyard };
                    p1.Graveyard.Add(c);
                    gameState.RegisterCardInstance(c);
                    return new[] { c };
                });

            Assert.Equal(ResolutionStatus.Completed, result.Status);
            Assert.NotNull(pending.LastRequest);
            Assert.Single(pending.LastRequest!.SelectableCardInstances);
            Assert.Equal(CardZone.Graveyard, pending.LastRequest.SelectableCardInstances[0].Zone);
        }

        [Fact]
        public async Task PlayerChoiceRequest_includes_renderable_instances_for_deck_candidate_targets()
        {
            var (result, pending) = await RunChoiceScenarioAsync(
                sourcePoolExpression: "deckCandidates",
                createCards: (gameState, p1) =>
                {
                    var c = new CardInstance(Guid.NewGuid(), 1002, p1.PlayerId) { CurrentZone = CardZone.Deck };
                    p1.Deck.Add(c);
                    gameState.RegisterCardInstance(c);
                    return new[] { c };
                });

            Assert.Equal(ResolutionStatus.Completed, result.Status);
            Assert.NotNull(pending.LastRequest);
            Assert.Single(pending.LastRequest!.SelectableCardInstances);
            Assert.Equal(CardZone.Deck, pending.LastRequest.SelectableCardInstances[0].Zone);
        }

        private static async Task<(TargetResolutionResult result, RecordingPendingActionService pending)> RunChoiceScenarioAsync(
            string sourcePoolExpression,
            Func<GameState, PlayerState, IEnumerable<CardInstance>> createCards)
        {
            var services = new ServiceCollection();
            var accessor = new GameContextAccessor();
            services.AddLogging();

            var pending = new RecordingPendingActionService();
            var query = new StubQueryExecutor(sourcePoolExpression);

            var buffService = new Mock<IBuffService>();
            buffService.Setup(x => x.GetEffectiveCost(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns<CardInstance, GameContext>((c, _) => c.BaseCost);
            buffService.Setup(x => x.GetEffectiveAttack(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns<CardInstance, GameContext>((c, _) => c.BaseAttack);
            buffService.Setup(x => x.GetEffectiveMaxHealth(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns<CardInstance, GameContext>((c, _) => c.BaseHealth);
            buffService.Setup(x => x.GetEffectiveKeywords(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(new List<CardKeyword>());

            services.AddSingleton<IGameContextAccessor>(accessor);
            services.AddSingleton<IQueryExecutor>(query);
            services.AddSingleton<IPendingActionService>(pending);
            services.AddSingleton<IBuffService>(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();

            var sp = services.BuildServiceProvider();
            var mapper = new MapperConfiguration(cfg => cfg.AddProfile(new CardInstanceProfile())).CreateMapper();

            var p1Id = Guid.NewGuid();
            var p2Id = Guid.NewGuid();
            var player1 = new PlayerState(p1Id);
            var player2 = new PlayerState(p2Id);
            var gameState = new GameState("choice-zone-test", player1, player2, p1Id)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>(),
                Phase = GamePhase.Main,
                CurrentPlayerId = p1Id
            };

            var cards = new List<CardInstance>(createCards(gameState, player1));
            query.SetResults(cards);

            var sourceCard = new CardInstance(Guid.NewGuid(), 9999, p1Id) { CurrentZone = CardZone.Field };
            player1.Field.Add(sourceCard);
            gameState.RegisterCardInstance(sourceCard);

            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                query,
                pending);

            var targetFactory = new TargetFactory(targetSelectionService, NullLogger<Target>.Instance);
            var conditionFactory = Mock.Of<IConditionFactory>();

            var config = new AbilityConfig
            {
                Targets = new Dictionary<string, TargetConfig>
                {
                    ["pick"] = new TargetConfig
                    {
                        Query = new QueryConfig { SourcePoolExpression = sourcePoolExpression },
                        PlayerChoiceOptions = new PlayerChoiceConfig
                        {
                            IsRequired = true,
                            MinCount = 1,
                            MaxCount = 1
                        }
                    }
                }
            };

            GameServiceProvider.ContextAccessor = accessor;
            using var ctx = new GameContext(sp, mapper, accessor, gameState, p1Id, CancellationToken.None);

            var ability = new Ability(config, conditionFactory, targetFactory, targetSelectionService, sourceCard);
            var result = await ability.ResolveTargetsAsync(ctx, isCancellable: true);
            return (result, pending);
        }

        private sealed class RecordingPendingActionService : IPendingActionService
        {
            public PlayerChoiceRequest? LastRequest { get; private set; }

            public Task<PlayerChoiceResult> WaitForPlayerChoiceAsync(string gameId, PlayerChoiceRequest request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                var chosen = request.SelectableTargetIds[0];
                return Task.FromResult(new PlayerChoiceResult { WasCancelled = false, ChosenTargetIds = new List<Guid> { chosen } });
            }

            public void CompletePlayerChoice(Guid choiceId, Dtos.SignalR.Actions.SubmitTargetChoiceActionDto dto) => throw new NotImplementedException();
            public PendingChoiceContext? GetPendingChoiceForPlayer(string gameId, Guid playerId) => throw new NotImplementedException();
            public void ForceResolveChoice(Guid choiceId, PlayerChoiceResult forcedResult) => throw new NotImplementedException();
        }

        private sealed class StubQueryExecutor : IQueryExecutor
        {
            private readonly string _expectedSourcePool;
            private IReadOnlyList<object> _results = Array.Empty<object>();

            public StubQueryExecutor(string expectedSourcePool)
            {
                _expectedSourcePool = expectedSourcePool;
            }

            public void SetResults(IEnumerable<CardInstance> cards)
            {
                _results = new List<object>(cards);
            }

            public QueryResult ExecuteQuery(string dsl, GameContext context, Dictionary<string, List<TargetInfo>>? resolvedTargets = null)
            {
                return new QueryResult { Success = false, ErrorMessage = "Not used in this test." };
            }

            public QueryResult ExecuteQuery(QueryConfig queryConfig, GameContext context, Dictionary<string, List<TargetInfo>>? resolvedTargets = null)
            {
                if (!string.Equals(queryConfig.SourcePoolExpression, _expectedSourcePool, StringComparison.Ordinal))
                {
                    return new QueryResult { Success = false, ErrorMessage = $"Unexpected SourcePoolExpression: {queryConfig.SourcePoolExpression}" };
                }

                return new QueryResult { Success = true, Results = _results };
            }
        }
    }
}
