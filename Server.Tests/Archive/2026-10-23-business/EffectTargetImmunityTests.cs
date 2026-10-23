using System;
using System.Collections.Generic;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class EffectTargetImmunityTests
    {
        [Fact]
        public void ProcessTargets_FieldFollowerWithEffectImmune_SkipsNonWhitelistedEffect()
        {
            using var fixture = CreateFixture(
                EffectType.Destroy,
                CardZone.Field,
                new List<CardKeyword> { CardKeyword.EffectImmune });

            var invoked = false;
            var result = fixture.Processor.ProcessTargets(
                fixture.Context,
                new List<TargetInfo> { TargetInfo.ForCard(fixture.Target) },
                (_, _) =>
                {
                    invoked = true;
                    return new DomainOperationResult { Success = true };
                },
                "TestEffect");

            Assert.True(result.Success);
            Assert.False(invoked);
            Assert.Empty(result.Results);
            Assert.Empty(result.GeneratedEvents);
        }

        [Theory]
        [InlineData(EffectType.Damage)]
        [InlineData(EffectType.ModifyStatus)]
        public void ProcessTargets_FieldFollowerWithEffectImmune_AllowsWhitelistedEffects(EffectType effectType)
        {
            using var fixture = CreateFixture(
                effectType,
                CardZone.Field,
                new List<CardKeyword> { CardKeyword.EffectImmune });

            var invoked = false;
            var result = fixture.Processor.ProcessTargets(
                fixture.Context,
                new List<TargetInfo> { TargetInfo.ForCard(fixture.Target) },
                (_, _) =>
                {
                    invoked = true;
                    return new DomainOperationResult { Success = true };
                },
                "TestEffect");

            Assert.True(result.Success);
            Assert.True(invoked);
            Assert.Single(result.Results);
        }

        [Fact]
        public void ProcessTargets_EffectImmuneOutsideField_DoesNotSkipEffect()
        {
            using var fixture = CreateFixture(
                EffectType.Destroy,
                CardZone.Hand,
                new List<CardKeyword> { CardKeyword.EffectImmune });

            var invoked = false;
            var result = fixture.Processor.ProcessTargets(
                fixture.Context,
                new List<TargetInfo> { TargetInfo.ForCard(fixture.Target) },
                (_, _) =>
                {
                    invoked = true;
                    return new DomainOperationResult { Success = true };
                },
                "TestEffect");

            Assert.True(result.Success);
            Assert.True(invoked);
            Assert.Single(result.Results);
        }

        [Fact]
        public void ProcessTargets_FieldFollowerWithEffectImmune_AllowsEffectFromSelf()
        {
            using var fixture = CreateFixture(
                EffectType.Destroy,
                CardZone.Field,
                new List<CardKeyword> { CardKeyword.EffectImmune },
                sourceIsTarget: true);

            var invoked = false;
            var result = fixture.Processor.ProcessTargets(
                fixture.Context,
                new List<TargetInfo> { TargetInfo.ForCard(fixture.Target) },
                (_, _) =>
                {
                    invoked = true;
                    return new DomainOperationResult { Success = true };
                },
                "TestEffect");

            Assert.True(result.Success);
            Assert.True(invoked);
            Assert.Single(result.Results);
        }

        [Fact]
        public void ProcessTargets_AuraRefreshWithEffectImmune_DoesNotSkipTarget()
        {
            using var fixture = CreateFixture(
                EffectType.Destroy,
                CardZone.Field,
                new List<CardKeyword> { CardKeyword.EffectImmune },
                isAuraRefresh: true);

            var invoked = false;
            var result = fixture.Processor.ProcessTargets(
                fixture.Context,
                new List<TargetInfo> { TargetInfo.ForCard(fixture.Target) },
                (_, _) =>
                {
                    invoked = true;
                    return new DomainOperationResult { Success = true };
                },
                "TestEffect");

            Assert.True(result.Success);
            Assert.True(invoked);
            Assert.Single(result.Results);
        }

        private static TestFixture CreateFixture(
            EffectType effectType,
            CardZone targetZone,
            List<CardKeyword> targetKeywords,
            bool sourceIsTarget = false,
            bool isAuraRefresh = false)
        {
            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("effect-target-immunity-tests", player, opponent, player.PlayerId)
            {
                BuffManager = Mock.Of<IBuffManager>()
            };

            var source = new CardInstance(Guid.NewGuid(), 1, player.PlayerId)
            {
                CurrentZone = CardZone.Field
            };
            var target = new CardInstance(Guid.NewGuid(), 2, opponent.PlayerId)
            {
                CurrentZone = targetZone
            };

            if (sourceIsTarget)
            {
                target = source;
            }

            var targetInstanceId = target.InstanceId;
            var buffService = new Mock<IBuffService>();
            buffService
                .Setup(service => service.GetEffectiveKeywords(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => card.InstanceId == targetInstanceId
                    ? targetKeywords
                    : new List<CardKeyword>());

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton(buffService.Object);
            var provider = services.BuildServiceProvider();

            player.Field.Add(source);
            if (!sourceIsTarget && targetZone == CardZone.Field)
            {
                opponent.Field.Add(target);
            }
            else if (!sourceIsTarget && targetZone == CardZone.Hand)
            {
                opponent.Hand.Add(target);
            }

            gameState.RegisterCardInstance(source);
            gameState.RegisterCardInstance(target);

            var effect = new Effect(
                new EffectConfig { Type = effectType },
                player.PlayerId,
                new List<TargetInfo> { TargetInfo.ForCard(target) },
                null,
                Guid.Empty,
                source.InstanceId);

            var context = new GameContext(
                provider,
                Mock.Of<IMapper>(),
                provider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                player.PlayerId,
                default,
                null,
                source,
                effect,
                isAuraRefresh: isAuraRefresh);

            GameServiceProvider.ContextAccessor = provider.GetRequiredService<IGameContextAccessor>();
            GameServiceProvider.Logger = NullLogger.Instance;

            return new TestFixture(
                context,
                target,
                new TargetProcessingService(NullLogger<TargetProcessingService>.Instance));
        }

        private sealed class TestFixture : IDisposable
        {
            public TestFixture(GameContext context, CardInstance target, TargetProcessingService processor)
            {
                Context = context;
                Target = target;
                Processor = processor;
            }

            public GameContext Context { get; }
            public CardInstance Target { get; }
            public TargetProcessingService Processor { get; }

            public void Dispose()
            {
                Context.Dispose();
            }
        }
    }
}
