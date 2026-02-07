using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers;
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
    public class EffectCompletedGeneratedCardsTests
    {
        [Fact]
        public void EffectCompletedEvent_DoesNotCollectGeneratedCards_BeforeCommit()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(Mock.Of<IBuffService>());
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IValueSourceResolver, StaticValueSourceResolver>();
            services.AddSingleton<ITargetProcessingService>(Mock.Of<ITargetProcessingService>());
            services.AddSingleton<ICardFactory>(Mock.Of<ICardFactory>());
            services.AddSingleton<GetTokenEffectHandler>();
            var serviceProvider = services.BuildServiceProvider();

            var playerId = Guid.NewGuid();
            var opponentId = Guid.NewGuid();
            var gameState = new GameState("generated-cards-test", new PlayerState(playerId), new PlayerState(opponentId), playerId)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            var existing = new CardInstance(Guid.NewGuid(), 1, playerId)
            {
                CurrentZone = CardZone.Hand
            };
            gameState.RegisterCardInstance(existing);
            gameState.Player1.Hand.Add(existing);

            var effectConfig = new EffectConfig
            {
                Type = EffectType.GetToken,
                Parameters = new Dictionary<string, ValueConfig>
                {
                    ["CardSourceMode"] = new StaticValueConfig { Value = CardSourceMode.Copy },
                    ["PlayerScope"] = new StaticValueConfig { Value = PlayerScope.Self }
                }
            };
            var effect = new Effect(effectConfig, playerId, new List<TargetInfo> { TargetInfo.ForCard(existing) }, null, Guid.NewGuid(), existing.InstanceId);

            var mapper = new Mock<IMapper>().Object;
            using var context = new GameContext(
                serviceProvider,
                mapper,
                serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                playerId,
                default,
                null,
                existing,
                effect);

            var executor = BuildEffectExecutor(serviceProvider);
            var result = executor.ExecuteEffect(effect, context);

            var completion = Assert.Single(result.GeneratedEvents.OfType<EffectCompletedEvent>());
            var copyEvent = Assert.Single(result.GeneratedEvents.OfType<CopyCardEvent>());
            Assert.True(completion.ParentEffectSucceeded);
            Assert.NotNull(copyEvent.CopiedCard);
            Assert.Empty(completion.GeneratedCards);
        }

        private static IEffectExecutor BuildEffectExecutor(IServiceProvider provider)
        {
            var registry = new EffectHandlerRegistry(NullLogger<EffectHandlerRegistry>.Instance);
            return new EffectExecutor(
                provider,
                provider.GetRequiredService<IValueSourceResolver>(),
                NullLogger<EffectExecutor>.Instance,
                registry);
        }

        private sealed class StaticValueSourceResolver : IValueSourceResolver
        {
            public object? Resolve(ValueConfig valueConfig, GameContext context)
                => valueConfig is StaticValueConfig staticValue ? staticValue.Value : null;

            public object? ResolveFormulaValue(string formula, GameContext context)
                => null;

            public object? ResolveFormulaValueWithTrace(string formula, GameContext context, IDslTraceCollector traceCollector)
                => ResolveFormulaValue(formula, context);
        }
    }
}
