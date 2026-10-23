using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class DeckUpdateEventDtoTests
    {
        [Fact]
        public async Task EventProcessor_emits_DeckUpdateEventDto_for_DeckCountChangedEvent()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameDataProvider>(Mock.Of<IGameDataProvider>());
            services.AddSingleton<IBuffService>(Mock.Of<IBuffService>());
            services.AddSingleton<IEventHistoryService>(Mock.Of<IEventHistoryService>());
            var sp = services.BuildServiceProvider();

            var mapper = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<CardInstanceProfile>();
            }).CreateMapper();

            var p1 = new PlayerState(Guid.NewGuid());
            var p2 = new PlayerState(Guid.NewGuid());

            var gameState = new GameState("deck-update-dto-test", p1, p2, p1.PlayerId)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            var accessor = new GameContextAccessor();
            GameServiceProvider.ContextAccessor = accessor;

            using var ctx = new GameContext(
                sp,
                mapper,
                accessor,
                gameState,
                p1.PlayerId);

            var ev = new DeckCountChangedEvent(p1, 7);
            var eventProcessor = new EventProcessor(
                NullLogger<EventProcessor>.Instance,
                mapper,
                Mock.Of<Domain.Abilities.Effects.IEffectExecutor>(),
                sp.GetRequiredService<IGameDataProvider>(),
                sp.GetRequiredService<IEventHistoryService>(),
                sp);

            var result = await eventProcessor.ProcessEventAsync(ev, ctx);

            var dto = Assert.IsType<DeckUpdateEventDto>(result.ResultingDtos.Single(x => x is DeckUpdateEventDto));
            Assert.Equal(p1.PlayerId, dto.SourcePlayerId);
            Assert.Equal(7, dto.DeckCount);
        }
    }
}
