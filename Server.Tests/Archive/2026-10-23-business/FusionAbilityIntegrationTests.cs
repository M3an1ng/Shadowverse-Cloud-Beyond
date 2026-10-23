using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class FusionAbilityIntegrationTests
    {
        [Fact]
        public void Fusion_is_usable_once_per_turn_and_resets_next_turn()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var accessor = new GameContextAccessor();
            services.AddSingleton<IGameContextAccessor>(accessor);
            var buffService = new Mock<IBuffService>();
            buffService
                .Setup(b => b.GetEffectiveKeywords(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(new List<CardKeyword>());
            buffService
                .Setup(b => b.GetEffectiveActionUsageLimit(It.IsAny<CardInstance>(), It.IsAny<CardActionUsageType>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, CardActionUsageType actionType, GameContext _) => card.GetBaseActionUsageLimit(actionType));
            services.AddSingleton<IBuffService>(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();

            var sp = services.BuildServiceProvider();
            var mapper = new MapperConfiguration(_ => { }).CreateMapper();

            var p1Id = Guid.NewGuid();
            var p2Id = Guid.NewGuid();
            var player1 = new PlayerState(p1Id) { TurnNumber = 1 };
            var player2 = new PlayerState(p2Id) { TurnNumber = 1 };

            var abilityManager = new Mock<IAbilityManager>();
            var fusionAbilityMock = new Mock<IAbility>();
            fusionAbilityMock.SetupGet(a => a.Type).Returns(AbilityType.Activated);
            fusionAbilityMock.SetupGet(a => a.AbilityKeywords).Returns(new List<AbilityKeywordConfig>
            {
                new AbilityKeywordConfig { Keyword = AbilityKeyword.Fusion }
            });
            fusionAbilityMock.Setup(a => a.HasKeyword(AbilityKeyword.Fusion)).Returns(true);
            var fusionAbility = fusionAbilityMock.Object;

            var gameState = new GameState("fusion-test", player1, player2, p1Id)
            {
                AbilityManager = abilityManager.Object,
                BuffManager = Mock.Of<IBuffManager>(),
                Phase = GamePhase.Main,
                CurrentPlayerId = p1Id
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1001, p1Id) { CurrentZone = CardZone.Hand };
            var materialA = new CardInstance(Guid.NewGuid(), 1002, p1Id) { CurrentZone = CardZone.Hand };
            var materialB = new CardInstance(Guid.NewGuid(), 1003, p1Id) { CurrentZone = CardZone.Hand };
            player1.Hand.Add(sourceCard);
            player1.Hand.Add(materialA);
            player1.Hand.Add(materialB);
            gameState.RegisterCardInstance(sourceCard);
            gameState.RegisterCardInstance(materialA);
            gameState.RegisterCardInstance(materialB);

            abilityManager
                .Setup(m => m.GetEffectiveAbilitiesForCard(sourceCard.InstanceId, It.IsAny<GameContext>()))
                .Returns(new List<IAbility> { fusionAbility });
            abilityManager
                .Setup(m => m.GetAbilitiesForCard(sourceCard.InstanceId))
                .Returns(new List<IAbility> { fusionAbility });
            abilityManager
                .Setup(m => m.IsAbilitySuppressed(It.IsAny<IAbility>(), It.IsAny<GameContext>()))
                .Returns(false);

            GameServiceProvider.ContextAccessor = accessor;
            using var ctx = new GameContext(sp, mapper, accessor, gameState, p1Id);

            var first = new FusionRequestedEvent(p1Id, new FusionActionDto { CardUid = sourceCard.InstanceId }).Commit(ctx);
            Assert.True(first.Success, first.ErrorMessage);
            var invoked = Assert.Single(first.GeneratedEvents.OfType<FusionInvokedEvent>());
            var invokedResult = invoked.Commit(ctx);
            Assert.True(invokedResult.Success, invokedResult.ErrorMessage);

            var fused = new FusionEvent(sourceCard, new List<CardInstance> { materialA }).Commit(ctx);
            Assert.True(fused.Success, fused.ErrorMessage);
            Assert.True(sourceCard.HasUsedFusionThisTurn());
            var fusionStateUpdated = Assert.Single(fused.GeneratedEvents.OfType<FusionStateUpdatedEvent>());
            Assert.Equal(1, fusionStateUpdated.FusionsThisTurn);
            Assert.Equal(1, fusionStateUpdated.MaxFusionsPerTurn);

            var second = new FusionRequestedEvent(p1Id, new FusionActionDto { CardUid = sourceCard.InstanceId }).Commit(ctx);
            Assert.False(second.Success);
            Assert.Empty(second.GeneratedEvents);

            var turnStart = player1.StartTurn(ctx);
            Assert.True(turnStart.Success, turnStart.ErrorMessage);
            Assert.False(sourceCard.HasUsedFusionThisTurn());

            var third = new FusionRequestedEvent(p1Id, new FusionActionDto { CardUid = sourceCard.InstanceId }).Commit(ctx);
            Assert.True(third.Success, third.ErrorMessage);
            var invokedThird = Assert.Single(third.GeneratedEvents.OfType<FusionInvokedEvent>());
            var thirdInvokedResult = invokedThird.Commit(ctx);
            Assert.True(thirdInvokedResult.Success, thirdInvokedResult.ErrorMessage);

            var fusedAgain = new FusionEvent(sourceCard, new List<CardInstance> { materialB }).Commit(ctx);
            Assert.True(fusedAgain.Success, fusedAgain.ErrorMessage);
            Assert.True(sourceCard.HasUsedFusionThisTurn());
        }
    }
}
