using System;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class DamageEventTests
    {
        [Theory]
        [InlineData(CardZone.Graveyard)]
        [InlineData(CardZone.Removed)]
        public void DamageEvent_TargetAlreadyOutOfPlay_SkipsResolution(CardZone targetZone)
        {
            const int definitionId = 1001;
            SetTestGameDataProvider(definitionId);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(Mock.Of<IBuffService>());
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var p1Id = Guid.NewGuid();
            var p2Id = Guid.NewGuid();
            var gameState = new GameState("damage-zone-guard", new PlayerState(p1Id), new PlayerState(p2Id), p1Id)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            var source = new CardInstance(Guid.NewGuid(), definitionId, p1Id) { CurrentZone = CardZone.Field };
            gameState.Player1.Field.Add(source);
            gameState.RegisterCardInstance(source);

            var target = new CardInstance(Guid.NewGuid(), definitionId, p2Id) { CurrentZone = targetZone };
            if (targetZone == CardZone.Graveyard)
            {
                gameState.Player2.Graveyard.Add(target);
            }
            gameState.RegisterCardInstance(target);

            using var context = new GameContext(
                serviceProvider,
                new Mock<IMapper>().Object,
                serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                p1Id);

            var evt = new DamageEvent(target, 3, source, DamageKind.Ability);
            var result = evt.Commit(context);

            Assert.False(result.Success);
            Assert.Empty(result.GeneratedEvents);
            Assert.Equal(0, evt.ActualDamage);
            Assert.Contains("no longer valid", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static void SetTestGameDataProvider(int definitionId)
        {
            var mockProvider = new Mock<IGameDataProvider>();
            mockProvider.SetupGet(provider => provider.IsInitialized).Returns(true);
            mockProvider.Setup(provider => provider.GetCardDefinition(definitionId)).Returns(new CardDefinition
            {
                Id = definitionId,
                Name = "DamageTestCard",
                Type = CardType.Follower,
                Attack = 2,
                Health = 2
            });
            mockProvider.Setup(provider => provider.GetAbilityConfigs(definitionId)).Returns(new System.Collections.Generic.List<AbilityConfig>());
            GameDataProvider.SetInstanceForTesting(mockProvider.Object);
        }
    }
}
