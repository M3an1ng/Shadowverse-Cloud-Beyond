using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Exceptions;
using ShadowverseBeyond.Server.Hubs;
using ShadowverseBeyond.Server.Hubs.ClientInterfaces;
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
    public class PlayerChoiceValidationTests
    {
        [Fact]
        public async Task GetPendingChoiceForPlayer_IsolatesGamesForSamePlayer()
        {
            var playerId = Guid.NewGuid();
            var gameService = new Mock<IGameService>();
            var clients = new Mock<IHubClients<IGameClient>>();
            clients.Setup(c => c.Group(It.IsAny<string>())).Returns(Mock.Of<IGameClient>());
            var hubContext = new Mock<IHubContext<GameHub, IGameClient>>();
            hubContext.SetupGet(h => h.Clients).Returns(clients.Object);
            using var services = new ServiceCollection()
                .AddSingleton<IGameService>(gameService.Object)
                .BuildServiceProvider();
            var pending = new PendingActionService(
                hubContext.Object,
                NullLogger<PendingActionService>.Instance,
                services,
                new MapperConfiguration(_ => { }).CreateMapper());
            var firstRequest = new PlayerChoiceRequest { PendingChoiceId = Guid.NewGuid(), RequestingPlayerId = playerId };
            var secondRequest = new PlayerChoiceRequest { PendingChoiceId = Guid.NewGuid(), RequestingPlayerId = playerId };

            var firstWait = pending.WaitForPlayerChoiceAsync("first-game", firstRequest, CancellationToken.None);
            var secondWait = pending.WaitForPlayerChoiceAsync("second-game", secondRequest, CancellationToken.None);

            Assert.Same(firstRequest, pending.GetPendingChoiceForPlayer("first-game", playerId)?.Request);
            Assert.Same(secondRequest, pending.GetPendingChoiceForPlayer("second-game", playerId)?.Request);
            Assert.Null(pending.GetPendingChoiceForPlayer("other-game", playerId));
            pending.ForceResolveChoice(firstRequest.PendingChoiceId, PlayerChoiceResult.Cancelled);
            pending.ForceResolveChoice(secondRequest.PendingChoiceId, PlayerChoiceResult.Cancelled);
            await Task.WhenAll(firstWait, secondWait);
        }

        [Fact]
        public async Task CompletePlayerChoice_rejects_invalid_selection_and_does_not_clear_pending_choice()
        {
            var gameId = "choice-validate";
            var p1Id = Guid.NewGuid();
            var p2Id = Guid.NewGuid();

            var gameState = new GameState(gameId, new PlayerState(p1Id), new PlayerState(p2Id), p1Id)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            var gameService = new Mock<IGameService>();
            gameService.Setup(x => x.GetGameState(gameId)).Returns(gameState);

            var client = new Mock<IGameClient>();
            var clients = new Mock<IHubClients<IGameClient>>();
            clients.Setup(c => c.Group(It.IsAny<string>())).Returns(client.Object);

            var hubContext = new Mock<IHubContext<GameHub, IGameClient>>();
            hubContext.SetupGet(h => h.Clients).Returns(clients.Object);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(gameService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var sp = services.BuildServiceProvider();

            var mapper = new MapperConfiguration(_ => { }).CreateMapper();
            var pending = new PendingActionService(
                hubContext.Object,
                NullLogger<PendingActionService>.Instance,
                sp,
                mapper);

            var choiceId = Guid.NewGuid();
            var sourceAbilityRuntimeId = Guid.NewGuid();
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();

            var request = new PlayerChoiceRequest
            {
                PendingChoiceId = choiceId,
                RequestingPlayerId = p1Id,
                SourceAbilityRuntimeId = sourceAbilityRuntimeId,
                SelectableTargetIds = new List<Guid> { a, b, c },
                MinSelectionCount = 2,
                MaxSelectionCount = 3,
                IsCancellable = true
            };

            var waitTask = pending.WaitForPlayerChoiceAsync(gameId, request, CancellationToken.None);
            Assert.False(waitTask.IsCompleted);

            Assert.Throws<GameLogicException>(() =>
                pending.CompletePlayerChoice(choiceId, new SubmitTargetChoiceActionDto
                {
                    PendingChoiceId = choiceId,
                    SourceAbilityRuntimeId = sourceAbilityRuntimeId,
                    ChosenTargetIds = new List<Guid> { a }
                }));
            Assert.NotNull(pending.GetPendingChoiceForPlayer(gameId, p1Id));
            Assert.False(waitTask.IsCompleted);

            Assert.Throws<GameLogicException>(() =>
                pending.CompletePlayerChoice(choiceId, new SubmitTargetChoiceActionDto
                {
                    PendingChoiceId = choiceId,
                    SourceAbilityRuntimeId = sourceAbilityRuntimeId,
                    ChosenTargetIds = new List<Guid> { a, b, c, Guid.NewGuid() }
                }));
            Assert.NotNull(pending.GetPendingChoiceForPlayer(gameId, p1Id));
            Assert.False(waitTask.IsCompleted);

            Assert.Throws<GameLogicException>(() =>
                pending.CompletePlayerChoice(choiceId, new SubmitTargetChoiceActionDto
                {
                    PendingChoiceId = choiceId,
                    SourceAbilityRuntimeId = sourceAbilityRuntimeId,
                    ChosenTargetIds = new List<Guid> { a, Guid.NewGuid() }
                }));
            Assert.NotNull(pending.GetPendingChoiceForPlayer(gameId, p1Id));
            Assert.False(waitTask.IsCompleted);

            pending.CompletePlayerChoice(choiceId, new SubmitTargetChoiceActionDto
            {
                PendingChoiceId = choiceId,
                SourceAbilityRuntimeId = sourceAbilityRuntimeId,
                ChosenTargetIds = new List<Guid> { a, b }
            });

            var result = await waitTask;
            Assert.False(result.WasCancelled);
            Assert.Equal(2, result.ChosenTargetIds.Count);
            Assert.Contains(a, result.ChosenTargetIds);
            Assert.Contains(b, result.ChosenTargetIds);
            Assert.Null(pending.GetPendingChoiceForPlayer(gameId, p1Id));
        }
    }
}
