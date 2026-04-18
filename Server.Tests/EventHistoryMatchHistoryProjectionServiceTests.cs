using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using System;
using System.Collections.Generic;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class EventHistoryMatchHistoryProjectionServiceTests
    {
        [Fact]
        public void BuildSnapshot_uses_original_cost_for_used_and_destroyed_history()
        {
            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());

            var playedCard = new CardInstance(Guid.NewGuid(), 1001, player1.PlayerId);
            var destroyedTarget = new CardInstance(Guid.NewGuid(), 1002, player2.PlayerId);
            var destroySource = new CardInstance(Guid.NewGuid(), 1003, player1.PlayerId);
            var destroyedTargetSnapshot = new CardInstanceSnapshot
            {
                InstanceId = destroyedTarget.InstanceId,
                OwnerPlayerId = player2.PlayerId,
                BaseDefinitionId = 1002,
                CurrentDefinitionId = 1002,
                Name = "Destroyed",
                CardType = CardType.Follower,
                CurrentZone = CardZone.Field,
                CurrentCost = 1
            };

            var historyService = new Mock<IEventHistoryService>();
            historyService
                .Setup(service => service.GetEvents("game-1"))
                .Returns(new List<IGameEvent>
                {
                    new TurnStartEvent(player1, timestamp: new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc)),
                    new PlayCardEvent(
                        player1,
                        playedCard,
                        PlayMode.Base,
                        paidCost: 2,
                        timestamp: new DateTime(2026, 4, 2, 0, 0, 1, DateTimeKind.Utc)),
                    new DestroyEvent(
                        destroyedTarget,
                        destroySource,
                        EventReasonSource.Ability,
                        timestamp: new DateTime(2026, 4, 2, 0, 0, 2, DateTimeKind.Utc),
                        destroyTargetSnapshot: destroyedTargetSnapshot)
                });

            var gameDataProvider = new Mock<IGameDataProvider>();
            gameDataProvider.Setup(provider => provider.GetCardDefinition(1001)).Returns(new CardDefinition
            {
                Id = 1001,
                Name = "Played",
                Cost = 5,
                Type = CardType.Spell,
                Rarity = CardRarity.Bronze,
                Class = CardClass.Neutral
            });
            gameDataProvider.Setup(provider => provider.GetCardDefinition(1002)).Returns(new CardDefinition
            {
                Id = 1002,
                Name = "Destroyed",
                Cost = 7,
                Type = CardType.Follower,
                Rarity = CardRarity.Silver,
                Class = CardClass.Neutral
            });
            gameDataProvider.Setup(provider => provider.GetCardDefinition(1003)).Returns(new CardDefinition
            {
                Id = 1003,
                Name = "Source",
                Cost = 3,
                Type = CardType.Follower,
                Rarity = CardRarity.Bronze,
                Class = CardClass.Neutral
            });

            var service = new EventHistoryMatchHistoryProjectionService(
                historyService.Object,
                gameDataProvider.Object,
                NullLogger<EventHistoryMatchHistoryProjectionService>.Instance);

            var snapshot = service.BuildSnapshot("game-1");

            var used = Assert.Single(snapshot.UsedCards);
            var destroyed = Assert.Single(snapshot.DestroyedCards);
            Assert.Equal(5, used.Cost);
            Assert.Equal(7, destroyed.Cost);
        }
    }
}
