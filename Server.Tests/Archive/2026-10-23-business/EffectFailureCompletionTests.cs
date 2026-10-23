using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public class EffectFailureCompletionTests
{
    [Fact]
    public void EffectExecutor_FailureAddsEffectFailedOutput()
    {
        var registry = new Mock<IEffectHandlerRegistry>();
        registry
            .Setup(r => r.TryGetHandler(It.IsAny<EffectType>(), out It.Ref<(Type handlerType, Type paramsType)?>.IsAny))
            .Returns(false);

        var resolver = new Mock<IValueSourceResolver>();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
        services.AddSingleton<IBuffService>(new Mock<IBuffService>().Object);
        services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
        var serviceProvider = services.BuildServiceProvider();

        var executor = new EffectExecutor(
            serviceProvider,
            resolver.Object,
            NullLogger<EffectExecutor>.Instance,
            registry.Object);

        var player = new PlayerState(Guid.NewGuid());
        var opponent = new PlayerState(Guid.NewGuid());
        var gameState = new GameState("effect-failure-output", player, opponent, player.PlayerId)
        {
            AbilityManager = new Mock<IAbilityManager>().Object,
            BuffManager = new Mock<IBuffManager>().Object
        };

        var mapper = new Mock<IMapper>();
        var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();

        using var context = new GameContext(serviceProvider, mapper.Object, accessor, gameState, player.PlayerId);

        var effectConfig = new EffectConfig
        {
            Type = EffectType.Random
        };
        var effect = new Effect(effectConfig, player.PlayerId, new List<TargetInfo>(), null, Guid.Empty, null);

        var result = executor.ExecuteEffect(effect, context);

        Assert.False(result.Success);
        Assert.Empty(result.GeneratedEvents.OfType<EffectCompletedEvent>());
        Assert.True(result.Outputs.TryGetValue("effectFailed", out var failed));
        Assert.Equal(true, failed);
    }
}
