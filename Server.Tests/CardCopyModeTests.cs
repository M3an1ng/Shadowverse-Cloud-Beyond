using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class CardCopyModeTests
    {
        private static ServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(new Mock<IBuffService>().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            return services.BuildServiceProvider();
        }

        private static GameState BuildGameState(Guid player1Id, Guid player2Id, IAbilityManager abilityManager)
        {
            var player1 = new PlayerState(player1Id);
            var player2 = new PlayerState(player2Id);
            return new GameState("test-game", player1, player2, player1Id)
            {
                AbilityManager = abilityManager,
                BuffManager = new Mock<IBuffManager>().Object
            };
        }

        private static void SetTestGameDataProvider(int definitionId)
        {
            var mockProvider = new Mock<IGameDataProvider>();
            mockProvider.SetupGet(provider => provider.IsInitialized).Returns(true);
            mockProvider.Setup(provider => provider.GetCardDefinition(definitionId)).Returns(new CardDefinition
            {
                Id = definitionId,
                Name = "TestCard",
                Type = CardType.Follower,
                Attack = 1,
                Health = 1
            });
            mockProvider.Setup(provider => provider.GetAbilityConfigs(definitionId)).Returns(new List<AbilityConfig>());
            GameDataProvider.SetInstanceForTesting(mockProvider.Object);
        }

        private static GameContext BuildContext(GameState gameState, Guid requestingPlayerId, IServiceProvider serviceProvider)
        {
            var mapper = new Mock<IMapper>().Object;
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            return new GameContext(serviceProvider, mapper, accessor, gameState, requestingPlayerId);
        }

        [Fact]
        public void CreateAndRegisterCardCopy_PreservesStateExceptAttackAndAct()
        {
            const int definitionId = 1;
            SetTestGameDataProvider(definitionId);

            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var abilityManager = new Mock<IAbilityManager>();
            abilityManager.Setup(manager => manager.CreateAbilitiesForCard(
                    It.IsAny<CardInstance>(),
                    It.IsAny<List<AbilityConfig>>(),
                    It.IsAny<Guid?>()))
                .Returns(new List<Ability>());

            var gameState = BuildGameState(player1Id, player2Id, abilityManager.Object);

            var buffManager = new Mock<IBuffManager>();
            buffManager.Setup(manager => manager.GetBuffsForCard(It.IsAny<Guid>()))
                .Returns(new List<Buff>());

            var cardFactory = new CardFactory(
                NullLogger<CardFactory>.Instance,
                new Mock<IBuffFactory>().Object,
                buffManager.Object);

            var original = new CardInstance(Guid.NewGuid(), definitionId, player1Id)
            {
                Damage = 4,
                IsEvolved = true,
                ZoneStayTurn = 2,
                AttacksThisTurn = 3,
                MaxAttacksPerTurn = 5,
                ActsThisTurn = 1,
                MaxActsPerTurn = 2,
                FusionsThisTurn = 1,
                MaxFusionsPerTurn = 2
            };
            original.AbilityState["custom"] = 11;
            original.ApplySilence();

            var copy = cardFactory.CreateAndRegisterCardCopy(
                gameState,
                original,
                player2Id,
                CardZone.Hand,
                exactCopy: true);

            Assert.Equal(player2Id, copy.OwnerPlayerId);
            Assert.Equal(original.CurrentDefinitionId, copy.CurrentDefinitionId);
            Assert.Equal(original.Damage, copy.Damage);
            Assert.Equal(original.IsEvolved, copy.IsEvolved);
            Assert.Equal(original.IsSilenced, copy.IsSilenced);
            Assert.Equal(original.MaxAttacksPerTurn, copy.MaxAttacksPerTurn);
            Assert.Equal(0, copy.ZoneStayTurn);
            Assert.Equal(0, copy.AttacksThisTurn);
            Assert.Equal(0, copy.ActsThisTurn);
            Assert.Equal(original.MaxActsPerTurn, copy.MaxActsPerTurn);
            Assert.Equal(0, copy.FusionsThisTurn);
            Assert.Equal(original.MaxFusionsPerTurn, copy.MaxFusionsPerTurn);
            Assert.Equal(11, copy.AbilityState["custom"]);
            Assert.Equal(CardZone.Hand, copy.CurrentZone);
            Assert.Contains(copy, gameState.GetPlayerState(player2Id)!.Hand);
        }

        [Fact]
        public void GetToken_CopyMode_UsesPlayerScopeOwner()
        {
            var serviceProvider = BuildServiceProvider();
            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var abilityManager = new Mock<IAbilityManager>().Object;
            var gameState = BuildGameState(player1Id, player2Id, abilityManager);
            var original = new CardInstance(Guid.NewGuid(), 1, player1Id);

            var effectConfig = new EffectConfig { Type = EffectType.GetToken };
            var effect = new Effect(effectConfig, player1Id, new List<TargetInfo> { TargetInfo.ForCard(original) }, null, Guid.NewGuid(), null);
            var parameters = new GetTokenEffectParams
            {
                CardSourceMode = CardSourceMode.Copy,
                PlayerScope = PlayerScope.Both
            };
            var handler = new GetTokenEffectHandler(
                NullLogger<GetTokenEffectHandler>.Instance,
                new Mock<ITargetProcessingService>().Object,
                new Mock<ICardFactory>().Object);

            using var context = BuildContext(gameState, player1Id, serviceProvider);
            var result = handler.Apply(effect, context, parameters);

            Assert.True(result.Success);
            var copyEvents = result.GeneratedEvents.OfType<CopyCardEvent>().ToList();
            var addEvents = result.GeneratedEvents.OfType<AddCardToHandEvent>().ToList();
            Assert.Equal(2, copyEvents.Count);
            Assert.Equal(2, addEvents.Count);
            Assert.All(copyEvents, evt =>
            {
                Assert.NotNull(evt.TargetPlayer);
                Assert.Equal(original.InstanceId, evt.SourceCard?.InstanceId);
                Assert.NotNull(evt.CopiedCard);
            });
            Assert.All(addEvents, evt =>
            {
                Assert.NotNull(evt.TargetPlayer);
                Assert.Single(evt.CardsToAdd);
                Assert.Contains(evt.CardsToAdd.Single(), copyEvents.Select(e => e.CopiedCard));
            });
        }
    }
}
