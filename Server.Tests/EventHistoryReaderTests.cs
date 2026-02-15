using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Infrastructure.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class EventHistoryReaderTests
    {
        [Fact]
        public async Task Events_returns_latest_history_on_each_access()
        {
            var historyService = new InMemoryEventHistoryService(NullLogger<InMemoryEventHistoryService>.Instance);
            var gameId = Guid.NewGuid().ToString("N");
            var reader = new EventHistoryReader(historyService, gameId);

            await historyService.StoreEventAsync(gameId, new TestHistoryEvent(EventType.Draw));
            Assert.Single(reader.Events);

            await historyService.StoreEventAsync(gameId, new TestHistoryEvent(EventType.PlayCard));
            Assert.Equal(2, reader.Events.Count());
        }

        private sealed class TestHistoryEvent : GameEventBase
        {
            public TestHistoryEvent(EventType eventType)
                : base(eventType)
            {
            }
        }
    }
}
