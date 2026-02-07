using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
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
    public class EffectCompletedGeneratedCardSnapshotsTests
    {
        [Fact]
        public void EffectCompletedEvent_DoesNotCollectSnapshots_BeforeCommit()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IValueSourceResolver, StaticValueSourceResolver>();
            services.AddSingleton<ITargetProcessingService, PassthroughTargetProcessingService>();
            services.AddSingleton<ICardFactory>(Mock.Of<ICardFactory>());
            services.AddSingleton<DestroyEffectHandler>();
            services.AddSingleton<IMapper>(Mock.Of<IMapper>());

            var attackBonus = 3;
            var buffService = new Mock<IBuffService>();
            buffService.Setup(s => s.GetEffectiveAttack(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) =>
                {
                    var baseAttack = card.OwnerPlayerId == Guid.Empty ? 0 : 2;
                    return baseAttack + attackBonus;
                });
            buffService.Setup(s => s.GetEffectiveMaxHealth(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(10);
            buffService.Setup(s => s.GetEffectiveCost(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(1);
            buffService.Setup(s => s.GetEffectiveKeywords(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(new List<ShadowverseBeyond.Server.Domain.Constants.CardKeyword>());
            buffService.Setup(s => s.GetEffectiveAbilityKeywordValue(It.IsAny<CardInstance>(), It.IsAny<ShadowverseBeyond.Server.Domain.Constants.AbilityKeyword>(), It.IsAny<int>(), It.IsAny<GameContext>()))
                .Returns((CardInstance _, ShadowverseBeyond.Server.Domain.Constants.AbilityKeyword __, int baseValue, GameContext ___) => baseValue);
            buffService.Setup(s => s.RemoveAllBuffs(It.IsAny<Guid>(), It.IsAny<GameContext>(), It.IsAny<Func<ShadowverseBeyond.Server.Domain.Buffs.Buff, bool>>()))
                .Callback(() => attackBonus = 0)
                .Returns(new DomainOperationResult { Success = true });
            buffService.Setup(s => s.InvalidateDerivedState(It.IsAny<GameState>(), It.IsAny<Guid>()));
            services.AddSingleton(buffService.Object);

            var serviceProvider = services.BuildServiceProvider();

            var playerId = Guid.NewGuid();
            var opponentId = Guid.NewGuid();
            var gameState = new GameState("snapshot-test", new PlayerState(playerId), new PlayerState(opponentId), playerId)
            {
                AbilityManager = BuildAbilityManagerMock(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, playerId) { CurrentZone = CardZone.Field };
            var targetCard = new CardInstance(Guid.NewGuid(), 2, opponentId) { CurrentZone = CardZone.Field };
            gameState.RegisterCardInstance(sourceCard);
            gameState.RegisterCardInstance(targetCard);
            gameState.Player1.Field.Add(sourceCard);
            gameState.Player2.Field.Add(targetCard);

            var effectConfig = new EffectConfig { Type = EffectType.Destroy };
            var effect = new Effect(effectConfig, playerId, new List<TargetInfo> { TargetInfo.ForCard(targetCard) }, null, Guid.NewGuid(), sourceCard.InstanceId);

            var mapper = serviceProvider.GetRequiredService<IMapper>();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            using var context = new GameContext(
                serviceProvider,
                mapper,
                accessor,
                gameState,
                playerId,
                default,
                null,
                sourceCard,
                effect);

            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.ContextAccessor.GameContext = context;
            GameServiceProvider.Logger = NullLogger.Instance;

            var executor = new EffectExecutor(
                serviceProvider,
                serviceProvider.GetRequiredService<IValueSourceResolver>(),
                NullLogger<EffectExecutor>.Instance,
                new EffectHandlerRegistry(NullLogger<EffectHandlerRegistry>.Instance));

            var result = executor.ExecuteEffect(effect, context);

            var completion = Assert.Single(result.GeneratedEvents.OfType<EffectCompletedEvent>());
            Assert.Empty(completion.GeneratedCardSnapshots);

            var destroyEvent = Assert.Single(result.GeneratedEvents.OfType<DestroyEvent>());
            var destroyResult = destroyEvent.Commit(context);
            Assert.True(destroyResult.Success);

            var moveEvent = Assert.Single(destroyResult.GeneratedEvents.OfType<CardMovedEvent>());
            var moveResult = moveEvent.Commit(context);
            Assert.True(moveResult.Success);
        }

        private static IAbilityManager BuildAbilityManagerMock()
        {
            var mock = new Mock<IAbilityManager>();
            mock.Setup(m => m.GetEffectiveAbilitiesForCard(It.IsAny<Guid>(), It.IsAny<GameContext>()))
                .Returns(new List<IAbility>());
            return mock.Object;
        }

        private sealed class StaticValueSourceResolver : IValueSourceResolver
        {
            public object? Resolve(ValueConfig valueConfig, GameContext context)
                => valueConfig is StaticValueConfig staticValue ? staticValue.Value : null;

            public object? ResolveFormulaValue(string formula, GameContext context)
                => null;

            public object? ResolveFormulaValueWithTrace(string formula, GameContext context, ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation.IDslTraceCollector traceCollector)
                => null;
        }

        private sealed class PassthroughTargetProcessingService : ITargetProcessingService
        {
            public GameStateActionResult ProcessTargets(
                GameContext context,
                IReadOnlyList<TargetInfo> targets,
                Func<TargetInfo, GameContext, DomainOperationResult> action,
                string effectName)
            {
                var result = new GameStateActionResult { Success = true };
                foreach (var target in targets)
                {
                    var opResult = action(target, context);
                    if (!opResult.Success)
                    {
                        result.Success = false;
                        result.ErrorMessage = opResult.ErrorMessage;
                    }

                    if (opResult.GeneratedEvents != null && opResult.GeneratedEvents.Count > 0)
                    {
                        result.GeneratedEvents.AddRange(opResult.GeneratedEvents);
                    }
                }

                return result;
            }
        }
    }
}
