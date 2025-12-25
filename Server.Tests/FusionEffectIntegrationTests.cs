using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class FusionEffectIntegrationTests
    {
        [Fact]
        public void Fusion_RemovesHandMaterials_ToRemovedZone_AndExposesOutputs()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(Mock.Of<IBuffService>());
            services.AddTransient<FusionEffectHandler>();
            services.AddSingleton<IValueSourceResolver, NullValueSourceResolver>();

            var sp = services.BuildServiceProvider();
            var mapper = new MapperConfiguration(_ => { }).CreateMapper();

            var p1Id = Guid.NewGuid();
            var p2Id = Guid.NewGuid();

            var player1 = new PlayerState(p1Id);
            var player2 = new PlayerState(p2Id);

            var gameState = new GameState("fusion-test", player1, player2, p1Id)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            var cardA = CreateRegisteredCard(gameState, p1Id, 1001, CardZone.Hand);
            var cardB = CreateRegisteredCard(gameState, p1Id, 1002, CardZone.Hand);

            var effectConfig = new EffectConfig { Type = EffectType.Fusion };
            var effect = new Effect(
                effectConfig,
                requestingPlayerId: p1Id,
                selectedTargets: new List<TargetInfo> { TargetInfo.ForCard(cardA), TargetInfo.ForCard(cardB) },
                parentAbilityId: null,
                triggeredEventId: Guid.NewGuid(),
                sourceCardId: null);

            var registry = new EffectHandlerRegistry(NullLogger<EffectHandlerRegistry>.Instance);
            var executor = new EffectExecutor(sp, sp.GetRequiredService<IValueSourceResolver>(), NullLogger<EffectExecutor>.Instance, registry);

            using var ctx = new GameContext(
                sp,
                mapper,
                sp.GetRequiredService<IGameContextAccessor>(),
                gameState,
                p1Id,
                default,
                triggeringEvent: null,
                sourceCard: null,
                currentEffect: effect);

            var handBefore = player1.Hand.Count;

            var result = executor.ExecuteEffect(effect, ctx);
            Assert.True(result.Success);

            CommitEventsRecursively(ctx, result.GeneratedEvents);

            Assert.Equal(handBefore - 2, player1.Hand.Count);
            Assert.Equal(CardZone.Removed, cardA.CurrentZone);
            Assert.Equal(CardZone.Removed, cardB.CurrentZone);

            var completed = result.GeneratedEvents.OfType<EffectCompletedEvent>().Single();
            Assert.Equal(2, completed.Outputs["fusedCount"]);

            var fusedIds = Assert.IsType<List<Guid>>(completed.Outputs["fusedIds"]);
            Assert.Contains(cardA.InstanceId, fusedIds);
            Assert.Contains(cardB.InstanceId, fusedIds);

            using var downstreamCtx = new GameContext(
                sp,
                mapper,
                sp.GetRequiredService<IGameContextAccessor>(),
                gameState,
                p1Id,
                default,
                triggeringEvent: completed);

            var fusedCountViaDsl = new EventIntFunction().Execute(new List<object?> { "fusedCount" }, downstreamCtx);
            Assert.Equal(2, fusedCountViaDsl);

            var fusedIdsViaDsl = (List<Guid>)new EventGuidListFunction().Execute(new List<object?> { "fusedIds" }, downstreamCtx)!;
            Assert.Contains(cardA.InstanceId, fusedIdsViaDsl);
            Assert.Contains(cardB.InstanceId, fusedIdsViaDsl);
        }

        private static CardInstance CreateRegisteredCard(GameState gameState, Guid ownerPlayerId, int definitionId, CardZone zone)
        {
            var card = new CardInstance(Guid.NewGuid(), definitionId, ownerPlayerId) { CurrentZone = zone };
            gameState.RegisterCardInstance(card);

            var owner = gameState.GetPlayerState(ownerPlayerId);
            Assert.NotNull(owner);
            switch (zone)
            {
                case CardZone.Hand:
                    owner!.Hand.Add(card);
                    break;
                case CardZone.Deck:
                    owner!.Deck.Add(card);
                    break;
                case CardZone.Field:
                    owner!.Field.Add(card);
                    break;
                case CardZone.Graveyard:
                    owner!.Graveyard.Add(card);
                    break;
                case CardZone.Extra:
                    owner!.Extra.Add(card);
                    break;
                case CardZone.Emblem:
                    owner!.Emblem.Add(card);
                    break;
            }

            return card;
        }

        private static void CommitEventsRecursively(GameContext context, IEnumerable<IGameEvent> events)
        {
            var queue = new Queue<IGameEvent>(events ?? Array.Empty<IGameEvent>());

            while (queue.Count > 0)
            {
                var next = queue.Dequeue();
                if (next is not ICommittableEvent committable)
                {
                    continue;
                }

                var op = committable.Commit(context);
                Assert.True(op.Success, op.ErrorMessage);

                foreach (var generated in op.GeneratedEvents ?? new List<IGameEvent>())
                {
                    queue.Enqueue(generated);
                }
            }
        }

        private sealed class NullValueSourceResolver : IValueSourceResolver
        {
            public object? Resolve(ValueConfig valueConfig, GameContext context)
            {
                if (valueConfig is StaticValueConfig s)
                    return s.Resolve(context, this);

                if (valueConfig is FormulaValueConfig f)
                    return ResolveFormulaValue(f.Formula, context);

                return valueConfig.Resolve(context, this);
            }

            public object? ResolveFormulaValue(string formula, GameContext context) => null;
        }
    }
}
