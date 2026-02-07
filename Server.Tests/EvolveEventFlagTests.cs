using System;
using System.Linq;
using Moq;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class EvolveEventFlagTests
    {
        [Fact]
        public void Apply_evolution_marks_event_as_not_super()
        {
            var mockProvider = new Mock<IGameDataProvider>();
            mockProvider
                .Setup(p => p.GetCardDefinition(It.IsAny<int>()))
                .Returns((int id) => new CardDefinition
                {
                    Id = id,
                    Type = CardType.Follower,
                    Cost = 2
                });
            GameDataProvider.SetInstanceForTesting(mockProvider.Object);

            var playerId = Guid.NewGuid();
            var player = new PlayerState(playerId);
            var card = new CardInstance(Guid.NewGuid(), 1, playerId);
            var result = card.ApplyEvolution(player, evolutionSourceCard: null);

            Assert.True(result.Success, result.ErrorMessage);
            var evolveEvent = result.GeneratedEvents.OfType<EvolveEvent>().Single();
            Assert.False(evolveEvent.IsSuperEvolution);
        }

        [Fact]
        public void Apply_super_evolution_marks_event_as_super()
        {
            var mockProvider = new Mock<IGameDataProvider>();
            mockProvider
                .Setup(p => p.GetCardDefinition(It.IsAny<int>()))
                .Returns((int id) => new CardDefinition
                {
                    Id = id,
                    Type = CardType.Follower,
                    Cost = 2
                });
            GameDataProvider.SetInstanceForTesting(mockProvider.Object);

            var playerId = Guid.NewGuid();
            var player = new PlayerState(playerId);
            var card = new CardInstance(Guid.NewGuid(), 1, playerId);
            var result = card.ApplySuperEvolution(player, evolutionSourceCard: null);

            Assert.True(result.Success, result.ErrorMessage);
            var evolveEvent = result.GeneratedEvents.OfType<EvolveEvent>().Single();
            Assert.True(evolveEvent.IsSuperEvolution);
            Assert.Contains(result.GeneratedEvents, e => e is SuperEvolveEvent);
        }
    }
}
