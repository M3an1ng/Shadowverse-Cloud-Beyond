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
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class RevealEventDtoTests
    {
        [Fact]
        public async Task EventProcessor_emits_CardRevealedEventDto()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameDataProvider>(Mock.Of<IGameDataProvider>());
            services.AddSingleton<IBuffService>(Mock.Of<IBuffService>());
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var sp = services.BuildServiceProvider();

            var mapper = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<CardInstanceProfile>();
            }).CreateMapper();

            var p1 = new PlayerState(Guid.NewGuid());
            var p2 = new PlayerState(Guid.NewGuid());

            var revealed = new CardInstance(Guid.NewGuid(), 1001, p1.PlayerId);

            var gameState = new GameState("reveal-dto-test", p1, p2, p1.PlayerId)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };
            gameState.RegisterCardInstance(revealed);

            var accessor = new GameContextAccessor();
            GameServiceProvider.ContextAccessor = accessor;

            using var ctx = new GameContext(
                sp,
                mapper,
                accessor,
                gameState,
                p1.PlayerId);

            var ev = new CardRevealedEvent(p1, revealed);

            var eventProcessor = new EventProcessor(
                NullLogger<EventProcessor>.Instance,
                mapper,
                Mock.Of<Domain.Abilities.Effects.IEffectExecutor>(),
                sp.GetRequiredService<IGameDataProvider>(),
                Mock.Of<IEventHistoryService>(),
                sp);

            var result = await eventProcessor.ProcessEventAsync(ev, ctx);

            Assert.Contains(result.ResultingDtos, dto => dto is CardRevealedEventDto);
        }
    }
}
