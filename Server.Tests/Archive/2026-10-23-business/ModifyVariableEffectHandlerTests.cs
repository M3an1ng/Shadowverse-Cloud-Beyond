using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class ModifyVariableEffectHandlerTests
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Mock<IMapper> _mockMapper = new();

        public ModifyVariableEffectHandlerTests()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(new Mock<IBuffService>().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public void Apply_PlayerAddOnMissingCounter_CreatesFromZeroAndAdds()
        {
            var (context, player) = CreateContext();
            var handler = CreateHandler();
            var effect = new Effect(
                new EffectConfig { Type = EffectType.ModifyVariable },
                player.PlayerId,
                selectedTargets: null,
                parentAbilityId: null,
                triggeredEventId: Guid.Empty,
                sourceCardId: null);
            var parameters = new ModifyCounterEffectParams
            {
                CounterName = "tempCounter",
                Operation = CounterOperation.Add,
                TargetType = TargetType.Player,
                PlayerScope = PlayerScope.Self,
                Amount = 3
            };

            var result = handler.Apply(effect, context, parameters);

            Assert.True(result.Success);
            Assert.Equal(3, player.GetCounter("tempCounter"));
            var counterEvent = Assert.Single(result.GeneratedEvents.OfType<CounterChangedEvent>());
            Assert.Equal("tempCounter", counterEvent.CounterName);
            Assert.Equal(0, counterEvent.OldValue);
            Assert.Equal(3, counterEvent.NewValue);
        }

        [Fact]
        public void Apply_CardAddOnMissingCounter_CreatesFromZeroAndAdds()
        {
            var (context, player) = CreateContext();
            var handler = CreateHandler();
            var card = new CardInstance(Guid.NewGuid(), 1, player.PlayerId);
            var effect = new Effect(
                new EffectConfig { Type = EffectType.ModifyVariable },
                player.PlayerId,
                selectedTargets: new List<TargetInfo> { TargetInfo.ForCard(card) },
                parentAbilityId: null,
                triggeredEventId: Guid.Empty,
                sourceCardId: card.InstanceId);
            var parameters = new ModifyCounterEffectParams
            {
                CounterName = "tempCounter",
                Operation = CounterOperation.Add,
                TargetType = TargetType.Card,
                Amount = 2
            };

            var result = handler.Apply(effect, context, parameters);

            Assert.True(result.Success);
            Assert.Equal(2, card.GetCounter("tempCounter"));
            var counterEvent = Assert.Single(result.GeneratedEvents.OfType<CounterChangedEvent>());
            Assert.Equal("tempCounter", counterEvent.CounterName);
            Assert.Equal(0, counterEvent.OldValue);
            Assert.Equal(2, counterEvent.NewValue);
        }

        private ModifyVariableEffectHandler CreateHandler()
        {
            return new ModifyVariableEffectHandler(
                NullLogger<ModifyVariableEffectHandler>.Instance,
                new TargetProcessingService(NullLogger<TargetProcessingService>.Instance));
        }

        private (GameContext Context, PlayerState Player) CreateContext()
        {
            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("modify-variable-tests", player, opponent, player.PlayerId);
            var context = new GameContext(
                _serviceProvider,
                _mockMapper.Object,
                _serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                player.PlayerId);

            return (context, player);
        }
    }
}
